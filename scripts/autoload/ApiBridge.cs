using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 全局 LLM API 调用桥接器（Autoload 单例）。
/// 配置来自 SaveManager（user://settings.json）。
/// </summary>
public partial class ApiBridge : Node
{
	public static ApiBridge Instance { get; private set; }

	private string _apiKey = "";
	private string _baseUrl = UserSettings.DefaultApiBaseUrl;
	private string _model = UserSettings.DefaultModelId;
	private int _maxTokens = 2048;
	private float _temperature = 0.7f;

	/// <summary>全局串行：避免并行 LLM 请求压垮网络栈；每次请求使用独立 HttpRequest，避免共享节点上 TCS 与完成信号错配。</summary>
	private static readonly SemaphoreSlim HttpGate = new(1, 1);

	/// <summary>遭遇帧 / 长提示词生成常需数十秒；服务端排队或模型恢复慢时需更长等待。</summary>
	private const int LlmCompletionWaitSeconds = 150;

	/// <summary>略大于等待上限，由引擎层结束挂起请求。</summary>
	private const double HttpRequestTimeoutSeconds = LlmCompletionWaitSeconds + 20.0;

	private static int _chatRequestSeq;

	/// <summary>按 Base URL 选择请求体与响应解析路径。</summary>
	private enum LlmApiKind
	{
		OpenAiCompatible,
		MiniMax,
		ArkResponses
	}

	private static string DescribeHttpRequestResult(long result)
	{
		if (!Enum.IsDefined(typeof(HttpRequest.Result), (int)result))
			return $"Unknown({result})";
		return ((HttpRequest.Result)result).ToString();
	}

	public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
	public string Model => _model;

	[Signal]
	public delegate void ResponseReceivedEventHandler(bool success, string content);

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		LoadConfigFromSaveManager();
	}

	private void LoadConfigFromSaveManager()
	{
		var sm = SaveManager.Instance;
		if (sm == null)
		{
			CallDeferred(MethodName.LoadConfigFromSaveManager);
			return;
		}

		var s = sm.Settings;
		_apiKey = s.ApiKey ?? "";
		_baseUrl = string.IsNullOrWhiteSpace(s.BaseUrl) ? UserSettings.DefaultApiBaseUrl : s.BaseUrl.TrimEnd('/');
		_model = string.IsNullOrWhiteSpace(s.Model) ? UserSettings.DefaultModelId : s.Model;
	}

	public void SetApiKey(string apiKey)
	{
		_apiKey = apiKey ?? "";
		PersistSettings();
	}

	public void Configure(string apiKey, string model = null, string baseUrl = null)
	{
		_apiKey = apiKey ?? "";
		_model = string.IsNullOrWhiteSpace(model) ? UserSettings.DefaultModelId : model;
		_baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? UserSettings.DefaultApiBaseUrl : baseUrl.TrimEnd('/');
		PersistSettings();
	}

	public void SetGenerationParams(int maxTokens = 2048, float temperature = 0.7f)
	{
		_maxTokens = maxTokens;
		_temperature = temperature;
	}

	private void PersistSettings()
	{
		var sm = SaveManager.Instance;
		if (sm == null) return;
		var u = sm.Settings;
		u.ApiKey = _apiKey;
		u.BaseUrl = _baseUrl;
		u.Model = _model;
		sm.UpdateSettings(u);
		LoadConfigFromSaveManager();
	}

	public async Task<ApiResult> ChatAsync(
		List<ChatMessage> messages,
		bool jsonMode = false,
		string fallback = null)
	{
		if (!IsConfigured)
		{
			GD.PrintErr("[ApiBridge] API Key 未配置");
			return ApiResult.Fail("API Key 未配置", fallback);
		}

		TimeManager.Instance?.PushGameTimeFreeze();
		try
		{
			await HttpGate.WaitAsync();
			HttpRequest http = null;
			try
			{
			var apiKind = ResolveLlmApiKind(_baseUrl);
			object body;
			string url;
			switch (apiKind)
			{
				case LlmApiKind.ArkResponses:
					body = BuildArkResponsesRequestBody(messages, jsonMode);
					url = $"{NormalizeArkBaseUrl(_baseUrl)}/api/v3/responses";
					break;
				case LlmApiKind.MiniMax:
					body = BuildMiniMaxRequestBody(messages, jsonMode);
					url = $"{_baseUrl.TrimEnd('/')}/v1/text/chatcompletion_v2";
					break;
				default:
					body = BuildOpenAiCompatibleRequestBody(messages, jsonMode);
					url = $"{_baseUrl.TrimEnd('/')}/chat/completions";
					break;
			}
			var jsonBody = JsonSerializer.Serialize(body);

			var headers = new string[]
			{
				"Content-Type: application/json",
				$"Authorization: Bearer {_apiKey}"
			};

			http = new HttpRequest
			{
				Timeout = HttpRequestTimeoutSeconds
			};
			AddChild(http);

			var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			var completed = false;
			var waitTimedOut = false;
			var reqId = System.Threading.Interlocked.Increment(ref _chatRequestSeq);

			void OnHttpRequestCompleted(long result, long responseCode, string[] hdrs, byte[] bodyBytes)
			{
				// 应用侧 WaitAsync 已超时并 CancelRequest 时，引擎仍可能再派发失败回调，避免重复刷 ERROR。
				if (waitTimedOut)
					return;
				if (completed) return;
				completed = true;
				if (GodotObject.IsInstanceValid(http))
				{
					http.RequestCompleted -= OnHttpRequestCompleted;
					http.QueueFree();
				}
				http = null;

				if (result != (long)HttpRequest.Result.Success)
				{
					var name = DescribeHttpRequestResult(result);
					var hint = (HttpRequest.Result)result == HttpRequest.Result.ConnectionError ||
					           (HttpRequest.Result)result == HttpRequest.Result.CantConnect ||
					           (HttpRequest.Result)result == HttpRequest.Result.CantResolve
						? " 无法连上服务端，请检查网络、代理、防火墙与设置里的 Base URL。"
						: "";
					GD.PrintErr($"[ApiBridge] HTTP 失败 #{reqId}: {name}({result}) httpStatus={responseCode}{hint}");
					tcs.TrySetResult(null);
					return;
				}

				var json = Encoding.UTF8.GetString(bodyBytes);
				tcs.TrySetResult(json);
			}

			http.RequestCompleted += OnHttpRequestCompleted;

			var error = http.Request(url, headers, HttpClient.Method.Post, jsonBody);

			if (error != Error.Ok)
			{
				GD.PrintErr($"[ApiBridge] 请求发送失败: {error}");
				if (GodotObject.IsInstanceValid(http))
				{
					http.RequestCompleted -= OnHttpRequestCompleted;
					http.QueueFree();
				}
				http = null;
				return ApiResult.Fail($"请求错误: {error}", fallback);
			}

			try
			{
				var responseJson = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(LlmCompletionWaitSeconds));
				return ParseResponse(responseJson, fallback, apiKind);
			}
			catch (TimeoutException)
			{
				waitTimedOut = true;
				GD.PrintErr(
					$"[ApiBridge] LLM 请求 #{reqId} 等待超时（{LlmCompletionWaitSeconds}s），使用 fallback。" +
					"（「最后一天」单回合常含两次串行 LLM 调用，慢网络下总耗时会叠加。）");
				completed = true;
				if (GodotObject.IsInstanceValid(http))
				{
					http.RequestCompleted -= OnHttpRequestCompleted;
					http.CancelRequest();
					http.QueueFree();
				}
				http = null;
				return ApiResult.Fail("请求超时", fallback);
			}
			catch (OperationCanceledException)
			{
				return ApiResult.Fail("请求被取消", fallback);
			}
			catch (Exception e)
			{
				GD.PrintErr($"[ApiBridge] 请求异常: {e.Message}");
				return ApiResult.Fail(e.Message, fallback);
			}
			}
			finally
			{
				if (http != null && GodotObject.IsInstanceValid(http))
					http.QueueFree();
				HttpGate.Release();
			}
		}
		finally
		{
			TimeManager.Instance?.PopGameTimeFreeze();
		}
	}

	public async Task<ApiResult> ChatTextAsync(
		string systemPrompt,
		string userMessage,
		string fallback = null)
	{
		var messages = new List<ChatMessage>
		{
			new() { Role = "system", Content = systemPrompt },
			new() { Role = "user", Content = userMessage }
		};
		return await ChatAsync(messages, jsonMode: false, fallback: fallback);
	}

	public async Task<ApiResult> ChatJsonAsync(
		string systemPrompt,
		string userMessage,
		string fallback = null)
	{
		var messages = new List<ChatMessage>
		{
			new() { Role = "system", Content = systemPrompt },
			new() { Role = "user", Content = userMessage }
		};
		return await ChatAsync(messages, jsonMode: true, fallback: fallback);
	}

	public async Task<ApiResult> ChatWithContextAsync(
		string systemPrompt,
		List<ChatMessage> history,
		string userMessage,
		bool jsonMode = false,
		string fallback = null)
	{
		var messages = new List<ChatMessage>
		{
			new() { Role = "system", Content = systemPrompt }
		};
		messages.AddRange(history);
		messages.Add(new ChatMessage { Role = "user", Content = userMessage });

		return await ChatAsync(messages, jsonMode, fallback: fallback);
	}

	private static LlmApiKind ResolveLlmApiKind(string baseUrl)
	{
		if (IsArkResponsesEndpoint(baseUrl))
			return LlmApiKind.ArkResponses;
		if (IsMiniMaxEndpoint(baseUrl))
			return LlmApiKind.MiniMax;
		return LlmApiKind.OpenAiCompatible;
	}

	/// <summary>火山方舟 Responses：<c>https://ark.&lt;region&gt;.volces.com</c>（不含路径）。</summary>
	private static bool IsArkResponsesEndpoint(string baseUrl)
	{
		if (string.IsNullOrWhiteSpace(baseUrl)) return false;
		if (!baseUrl.Contains("volces.com", StringComparison.OrdinalIgnoreCase))
			return false;
		return baseUrl.Contains("ark.", StringComparison.OrdinalIgnoreCase)
		       || baseUrl.Contains("/ark", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsMiniMaxEndpoint(string baseUrl)
	{
		if (string.IsNullOrWhiteSpace(baseUrl)) return false;
		return baseUrl.Contains("minimaxi.com", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>若用户粘贴了完整 endpoint，去掉尾部 <c>/api/v3</c> 或 <c>/api/v3/responses</c>。</summary>
	private static string NormalizeArkBaseUrl(string baseUrl)
	{
		var u = baseUrl.Trim().TrimEnd('/');
		var lower = u.ToLowerInvariant();
		if (lower.EndsWith("/api/v3/responses", StringComparison.Ordinal))
			return u[..^"/api/v3/responses".Length].TrimEnd('/');
		if (lower.EndsWith("/api/v3", StringComparison.Ordinal))
			return u[..^"/api/v3".Length].TrimEnd('/');
		return u;
	}

	/// <summary>火山方舟 Responses API：<c>POST /api/v3/responses</c>；关闭深度思考以降低延迟。</summary>
	private object BuildArkResponsesRequestBody(List<ChatMessage> messages, bool jsonMode)
	{
		var input = new List<Dictionary<string, object>>();
		foreach (var m in messages)
		{
			if (string.IsNullOrEmpty(m.Role)) continue;
			input.Add(new Dictionary<string, object>
			{
				["role"] = m.Role,
				["content"] = m.Content ?? ""
			});
		}

		var body = new Dictionary<string, object>
		{
			["model"] = _model,
			["input"] = input,
			["thinking"] = new Dictionary<string, string> { ["type"] = "disabled" },
			["stream"] = false,
			["temperature"] = _temperature,
			["max_output_tokens"] = _maxTokens
		};

		if (jsonMode)
		{
			body["text"] = new Dictionary<string, object>
			{
				["format"] = new Dictionary<string, string> { ["type"] = "json_object" }
			};
		}

		return body;
	}

	/// <summary>OpenAI 兼容：<c>/v1/chat/completions</c>，支持 <c>response_format</c> JSON 模式。</summary>
	private object BuildOpenAiCompatibleRequestBody(List<ChatMessage> messages, bool jsonMode)
	{
		var msgList = messages.Select(m => new { role = m.Role, content = m.Content }).ToList();

		var body = new Dictionary<string, object>
		{
			["model"] = _model,
			["messages"] = msgList,
			["max_tokens"] = _maxTokens,
			["temperature"] = _temperature,
			["stream"] = false
		};

		if (jsonMode)
		{
			body["response_format"] = new Dictionary<string, string>
			{
				["type"] = "json_object"
			};
		}

		return body;
	}

	/// <summary>MiniMax：<c>/v1/text/chatcompletion_v2</c>，使用 <c>max_completion_tokens</c>；JSON 模式靠 system 约束。</summary>
	private object BuildMiniMaxRequestBody(List<ChatMessage> messages, bool jsonMode)
	{
		var working = new List<ChatMessage>(messages.Count);
		foreach (var m in messages)
			working.Add(new ChatMessage { Role = m.Role, Content = m.Content ?? "" });

		if (jsonMode)
		{
			const string jsonHint = "\n\n【输出要求】仅输出一个合法 JSON 对象，不要使用 Markdown 代码围栏。";
			var sysIdx = working.FindIndex(m => m.Role == "system");
			if (sysIdx >= 0)
				working[sysIdx].Content += jsonHint;
			else
				working.Insert(0, new ChatMessage
				{
					Role = "system",
					Content = "【输出要求】仅输出一个合法 JSON 对象，不要使用 Markdown 代码围栏。"
				});
		}

		var msgList = working.Select(m => new { role = m.Role, content = m.Content }).ToList();
		var maxOut = Math.Min(_maxTokens, 2048);
		// 不向模型传入 tools；显式 none，避免默认 auto 走工具/外部调用路径增加延迟。
		// 「深度思考」：M2.7 系在官方文档中为推理模型，公开 ChatCompletionReq 中未见可关闭推理的开关。
		return new Dictionary<string, object>
		{
			["model"] = _model,
			["messages"] = msgList,
			["max_completion_tokens"] = maxOut,
			["temperature"] = _temperature,
			["stream"] = false,
			["tool_choice"] = "none"
		};
	}

	private ApiResult ParseResponse(string json, string fallback, LlmApiKind apiKind)
	{
		if (json == null)
		{
			return ApiResult.Fail("HTTP 请求失败", fallback);
		}

		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (apiKind == LlmApiKind.ArkResponses)
				return ParseArkResponsesBody(root, json, fallback);

			if (apiKind == LlmApiKind.MiniMax &&
			    root.TryGetProperty("base_resp", out var br) &&
			    br.TryGetProperty("status_code", out var scEl))
			{
				long code = 0;
				if (scEl.ValueKind == JsonValueKind.Number)
					code = scEl.GetInt64();
				if (code != 0)
				{
					var sms = br.TryGetProperty("status_msg", out var sm) ? sm.GetString() ?? "" : "";
					GD.PrintErr($"[ApiBridge] MiniMax base_resp: {code} {sms}");
					return ApiResult.Fail(string.IsNullOrEmpty(sms) ? $"MiniMax 错误 ({code})" : sms, fallback);
				}
			}

			if (root.TryGetProperty("error", out var errorEl))
			{
				var errorMsg = errorEl.TryGetProperty("message", out var msgEl)
					? msgEl.GetString() ?? "未知 API 错误"
					: "未知 API 错误";
				GD.PrintErr($"[ApiBridge] API 错误: {errorMsg}");
				return ApiResult.Fail(errorMsg, fallback);
			}

			if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
				return ApiResult.Fail("响应无 choices", fallback);

			var first = choices[0];
			if (!first.TryGetProperty("message", out var message))
				return ApiResult.Fail("响应缺少 message", fallback);

			var content = message.TryGetProperty("content", out var contentEl)
				? contentEl.GetString() ?? ""
				: "";

			EmitSignal(SignalName.ResponseReceived, true, content);
			return ApiResult.Ok(content);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[ApiBridge] 响应解析失败: {e.Message}");
			return ApiResult.Fail(e.Message, fallback);
		}
	}

	private ApiResult ParseArkResponsesBody(JsonElement root, string rawJson, string fallback)
	{
		if (TryGetVolcengineResponseMetadataError(root, out var metaErr))
		{
			GD.PrintErr($"[ApiBridge] Ark ResponseMetadata: {metaErr}");
			return ApiResult.Fail(metaErr, fallback);
		}

		if (root.TryGetProperty("error", out var topErr))
		{
			var errMsg = topErr.TryGetProperty("message", out var em) ? em.GetString() ?? "" : topErr.GetRawText();
			GD.PrintErr($"[ApiBridge] Ark error: {errMsg}");
			return ApiResult.Fail(string.IsNullOrEmpty(errMsg) ? "Ark API 错误" : errMsg, fallback);
		}

		var node = UnwrapVolcengineResultWrapper(root);

		if (TryGetVolcengineResponseMetadataError(node, out metaErr))
		{
			GD.PrintErr($"[ApiBridge] Ark Result.ResponseMetadata: {metaErr}");
			return ApiResult.Fail(metaErr, fallback);
		}

		if (node.TryGetProperty("error", out var innerErr))
		{
			var errMsg = innerErr.TryGetProperty("message", out var em2) ? em2.GetString() ?? "" : innerErr.GetRawText();
			return ApiResult.Fail(string.IsNullOrEmpty(errMsg) ? "Ark API 错误" : errMsg, fallback);
		}

		if (TryExtractArkAssistantText(node, out var text) && !string.IsNullOrEmpty(text))
		{
			EmitSignal(SignalName.ResponseReceived, true, text);
			return ApiResult.Ok(text);
		}

		if (node.TryGetProperty("choices", out var ch) && ch.GetArrayLength() > 0)
		{
			var first = ch[0];
			if (first.TryGetProperty("message", out var msg) &&
			    msg.TryGetProperty("content", out var c))
			{
				var s = c.GetString() ?? "";
				EmitSignal(SignalName.ResponseReceived, true, s);
				return ApiResult.Ok(s);
			}
		}

		var preview = rawJson.Length > 400 ? rawJson.Substring(0, 400) + "…" : rawJson;
		GD.PrintErr($"[ApiBridge] Ark 响应无法解析正文，片段: {preview}");
		return ApiResult.Fail("Ark 响应格式无法识别", fallback);
	}

	private static JsonElement UnwrapVolcengineResultWrapper(JsonElement root)
	{
		if (root.TryGetProperty("Result", out var r)) return r;
		if (root.TryGetProperty("result", out var r2)) return r2;
		return root;
	}

	private static bool TryGetVolcengineResponseMetadataError(JsonElement root, out string error)
	{
		error = null;
		if (!root.TryGetProperty("ResponseMetadata", out var rm))
			return false;
		if (!rm.TryGetProperty("Error", out var er))
			return false;
		var code = er.TryGetProperty("Code", out var c) ? c.GetString() ?? "" : "";
		var msg = er.TryGetProperty("Message", out var m) ? m.GetString() ?? "" : "";
		error = string.IsNullOrEmpty(msg) ? code : $"{code}: {msg}";
		return !string.IsNullOrEmpty(error);
	}

	/// <summary>从 Responses API 的 <c>output</c> 或兼容结构中提取助手文本。</summary>
	private static bool TryExtractArkAssistantText(JsonElement node, out string text)
	{
		text = null;

		if (node.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
		{
			var sb = new StringBuilder();
			foreach (var item in output.EnumerateArray())
			{
				if (item.TryGetProperty("type", out var typeEl))
				{
					var t = typeEl.GetString() ?? "";
					if (t == "output_text" || t == "text")
					{
						if (item.TryGetProperty("text", out var topTx))
							sb.Append(topTx.GetString());
						continue;
					}

					if (!string.IsNullOrEmpty(t) && t != "message" && t != "output_message")
						continue;
				}

				if (!item.TryGetProperty("content", out var content))
					continue;

				if (content.ValueKind == JsonValueKind.String)
				{
					sb.Append(content.GetString());
					continue;
				}

				if (content.ValueKind == JsonValueKind.Array)
				{
					foreach (var part in content.EnumerateArray())
					{
						var pType = part.TryGetProperty("type", out var pt) ? pt.GetString() ?? "" : "";
						if (pType == "output_text" || pType == "text")
						{
							if (part.TryGetProperty("text", out var tx))
								sb.Append(tx.GetString());
						}
					}
				}
			}

			var joined = sb.ToString();
			if (!string.IsNullOrEmpty(joined))
			{
				text = joined;
				return true;
			}
		}

		if (node.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.String)
		{
			text = ot.GetString();
			return !string.IsNullOrEmpty(text);
		}

		return false;
	}

	public class ChatMessage
	{
		[JsonPropertyName("role")]
		public string Role { get; set; } = "user";

		[JsonPropertyName("content")]
		public string Content { get; set; } = "";
	}

	public class ApiResult
	{
		public bool Success { get; set; }
		public string Content { get; set; } = "";
		public string Error { get; set; } = "";

		public bool TryParseJson(out JsonDocument doc)
		{
			doc = null;
			if (!Success || string.IsNullOrEmpty(Content)) return false;
			try
			{
				doc = JsonDocument.Parse(Content);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static ApiResult Ok(string content) => new() { Success = true, Content = content ?? "" };
		public static ApiResult Fail(string error, string fallback = null) => new()
		{
			Success = false,
			Error = error,
			Content = fallback ?? ""
		};
	}
}
