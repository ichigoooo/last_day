using System.Text.Json.Serialization;

/// <summary>运行时死因：由 LLM 生成或兜底文案；Id 为正文稳定哈希便于存档引用。</summary>
public class DeathCause
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	[JsonPropertyName("text")]
	public string Text { get; set; } = "";
}
