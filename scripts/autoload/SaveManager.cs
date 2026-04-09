using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 持久化：存档根（save_data.json，预留扩展）与用户设置（settings.json）。
/// </summary>
public partial class SaveManager : Node
{
	public const string SaveDataPath = "user://save_data.json";
	public const string SettingsPath = "user://settings.json";
	private const string LegacyApiConfigPath = "user://api_config.json";

	private static SaveManager _instance;
	public static SaveManager Instance => _instance;

	private SaveDataRoot _saveData = new();
	private UserSettings _settings = new();

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public SaveDataRoot SaveData => _saveData;
	public UserSettings Settings => _settings;

	public override void _EnterTree()
	{
		_instance = this;
	}

	public override void _Ready()
	{
		LoadSettingsInternal();
		MigrateLegacyApiConfig();
		MigrateMiniMaxToArkIfNeeded();
		LoadSaveDataInternal();
	}

	private void MigrateLegacyApiConfig()
	{
		if (!Godot.FileAccess.FileExists(LegacyApiConfigPath)) return;
		try
		{
			using var f = Godot.FileAccess.Open(LegacyApiConfigPath, Godot.FileAccess.ModeFlags.Read);
			if (f == null) return;
			var legacy = JsonSerializer.Deserialize<LegacyApiConfig>(f.GetAsText());
			if (legacy == null) return;
			if (!string.IsNullOrEmpty(legacy.ApiKey)) _settings.ApiKey = legacy.ApiKey;
			if (!string.IsNullOrEmpty(legacy.BaseUrl)) _settings.BaseUrl = legacy.BaseUrl.TrimEnd('/');
			if (!string.IsNullOrEmpty(legacy.Model)) _settings.Model = legacy.Model;
			_settings.FirstSetupCompleted = true;
			SaveSettingsInternal();
			var abs = ProjectSettings.GlobalizePath(LegacyApiConfigPath);
			if (File.Exists(abs)) File.Delete(abs);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SaveManager] 迁移 api_config 失败: {e.Message}");
		}
	}

	private class LegacyApiConfig
	{
		[JsonPropertyName("api_key")] public string ApiKey { get; set; }
		[JsonPropertyName("base_url")] public string BaseUrl { get; set; }
		[JsonPropertyName("model")] public string Model { get; set; }
	}

	/// <summary>旧版 MiniMax 配置无法用于火山方舟；一次性迁移为豆包默认并清空 Key，需用户填写 Ark Key。</summary>
	private void MigrateMiniMaxToArkIfNeeded()
	{
		if (!LooksLikeMiniMaxSettings(_settings))
			return;

		GD.Print("[SaveManager] 检测到 MiniMax 配置，已切换为火山方舟（豆包）默认 Endpoint；API Key 已清空，请填写 Ark 端点密钥。");
		_settings.BaseUrl = UserSettings.DefaultApiBaseUrl;
		_settings.Model = UserSettings.DefaultModelId;
		_settings.ApiKey = "";
		SaveSettingsInternal();
	}

	private static bool LooksLikeMiniMaxSettings(UserSettings s)
	{
		if (s == null) return false;
		if (!string.IsNullOrEmpty(s.BaseUrl) &&
		    s.BaseUrl.Contains("minimaxi", StringComparison.OrdinalIgnoreCase))
			return true;
		if (!string.IsNullOrEmpty(s.Model) &&
		    (s.Model.Contains("MiniMax", StringComparison.OrdinalIgnoreCase) ||
		     s.Model.Contains("minimax", StringComparison.OrdinalIgnoreCase)))
			return true;
		return false;
	}

	public void LoadSettings()
	{
		LoadSettingsInternal();
	}

	/// <summary>打开设置页时可调用：若本地仍为 MiniMax，切换豆包并清空 Key（幂等）。</summary>
	public void EnsureMiniMaxMigratedToArk()
	{
		MigrateMiniMaxToArkIfNeeded();
	}

	private void LoadSettingsInternal()
	{
		if (!Godot.FileAccess.FileExists(SettingsPath)) return;
		try
		{
			using var f = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Read);
			if (f == null) return;
			var loaded = JsonSerializer.Deserialize<UserSettings>(f.GetAsText(), JsonOpts);
			if (loaded != null) _settings = loaded;
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SaveManager] 读取设置失败: {e.Message}");
		}
	}

	public void SaveSettings()
	{
		SaveSettingsInternal();
	}

	private void SaveSettingsInternal()
	{
		try
		{
			var json = JsonSerializer.Serialize(_settings, JsonOpts);
			using var f = Godot.FileAccess.Open(SettingsPath, Godot.FileAccess.ModeFlags.Write);
			f?.StoreString(json);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SaveManager] 写入设置失败: {e.Message}");
		}
	}

	public void UpdateSettings(UserSettings next)
	{
		_settings = next ?? new UserSettings();
		SaveSettingsInternal();
	}

	private void LoadSaveDataInternal()
	{
		if (!Godot.FileAccess.FileExists(SaveDataPath)) return;
		try
		{
			using var f = Godot.FileAccess.Open(SaveDataPath, Godot.FileAccess.ModeFlags.Read);
			if (f == null) return;
			var loaded = JsonSerializer.Deserialize<SaveDataRoot>(f.GetAsText(), JsonOpts);
			if (loaded != null) _saveData = loaded;
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SaveManager] 读取存档失败: {e.Message}");
		}
	}

	public void SaveSaveData()
	{
		try
		{
			var json = JsonSerializer.Serialize(_saveData, JsonOpts);
			using var f = Godot.FileAccess.Open(SaveDataPath, Godot.FileAccess.ModeFlags.Write);
			f?.StoreString(json);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[SaveManager] 写入存档失败: {e.Message}");
		}
	}

}
