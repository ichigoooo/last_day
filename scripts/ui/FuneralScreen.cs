using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>终章仪式：弥留遗愿 → 系统报告 → 讣告 → 墓志铭 → 留档卡片。</summary>
public partial class FuneralScreen : Control
{
	private VBoxContainer _col;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		var bg = new ColorRect
		{
			Color = new Color(0.04f, 0.05f, 0.06f, 1f),
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
			Text = "最终归档",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeTitle);
		_col.AddChild(title);

		var subtitle = new Label
		{
			Text = "系统不会评价你。它只把最后留下来的东西，逐项记清。",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		subtitle.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBodySmall);
		subtitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.74f, 0.78f, 1f));
		_col.AddChild(subtitle);

		Callable.From(RunFlowAsync).CallDeferred();
	}

	private async void RunFlowAsync()
	{
		var session = GameManager.Instance?.Session;
		if (session == null) return;

		await StepFinalWishAsync(session);
		StepSystemReport(session);
		await StepObituaryAsync(session);
		await StepEpitaphAsync(session);
		await StepShareCardAsync(session);
		await StepGoMeditationAsync();
	}

	private async Task StepFinalWishAsync(GameSession session)
	{
		var h = MakeSection("遗愿登记");
		_col.AddChild(h);

		var prompt = new Label
		{
			Text = "还剩这一点电。你想把它留给谁，或者留给哪一句来不及说完的话？",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		prompt.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBody);
		_col.AddChild(prompt);

		var tip = new Label
		{
			Text = "这次我照原话登记，不替你润色。",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		tip.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeCaption);
		tip.AddThemeColorOverride("font_color", new Color(0.68f, 0.7f, 0.75f, 1f));
		_col.AddChild(tip);

		var loading = new Label { Text = "正在整理可供登记的几句候选…" };
		_col.AddChild(loading);
		var suggestions = await FetchWishSuggestionsAsync(session);
		loading.QueueFree();

		var edit = new TextEdit
		{
			CustomMinimumSize = new Vector2(0, 120),
			PlaceholderText = "写一句你愿意留下来的原话…"
		};
		edit.Text = session.FinalWish;
		_col.AddChild(edit);

		var grid = new GridContainer { Columns = 1 };
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		foreach (var suggestion in suggestions)
		{
			var cap = suggestion;
			var b = new Button { Text = cap, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			b.Pressed += () => edit.Text = cap;
			grid.AddChild(b);
		}
		_col.AddChild(grid);

		var hint = new Label
		{
			Visible = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		hint.AddThemeColorOverride("font_color", new Color(0.82f, 0.52f, 0.52f, 1f));
		_col.AddChild(hint);

		var confirm = MakeWideButton("登记遗愿");
		_col.AddChild(confirm);

		while (true)
		{
			await ToSignal(confirm, Button.SignalName.Pressed);
			var wish = edit.Text?.Trim() ?? "";
			if (string.IsNullOrEmpty(wish))
			{
				hint.Text = "遗愿栏仍是空的。至少留下一句你愿意负责的话。";
				hint.Visible = true;
				continue;
			}
			if (CrisisKeywordGuard.ContainsCrisisContent(wish))
			{
				await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
				continue;
			}

			session.FinalWish = wish;
			GameManager.Instance?.ActivityLog?.AppendChoice(GameManager.Phase.Funeral.ToString(), "终章遗愿登记", "final_wish", wish);
			break;
		}

		confirm.QueueFree();
		grid.QueueFree();
		edit.QueueFree();
		hint.QueueFree();
		tip.QueueFree();
		prompt.QueueFree();
		h.QueueFree();
	}

	private void StepSystemReport(GameSession session)
	{
		var h = MakeSection("最终系统报告");
		_col.AddChild(h);

		var report = new Label
		{
			Text = BuildSystemReportText(session),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		report.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBody);
		_col.AddChild(report);
	}

	private async Task StepObituaryAsync(GameSession session)
	{
		var h = MakeSection("讣告");
		_col.AddChild(h);

		var loading = new Label { Text = "正在归档这一天留下来的说法…" };
		_col.AddChild(loading);
		var text = await FetchEulogyAsync(session);
		session.FuneralEulogy = text;
		GameManager.Instance?.ActivityLog?.AppendNote(GameManager.Phase.Funeral.ToString(), $"【讣告】{text}");
		loading.QueueFree();

		var tw = new TypewriterLabel { CharsPerSecond = 30f };
		_col.AddChild(tw);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		var skip = new Button { Text = "跳过打字" };
		skip.Pressed += () => tw.SkipToEnd();
		row.AddChild(skip);
		_col.AddChild(row);

		tw.StartTyping(text);
		await ToSignal(tw, TypewriterLabel.SignalName.Finished);

		row.QueueFree();
		var next = MakeWideButton("继续归档");
		_col.AddChild(next);
		await ToSignal(next, Button.SignalName.Pressed);
		next.QueueFree();
		skip.QueueFree();
		h.QueueFree();
		tw.QueueFree();
	}

	private async Task StepEpitaphAsync(GameSession session)
	{
		var h = MakeSection("刻字校对");
		_col.AddChild(h);

		var prompt = new Label
		{
			Text = "留档前，请核对刻字。若觉得不准，你可以改一次。",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_col.AddChild(prompt);

		var loading = new Label { Text = "正在生成墓志铭候选…" };
		_col.AddChild(loading);
		var suggestions = await FetchEpitaphsAsync(session);
		loading.QueueFree();

		var custom = new LineEdit
		{
			PlaceholderText = "或直接改成你认得出自己的那一句…",
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		custom.Text = suggestions.Length > 0 ? suggestions[0] : Phase1Copy.FallbackEpitaphs()[0];
		_col.AddChild(custom);

		var grid = new GridContainer { Columns = 1 };
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		foreach (var suggestion in suggestions)
		{
			var cap = suggestion;
			var b = new Button { Text = cap };
			b.Pressed += () => custom.Text = cap;
			grid.AddChild(b);
		}
		_col.AddChild(grid);

		var confirm = MakeWideButton("确认刻字");
		_col.AddChild(confirm);
		await ToSignal(confirm, Button.SignalName.Pressed);

		var chosen = custom.Text?.Trim() ?? "";
		if (string.IsNullOrWhiteSpace(chosen))
			chosen = Phase1Copy.FallbackEpitaphs()[0];
		if (CrisisKeywordGuard.ContainsCrisisContent(chosen))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			chosen = Phase1Copy.FallbackEpitaphs()[0];
		}

		session.Epitaph = chosen;
		GameManager.Instance?.ActivityLog?.AppendChoice(GameManager.Phase.Funeral.ToString(), "墓志铭校对", "epitaph", chosen);

		confirm.QueueFree();
		grid.QueueFree();
		custom.QueueFree();
		prompt.QueueFree();
		h.QueueFree();
	}

	private async Task StepShareCardAsync(GameSession session)
	{
		var h = MakeSection("留档卡片");
		_col.AddChild(h);

		var yuan = MoneySystem.Instance?.Yuan ?? 0;
		var bat = BatterySystem.Instance?.Percent ?? 0f;
		var msgs = ClosurePromptVars.CountPhoneMessages(session);

		var card = new ShareCard
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 320)
		};
		_col.AddChild(card);
		if (!card.IsNodeReady())
			await ToSignal(card, Node.SignalName.Ready);
		card.ApplyFromSession(session, yuan, bat, msgs);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 14);
		var copy = new Button { Text = "复制留档全文" };
		copy.Pressed += () => card.CopyToClipboard();
		var next = new Button { Text = "继续" };
		row.AddChild(copy);
		row.AddChild(next);
		_col.AddChild(row);

		await ToSignal(next, Button.SignalName.Pressed);
		row.QueueFree();
		h.QueueFree();
		card.QueueFree();
	}

	private async Task StepGoMeditationAsync()
	{
		var go = MakeWideButton("继续");
		_col.AddChild(go);
		await ToSignal(go, Button.SignalName.Pressed);
		go.QueueFree();
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Meditation);
	}

	private static string BuildSystemReportText(GameSession session)
	{
		var yuan = MoneySystem.Instance?.Yuan ?? 0;
		var battery = BatterySystem.Instance?.Percent ?? 0f;
		var messages = ClosurePromptVars.CountPhoneMessages(session);
		var deleted = EstimateDeletedIntentCount(session);
		var keystrokes = EstimatePlayerTextCount(session);
		var locLine = ClosurePromptVars.FormatVisitedLine(session);
		var final = string.IsNullOrWhiteSpace(session.FinalWish) ? "——" : session.FinalWish.Trim();

		var sb = new StringBuilder();
		sb.AppendLine("────────────────");
		sb.AppendLine($"最终耗时：{TimeManager.Instance?.GetClockDisplay() ?? "24:00"}");
		sb.AppendLine($"已永久粉碎：约 {deleted} 件执念");
		sb.AppendLine($"仅保留：约 {messages} 条痕迹");
		sb.AppendLine($"输入轨迹：约 {keystrokes} 次敲击");
		sb.AppendLine($"最终登记：{final}");
		sb.AppendLine($"现金结余：{yuan} 元");
		sb.AppendLine($"手机电量：{battery:0.#}%");
		sb.AppendLine($"到访轨迹：{locLine}");
		sb.Append("────────────────");
		return sb.ToString();
	}

	private static int EstimateDeletedIntentCount(GameSession session)
	{
		if (session?.ActivityLog?.Entries == null) return 0;
		var count = 0;
		foreach (var entry in session.ActivityLog.Entries)
		{
			if (entry.Kind == ActivityKinds.LastDayTurn && entry.Text.Contains("输入："))
				count++;
		}
		return Mathf.Max(1, count / 2);
	}

	private static int EstimatePlayerTextCount(GameSession session)
	{
		if (session?.ActivityLog?.Entries == null) return 0;
		var total = 0;
		foreach (var entry in session.ActivityLog.Entries)
		{
			if (entry.Kind == ActivityKinds.LastDayTurn || entry.Kind == ActivityKinds.PhoneMessage ||
			    entry.Kind == ActivityKinds.FaceToFaceDialogue || entry.Kind == ActivityKinds.ReaperDialogue)
				total += entry.Text?.Length ?? 0;
		}
		return Mathf.Max(1, total);
	}

	private static Label MakeSection(string text)
	{
		var h = new Label { Text = text };
		h.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeSection);
		return h;
	}

	private static Button MakeWideButton(string text)
	{
		var b = new Button { Text = text };
		b.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
		b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return b;
	}

	private static async Task<string[]> FetchWishSuggestionsAsync(GameSession session)
	{
		var profile = session?.Soul ?? new SoulProfile();
		var fallback = Phase1Copy.FallbackWishes(profile);
		var api = ApiBridge.Instance;
		if (api == null || !api.IsConfigured) return fallback;

		var grim = PromptLoader.LoadSystem("grim_reaper");
		var task = PromptLoader.LoadSystem("wish_suggestions");
		var user = PromptLoader.ApplyVars(PromptLoader.LoadUser("wish_suggestions"), new Dictionary<string, string>
		{
			["tags"] = string.Join("、", profile.Tags ?? Array.Empty<string>()),
			["work"] = profile.WorkText,
			["relation"] = profile.RelationText,
			["escape"] = profile.EscapeText,
			["archive_summary"] = session?.ArchiveSummary ?? ""
		});

		var result = await api.ChatJsonAsync(PromptLoader.Combine(grim, task), user, fallback: null);
		if (!result.Success || string.IsNullOrWhiteSpace(result.Content)) return fallback;

		try
		{
			using var doc = JsonDocument.Parse(result.Content);
			if (!doc.RootElement.TryGetProperty("wishes", out var arr) || arr.ValueKind != JsonValueKind.Array)
				return fallback;
			var list = new List<string>();
			foreach (var item in arr.EnumerateArray())
			{
				var s = item.GetString()?.Trim();
				if (!string.IsNullOrEmpty(s)) list.Add(s);
			}
			return list.Count >= 3 ? new[] { list[0], list[1], list[2] } : fallback;
		}
		catch
		{
			return fallback;
		}
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
			GD.PrintErr($"[FuneralScreen] 讣告 JSON: {e.Message}");
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
}
