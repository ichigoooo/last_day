using Godot;

/// <summary>24:00 之后的黑屏留白：绝对静默，短暂停留，再进入终章仪式。</summary>
public partial class DeathScreen : Control
{
	[Export] public float HoldBlackSeconds { get; set; } = 5.0f;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		MouseFilter = MouseFilterEnum.Stop;
		Callable.From(RunSequenceAsync).CallDeferred();
	}

	private async void RunSequenceAsync()
	{
		var overlay = new ColorRect
		{
			Color = Colors.Black,
			MouseFilter = MouseFilterEnum.Stop
		};
		overlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(overlay);
		Modulate = Colors.White;
		await ToSignal(GetTree().CreateTimer(HoldBlackSeconds), SceneTreeTimer.SignalName.Timeout);
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Funeral);
	}
}
