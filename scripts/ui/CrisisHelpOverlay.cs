using Godot;
using System.Threading.Tasks;

/// <summary>
/// 全屏危机求助：热线 400-161-9995，返回主菜单。
/// </summary>
public partial class CrisisHelpOverlay : CanvasLayer
{
	public const string ScenePath = "res://scenes/crisis_help_overlay.tscn";

	private TaskCompletionSource _acknowledged;

	public override void _Ready()
	{
		Layer = 200;
		var back = GetNodeOrNull<Button>("%BackMainButton");
		if (back != null)
			back.Pressed += OnBackMainPressed;
		var center = GetNodeOrNull<Control>("Center");
		if (center != null)
			AppTheme.ApplyTo(center);
	}

	private async void OnBackMainPressed()
	{
		GetTree().Paused = false;
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
		_acknowledged?.TrySetResult();
		QueueFree();
	}

	/// <summary>暂停游戏树并显示求助层；用户确认后切换主菜单并释放本节点。</summary>
	public static void ShowBlocking(SceneTree tree)
	{
		if (tree == null) return;
		if (!ResourceLoader.Exists(ScenePath))
		{
			GD.PrintErr("[CrisisHelpOverlay] 场景缺失: " + ScenePath);
			_ = SceneSwitcher.Instance?.SwitchToAsync(GameManager.Phase.MainMenu);
			return;
		}
		var ps = GD.Load<PackedScene>(ScenePath);
		var inst = ps.Instantiate<CrisisHelpOverlay>();
		tree.Paused = true;
		tree.Root.AddChild(inst);
	}

	/// <summary>用于 async 流程：在用户点击「返回主菜单」并完成场景切换后完成 Task。</summary>
	public static Task ShowBlockingAsync(SceneTree tree)
	{
		if (tree == null) return Task.CompletedTask;
		if (!ResourceLoader.Exists(ScenePath))
		{
			GD.PrintErr("[CrisisHelpOverlay] 场景缺失: " + ScenePath);
			return SceneSwitcher.Instance != null
				? SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu)
				: Task.CompletedTask;
		}
		var ps = GD.Load<PackedScene>(ScenePath);
		var inst = ps.Instantiate<CrisisHelpOverlay>();
		inst._acknowledged = new TaskCompletionSource();
		tree.Paused = true;
		tree.Root.AddChild(inst);
		return inst._acknowledged.Task;
	}
}
