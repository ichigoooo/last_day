using Godot;
using System;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>冥想引导：结构化四段 JSON，可跳过进入终局。</summary>
public partial class MeditationScreen : Control
{
	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

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
			Text = "死亡冥想",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeTitle);
		v.AddChild(title);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 520)
		};
		v.AddChild(scroll);

		var body = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		body.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBody);
		scroll.AddChild(body);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 20);
		var skip = new Button { Text = "跳过" };
		skip.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		var next = new Button { Text = "进入终局" };
		next.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		row.AddChild(skip);
		row.AddChild(next);
		v.AddChild(row);

		Callable.From(() => RunAsync(body, skip, next)).CallDeferred();
	}

	private async void RunAsync(Label body, Button skip, Button next)
	{
		body.Text = await FetchMeditationPlainAsync();

		var tcs = new TaskCompletionSource<bool>();
		void Done()
		{
			if (tcs.Task.IsCompleted) return;
			tcs.TrySetResult(true);
		}
		skip.Pressed += Done;
		next.Pressed += Done;
		await tcs.Task;
		skip.Pressed -= Done;
		next.Pressed -= Done;
		skip.Disabled = true;
		next.Disabled = true;

		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Ending);
	}

	private static async Task<string> FetchMeditationPlainAsync()
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
		return $"【进入】\n{o}\n\n【放慢】\n{br}\n\n【回声】\n{ec}\n\n【收束】\n{cl}";
	}

	private static string FormatPlainFallback()
	{
		var q = Phase1Copy.FallbackMeditationQuarters();
		return FormatPlain(q.Opening, q.Breath, q.Echo, q.Closing);
	}
}
