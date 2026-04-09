using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// LLM 输出展示前轻量清洗：敏感词弱化。
/// </summary>
public static class ContentSafetyFilter
{
	/// <summary>单字 +「阿姨」多为模型臆造姓氏称呼；双字前缀易误伤「隔壁阿姨」等，故仅收单字。</summary>
	private static readonly Regex MotherSingleSurnameAuntPattern = new(@"([\u4e00-\u9fff])阿姨",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex FatherSingleSurnameUnclePattern = new(@"([\u4e00-\u9fff])(叔叔|伯伯|大叔)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex FatherSingleSurnameShuPattern = new(@"([\u4e00-\u9fff])叔",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly HashSet<string> AuntUnclePrefixExclude =
	[
		"好", "老", "小", "那", "这", "某", "每", "各", "多", "大", "二", "三", "四", "五", "六", "七", "八", "九", "十",
		"阿", "隔", "邻", "对", "神", "堂", "表", "姑", "舅", "叔"
	];

	private static readonly Regex InventedAuntCallerName = new(@"^[\u4e00-\u9fff]{1,2}阿姨$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex InventedUncleCallerName = new(@"^[\u4e00-\u9fff]{1,2}(叔叔|伯伯|大叔)$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex InventedShuCallerName = new(@"^[\u4e00-\u9fff]{1,2}叔$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly string[] MotherFigureRoleMarkers =
	[
		"母亲", "妈妈", "妈咪", "阿妈", "咱妈", "你妈", "岳母", "丈母娘", "婆婆", "继母"
	];

	private static readonly string[] FatherFigureRoleMarkers =
	[
		"父亲", "爸爸", "你爸", "咱爸", "爹", "岳父", "公公", "你的父亲"
	];

	private static readonly string[] SoftReplace =
	[
		"血腥", "暴力", "自残", "自杀", "色情", "淫秽"
	];

	/// <summary>输入区上方自定义说明标签文案：模型常误把旁白塞进 custom_hint，此处限制为短标签。</summary>
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
	/// 「最后一天」遭遇帧：将模型臆造的「某姓+阿姨/叔叔」等从核心家人称谓中剥离，旁白与标题改用「她/他」或仅保留关系（如你的母亲）。
	/// </summary>
	public static void NormalizeLastDayEncounterFamilyAppellations(EncounterFrame f)
	{
		if (f == null) return;
		var role = f.CharacterRole ?? "";
		var motherFig = LooksLikeMotherFigureRole(role);
		var fatherFig = !motherFig && LooksLikeFatherFigureRole(role);
		var name = (f.CharacterName ?? "").Trim();

		if (motherFig && InventedAuntCallerName.IsMatch(name))
		{
			ReplaceTokenInFrameTexts(f, name, "她");
			f.CharacterName = "";
		}
		else if (fatherFig && (InventedUncleCallerName.IsMatch(name) || InventedShuCallerName.IsMatch(name)))
		{
			ReplaceTokenInFrameTexts(f, name, "他");
			f.CharacterName = "";
		}

		if (motherFig)
		{
			f.Narration = ReplaceMotherAuntRefs(f.Narration);
			f.DialogueStageNote = ReplaceMotherAuntRefs(f.DialogueStageNote);
			f.EncounterSummary = ReplaceMotherAuntRefs(f.EncounterSummary);
			if (f.Options != null)
				foreach (var o in f.Options)
					o.Label = ReplaceMotherAuntRefs(o.Label);
		}
		else if (fatherFig)
		{
			f.Narration = ReplaceFatherUncleRefs(f.Narration);
			f.DialogueStageNote = ReplaceFatherUncleRefs(f.DialogueStageNote);
			f.EncounterSummary = ReplaceFatherUncleRefs(f.EncounterSummary);
			if (f.Options != null)
				foreach (var o in f.Options)
					o.Label = ReplaceFatherUncleRefs(o.Label);
		}
	}

	private static string ReplaceMotherAuntRefs(string s)
	{
		if (string.IsNullOrEmpty(s)) return s;
		return MotherSingleSurnameAuntPattern.Replace(s, m =>
		{
			var prefix = m.Groups[1].Value;
			if (AuntUnclePrefixExclude.Contains(prefix)) return m.Value;
			return "她";
		});
	}

	private static string ReplaceFatherUncleRefs(string s)
	{
		if (string.IsNullOrEmpty(s)) return s;
		s = FatherSingleSurnameUnclePattern.Replace(s, m =>
		{
			var prefix = m.Groups[1].Value;
			if (AuntUnclePrefixExclude.Contains(prefix)) return m.Value;
			return "他";
		});
		s = FatherSingleSurnameShuPattern.Replace(s, m =>
		{
			var prefix = m.Groups[1].Value;
			if (AuntUnclePrefixExclude.Contains(prefix)) return m.Value;
			return "他";
		});
		return s;
	}

	private static bool LooksLikeMotherFigureRole(string r)
	{
		if (string.IsNullOrEmpty(r)) return false;
		foreach (var m in MotherFigureRoleMarkers)
			if (r.Contains(m, StringComparison.Ordinal))
				return true;
		return false;
	}

	private static bool LooksLikeFatherFigureRole(string r)
	{
		if (string.IsNullOrEmpty(r)) return false;
		foreach (var m in FatherFigureRoleMarkers)
			if (r.Contains(m, StringComparison.Ordinal))
				return true;
		return false;
	}

	private static void ReplaceTokenInFrameTexts(EncounterFrame f, string token, string pronoun)
	{
		if (string.IsNullOrEmpty(token)) return;
		f.Narration = ReplaceWholePhrase(f.Narration ?? "", token, pronoun);
		f.DialogueStageNote = ReplaceWholePhrase(f.DialogueStageNote ?? "", token, pronoun);
		f.EncounterSummary = ReplaceWholePhrase(f.EncounterSummary ?? "", token, pronoun);
		if (f.Options == null) return;
		foreach (var o in f.Options)
			o.Label = ReplaceWholePhrase(o.Label ?? "", token, pronoun);
	}

	private static string ReplaceWholePhrase(string s, string token, string pronoun)
	{
		if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(token)) return s;
		return s.Replace(token, pronoun, StringComparison.Ordinal);
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
