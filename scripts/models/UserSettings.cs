using System.Text.Json.Serialization;

/// <summary>
/// user://settings.json — API 与首次引导。
/// </summary>
public class UserSettings
{
	/// <summary>火山方舟（豆包）REST 根地址，不含 <c>/api/v3</c> 路径。</summary>
	public const string DefaultApiBaseUrl = "https://ark.cn-beijing.volces.com";

	/// <summary>控制台推理接入点对应的模型 ID，可按需更换。</summary>
	public const string DefaultModelId = "doubao-seed-2-0-mini-260215";

	[JsonPropertyName("api_key")]
	public string ApiKey { get; set; } = "";

	[JsonPropertyName("base_url")]
	public string BaseUrl { get; set; } = DefaultApiBaseUrl;

	[JsonPropertyName("model")]
	public string Model { get; set; } = DefaultModelId;

	/// <summary>用户已完成首次 API 设置（可跳过引导）。</summary>
	[JsonPropertyName("first_setup_completed")]
	public bool FirstSetupCompleted { get; set; }
}
