using Godot;

/// <summary>顶栏：时钟弧 + 文本时间 + 现金 + 电量条。</summary>
public partial class StatusBar : Control
{
	private ClockArcControl _arc;
	private Label _time;
	private Label _phase;
	private Label _money;
	private ProgressBar _batteryBar;

	public override void _Ready()
	{
		_arc = GetNodeOrNull<ClockArcControl>("%ClockArc");
		_time = GetNodeOrNull<Label>("%BarTimeLabel");
		_phase = GetNodeOrNull<Label>("%BarPhaseLabel");
		_money = GetNodeOrNull<Label>("%BarMoneyLabel");
		_batteryBar = GetNodeOrNull<ProgressBar>("%BatteryBar");
	}

	public void Refresh()
	{
		var tm = TimeManager.Instance;
		var money = MoneySystem.Instance;
		var bat = BatterySystem.Instance;

		if (_arc != null && tm != null)
			_arc.SyncProgress(tm.GetDayProgress01());
		if (_time != null && tm != null)
			_time.Text = tm.GetClockDisplay();
		if (_phase != null && tm != null)
			_phase.Text = PhaseName(tm.GetDaylightPhaseIndex());
		if (_money != null)
			_money.Text = $"现金 {money?.Yuan ?? 0} 元";
		if (_batteryBar != null)
		{
			_batteryBar.Value = bat?.Percent ?? 0;
			_batteryBar.ShowPercentage = false;
		}
	}

	private static string PhaseName(int p) => p switch
	{
		0 => "清晨",
		1 => "正午",
		2 => "午后",
		3 => "黄昏",
		4 => "夜晚",
		5 => "午夜",
		_ => ""
	};
}
