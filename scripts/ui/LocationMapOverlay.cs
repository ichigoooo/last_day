using Godot;

/// <summary>全屏叠层：点选 8 个场所卡片式传送（扣 30 游戏分钟）。</summary>
public partial class LocationMapOverlay : CanvasLayer
{
	[Signal]
	public delegate void LocationPickedEventHandler(string locationId);

	public override void _Ready()
	{
		var panel = GetNodeOrNull<Control>("MapPanel");
		if (panel != null)
			AppTheme.ApplyTo(panel);

		var grid = GetNodeOrNull<GridContainer>("%LocGrid");
		var close = GetNodeOrNull<Button>("%MapClose");
		if (close != null) close.Pressed += () => Visible = false;

		if (grid == null) return;
		foreach (var child in grid.GetChildren())
			child.QueueFree();

		foreach (var loc in LocationManager.All)
		{
			var b = new Button
			{
				Text = loc.DisplayName,
				CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight)
			};
			b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			var id = loc.Id;
			b.Pressed += () =>
			{
				EmitSignal(SignalName.LocationPicked, id);
				Visible = false;
			};
			grid.AddChild(b);
		}

		Visible = false;
	}

	public void Open()
	{
		Visible = true;
	}
}
