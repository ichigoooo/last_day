using System;

/// <summary>
/// 本地情绪安全：自杀/自伤相关关键词（不依赖 LLM）。命中则中止当前流程。
/// </summary>
public static class CrisisKeywordGuard
{
	private static readonly string[] Keywords =
	[
		"自杀", "自殺", "自尽", "轻生", "不想活", "活不下去", "结束生命", "结束自己",
		"自残", "自殘", "割腕", "割手臂", "跳楼", "上吊", "服毒", "吃安眠药", "烧炭",
		"一了百了", "死了算了", "去死吧", "想死", "寻死", "了断", "自我了断",
		"kys", "suicide", "kill myself", "end my life"
	];

	/// <summary>若文本包含任一危机关键词则返回 true（中英大小写不敏感仅作用于 ASCII 片段）。</summary>
	public static bool ContainsCrisisContent(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return false;
		var t = text.Trim();
		foreach (var kw in Keywords)
		{
			if (string.IsNullOrEmpty(kw)) continue;
			if (kw.Length == 1)
			{
				if (t.Contains(kw, StringComparison.Ordinal)) return true;
				continue;
			}
			var cmp = IsAsciiLetters(kw)
				? StringComparison.OrdinalIgnoreCase
				: StringComparison.Ordinal;
			if (t.Contains(kw, cmp)) return true;
		}
		return false;
	}

	private static bool IsAsciiLetters(string s)
	{
		foreach (var c in s)
		{
			if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') return true;
		}
		return false;
	}
}
