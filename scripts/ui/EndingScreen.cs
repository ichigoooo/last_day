using Godot;
using System.Threading.Tasks;

/// <summary>单次体验收束：可选承诺，返回主菜单并重置会话。</summary>
public partial class EndingScreen : Control
{
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
			Text = "晨光",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeColorOverride("font_color", new Color(0.15f, 0.14f, 0.12f, 1f));
		title.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeDisplay);
		v.AddChild(title);

		var sub = new Label
		{
			Text = "这一局停在这里。若想再活一次「最后一天」，可从主菜单重新开始。",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		sub.AddThemeColorOverride("font_color", new Color(0.25f, 0.24f, 0.22f, 1f));
		sub.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBodySmall);
		sub.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		v.AddChild(sub);

		var pledgeLabel = new Label { Text = "（可选）留给自己的一句承诺，或留白。" };
		pledgeLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.2f, 0.18f, 1f));
		pledgeLabel.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeCaption);
		v.AddChild(pledgeLabel);

		var edit = new LineEdit
		{
			PlaceholderText = "写一句，或留空。",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight)
		};
		v.AddChild(edit);

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
	}
}
