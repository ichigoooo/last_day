using Godot;

/// <summary>手机电量 0–100；仅在实际「掏出手机」界面时掉电；叙事回合内另有 <see cref="ApplyTurnDrain"/>。</summary>
public partial class BatterySystem : Node
{
	public static BatterySystem Instance { get; private set; }

	public float Percent { get; private set; } = 100f;

	/// <summary>每次打开手机叠层时的基础耗电（与叙事亮屏无关）。</summary>
	public const float PhoneOpenDrainPercent = 1.5f;

	public override void _EnterTree()
	{
		Instance = this;
	}

	/// <summary>玩家从口袋掏出手机、打开叠层时调用；不因游戏时间自然流逝而掉电。</summary>
	public void ApplyPhoneOpenedDrain()
	{
		Percent = Mathf.Max(0f, Percent - PhoneOpenDrainPercent);
	}

	public void ResetForSession()
	{
		Percent = 100f;
	}

	public void ApplyTurnDrain(string screenUsage)
	{
		var drain = screenUsage switch
		{
			"none" => 0f,
			"brief" => 1.5f,
			"active" => 4f,
			_ => 2f
		};
		Percent = Mathf.Max(0f, Percent - drain);
	}

	public void ChargeMinutes(int gameMinutes)
	{
		if (gameMinutes <= 0) return;
		Percent = Mathf.Min(100f, Percent + gameMinutes * 0.35f);
	}

	/// <summary>消耗约 1 游戏小时，换一截电量（充电宝）。</summary>
	public void ApplyPowerBankCharge()
	{
		TimeManager.Instance?.AdvanceGameMinutes(60);
		Percent = Mathf.Min(100f, Percent + 32f);
	}
}
