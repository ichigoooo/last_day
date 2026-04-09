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

		SaveManager.Instance?.EnsureMiniMaxMigratedToArk();
		var s = SaveManager.Instance?.Settings ?? new UserSettings();
		_keyEdit.Text = s.ApiKey;
		_urlEdit.Text = s.BaseUrl;
		_modelEdit.Text = s.Model;
		ApiBridge.Instance?.Configure(s.ApiKey, s.Model, s.BaseUrl);

		_saveButton.Pressed += OnSavePressed;
	}

	private async void OnSavePressed()
	{
		var sm = SaveManager.Instance;
		if (sm == null) return;

		var key = _keyEdit.Text.Trim();
		if (string.IsNullOrEmpty(key))
		{
			_statusLabel.Text = "请填写 API Key，未配置无法体验游戏。";
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
		await ToSignal(GetTree().CreateTimer(0.4f), Timer.SignalName.Timeout);
		await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}
}
