using Godot;

/// <summary>圆形进度弧：表示「最后一天」在时间轴上的位置。</summary>
public partial class ClockArcControl : Control
{
	public float Progress { get; set; }

	public override void _Draw()
	{
		var r = Mathf.Min(Size.X, Size.Y) * 0.5f - 6f;
		var c = Size / 2;
		DrawArc(c, r, 0f, Mathf.Tau, 48, new Color(1, 1, 1, 0.12f), 3f, true);
		var from = -Mathf.Pi / 2f;
		var sweep = Progress * Mathf.Tau;
		if (sweep > 0.001f)
			DrawArc(c, r, from, from + sweep, 64, AppTheme.AccentCoolBlue, 5f, true);
		DrawCircle(c, 4f, AppTheme.AccentGold);
	}

	public void SyncProgress(float p)
	{
		Progress = p;
		QueueRedraw();
	}
}
