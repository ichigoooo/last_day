using System.Text.Json.Serialization;

/// <summary>死神花钱菜单条目（来自 resources/spend_options.json）。</summary>
public class SpendOption
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("description")]
	public string Description { get; set; } = "";

	[JsonPropertyName("amount")]
	public int Amount { get; set; }
}
