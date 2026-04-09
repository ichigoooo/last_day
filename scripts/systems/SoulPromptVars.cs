using System;
using System.Collections.Generic;

/// <summary>
/// 灵魂画像 → Prompt 变量（最后一天、手机等）。</summary>
public static class SoulPromptVars
{
	public static void AddSoulFields(Dictionary<string, string> vars, SoulProfile soul)
	{
		soul ??= new SoulProfile();
		var tags = soul.Tags ?? [];
		var joined = string.Join("、", tags);
		vars["soul_tags"] = string.IsNullOrEmpty(joined) ? "（无标签）" : joined;
		vars["work_excerpt"] = Excerpt(soul.WorkText, 100);
		vars["relation_excerpt"] = Excerpt(soul.RelationText, 100);
		vars["escape_excerpt"] = Excerpt(soul.EscapeText, 100);
	}

	public static string Excerpt(string t, int maxChars)
	{
		if (string.IsNullOrWhiteSpace(t)) return "（未写）";
		var one = t.Trim().Replace('\n', ' ').Replace('\r', ' ');
		return one.Length <= maxChars ? one : one[..maxChars] + "…";
	}

	/// <summary>由标签确定性挑选语气提示，增加死因句式差异。</summary>
	public static string ToneHintForDeathCause(SoulProfile soul)
	{
		var h = HashTags(soul);
		var hints = new[]
		{
			"句式像旧档案的结论栏，动词开头，少用比喻堆叠。",
			"冷叙述，一句点出讽刺，不要排比。",
			"短句为主，避免「仿佛」「如同」开头。"
		};
		return hints[Math.Abs(h) % hints.Length];
	}

	public static (string Relationship, string PersonaHint) PickChatPersona(SoulProfile soul)
	{
		var h = HashTags(soul);
		var rels = new[] { "朋友", "同事", "家人", "旧识" };
		var hints = new[] { "克制、短句", "略带调侃", "平静、少打断", "话少、重听" };
		return (rels[Math.Abs(h) % rels.Length], hints[Math.Abs(h / 13) % hints.Length]);
	}

	private static int HashTags(SoulProfile soul)
	{
		var h = 17;
		foreach (var t in soul?.Tags ?? Array.Empty<string>())
			h = h * 31 + (t?.GetHashCode() ?? 0);
		h ^= soul?.WorkText?.GetHashCode() ?? 0;
		return h;
	}
}
