using System.Text.Json.Serialization;

/// <summary>
/// 系统对单回合行动的裁决结果（时间/钱/电/消息等），交给 narrative_render 润色。
/// </summary>
public class ResolvedAction
{
	[JsonPropertyName("game_minutes_cost")]
	public int GameMinutesCost { get; set; }

	[JsonPropertyName("money_delta_yuan")]
	public int MoneyDeltaYuan { get; set; }

	/// <summary>本回合叙事是否计为「用过手机」以触发 <see cref="BatterySystem.ApplyTurnDrain"/>（与是否打开过手机 UI 无关）。</summary>
	[JsonPropertyName("screen_active")]
	public bool ScreenActive { get; set; } = true;

	[JsonPropertyName("enqueue_message_send")]
	public bool EnqueueMessageSend { get; set; }

	[JsonPropertyName("message_target_hint")]
	public string MessageTargetHint { get; set; } = "";

	[JsonPropertyName("intent_type")]
	public string IntentType { get; set; } = "";

	[JsonPropertyName("location_changed")]
	public bool LocationChanged { get; set; }

	[JsonPropertyName("new_location_id")]
	public string NewLocationId { get; set; } = "";

	/// <summary>未能映射到 canonical 地点时的玩家目的地原文，供遭遇帧与 UI 展示。</summary>
	[JsonPropertyName("freeform_destination_text")]
	public string FreeformDestinationText { get; set; } = "";

	[JsonPropertyName("reject_note")]
	public string RejectNote { get; set; } = "";

	/// <summary>供 LLM 理解的简短系统说明（中文）。</summary>
	[JsonPropertyName("system_note")]
	public string SystemNote { get; set; } = "";
}
