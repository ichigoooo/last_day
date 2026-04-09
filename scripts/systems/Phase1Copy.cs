using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>阶段 1 LLM 失败或未配置时的固定兜底文案。</summary>
public static class Phase1Copy
{
	public static string FallbackAnnotation()
	{
		return "墨迹未干，你在三线之间走得歪扭，却比空白更接近真实。";
	}

	public static string FallbackOpening(string deathCausePreview)
	{
		var tail = string.IsNullOrEmpty(deathCausePreview)
			? ""
			: (deathCausePreview.Length > 40 ? deathCausePreview[..40] + "…" : deathCausePreview);
		return string.IsNullOrEmpty(tail)
			? "裁定已盖。下面是你仍握得住的那一点点时间。"
			: $"纸面写着：「{tail}」——名字不重要了，重要的是你还剩几行空白可填。";
	}

	/// <summary>无 API 或生成失败时的死因正文（与标签略作关联）。</summary>
	public static string FallbackDeathCauseBody(SoulProfile soul)
	{
		var tags = new HashSet<string>(soul?.Tags ?? []);
		if (tags.Contains("工作"))
			return "在第三十七次「再对齐一下」里，你的存在感被误标为可忽略项，随后在一次合并提交中静默删除。";
		if (tags.Contains("关系"))
			return "你已读不回的那一栏，终于反过来读你：沉默在两端同时到期。";
		if (tags.Contains("逃避"))
			return "你把人生设成「稍后提醒」，提醒在日历之外排队，直到账户注销。";
		return "纸页写满旁注，正文却始终空白——于是空白成了最后的死因。";
	}

	public static string[] FallbackWishes(SoulProfile soul)
	{
		var tags = new HashSet<string>(soul?.Tags ?? []);
		if (tags.Contains("关系"))
			return ["给一个人当面说一句迟到的谢谢", "删掉一段消耗你的对话记录", "认真听别人讲完三句话"];
		if (tags.Contains("工作"))
			return ["把工位收拾成可以随时离开的样子", "给未完成的文档写一行交接注脚", "对镜子练习一次平静的离职微笑"];
		if (tags.Contains("逃避"))
			return ["把手机放远一小时，去窗边站满一百次呼吸", "写下一件明天仍不想做的事并划掉它", "走一条从未走过的回家路线"];
		return ["吃一样真正想吃的食物", "给在乎的人发一条不解释理由的问候", "在纸上写一句对自己诚实的话"];
	}

	public static string FallbackEulogy(SoulProfile soul)
	{
		var w = soul?.WorkText ?? "";
		var quote = string.IsNullOrWhiteSpace(w) ? "这一天" : $"「{(w.Length > 24 ? w[..24] + "…" : w)}」";
		return $"有人说{quote}不过如此。纸面仍短，却把一个人从空白里扶了起来。悼词到此为止，余下是沉默。";
	}

	public static string[] FallbackEpitaphs()
	{
		return ["此处曾认真活过一天", "来不及写完，也算落款", "迟到的诚实安放于此"];
	}

	public static string FallbackLastWords()
	{
		return "如果还有下一页，请从一句真话写起。";
	}

	public static (string Opening, string Breath, string Echo, string Closing) FallbackMeditationQuarters()
	{
		return (
			"先停一下，不必解释这一天。",
			"呼吸放慢，像把声音从胸口挪开半寸。",
			"你写下的那些话还在，不必当场读懂。",
			"门外的光还在，等你愿意再迈一步。"
		);
	}
}
