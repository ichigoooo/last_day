using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>宣判：死因 + 开场白 + 放行进入最后一天。</summary>
public partial class VerdictScreen : Control
{
	private Label _deathCauseLabel;
	private Label _openingLabel;
	private Button _backButton;
	private Label _wishHintLabel;
	private bool _timelineActive;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		ApplyPaperStyle();

		_deathCauseLabel = GetNode<Label>("%DeathCauseLabel");
		_openingLabel = GetNode<Label>("%OpeningLabel");
		_backButton = GetNode<Button>("%BackButton");
		_wishHintLabel = GetNodeOrNull<Label>("%WishHintLabel");
		if (_wishHintLabel != null) _wishHintLabel.Visible = false;

		_backButton.Pressed += OnBackPressed;

		var session = GameManager.Instance?.Session;
		if (session != null)
		{
			_deathCauseLabel.Text = session.DeathCauseText;
			_openingLabel.Text = session.ReaperOpening;

			var log = GameManager.Instance?.ActivityLog;
			if (log != null && !string.IsNullOrEmpty(session.ReaperOpening))
				log.AppendReaperDialogue(GameManager.Phase.Verdict.ToString(), "reaper", session.ReaperOpening);
		}

		HideLegacyVerdictUi();
		Callable.From(BeginReleaseTimeline).CallDeferred();
	}

	private void HideLegacyVerdictUi()
	{
		var title = GetNodeOrNull<Control>("%TitleLabel");
		var paper = GetNodeOrNull<Control>("%PaperPanel");
		var bottom = GetNodeOrNull<Control>("%BottomRow");
		if (title != null) title.Visible = false;
		if (paper != null) paper.Visible = false;
		if (bottom != null) bottom.Visible = false;
	}

	private void BeginReleaseTimeline()
	{
		var session = GameManager.Instance?.Session;
		DialogicRuntime.SetVariable(this, "death_cause_text", session?.DeathCauseText ?? "");
		DialogicRuntime.SetVariable(this, "reaper_opening", session?.ReaperOpening ?? "");
		DialogicRuntime.ConnectTimelineEnded(this, Callable.From(OnTimelineEnded));
		_timelineActive = true;
		AudioManager.Instance?.PlaySfx(AudioManager.SfxGavel);
		DialogicRuntime.StartTimeline(this, DialogicRuntime.VerdictReleaseTimeline);
	}

	private async void OnTimelineEnded()
	{
		if (!_timelineActive) return;
		_timelineActive = false;
		AudioManager.Instance?.PlaySfx(AudioManager.SfxStamp);
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.LastDay);
	}

	private void ApplyPaperStyle()
	{
		var paper = GetNodeOrNull<PanelContainer>("%PaperPanel");
		if (paper == null) return;
		var sb = new StyleBoxFlat
		{
			BgColor = new Color(0.82f, 0.78f, 0.72f),
			BorderColor = new Color(0.45f, 0.4f, 0.36f)
		};
		sb.SetBorderWidthAll(2);
		sb.SetCornerRadiusAll(4);
		sb.ShadowColor = new Color(0, 0, 0, 0.35f);
		sb.ShadowSize = 6;
		sb.ShadowOffset = new Vector2(2, 3);
		sb.ContentMarginLeft = 20;
		sb.ContentMarginRight = 20;
		sb.ContentMarginTop = 16;
		sb.ContentMarginBottom = 16;
		paper.AddThemeStyleboxOverride("panel", sb);

		_deathCauseLabel = GetNode<Label>("%DeathCauseLabel");
		_openingLabel = GetNode<Label>("%OpeningLabel");
		var ink = new Color(0.18f, 0.14f, 0.12f);
		_deathCauseLabel.AddThemeColorOverride("font_color", ink);
		_openingLabel.AddThemeColorOverride("font_color", ink);
	}

	private static async Task<string[]> FetchWishSuggestionsAsync(SoulProfile profile, string[] fallback)
	{
		var api = ApiBridge.Instance;
		var grim = PromptLoader.LoadSystem("grim_reaper");
		var taskSystem = PromptLoader.LoadSystem("wish_suggestions");
		var tmpl = PromptLoader.LoadUser("wish_suggestions");
		var vars = new Dictionary<string, string>
		{
			["tags"] = string.Join("、", profile.Tags ?? []),
			["work"] = profile.WorkText,
			["relation"] = profile.RelationText,
			["escape"] = profile.EscapeText
		};
		var user = PromptLoader.ApplyVars(tmpl, vars);
		var system = PromptLoader.Combine(grim, taskSystem);
		if (api == null || !api.IsConfigured) return fallback;

		var r = await api.ChatJsonAsync(system, user, fallback: null);
		if (!r.Success || string.IsNullOrWhiteSpace(r.Content)) return fallback;

		try
		{
			using var doc = JsonDocument.Parse(r.Content);
			if (!doc.RootElement.TryGetProperty("wishes", out var w) || w.ValueKind != JsonValueKind.Array)
				return fallback;
			var arr = new List<string>();
			foreach (var el in w.EnumerateArray())
			{
				var s = el.GetString();
				if (!string.IsNullOrWhiteSpace(s)) arr.Add(s.Trim());
			}
			if (arr.Count >= 3) return [arr[0], arr[1], arr[2]];
		}
		catch { }

		return fallback;
	}

	private async void OnBackPressed()
	{
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}

	public override void _ExitTree()
	{
		DialogicRuntime.DisconnectTimelineEnded(this, Callable.From(OnTimelineEnded));
		DialogicRuntime.EndTimeline(this);
	}
}
