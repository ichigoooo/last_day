using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>葬礼：悼词 → 墓志铭 → 遗言 → 总结 → 讣告卡 → 冥想。</summary>
public partial class FuneralScreen : Control
{
	private VBoxContainer _col;

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

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		margin.AddChild(scroll);

		_col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_col.AddThemeConstantOverride("separation", 20);
		scroll.AddChild(_col);

		var title = new Label
		{
			Text = "葬礼",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeTitle);
		_col.AddChild(title);

		Callable.From(RunFlowAsync).CallDeferred();
	}

	private async void RunFlowAsync()
	{
		var session = GameManager.Instance?.Session;
		if (session == null) return;

		await StepEulogyAsync(session);
		await StepEpitaphAsync(session);
		await StepLastWordsAsync(session);
		StepSummary(session);
		await StepShareCardAsync(session);
		await StepGoMeditationAsync();
	}

	private async Task StepEulogyAsync(GameSession session)
	{
		var hint = new Label { Text = "正在生成悼词…" };
		_col.AddChild(hint);

		var text = await FetchEulogyAsync(session);
		session.FuneralEulogy = text;
		hint.QueueFree();

		var h = new Label { Text = "悼词" };
		h.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeSection);
		_col.AddChild(h);

		var tw = new TypewriterLabel();
		tw.CharsPerSecond = 32f;
		_col.AddChild(tw);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		var skip = new Button { Text = "跳过打字" };
		skip.Pressed += () => tw.SkipToEnd();
		row.AddChild(skip);
		_col.AddChild(row);

		tw.StartTyping(text);
		await ToSignal(tw, TypewriterLabel.SignalName.Finished);

		skip.QueueFree();
		row.QueueFree();

		var next = MakeWideButton("继续");
		_col.AddChild(next);
		await ToSignal(next, Button.SignalName.Pressed);
		next.QueueFree();
		ClearStepNodes(h, tw);
	}

	private async Task StepEpitaphAsync(GameSession session)
	{
		var h = new Label { Text = "墓志铭" };
		h.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeSection);
		_col.AddChild(h);

		var loading = new Label { Text = "生成墓志铭候选…" };
		_col.AddChild(loading);

		var suggestions = await FetchEpitaphsAsync(session);
		loading.QueueFree();

		var custom = new LineEdit { PlaceholderText = "或自填墓志铭…" };
		custom.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_col.AddChild(custom);

		var g = new GridContainer { Columns = 1 };
		g.AddThemeConstantOverride("h_separation", 8);
		g.AddThemeConstantOverride("v_separation", 8);

		var fbEp = Phase1Copy.FallbackEpitaphs();
		string chosen = suggestions.Length > 0 ? suggestions[0] : fbEp[0];
		custom.Text = chosen;

		foreach (var s in suggestions)
		{
			var cap = s;
			var b = new Button { Text = cap };
			b.Pressed += () =>
			{
				chosen = cap;
				custom.Text = cap;
			};
			g.AddChild(b);
		}
		_col.AddChild(g);

		var confirm = MakeWideButton("确认墓志铭");
		_col.AddChild(confirm);

		await ToSignal(confirm, Button.SignalName.Pressed);
		if (!string.IsNullOrWhiteSpace(custom.Text))
			chosen = custom.Text.Trim();
		if (CrisisKeywordGuard.ContainsCrisisContent(chosen))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			return;
		}
		session.Epitaph = chosen;

		confirm.QueueFree();
		custom.QueueFree();
		g.QueueFree();
		h.QueueFree();
	}

	private async Task StepLastWordsAsync(GameSession session)
	{
		var h = new Label { Text = "遗言一句" };
		h.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeSection);
		_col.AddChild(h);

		var edit = new LineEdit { PlaceholderText = "写下一句遗言…" };
		edit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_col.AddChild(edit);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		var gen = new Button { Text = "生成参考" };
		row.AddChild(gen);
		_col.AddChild(row);

		gen.Pressed += async () =>
		{
			gen.Disabled = true;
			var t = await FetchLastWordsAsync(session);
			edit.Text = t;
			gen.Disabled = false;
		};

		var confirm = MakeWideButton("确认遗言");
		_col.AddChild(confirm);

		await ToSignal(confirm, Button.SignalName.Pressed);
		var lastRaw = string.IsNullOrWhiteSpace(edit.Text) ? "" : edit.Text.Trim();
		if (CrisisKeywordGuard.ContainsCrisisContent(lastRaw))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			return;
		}
		session.LastWords = string.IsNullOrWhiteSpace(lastRaw) ? Phase1Copy.FallbackLastWords() : lastRaw;

		confirm.QueueFree();
		row.QueueFree();
		edit.QueueFree();
		h.QueueFree();
	}

	private void StepSummary(GameSession session)
	{
		var h = new Label { Text = "本局总结" };
		h.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeSection);
		_col.AddChild(h);

		var yuan = MoneySystem.Instance?.Yuan ?? 0;
		var bat = BatterySystem.Instance?.Percent ?? 0f;
		var msgs = ClosurePromptVars.CountPhoneMessages(session);
		var locLine = ClosurePromptVars.FormatVisitedLine(session);

		var box = new Label
		{
			Text = $"现金结余：{yuan} 元\n手机电量：{bat:0.#}%\n消息记录条数：{msgs}\n到访：{locLine}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_col.AddChild(box);
	}

	private async Task StepShareCardAsync(GameSession session)
	{
		var h = new Label { Text = "讣告卡" };
		h.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeSection);
		_col.AddChild(h);

		var yuan = MoneySystem.Instance?.Yuan ?? 0;
		var bat = BatterySystem.Instance?.Percent ?? 0f;
		var msgs = ClosurePromptVars.CountPhoneMessages(session);

		var card = new ShareCard();
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.CustomMinimumSize = new Vector2(0, 280);
		_col.AddChild(card);
		// Ready は AddChild 中に同期で発火すると、await ToSignal(Ready) が購読より先に飛んで永久待ちになり得る。
		if (!card.IsNodeReady())
			await ToSignal(card, Node.SignalName.Ready);
		card.ApplyFromSession(session, yuan, bat, msgs);

		var copy = MakeWideButton("复制讣告卡全文");
		copy.Pressed += () => card.CopyToClipboard();
		_col.AddChild(copy);
	}

	private async Task StepGoMeditationAsync()
	{
		var go = MakeWideButton("进入冥想");
		_col.AddChild(go);
		await ToSignal(go, Button.SignalName.Pressed);
		go.QueueFree();
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Meditation);
	}

	private static Button MakeWideButton(string text)
	{
		var b = new Button { Text = text };
		b.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return b;
	}

	private static void ClearStepNodes(params Node[] nodes)
	{
		foreach (var n in nodes)
			n?.QueueFree();
	}

	private static async Task<string> FetchEulogyAsync(GameSession session)
	{
		var api = ApiBridge.Instance;
		var vars = ClosurePromptVars.BuildForEulogyAndMeditation(session);
		var system = PromptLoader.LoadSystem("grim_reaper");
		var task = PromptLoader.LoadSystem("eulogy");
		var user = PromptLoader.ApplyVars(PromptLoader.LoadUser("eulogy"), vars);
		var combined = PromptLoader.Combine(system, task);

		if (api == null || !api.IsConfigured)
			return Phase1Copy.FallbackEulogy(session.Soul);

		var r = await api.ChatJsonAsync(combined, user, fallback: null);
		if (r.Success && TryParseEulogy(r.Content, out var eu) && !string.IsNullOrWhiteSpace(eu))
			return eu.Trim();

		return Phase1Copy.FallbackEulogy(session.Soul);
	}

	private static async Task<string[]> FetchEpitaphsAsync(GameSession session)
	{
		var api = ApiBridge.Instance;
		var vars = ClosurePromptVars.BuildForEpitaph(session);
		var system = PromptLoader.LoadSystem("grim_reaper");
		var task = PromptLoader.LoadSystem("epitaph_suggestions");
		var user = PromptLoader.ApplyVars(PromptLoader.LoadUser("epitaph_suggestions"), vars);
		var combined = PromptLoader.Combine(system, task);

		if (api == null || !api.IsConfigured)
			return Phase1Copy.FallbackEpitaphs();

		var r = await api.ChatJsonAsync(combined, user, fallback: null);
		if (r.Success && TryParseEpitaphs(r.Content, out var arr) && arr.Length >= 3)
			return arr;

		return Phase1Copy.FallbackEpitaphs();
	}

	private static async Task<string> FetchLastWordsAsync(GameSession session)
	{
		var api = ApiBridge.Instance;
		var vars = ClosurePromptVars.BuildForEulogyAndMeditation(session);
		var system = PromptLoader.LoadSystem("grim_reaper");
		var task = PromptLoader.LoadSystem("last_words");
		var user = PromptLoader.ApplyVars(PromptLoader.LoadUser("last_words"), vars);
		var combined = PromptLoader.Combine(system, task);

		if (api == null || !api.IsConfigured)
			return Phase1Copy.FallbackLastWords();

		var r = await api.ChatJsonAsync(combined, user, fallback: null);
		if (r.Success && TryParseLastWords(r.Content, out var lw) && !string.IsNullOrWhiteSpace(lw))
			return lw.Trim();

		return Phase1Copy.FallbackLastWords();
	}

	private static bool TryParseEulogy(string json, out string eulogy)
	{
		eulogy = "";
		try
		{
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("eulogy", out var el))
			{
				eulogy = el.GetString() ?? "";
				return !string.IsNullOrWhiteSpace(eulogy);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[FuneralScreen] 悼词 JSON: {e.Message}");
		}
		return false;
	}

	private static bool TryParseEpitaphs(string json, out string[] epitaphs)
	{
		epitaphs = Array.Empty<string>();
		try
		{
			using var doc = JsonDocument.Parse(json);
			if (!doc.RootElement.TryGetProperty("epitaphs", out var arr) || arr.ValueKind != JsonValueKind.Array)
				return false;
			var list = new List<string>();
			foreach (var item in arr.EnumerateArray())
			{
				var s = item.GetString()?.Trim();
				if (!string.IsNullOrEmpty(s)) list.Add(s);
			}
			if (list.Count >= 3)
			{
				epitaphs = list.ToArray();
				return true;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[FuneralScreen] 墓志铭 JSON: {e.Message}");
		}
		return false;
	}

	private static bool TryParseLastWords(string json, out string lastWords)
	{
		lastWords = "";
		try
		{
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("last_words", out var el))
			{
				lastWords = el.GetString() ?? "";
				return !string.IsNullOrWhiteSpace(lastWords);
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[FuneralScreen] 遗言 JSON: {e.Message}");
		}
		return false;
	}
}
