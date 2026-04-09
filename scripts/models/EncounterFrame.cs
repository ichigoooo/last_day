using System;
using System.Collections.Generic;
using Godot;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// 「最后一天」单回合遭遇呈现协议：地点、人物、视觉 brief、旁白与选项。声明式展示，不写入系统资源。
/// </summary>
public sealed class EncounterFrame
{
	public string PlaceName { get; set; } = "";
	public string ArrivalMode { get; set; } = "symbolic";
	public string EncounterType { get; set; } = "moment";
	public string EncounterSummary { get; set; } = "";
	public string CharacterName { get; set; } = "";
	public string CharacterRole { get; set; } = "";
	public string SceneVisualBrief { get; set; } = "";
	public string CharacterVisualBrief { get; set; } = "";
	public bool ShowSceneImage { get; set; } = true;
	public bool ShowCharacterFrame { get; set; }
	public string Narration { get; set; } = "";
	public List<NarrativeOption> Options { get; set; } = new();
	public string CustomHint { get; set; } = "";

	public static EncounterFrame CreateDefault(string narrationFallback)
	{
		var n = string.IsNullOrWhiteSpace(narrationFallback)
			? "时间从指缝里滑过去，你没再多说什么。"
			: narrationFallback.Trim();
		return new EncounterFrame
		{
			PlaceName = "此处",
			ArrivalMode = "symbolic",
			EncounterType = "pause",
			EncounterSummary = "",
			CharacterName = "",
			CharacterRole = "",
			SceneVisualBrief = "室内一角，安静、低饱和、剪影式构图",
			CharacterVisualBrief = "",
			ShowSceneImage = true,
			ShowCharacterFrame = false,
			Narration = n,
			Options =
			[
				new NarrativeOption { Slot = "face", Label = "继续把事做完" },
				new NarrativeOption { Slot = "deflect", Label = "先转开注意力" },
				new NarrativeOption { Slot = "pause", Label = "停下来发呆一会" }
			],
			CustomHint = "也可以自己输入真正想做的事。"
		};
	}

	/// <summary>仅去掉 markdown 代码围栏，不截取花括号（避免误伤合法 JSON）。</summary>
	private static string StripMarkdownFencesOnly(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
		var s = raw.Trim();
		if (!s.StartsWith("```", StringComparison.Ordinal)) return s;
		var nl = s.IndexOf('\n');
		if (nl > 0) s = s[(nl + 1)..];
		var fenceEnd = s.LastIndexOf("```", StringComparison.Ordinal);
		if (fenceEnd > 0) s = s[..fenceEnd];
		return s.Trim();
	}

	/// <summary>去掉模型常见的 markdown 代码围栏，并截取首个 <c>{</c> 到最后一个 <c>}</c>，避免整段响应无法 <see cref="JsonDocument.Parse"/>。</summary>
	private static string PrepareJsonPayload(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";
		var s = StripMarkdownFencesOnly(raw);

		var i0 = s.IndexOf('{');
		var i1 = s.LastIndexOf('}');
		if (i0 >= 0 && i1 > i0)
			s = s.Substring(i0, i1 - i0 + 1);
		return s;
	}

	public static EncounterFrame Parse(string json, string narrationFallback)
	{
		var def = CreateDefault(narrationFallback);
		if (string.IsNullOrWhiteSpace(json)) return def;

		var candidates = new[]
		{
			PrepareJsonPayload(json),
			StripMarkdownFencesOnly(json).Trim(),
			json.Trim()
		};

		foreach (var prepared in candidates)
		{
			if (string.IsNullOrWhiteSpace(prepared)) continue;
			try
			{
				var work = prepared;
				for (var peel = 0; peel < 8; peel++)
				{
					using var doc = JsonDocument.Parse(work);
					var r = doc.RootElement;
					if (r.ValueKind == JsonValueKind.String)
					{
						work = r.GetString() ?? "";
						if (string.IsNullOrWhiteSpace(work)) break;
						continue;
					}

					if (r.ValueKind == JsonValueKind.Array)
					{
						if (r.GetArrayLength() == 0) break;
						work = r[0].GetRawText();
						continue;
					}

					if (r.ValueKind == JsonValueKind.Object)
					{
						var f = BuildFrameFromRoot(r, def);
						return Normalize(f, narrationFallback);
					}

					break;
				}
			}
			catch
			{
				// 尝试下一候选字符串
			}
		}

		GD.PrintErr("[EncounterFrame] 全部 JSON 候选均失败，使用本回合兜底遭遇帧。");
		return def;
	}

	private static EncounterFrame BuildFrameFromRoot(JsonElement root, EncounterFrame def)
	{
		var f = new EncounterFrame
			{
				PlaceName = ReadStr(root, "place_name", def.PlaceName),
				ArrivalMode = NormalizeArrival(ReadStr(root, "arrival_mode", def.ArrivalMode)),
				EncounterType = ReadStr(root, "encounter_type", def.EncounterType),
				EncounterSummary = ReadStr(root, "encounter_summary", ""),
				CharacterName = ReadStr(root, "character_name", ""),
				CharacterRole = ReadStr(root, "character_role", ""),
				SceneVisualBrief = ReadStr(root, "scene_visual_brief", def.SceneVisualBrief),
				CharacterVisualBrief = ReadStr(root, "character_visual_brief", ""),
				ShowSceneImage = ReadBool(root, "show_scene_image", true),
				ShowCharacterFrame = ReadBool(root, "show_character_frame", false),
				Narration = ReadStr(root, "narration", def.Narration),
				CustomHint = ReadStr(root, "custom_hint", def.CustomHint)
			};
			if (root.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
			{
				f.Options.Clear();
				foreach (var el in opts.EnumerateArray())
				{
					if (el.ValueKind == JsonValueKind.String)
					{
						var only = el.GetString()?.Trim() ?? "";
						if (!string.IsNullOrEmpty(only))
							f.Options.Add(new NarrativeOption { Slot = "", Label = only });
						continue;
					}

					if (el.ValueKind != JsonValueKind.Object)
						continue;
					var slot = el.TryGetProperty("slot", out var s) ? s.GetString() ?? "" : "";
					var label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
					if (!string.IsNullOrEmpty(label))
						f.Options.Add(new NarrativeOption { Slot = slot, Label = label });
				}
			}
			if (f.Options.Count == 0)
				f.Options = new List<NarrativeOption>(def.Options);
			return f;
	}

	/// <summary>与底部「手机」按钮冲突：三选项不得引导使用手机 UI。</summary>
	private static readonly string[] PhoneGameplayOptionMarkers =
	{
		"手机", "微信", "短信", "朋友圈", "接电话", "打电话", "拨号", "扫码",
		"发消息", "看消息", "查消息", "刷消息", "回消息", "读短信", "写短信",
		"未读消息", "新消息", "视频通话", "语音通话", "语音消息", "视频聊天",
		"刷抖音", "掏手机", "拿手机", "摸手机"
	};

	private static readonly Regex PhoneMessageActPattern = new(
		"[看查刷发回复读].{0,8}消息",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>主选项文案是否像在引导使用手机专用机制（底部按钮已承担）。</summary>
	public static bool LooksLikePhoneGameplayOption(string label)
	{
		if (string.IsNullOrWhiteSpace(label)) return false;
		var t = label.Trim();
		foreach (var m in PhoneGameplayOptionMarkers)
			if (t.Contains(m, StringComparison.Ordinal))
				return true;
		return PhoneMessageActPattern.IsMatch(t);
	}

	private static void RemovePhoneGameplayOptions(EncounterFrame f)
	{
		if (f?.Options == null || f.Options.Count == 0) return;
		f.Options.RemoveAll(o => o == null || LooksLikePhoneGameplayOption(o.Label ?? ""));
	}

	public static EncounterFrame Normalize(EncounterFrame f, string narrationFallback)
	{
		if (f == null) return CreateDefault(narrationFallback);
		if (string.IsNullOrWhiteSpace(f.PlaceName)) f.PlaceName = "此处";
		f.ArrivalMode = NormalizeArrival(f.ArrivalMode);
		if (string.IsNullOrWhiteSpace(f.EncounterType)) f.EncounterType = "moment";
		if (string.IsNullOrWhiteSpace(f.SceneVisualBrief)) f.SceneVisualBrief = "安静、低饱和的场景剪影";
		if (f.ShowCharacterFrame)
		{
			if (string.IsNullOrWhiteSpace(f.CharacterVisualBrief))
				f.CharacterVisualBrief = "侧身轮廓，面部不清，克制剪影";
		}
		else
		{
			f.CharacterName = "";
			f.CharacterRole = "";
			f.CharacterVisualBrief = "";
		}
		if (string.IsNullOrWhiteSpace(f.Narration))
			f.Narration = string.IsNullOrWhiteSpace(narrationFallback)
				? CreateDefault("").Narration
				: narrationFallback.Trim();
		PadShortNarration(f);
		RemovePhoneGameplayOptions(f);
		while (f.Options.Count < 3)
		{
			var i = f.Options.Count;
			f.Options.Add(new NarrativeOption
			{
				Slot = i == 0 ? "face" : i == 1 ? "deflect" : "pause",
				Label = i == 0 ? "继续" : i == 1 ? "换一件事" : "停一会"
			});
		}
		f.CustomHint = ContentSafetyFilter.ClampLastDayCustomHint(f.CustomHint ?? "");
		if (LooksLikePhoneGameplayOption(f.CustomHint))
			f.CustomHint = "也可以自己输入真正想做的事。";
		if (string.IsNullOrWhiteSpace(f.CustomHint))
			f.CustomHint = "也可以自己输入真正想做的事。";
		return f;
	}

	public static void SanitizeForDisplay(EncounterFrame f)
	{
		if (f == null) return;
		f.PlaceName = ContentSafetyFilter.SanitizeDisplay(f.PlaceName ?? "");
		f.EncounterSummary = ContentSafetyFilter.NormalizeLastDayNoMetaPlayerYou(
			ContentSafetyFilter.SanitizeDisplay(f.EncounterSummary ?? ""));
		f.CharacterName = ContentSafetyFilter.SanitizeDisplay(f.CharacterName ?? "");
		f.CharacterRole = ContentSafetyFilter.SanitizeDisplay(f.CharacterRole ?? "");
		f.Narration = ContentSafetyFilter.NormalizeLastDayNoMetaPlayerYou(
			ContentSafetyFilter.SanitizeDisplay(f.Narration ?? ""));
		f.CustomHint = ContentSafetyFilter.ClampLastDayCustomHint(ContentSafetyFilter.SanitizeDisplay(f.CustomHint ?? ""));
		foreach (var o in f.Options)
			o.Label = ContentSafetyFilter.SanitizeDisplay(o.Label ?? "");
	}

	public NarrativeTurn ToLegacyNarrativeTurn()
	{
		var n = (Narration ?? "").Trim();
		var sum = (EncounterSummary ?? "").Trim();
		string body;
		if (string.IsNullOrEmpty(n))
			body = sum;
		else if (string.IsNullOrEmpty(sum) || sum == n || n.Contains(sum, StringComparison.Ordinal) ||
		         sum.Contains(n, StringComparison.Ordinal))
			body = n;
		else
			body = n + "\n\n" + sum;

		return new NarrativeTurn
		{
			Narration = body,
			Options = Options,
			CustomHint = CustomHint,
			ShowCharacterFrame = ShowCharacterFrame
		};
	}

	public static string BuildFallbackJson()
	{
		return "{\"place_name\":\"此处\",\"arrival_mode\":\"symbolic\",\"encounter_type\":\"pause\",\"encounter_summary\":\"\",\"character_name\":\"\",\"character_role\":\"\",\"scene_visual_brief\":\"室内一角，安静、低饱和\",\"character_visual_brief\":\"\",\"show_scene_image\":true,\"show_character_frame\":false,\"narration\":\"时间从指缝里滑过去，你没再多说什么。\",\"options\":[{\"slot\":\"face\",\"label\":\"继续把事做完\"},{\"slot\":\"deflect\",\"label\":\"先转开注意力\"},{\"slot\":\"pause\",\"label\":\"停下来发呆一会\"}],\"custom_hint\":\"也可以自己输入真正想做的事。\"}";
	}

	private static string ReadStr(JsonElement root, string name, string def)
	{
		if (!root.TryGetProperty(name, out var el)) return def;
		return el.ValueKind switch
		{
			JsonValueKind.String => el.GetString() ?? def,
			JsonValueKind.Number => el.GetRawText(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			_ => el.ValueKind == JsonValueKind.Null ? def : el.GetRawText()
		};
	}

	private static bool ReadBool(JsonElement root, string name, bool def)
	{
		if (!root.TryGetProperty(name, out var el)) return def;
		return el.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.String => el.GetString() is "true" or "1",
			JsonValueKind.Number => el.TryGetInt32(out var n) && n != 0,
			_ => def
		};
	}

	private static string NormalizeArrival(string v) => v switch
	{
		"real" or "symbolic" or "blocked_at_door" or "memory_projection" or "unreachable_today" => v,
		_ => "symbolic"
	};

	/// <summary>模型偶发只给一句短旁白时，补一层感官尾巴，避免 MUD 区空白感（不改变已足够长的文本）。</summary>
	private static void PadShortNarration(EncounterFrame f)
	{
		var n = (f.Narration ?? "").Trim();
		if (n.Length >= 40) return;
		if (string.IsNullOrEmpty(n)) return;
		f.Narration = n + " 纸页轻响，你听见自己的呼吸与窗外极细的风声；这一刻像被放慢半拍，字句落下去，心里却还悬着半句。";
	}
}
