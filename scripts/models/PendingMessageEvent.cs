using System.Text.Json.Serialization;

/// <summary>
/// 待发手机回复：到点由 MessageSystem 调 message_reply。
/// </summary>
public class PendingMessageEvent
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	/// <summary>剩余游戏分钟（随 TimeManager 递减）。</summary>
	[JsonPropertyName("remaining_game_minutes")]
	public int RemainingGameMinutes { get; set; }

	[JsonPropertyName("relationship")]
	public string Relationship { get; set; } = "朋友";

	[JsonPropertyName("persona_hint")]
	public string PersonaHint { get; set; } = "克制、短句";

	[JsonPropertyName("latest_message")]
	public string LatestMessage { get; set; } = "";
}
