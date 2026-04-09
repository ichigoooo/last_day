using Godot;

/// <summary>
/// 逐字显示文本；支持跳过到全文。
/// </summary>
public partial class TypewriterLabel : Label
{
	private Timer _timer;
	private string _full = "";
	private int _charIndex;

	[Signal]
	public delegate void FinishedEventHandler();

	public float CharsPerSecond { get; set; } = 28f;

	public override void _Ready()
	{
		// 必须换行，否则 Label 会按单行计算最小宽度，在 VBox/Scroll 里把整屏横向撑开。
		AutowrapMode = TextServer.AutowrapMode.WordSmart;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ShrinkBegin;

		_timer = new Timer { OneShot = false, Autostart = false };
		AddChild(_timer);
		_timer.Timeout += OnTick;
	}

	public void StartTyping(string fullText)
	{
		_full = fullText ?? "";
		_charIndex = 0;
		Text = "";
		if (_full.Length == 0)
		{
			EmitSignal(SignalName.Finished);
			return;
		}
		_timer.WaitTime = 1f / float.Max(CharsPerSecond, 1f);
		if (!_timer.IsStopped()) _timer.Stop();
		_timer.Start();
	}

	public void SkipToEnd()
	{
		if (_timer != null && !_timer.IsStopped()) _timer.Stop();
		Text = _full;
		_charIndex = _full.Length;
		EmitSignal(SignalName.Finished);
	}

	private void OnTick()
	{
		if (_charIndex >= _full.Length)
		{
			_timer.Stop();
			EmitSignal(SignalName.Finished);
			return;
		}
		_charIndex++;
		Text = _full.Substring(0, _charIndex);
	}
}
