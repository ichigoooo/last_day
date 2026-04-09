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

		ApplyMainMenuParchmentPresentation();

		foreach (var b in new[] { _settingsButton, _startButton, _testApiButton })
		{
			b.CustomMinimumSize = new Vector2(0, 64);
			b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		}

		_startButton.CustomMinimumSize = new Vector2(0, 72);

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
		ApplyMainMenuHintColors();
	}

	/// <summary>
	/// 主菜单使用浅色羊皮纸背景图：覆盖 AppTheme 的浅色字，改为墨色并略增字号以保证对比度。
	/// </summary>
	private void ApplyMainMenuParchmentPresentation()
	{
		var ink = new Color(0.16f, 0.12f, 0.1f);
		var inkMuted = new Color(0.38f, 0.31f, 0.26f);
		var btnInk = new Color(0.14f, 0.11f, 0.09f);
		var btnHover = new Color(0.42f, 0.3f, 0.14f);
		var btnPressed = new Color(0.1f, 0.08f, 0.07f);

		var title = GetNodeOrNull<Label>("Margin/VBox/Title");
		if (title != null)
		{
			title.AddThemeFontSizeOverride("font_size", 56);
			title.AddThemeColorOverride("font_color", ink);
			title.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
			title.AddThemeConstantOverride("shadow_offset_x", 0);
			title.AddThemeConstantOverride("shadow_offset_y", 0);
		}

		_hintLabel.AddThemeFontSizeOverride("font_size", 34);
		_hintLabel.AddThemeColorOverride("font_color", inkMuted);
		_hintLabel.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
		_hintLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		_hintLabel.AddThemeConstantOverride("shadow_offset_y", 0);

		foreach (var b in new[] { _settingsButton, _startButton, _testApiButton })
		{
			var fs = b == _startButton ? 44 : 40;
			b.AddThemeFontSizeOverride("font_size", fs);
			b.AddThemeColorOverride("font_color", btnInk);
			b.AddThemeColorOverride("font_hover_color", btnHover);
			b.AddThemeColorOverride("font_pressed_color", btnPressed);
			b.AddThemeColorOverride("font_disabled_color", new Color(btnInk, 0.45f));
			b.AddThemeColorOverride("font_focus_color", btnInk);
		}
	}

	private void ApplyMainMenuHintColors()
	{
		var inkMuted = new Color(0.38f, 0.31f, 0.26f);
		var inkWarn = new Color(0.5f, 0.22f, 0.18f);
		if (_hintLabel == null) return;
		var t = _hintLabel.Text ?? "";
		var isError = t.StartsWith("失败") || t.Contains("无法") || t.StartsWith("未配置") || t.StartsWith("请先");
		_hintLabel.AddThemeColorOverride("font_color", isError ? inkWarn : inkMuted);
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
			ApplyMainMenuHintColors();
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
			ApplyMainMenuHintColors();
			return;
		}

		_hintLabel.Text = "请求中…";
		ApplyMainMenuHintColors();
		var result = await ApiBridge.Instance.ChatTextAsync(
			"你是一个极简助手，只输出一个词：你好",
			"请回复「你好」两个字。",
			fallback: "（请求失败）");

		_hintLabel.Text = result.Success
			? $"API 正常：{result.Content.Trim()}"
			: $"失败：{result.Error}";
		ApplyMainMenuHintColors();
	}
}
