using System.Collections.Generic;

/// <summary>「最后一天」现场面对面对话运行时状态（不入存档，仅内存）。</summary>
public sealed class DialogueSession
{
	public string SessionId { get; set; } = "";
	public string LocationId { get; set; } = "";
	public string CharacterName { get; set; } = "";
	public string CharacterRole { get; set; } = "";
	public string CharacterVisualBrief { get; set; } = "";
	public string SceneVisualBrief { get; set; } = "";
	public bool ShowSceneImage { get; set; } = true;
	/// <summary>冻结的展示地点名（与遭遇帧一致）。</summary>
	public string PlaceDisplayName { get; set; } = "";

	/// <summary>紧凑会话摘要，供续聊 prompt 使用。</summary>
	public string RunningSummary { get; set; } = "";

	public List<string> RecentExchangeLines { get; set; } = new();
}
