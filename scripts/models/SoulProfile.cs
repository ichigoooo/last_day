using System.Text.Json.Serialization;

/// <summary>
/// 灵魂画像：入殓登记三问与标签（阶段 1 填充）。
/// </summary>
public class SoulProfile
{
	[JsonPropertyName("tags")]
	public string[] Tags { get; set; } = [];

	[JsonPropertyName("work")]
	public string WorkText { get; set; } = "";

	[JsonPropertyName("relation")]
	public string RelationText { get; set; } = "";

	[JsonPropertyName("escape")]
	public string EscapeText { get; set; } = "";
}
