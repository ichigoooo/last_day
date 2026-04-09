using Godot;
using System;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>真相揭示：将模拟死亡的体验返还给现实。</summary>
public partial class MeditationScreen : Control
{
	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		var bg = new ColorRect
		{
			Color = new Color(0.12f, 0.11f, 0.1f, 1f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);
		MoveChild(bg, 0);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 28);
		margin.AddThemeConstantOverride("margin_right", 28);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_bottom", 32);
		AddChild(margin);

		var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		v.AddThemeConstantOverride("separation", 18);
		margin.AddChild(v);

		var title = new Label
		{
			Text = "回执",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeTitle);
		v.AddChild(title);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 420)
		};
		v.AddChild(scroll);

		var body = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		body.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBody);
		scroll.AddChild(body);

		var q = new Label
		{
			Text = "既然没有死，明天醒来之后，你第一件想做得不一样的事是什么？",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		q.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBody);
		v.AddChild(q);

		var edit = new LineEdit
		{
			PlaceholderText = "写一句，或留白让它在心里发生。",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight)
		};
		v.AddChild(edit);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 20);
		var skip = new Button { Text = "留白" };
		skip.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		var next = new Button { Text = "回到现实" };
		next.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		row.AddChild(skip);
		row.AddChild(next);
		v.AddChild(row);

		Callable.From(() => RunAsync(body, edit, skip, next)).CallDeferred();
	}

	private async void RunAsync(Label body, LineEdit edit, Button skip, Button next)
	{
		body.Text = await FetchRealityReflectionAsync();

		var tcs = new TaskCompletionSource<bool>();
		skip.Pressed += () =>
		{
			edit.Text = "";
			if (!tcs.Task.IsCompleted) tcs.TrySetResult(true);
		};
		next.Pressed += () =>
		{
			if (!tcs.Task.IsCompleted) tcs.TrySetResult(true);
		};
		await tcs.Task;

		var line = edit.Text?.Trim() ?? "";
		var session = GameManager.Instance?.Session;
		if (session != null)
			session.EndingPledge = line;
		if (!string.IsNullOrWhiteSpace(line))
			GameManager.Instance?.ActivityLog?.AppendChoice(GameManager.Phase.Meditation.ToString(), "回到现实的一句", "return_line", line);
		else
			GameManager.Instance?.ActivityLog?.AppendNote(GameManager.Phase.Meditation.ToString(), "【回到现实的一句】（留白）");

		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Ending);
	}

	private static async Task<string> FetchRealityReflectionAsync()
	{
		var session = GameManager.Instance?.Session;
		if (session == null) return FormatPlainFallback();

		var api = ApiBridge.Instance;
		var vars = ClosurePromptVars.BuildForEulogyAndMeditation(session);
		var system = PromptLoader.LoadSystem("grim_reaper");
		var task = PromptLoader.LoadSystem("meditation_reflection");
		var user = PromptLoader.ApplyVars(PromptLoader.LoadUser("meditation_reflection"), vars);
		var combined = PromptLoader.Combine(system, task);

		if (api == null || !api.IsConfigured)
			return FormatPlainFallback();

		var r = await api.ChatJsonAsync(combined, user, fallback: null);
		if (r.Success && TryParseMeditation(r.Content, out var o, out var br, out var ec, out var cl))
			return FormatPlain(o, br, ec, cl);

		return FormatPlainFallback();
	}

	private static bool TryParseMeditation(string json, out string o, out string br, out string ec, out string cl)
	{
		o = br = ec = cl = "";
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			o = root.TryGetProperty("opening", out var a) ? a.GetString() ?? "" : "";
			br = root.TryGetProperty("breath", out var b) ? b.GetString() ?? "" : "";
			ec = root.TryGetProperty("echo", out var e) ? e.GetString() ?? "" : "";
			cl = root.TryGetProperty("closing", out var c) ? c.GetString() ?? "" : "";
			return !(string.IsNullOrWhiteSpace(o) && string.IsNullOrWhiteSpace(br));
		}
		catch (Exception e)
		{
			GD.PrintErr($"[MeditationScreen] JSON: {e.Message}");
			return false;
		}
	}

	private static string FormatPlain(string o, string br, string ec, string cl)
	{
		return $"{o}\n\n{br}\n\n{ec}\n\n{cl}";
	}

	private static string FormatPlainFallback()
	{
		return "档案归好了。\n\n还有一件不属于手续的事。\n\n你没有死。你只是被迫提前看了一眼，失去之后，什么会真的留下来。\n\n你的时间没有被宣判。它只是刚刚重新回到你手里。";
	}
}
