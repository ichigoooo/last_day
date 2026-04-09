using System.Text.Json.Serialization;

/// <summary>
/// 单局运行时数据（单次完整体验的一局）。
/// </summary>
public class GameSession
{
	[JsonPropertyName("soul")]
	public SoulProfile Soul { get; set; } = new();

	/// <summary>
	/// 宣判阶段确认的本局核心遗愿。
	/// 它是“这一天最想完成的一件事”，不是当前回合正在输入的即时动作。
	/// </summary>
	[JsonPropertyName("final_wish")]
	public string FinalWish { get; set; } = "";

	[JsonPropertyName("death_cause_id")]
	public string DeathCauseId { get; set; } = "";

	/// <summary>当前局选中的死因全文（展示用）。</summary>
	[JsonPropertyName("death_cause_text")]
	public string DeathCauseText { get; set; } = "";

	/// <summary>由序章档案处提炼出的中段叙事锚点。</summary>
	[JsonPropertyName("archive_summary")]
	public string ArchiveSummary { get; set; } = "";

	[JsonPropertyName("reaper_annotation")]
	public string ReaperAnnotation { get; set; } = "";

	[JsonPropertyName("reaper_opening")]
	public string ReaperOpening { get; set; } = "";

	/// <summary>本局行为时间线（对话、选项、短信、场景），用于终局悼词等 LLM 输入。</summary>
	[JsonPropertyName("activity_log")]
	public SessionActivityLog ActivityLog { get; set; } = new();

	/// <summary>最后一天有限世界状态（地点、摘要、回合计数等）。</summary>
	[JsonPropertyName("world")]
	public WorldState World { get; set; } = new();

	/// <summary>葬礼阶段生成的悼词全文。</summary>
	[JsonPropertyName("funeral_eulogy")]
	public string FuneralEulogy { get; set; } = "";

	/// <summary>用户确认的墓志铭。</summary>
	[JsonPropertyName("epitaph")]
	public string Epitaph { get; set; } = "";

	/// <summary>用户确认的一句遗言。</summary>
	[JsonPropertyName("last_words")]
	public string LastWords { get; set; } = "";

	/// <summary>终局页可选一句承诺（不留存多局历史）。</summary>
	[JsonPropertyName("ending_pledge")]
	public string EndingPledge { get; set; } = "";
}
