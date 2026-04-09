using Godot;
using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>静默收束：把卡片留存到 user://，并把玩家送回主菜单。</summary>
public partial class EndingScreen : Control
{
	private bool _saved;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		var bg = new ColorRect
		{
			Color = new Color(0.92f, 0.9f, 0.86f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);
		MoveChild(bg, 0);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_top", 52);
		margin.AddThemeConstantOverride("margin_bottom", 44);
		AddChild(margin);

		var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		v.AddThemeConstantOverride("separation", 24);
		margin.AddChild(v);

		var title = new Label
		{
			Text = "回到现实",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeColorOverride("font_color", new Color(0.15f, 0.14f, 0.12f, 1f));
		title.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeDisplay);
		v.AddChild(title);

		var session = GameManager.Instance?.Session;
		var mainText = string.IsNullOrWhiteSpace(session?.EndingPledge)
			? "你的 24 小时已经用完了。但你的时间，没有。"
			: $"你替明天留下了一句回执：\n\n「{session.EndingPledge.Trim()}」";
		var sub = new Label
		{
			Text = $"{mainText}\n\n留档卡片已静默写入本地。要不要带着它回去，由你决定。",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		sub.AddThemeColorOverride("font_color", new Color(0.25f, 0.24f, 0.22f, 1f));
		sub.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBodySmall);
		sub.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		v.AddChild(sub);

		var back = new Button { Text = "返回主菜单" };
		back.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		back.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		v.AddChild(back);

		back.Pressed += async () =>
		{
			GameManager.Instance?.ResetSession();
			if (SceneSwitcher.Instance != null)
				await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
		};

		Callable.From(SaveClosureCardQuietly).CallDeferred();
	}

	private void SaveClosureCardQuietly()
	{
		if (_saved) return;
		_saved = true;

		var session = GameManager.Instance?.Session;
		if (session == null) return;

		var content = ShareCard.BuildShareText(session, MoneySystem.Instance?.Yuan ?? 0, BatterySystem.Instance?.Percent ?? 0f,
			ClosurePromptVars.CountPhoneMessages(session));
		if (!string.IsNullOrWhiteSpace(session.EndingPledge))
			content += $"\n\n回到现实的一句\n{session.EndingPledge.Trim()}";

		var dir = ProjectSettings.GlobalizePath("user://");
		var fileName = $"closure_card_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
		var path = Path.Combine(dir, fileName);
		File.WriteAllText(path, content);
	}
}
