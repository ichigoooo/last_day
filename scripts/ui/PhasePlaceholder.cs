using Godot;
using System.Threading.Tasks;

/// <summary>
/// Demo 占位阶段场景：标题 + 返回主菜单。
/// </summary>
public partial class PhasePlaceholder : Control
{
	public override void _Ready()
	{
		AppTheme.ApplyTo(this);

		var back = GetNodeOrNull<Button>("%BackButton");
		if (back != null) back.Pressed += OnBack;

		var next = GetNodeOrNull<Button>("%NextButton");
		if (next != null) next.Pressed += OnNext;

		// 终局为单次体验收束：只保留一个返回主菜单入口。
		if (GameManager.Instance?.CurrentPhase == GameManager.Phase.Ending)
		{
			if (back != null) back.Visible = false;
			if (next != null) next.Text = "返回主菜单";
		}
	}

	private async void OnBack()
	{
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}

	private async void OnNext()
	{
		var next = GameManager.Instance?.CurrentPhase switch
		{
			GameManager.Phase.DeathRegistration => GameManager.Phase.Verdict,
			GameManager.Phase.Verdict => GameManager.Phase.LastDay,
			GameManager.Phase.LastDay => GameManager.Phase.Death,
			GameManager.Phase.Death => GameManager.Phase.Funeral,
			GameManager.Phase.Funeral => GameManager.Phase.Meditation,
			GameManager.Phase.Meditation => GameManager.Phase.Ending,
			GameManager.Phase.Ending => GameManager.Phase.MainMenu,
			_ => GameManager.Phase.MainMenu
		};
		await SceneSwitcher.Instance.SwitchToAsync(next);
	}
}
