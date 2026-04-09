using System.Text.Json.Serialization;

public class DeathSpendResult
{
	[JsonPropertyName("narration")]
	public string Narration { get; set; } = "";

	[JsonPropertyName("effect_line")]
	public string EffectLine { get; set; } = "";

	[JsonPropertyName("memory_tag")]
	public string MemoryTag { get; set; } = "";
}
