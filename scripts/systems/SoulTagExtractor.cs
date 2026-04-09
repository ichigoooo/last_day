using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 从三问中提取灵魂标签：关键词规则 + 可选 LLM 精炼。
/// 标准标签：工作、关系、逃避。
/// </summary>
public static class SoulTagExtractor
{
	private static readonly string[] WorkKeywords =
	[
		"工作", "上班", "公司", "老板", "同事", "加班", "项目", "会议", "ppt", "kpi", "客户", "工资",
		"离职", "职场", "工位", "甲方", "需求", "周报", "打卡", "考勤", "领导", "业绩", "简历", "面试"
	];

	private static readonly string[] RelationKeywords =
	[
		"关系", "父母", "朋友", "恋人", "爱", "分手", "结婚", "家庭", "孩子", "老婆", "老公", "离婚",
		"冷战", "聊天", "社交", "孤独", "亲密", "婚礼", "恋爱", "亲情", "友情", "伴侣", "家人"
	];

	private static readonly string[] EscapeKeywords =
	[
		"逃避", "拖延", "明天", "以后", "躺平", "刷剧", "游戏", "手机", "不想", "假装", "麻醉", "碎片",
		"短视频", "熬夜", "宿醉", "拖延症", "再等等", "再说吧", "无所谓"
	];

	public static void ApplyKeywordTags(SoulProfile profile)
	{
		if (profile == null) return;
		var blob = $"{profile.WorkText}\n{profile.RelationText}\n{profile.EscapeText}";
		var lower = blob.ToLowerInvariant();
		var tags = new HashSet<string>();
		if (ContainsAny(lower, WorkKeywords)) tags.Add("工作");
		if (ContainsAny(blob, RelationKeywords)) tags.Add("关系");
		if (ContainsAny(blob, EscapeKeywords)) tags.Add("逃避");
		profile.Tags = tags.ToArray();
	}

	private static bool ContainsAny(string text, string[] keys)
	{
		foreach (var k in keys)
		{
			if (text.Contains(k, StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	/// <summary>在关键词结果上合并 LLM 判断；未配置或失败则保持原 tags。</summary>
	public static async Task TryRefineWithLlmAsync(SoulProfile profile)
	{
		if (profile == null) return;
		var api = ApiBridge.Instance;
		if (api == null || !api.IsConfigured) return;

		var system = PromptLoader.LoadSystem("soul_tag_extraction");
		var tmpl = PromptLoader.LoadUser("soul_tag_extraction");
		var user = PromptLoader.ApplyVars(tmpl, new Dictionary<string, string>
		{
			["work"] = profile.WorkText,
			["relation"] = profile.RelationText,
			["escape"] = profile.EscapeText
		});

		var result = await api.ChatJsonAsync(system, user, fallback: null);
		if (!result.Success || string.IsNullOrWhiteSpace(result.Content)) return;

		try
		{
			using var doc = JsonDocument.Parse(result.Content);
			if (!doc.RootElement.TryGetProperty("tags", out var arr) || arr.ValueKind != JsonValueKind.Array)
				return;
			var allowed = new HashSet<string>(profile.Tags ?? []);
			foreach (var el in arr.EnumerateArray())
			{
				var t = el.GetString();
				if (t == "工作" || t == "关系" || t == "逃避") allowed.Add(t);
			}
			profile.Tags = allowed.ToArray();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SoulTagExtractor] JSON 解析失败: {e.Message}");
		}
	}
}
