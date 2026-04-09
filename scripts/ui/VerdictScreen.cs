using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>宣判：死因 + 开场白 + 遗愿确认。</summary>
public partial class VerdictScreen : Control
{
	private Label _deathCauseLabel;
	private Label _openingLabel;
	private TextEdit _wishEdit;
	private Button _suggestButton;
	private Button _confirmButton;
	private Button _backButton;
	private HBoxContainer _suggestionRow;
	private Button _s1;
	private Button _s2;
	private Button _s3;
	private Label _wishHintLabel;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);
		ApplyPaperStyle();

		_deathCauseLabel = GetNode<Label>("%DeathCauseLabel");
		_openingLabel = GetNode<Label>("%OpeningLabel");
		_wishEdit = GetNode<TextEdit>("%WishEdit");
		_suggestButton = GetNode<Button>("%SuggestButton");
		_confirmButton = GetNode<Button>("%ConfirmButton");
		_backButton = GetNode<Button>("%BackButton");
		_suggestionRow = GetNode<HBoxContainer>("%SuggestionRow");
		_s1 = GetNode<Button>("%WishSuggest1");
		_s2 = GetNode<Button>("%WishSuggest2");
		_s3 = GetNode<Button>("%WishSuggest3");
		_wishHintLabel = GetNodeOrNull<Label>("%WishHintLabel");
		if (_wishHintLabel != null) _wishHintLabel.Visible = false;

		_suggestionRow.Visible = false;
		_s1.Pressed += () => ApplySuggestion(_s1.Text);
		_s2.Pressed += () => ApplySuggestion(_s2.Text);
		_s3.Pressed += () => ApplySuggestion(_s3.Text);

		_suggestButton.Pressed += OnSuggestPressed;
		_confirmButton.Pressed += OnConfirmPressed;
		_backButton.Pressed += OnBackPressed;

		var session = GameManager.Instance?.Session;
		if (session != null)
		{
			_deathCauseLabel.Text = session.DeathCauseText;
			_openingLabel.Text = session.ReaperOpening;
			if (!string.IsNullOrEmpty(session.FinalWish))
				_wishEdit.Text = session.FinalWish;

			var log = GameManager.Instance?.ActivityLog;
			if (log != null && !string.IsNullOrEmpty(session.ReaperOpening))
				log.AppendReaperDialogue(GameManager.Phase.Verdict.ToString(), "reaper", session.ReaperOpening);
		}

		Callable.From(EnsureApiConfiguredAsync).CallDeferred();
	}

	private async void EnsureApiConfiguredAsync()
	{
		if (ApiBridge.Instance != null && ApiBridge.Instance.IsConfigured) return;
		if (_wishHintLabel != null)
		{
			_wishHintLabel.Text = "未配置 API Key，无法继续。将前往设置。";
			_wishHintLabel.Visible = true;
		}
		_confirmButton.Disabled = true;
		_suggestButton.Disabled = true;
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Settings);
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

	private void ApplySuggestion(string t)
	{
		if (string.IsNullOrEmpty(t)) return;
		_wishEdit.Text = t;
		GameManager.Instance?.ActivityLog?.AppendChoice(GameManager.Phase.Verdict.ToString(), "遗愿建议", "pick_suggestion", t);
	}

	private async void OnSuggestPressed()
	{
		_suggestButton.Disabled = true;
		_suggestionRow.Visible = false;

		var profile = GameManager.Instance?.Soul;
		var fb = Phase1Copy.FallbackWishes(profile ?? new SoulProfile());
		var list = await FetchWishSuggestionsAsync(profile ?? new SoulProfile(), fb);

		_s1.Text = list[0];
		_s2.Text = list[1];
		_s3.Text = list[2];
		_suggestionRow.Visible = true;
		_suggestButton.Disabled = false;
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

	private async void OnConfirmPressed()
	{
		var w = _wishEdit.Text.Trim();
		if (string.IsNullOrEmpty(w))
		{
			if (_wishHintLabel != null)
			{
				_wishHintLabel.Text = "请写一句遗愿，或点「没有想法」选一条。";
				_wishHintLabel.Visible = true;
			}
			return;
		}
		if (CrisisKeywordGuard.ContainsCrisisContent(w))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			return;
		}
		GameManager.Instance?.SetFinalWish(w);
		GameManager.Instance?.ActivityLog?.AppendChoice(GameManager.Phase.Verdict.ToString(), "遗愿确认", "final_wish", w);
		AudioManager.Instance?.PlaySfx(AudioManager.SfxStamp);
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.LastDay);
	}

	private async void OnBackPressed()
	{
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}
}
