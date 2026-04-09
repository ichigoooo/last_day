using System.Text.Json.Serialization;

/// <summary>
/// user://save_data.json 根结构。本作为单次体验，不保留多局「轮回簿」式记录。
/// </summary>
public class SaveDataRoot
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;
}
