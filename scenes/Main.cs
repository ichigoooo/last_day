using Godot;

/// <summary>
/// 入口：检查首次设置 → 主菜单 → 跳转各阶段占位场景。
/// </summary>
public partial class Main : Control
{
	private Button _settingsButton;
	private Button _startButton;
	private Button _testApiButton;
	private Label _hintLabel;

	public override async void _Ready()
	{
		AppTheme.ApplyTo(this);
		GameManager.Instance?.SetPhase(GameManager.Phase.MainMenu);

		_settingsButton = GetNode<Button>("%OpenSettingsButton");
		_startButton = GetNode<Button>("%StartFlowButton");
		_testApiButton = GetNode<Button>("%TestApiButton");
		_hintLabel = GetNode<Label>("%HintLabel");

		foreach (var b in new[] { _settingsButton, _startButton, _testApiButton })
		{
			b.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
			b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		}

		_settingsButton.Pressed += OnOpenSettings;
		_startButton.Pressed += OnStartFlow;
		_testApiButton.Pressed += OnTestApi;

		RefreshHint();

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		var sm = SaveManager.Instance;
		if (sm != null && !sm.Settings.FirstSetupCompleted && SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Settings);
	}

	private void RefreshHint()
	{
		var sm = SaveManager.Instance;
		var done = sm?.Settings.FirstSetupCompleted == true && ApiBridge.Instance?.IsConfigured == true;
		_hintLabel.Text = done
			? "已配置 API，可从「开始体验」进入流程。"
			: "请先在「API 设置」中填写并保存 API Key，否则无法体验。";
	}

	private async void OnOpenSettings()
	{
		await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Settings);
	}

	private async void OnStartFlow()
	{
		if (ApiBridge.Instance != null && !ApiBridge.Instance.IsConfigured)
		{
			_hintLabel.Text = "请先填写并保存 API Key。";
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Settings);
			return;
		}

		await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.DeathRegistration);
	}

	private async void OnTestApi()
	{
		if (ApiBridge.Instance == null || !ApiBridge.Instance.IsConfigured)
		{
			_hintLabel.Text = "未配置 API，无法测试。";
			return;
		}

		_hintLabel.Text = "请求中…";
		var result = await ApiBridge.Instance.ChatTextAsync(
			"你是一个极简助手，只输出一个词：你好",
			"请回复「你好」两个字。",
			fallback: "（请求失败）");

		_hintLabel.Text = result.Success
			? $"API 正常：{result.Content.Trim()}"
			: $"失败：{result.Error}";
	}
}
