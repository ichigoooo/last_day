/// <summary>单回合「意图→结算→遭遇帧」的对外结果。</summary>
public class LastDayTurnResult
{
	public bool Ok { get; set; }
	public string Error { get; set; } = "";
	/// <summary>本回合结构化呈现协议；UI 应优先使用此对象。</summary>
	public EncounterFrame Encounter { get; set; } = new();
	/// <summary>兼容旧 UI 路径；等价于 <see cref="Encounter"/> 的旁白与选项子集。</summary>
	public NarrativeTurn Narrative { get; set; } = new();
	public ResolvedAction Resolved { get; set; } = new();
	public string IntentSummary { get; set; } = "";
	/// <summary>本回合是否关闭了面对面对话并回到 ambient（用于 UI 一次性格式化恢复）。</summary>
	public bool ClosedFaceToFaceDialogue { get; set; }
}
