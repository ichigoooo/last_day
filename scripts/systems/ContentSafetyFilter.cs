using System;
using System.Text.RegularExpressions;

/// <summary>
/// LLM 输出展示前轻量清洗：敏感词弱化。
/// </summary>
public static class ContentSafetyFilter
{
	private static readonly string[] SoftReplace =
	[
		"血腥", "暴力", "自残", "自杀", "色情", "淫秽"
	];

	/// <summary>第四颗「自定义」按钮文案：模型常误把旁白塞进 custom_hint，此处限制为短标签。</summary>
	public static string ClampLastDayCustomHint(string text, int maxLen = 16)
	{
		if (string.IsNullOrWhiteSpace(text)) return text ?? "";
		var s = text.Trim().Replace("\n", " ").Replace("\r", "");
		if (s.Length <= maxLen) return s;
		return "也可以自己输入…";
	}

	public static string SanitizeDisplay(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;
		var s = text.Trim();
		foreach (var w in SoftReplace)
		{
			if (s.Contains(w, StringComparison.Ordinal))
				s = s.Replace(w, "……", StringComparison.Ordinal);
		}
		return s;
	}

	/// <summary>
	/// 「最后一天」遭遇帧展示：把模型常用的元叙事主语「玩家」改为第二人称「你」，避免与沉浸式旁白冲突。
	/// 对少量固定复合词（如「游戏玩家」）保留不拆。
	/// </summary>
	public static string NormalizeLastDayNoMetaPlayerYou(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;
		var s = text;
		const string phGame = "\uE000GH_GAMEPLAYER\uE001";
		const string phOld = "\uE000GH_OLDPLAYER\uE001";
		const string phNewbie = "\uE000GH_NEWBIE\uE001";
		if (s.Contains("游戏玩家", StringComparison.Ordinal))
			s = s.Replace("游戏玩家", phGame, StringComparison.Ordinal);
		if (s.Contains("老玩家", StringComparison.Ordinal))
			s = s.Replace("老玩家", phOld, StringComparison.Ordinal);
		if (s.Contains("新手玩家", StringComparison.Ordinal))
			s = s.Replace("新手玩家", phNewbie, StringComparison.Ordinal);
		if (!s.Contains("玩家", StringComparison.Ordinal))
			return s;
		s = s.Replace("玩家", "你", StringComparison.Ordinal);
		s = s.Replace(phGame, "游戏玩家", StringComparison.Ordinal);
		s = s.Replace(phOld, "老玩家", StringComparison.Ordinal);
		s = s.Replace(phNewbie, "新手玩家", StringComparison.Ordinal);
		return s;
	}

	/// <summary>死因等短句：上限与套话轻削。</summary>
	public static string PolishDeathCauseBody(string text, int maxLen = 220)
	{
		if (string.IsNullOrEmpty(text)) return text;
		var s = SanitizeDisplay(text.Trim());
		s = Regex.Replace(s, @"\s+", " ");
		s = Regex.Replace(s, "(仿佛)\\1+", "$1");
		if (s.Length > maxLen)
			s = s[..maxLen].TrimEnd() + "……";
		return s;
	}
}
