using Godot;

/// <summary>
/// 全局游戏状态：阶段、灵魂画像、遗愿、当前局数据。
/// </summary>
public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	/// <summary>流程阶段（含主菜单、设置）与单次体验叙事各段。</summary>
	public enum Phase
	{
		MainMenu,
		Settings,
		DeathRegistration,
		Verdict,
		LastDay,
		Death,
		Funeral,
		Meditation,
		/// <summary>单次体验的收束阶段，之后返回主菜单（非轮回、不可多局累积）。</summary>
		Ending
	}

	public Phase CurrentPhase { get; set; } = Phase.MainMenu;

	public SoulProfile Soul { get; private set; } = new();
	public GameSession Session { get; private set; } = new();

	/// <summary>当前局的一日时间线（与 Session.ActivityLog 同一实例）。</summary>
	public SessionActivityLog ActivityLog => Session.ActivityLog;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public void SetPhase(Phase phase)
	{
		CurrentPhase = phase;
	}

	public void ResetSession()
	{
		Session = new GameSession();
		Soul = new SoulProfile();
	}

	public void ApplySoulProfile(SoulProfile profile)
	{
		Soul = profile ?? new SoulProfile();
		Session.Soul = Soul;
	}

	public void SetFinalWish(string wish)
	{
		Session.FinalWish = wish ?? "";
	}
}
