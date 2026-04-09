using Godot;
using System.Threading.Tasks;

/// <summary>死亡过渡：画面渐冷、叠黑、静候后进入葬礼。</summary>
public partial class DeathScreen : Control
{
	[Export] public float DesaturateSeconds { get; set; } = 2.2f;
	[Export] public float HoldBlackSeconds { get; set; } = 2.8f;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		MouseFilter = MouseFilterEnum.Stop;
		Callable.From(RunSequenceAsync).CallDeferred();
	}

	private async void RunSequenceAsync()
	{
		AudioManager.Instance?.PlaySfx(AudioManager.SfxDeathAmbient);
		var overlay = new ColorRect
		{
			Color = Colors.Black,
			Modulate = new Color(1, 1, 1, 0),
			MouseFilter = MouseFilterEnum.Ignore
		};
		overlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(overlay);

		var skip = new Button
		{
			Text = "跳过",
			Flat = true
		};
		skip.SetAnchorsPreset(LayoutPreset.TopRight);
		skip.OffsetLeft = -160;
		skip.OffsetTop = 12;
		skip.OffsetRight = -20;
		skip.OffsetBottom = 12 + AppTheme.MinButtonHeight;
		AddChild(skip);

		var skipped = false;
		skip.Pressed += () => { skipped = true; };

		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(this, "modulate", new Color(0.35f, 0.35f, 0.38f, 1f), DesaturateSeconds);
		tw.TweenProperty(overlay, "modulate:a", 1.0f, DesaturateSeconds + 0.4f);

		await ToSignal(tw, Tween.SignalName.Finished);

		var wait = HoldBlackSeconds;
		while (wait > 0 && !skipped)
		{
			await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
			wait -= 0.1f;
		}

		skip.QueueFree();
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Funeral);
	}
}
