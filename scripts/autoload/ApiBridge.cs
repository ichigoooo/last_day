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
	private string _baseUrl = "https://api.deepseek.com";
	private string _model = "deepseek-chat";
	private int _maxTokens = 2048;
	private float _temperature = 0.7f;

	/// <summary>全局串行：避免并行 LLM 请求压垮网络栈；每次请求使用独立 HttpRequest，避免共享节点上 TCS 与完成信号错配。</summary>
	private static readonly SemaphoreSlim HttpGate = new(1, 1);

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
		_baseUrl = string.IsNullOrWhiteSpace(s.BaseUrl) ? "https://api.deepseek.com" : s.BaseUrl.TrimEnd('/');
		_model = string.IsNullOrWhiteSpace(s.Model) ? "deepseek-chat" : s.Model;
	}

	public void SetApiKey(string apiKey)
	{
		_apiKey = apiKey ?? "";
		PersistSettings();
	}

	public void Configure(string apiKey, string model = "deepseek-chat", string baseUrl = "https://api.deepseek.com")
	{
		_apiKey = apiKey ?? "";
		_model = string.IsNullOrWhiteSpace(model) ? "deepseek-chat" : model;
		_baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepseek.com" : baseUrl.TrimEnd('/');
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
			var body = BuildRequestBody(messages, jsonMode);
			var jsonBody = JsonSerializer.Serialize(body);

			var headers = new string[]
			{
				"Content-Type: application/json",
				$"Authorization: Bearer {_apiKey}"
			};

			var url = $"{_baseUrl}/chat/completions";

			http = new HttpRequest
			{
				Timeout = 50.0
			};
			AddChild(http);

			var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			var completed = false;

			void OnHttpRequestCompleted(long result, long responseCode, string[] hdrs, byte[] bodyBytes)
			{
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
					GD.PrintErr($"[ApiBridge] HTTP 错误: result={result}, code={responseCode}");
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
				var responseJson = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(45));
				return ParseResponse(responseJson, fallback);
			}
			catch (TimeoutException)
			{
				GD.PrintErr("[ApiBridge] LLM 请求超时（45s），使用 fallback");
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

	private object BuildRequestBody(List<ChatMessage> messages, bool jsonMode)
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

	private ApiResult ParseResponse(string json, string fallback)
	{
		if (json == null)
		{
			return ApiResult.Fail("HTTP 请求失败", fallback);
		}

		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (root.TryGetProperty("error", out var errorEl))
			{
				var errorMsg = errorEl.TryGetProperty("message", out var msgEl)
					? msgEl.GetString() ?? "未知 API 错误"
					: "未知 API 错误";
				GD.PrintErr($"[ApiBridge] API 错误: {errorMsg}");
				return ApiResult.Fail(errorMsg, fallback);
			}

			var content = root
				.GetProperty("choices")[0]
				.GetProperty("message")
				.GetProperty("content")
				.GetString();

			EmitSignal(SignalName.ResponseReceived, true, content);
			return ApiResult.Ok(content);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[ApiBridge] 响应解析失败: {e.Message}");
			return ApiResult.Fail(e.Message, fallback);
		}
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
