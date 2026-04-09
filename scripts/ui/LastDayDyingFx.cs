using Godot;

/// <summary>最后一天全屏后处理：饱和度/色温随时间推进，死亡前脉冲。</summary>
public partial class LastDayDyingFx : ColorRect
{
	private ShaderMaterial _mat;
	private float _pulse;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		var sh = GD.Load<Shader>("res://shaders/last_day_dying_ui.gdshader");
		if (sh == null)
		{
			GD.PushWarning("[LastDayDyingFx] Shader 未找到，后处理关闭。");
			Visible = false;
			return;
		}
		_mat = new ShaderMaterial { Shader = sh };
		Material = _mat;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (_mat == null) return;
		var tm = TimeManager.Instance;
		var p = tm?.GetDayProgress01() ?? 0f;
		_mat.SetShaderParameter("day_progress", p);
		_pulse = (float)Mathf.MoveToward(_pulse, 0.0, delta * 1.85);
		_mat.SetShaderParameter("death_pulse", _pulse);
	}

	/// <summary>时间终局切入死亡场景前调用，短促压暗与去饱和。</summary>
	public void TriggerDeathPulse()
	{
		_pulse = 1f;
		if (_mat != null)
			_mat.SetShaderParameter("death_pulse", _pulse);
	}
}
