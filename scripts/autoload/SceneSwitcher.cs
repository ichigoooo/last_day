using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 统一场景切换与简单淡入淡出过渡。
/// </summary>
public partial class SceneSwitcher : Node
{
	public static SceneSwitcher Instance { get; private set; }

	private static readonly IReadOnlyDictionary<GameManager.Phase, string> Paths = new Dictionary<GameManager.Phase, string>
	{
		{ GameManager.Phase.MainMenu, "res://scenes/main.tscn" },
		{ GameManager.Phase.Settings, "res://scenes/settings.tscn" },
		{ GameManager.Phase.DeathRegistration, "res://scenes/death_registration.tscn" },
		{ GameManager.Phase.Verdict, "res://scenes/verdict.tscn" },
		{ GameManager.Phase.LastDay, "res://scenes/last_day.tscn" },
		{ GameManager.Phase.Death, "res://scenes/death.tscn" },
		{ GameManager.Phase.Funeral, "res://scenes/funeral.tscn" },
		{ GameManager.Phase.Meditation, "res://scenes/meditation.tscn" },
		{ GameManager.Phase.Ending, "res://scenes/ending.tscn" }
	};

	private CanvasLayer _overlayLayer;
	private ColorRect _fadeRect;

	[Export] public float FadeSeconds { get; set; } = 0.35f;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_overlayLayer = new CanvasLayer { Layer = 100 };
		AddChild(_overlayLayer);
		_fadeRect = new ColorRect
		{
			Color = Colors.Black,
			Modulate = new Color(1, 1, 1, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_overlayLayer.AddChild(_fadeRect);
		_fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
	}

	/// <summary>切换到指定阶段场景并更新 GameManager 阶段。</summary>
	public async Task SwitchToAsync(GameManager.Phase phase)
	{
		if (!Paths.TryGetValue(phase, out var path))
		{
			GD.PrintErr($"[SceneSwitcher] 未注册阶段: {phase}");
			return;
		}

		if (!ResourceLoader.Exists(path))
		{
			GD.PrintErr($"[SceneSwitcher] 场景不存在: {path}");
			return;
		}

		await FadeOutAsync();
		var err = GetTree().ChangeSceneToFile(path);
		if (err != Error.Ok)
			GD.PrintErr($"[SceneSwitcher] 切换失败: {err}");
		else
			GameManager.Instance?.SetPhase(phase);

		await FadeInAsync();
	}

	/// <summary>不更新阶段枚举（例如重载当前场景）。</summary>
	public async Task ReloadCurrentAsync()
	{
		var path = GetTree().CurrentScene?.SceneFilePath;
		if (string.IsNullOrEmpty(path)) return;
		await FadeOutAsync();
		GetTree().ChangeSceneToFile(path);
		await FadeInAsync();
	}

	private async Task FadeOutAsync()
	{
		if (_fadeRect == null) return;
		AudioManager.Instance?.PlaySfx(AudioManager.SfxSceneTransition);
		var tw = CreateTween();
		tw.TweenProperty(_fadeRect, "modulate:a", 1.0f, FadeSeconds);
		await ToSignal(tw, Tween.SignalName.Finished);
	}

	private async Task FadeInAsync()
	{
		if (_fadeRect == null) return;
		_fadeRect.Modulate = new Color(1, 1, 1, 1);
		var tw = CreateTween();
		tw.TweenProperty(_fadeRect, "modulate:a", 0.0f, FadeSeconds);
		await ToSignal(tw, Tween.SignalName.Finished);
	}
}
