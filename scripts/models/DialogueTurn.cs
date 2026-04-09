using System.Collections.Generic;

/// <summary>单轮面对面对话：对方发言、可选舞台说明、玩家回应选项与结束标记（解析自 LLM JSON）。</summary>
public sealed class DialogueTurn
{
	public string NpcSpokenLine { get; set; } = "";
	public string StageDirection { get; set; } = "";
	public List<NarrativeOption> ReplyOptions { get; set; } = new();
	public string CustomReplyHint { get; set; } = "用一句话回应对方";
	public bool DialogueEnded { get; set; }
	/// <summary>当 <see cref="DialogueEnded"/> 为 true 时，用于切回 ambient 的一句过渡旁白（可为空）。</summary>
	public string ClosingAmbientNote { get; set; } = "";
}
