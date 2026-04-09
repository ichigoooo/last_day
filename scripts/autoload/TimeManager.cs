using Godot;

/// <summary>
/// 游戏内时间：约 1 真实秒 ≈ 1 游戏分钟（即约 1 真实分 ≈ 1 游戏小时）；从 8:00 起算至日终。
/// </summary>
public partial class TimeManager : Node
{
	public static TimeManager Instance { get; private set; }

	/// <summary>从当日 0:00 起的游戏分钟数。</summary>
	public int GameMinuteFromMidnight { get; private set; }

	public const int DayStartMinute = 8 * 60;
	public const int DayEndMinute = 23 * 60 + 59;

	private double _carrySeconds;
	private bool _dayEndEmitted;

	/// <summary>大于 0 时暂停「自然流逝」的游戏分钟（如等待 LLM 响应期间）。可重入。</summary>
	private int _gameTimeFreezeCount;

	[Signal]
	public delegate void GameMinutesAdvancedEventHandler(int deltaMinutes);

	/// <summary>首次到达日终（23:59）时发出一次。</summary>
	[Signal]
	public delegate void DaytimeDepletedEventHandler();

	[Signal]
	public delegate void DaylightPhaseChangedEventHandler(int phaseIndex);

	private int _lastPhase = -1;

	public override void _EnterTree()
	{
		Instance = this;
	}

	/// <summary>等待大模型等异步期间调用，与 <see cref="PopGameTimeFreeze"/> 成对。</summary>
	public void PushGameTimeFreeze()
	{
		_gameTimeFreezeCount++;
	}

	public void PopGameTimeFreeze()
	{
		if (_gameTimeFreezeCount > 0)
			_gameTimeFreezeCount--;
	}

	public override void _Process(double delta)
	{
		if (_gameTimeFreezeCount > 0)
			return;
		_carrySeconds += delta;
		while (_carrySeconds >= 1.0)
		{
			_carrySeconds -= 1.0;
			AdvanceGameMinutes(1);
		}
	}

	public void ResetForLastDay()
	{
		GameMinuteFromMidnight = DayStartMinute;
		_carrySeconds = 0;
		_dayEndEmitted = false;
		_lastPhase = -1;
		EmitPhaseIfChanged();
	}

	/// <summary>0–1，覆盖从 DayStart 到 DayEnd 的进度。</summary>
	public float GetDayProgress01()
	{
		var span = (float)(DayEndMinute - DayStartMinute);
		if (span <= 0f) return 1f;
		var t = (GameMinuteFromMidnight - DayStartMinute) / span;
		return Mathf.Clamp(t, 0f, 1f);
	}

	/// <summary>用于状态栏与环境光：0 清晨 … 5 午夜。</summary>
	public int GetDaylightPhaseIndex()
	{
		var m = GameMinuteFromMidnight;
		if (m < 6 * 60) return 5;
		if (m < 9 * 60) return 0;
		if (m < 14 * 60) return 1;
		if (m < 17 * 60) return 2;
		if (m < 20 * 60) return 3;
		if (m < 24 * 60) return 4;
		return 4;
	}

	/// <summary>浅底色叠加（与 AppTheme 协调的冷色渐变）。</summary>
	public Color GetAmbientTint()
	{
		return GetDaylightPhaseIndex() switch
		{
			0 => new Color(0.12f, 0.14f, 0.18f, 0.92f),
			1 => new Color(0.1f, 0.12f, 0.16f, 0.94f),
			2 => new Color(0.11f, 0.11f, 0.15f, 0.94f),
			3 => new Color(0.14f, 0.1f, 0.12f, 0.95f),
			4 => new Color(0.08f, 0.09f, 0.14f, 0.96f),
			5 => new Color(0.06f, 0.07f, 0.1f, 0.97f),
			_ => new Color(0.1f, 0.1f, 0.14f, 0.94f)
		};
	}

	public string GetClockDisplay()
	{
		var m = Mathf.Clamp(GameMinuteFromMidnight, 0, 24 * 60 - 1);
		var h = m / 60;
		var min = m % 60;
		return $"{h:00}:{min:00}";
	}

	public bool IsAtOrPastDayEnd => GameMinuteFromMidnight >= DayEndMinute;

	public void AdvanceGameMinutes(int delta)
	{
		if (delta <= 0) return;
		var before = GameMinuteFromMidnight;
		GameMinuteFromMidnight = Mathf.Min(GameMinuteFromMidnight + delta, DayEndMinute);
		var actual = GameMinuteFromMidnight - before;
		if (actual > 0)
			EmitSignal(SignalName.GameMinutesAdvanced, actual);

		if (GameMinuteFromMidnight >= DayEndMinute && !_dayEndEmitted)
		{
			_dayEndEmitted = true;
			EmitSignal(SignalName.DaytimeDepleted);
		}

		EmitPhaseIfChanged();
	}

	private void EmitPhaseIfChanged()
	{
		var p = GetDaylightPhaseIndex();
		if (p == _lastPhase) return;
		_lastPhase = p;
		EmitSignal(SignalName.DaylightPhaseChanged, p);
	}
}
