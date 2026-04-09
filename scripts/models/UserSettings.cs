using System.Text.Json.Serialization;

/// <summary>
/// user://settings.json — API 与首次引导。
/// </summary>
public class UserSettings
{
	[JsonPropertyName("api_key")]
	public string ApiKey { get; set; } = "";

	[JsonPropertyName("base_url")]
	public string BaseUrl { get; set; } = "https://api.deepseek.com";

	[JsonPropertyName("model")]
	public string Model { get; set; } = "deepseek-chat";

	/// <summary>用户已完成首次 API 设置（可跳过引导）。</summary>
	[JsonPropertyName("first_setup_completed")]
	public bool FirstSetupCompleted { get; set; }
}
