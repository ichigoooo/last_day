using Godot;

/// <summary>
/// API Key / Base URL 首次设置页。
/// </summary>
public partial class SettingsScreen : Control
{
	private LineEdit _keyEdit;
	private LineEdit _urlEdit;
	private LineEdit _modelEdit;
	private Label _statusLabel;
	private Button _saveButton;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);

		_keyEdit = GetNode<LineEdit>("%ApiKeyEdit");
		_urlEdit = GetNode<LineEdit>("%BaseUrlEdit");
		_modelEdit = GetNode<LineEdit>("%ModelEdit");
		_statusLabel = GetNode<Label>("%StatusLabel");
		_saveButton = GetNode<Button>("%SaveButton");

		ApplySettingsCardPresentation();

		SaveManager.Instance?.EnsureMiniMaxMigratedToArk();
		var s = SaveManager.Instance?.Settings ?? new UserSettings();
		_keyEdit.Text = s.ApiKey;
		_urlEdit.Text = s.BaseUrl;
		_modelEdit.Text = s.Model;
		ApiBridge.Instance?.Configure(s.ApiKey, s.Model, s.BaseUrl);

		_saveButton.Pressed += OnSavePressed;
	}

	/// <summary>
	/// 设置页使用浅色卡面素材：墨色文字、去掉浅色描边，保证在羊皮纸纹理上可读。
	/// </summary>
	private void ApplySettingsCardPresentation()
	{
		var ink = new Color(0.16f, 0.12f, 0.1f);
		var inkMuted = new Color(0.36f, 0.3f, 0.24f);
		var fieldInk = new Color(0.14f, 0.12f, 0.1f);
		var btnInk = new Color(0.14f, 0.11f, 0.09f);
		var btnHover = new Color(0.42f, 0.3f, 0.14f);
		var btnPressed = new Color(0.1f, 0.08f, 0.07f);

		void StyleLabel(Label lb, int fontSize)
		{
			lb.AddThemeFontSizeOverride("font_size", fontSize);
			lb.AddThemeColorOverride("font_color", lb == _statusLabel ? inkMuted : ink);
			lb.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
			lb.AddThemeConstantOverride("shadow_offset_x", 0);
			lb.AddThemeConstantOverride("shadow_offset_y", 0);
		}

		var header = GetNodeOrNull<Label>("Panel/VBox/Header");
		if (header != null) StyleLabel(header, 38);
		var l1 = GetNodeOrNull<Label>("Panel/VBox/L1");
		if (l1 != null) StyleLabel(l1, AppTheme.FontSizeBody);
		var l2 = GetNodeOrNull<Label>("Panel/VBox/L2");
		if (l2 != null) StyleLabel(l2, AppTheme.FontSizeBody);
		var l3 = GetNodeOrNull<Label>("Panel/VBox/L3");
		if (l3 != null) StyleLabel(l3, AppTheme.FontSizeBody);
		StyleLabel(_statusLabel, AppTheme.FontSizeCaption);

		foreach (var le in new[] { _keyEdit, _urlEdit, _modelEdit })
		{
			le.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBodySmall);
			le.AddThemeColorOverride("font_color", fieldInk);
			le.AddThemeColorOverride("caret_color", new Color(0.35f, 0.22f, 0.12f));
			le.AddThemeColorOverride("font_placeholder_color", new Color(0.45f, 0.38f, 0.32f));
			le.CustomMinimumSize = new Vector2(0, 52);
		}

		_saveButton.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBody);
		_saveButton.AddThemeColorOverride("font_color", btnInk);
		_saveButton.AddThemeColorOverride("font_hover_color", btnHover);
		_saveButton.AddThemeColorOverride("font_pressed_color", btnPressed);
		_saveButton.CustomMinimumSize = new Vector2(0, 58);
	}

	private async void OnSavePressed()
	{
		var sm = SaveManager.Instance;
		if (sm == null) return;

		var key = _keyEdit.Text.Trim();
		if (string.IsNullOrEmpty(key))
		{
			_statusLabel.Text = "请填写 API Key，未配置无法体验游戏。";
			_statusLabel.AddThemeColorOverride("font_color", new Color(0.52f, 0.22f, 0.18f));
			return;
		}

		var u = sm.Settings;
		u.ApiKey = key;
		u.BaseUrl = string.IsNullOrWhiteSpace(_urlEdit.Text)
			? UserSettings.DefaultApiBaseUrl
			: _urlEdit.Text.Trim().TrimEnd('/');
		u.Model = string.IsNullOrWhiteSpace(_modelEdit.Text) ? UserSettings.DefaultModelId : _modelEdit.Text.Trim();
		u.FirstSetupCompleted = true;
		sm.UpdateSettings(u);

		ApiBridge.Instance?.Configure(u.ApiKey, u.Model, u.BaseUrl);

		_statusLabel.Text = "已保存。";
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.22f, 0.42f, 0.28f));
		await ToSignal(GetTree().CreateTimer(0.4f), Timer.SignalName.Timeout);
		await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}
}
