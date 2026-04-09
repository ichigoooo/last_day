using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>入殓登记：三问 → 灵魂标签 → 死因 → 批注（打字机）→ 前往宣判。</summary>
public partial class DeathRegistrationScreen : Control
{
	private TextEdit _workEdit;
	private TextEdit _relationEdit;
	private TextEdit _escapeEdit;
	private Button _submitButton;
	private Label _statusLabel;
	private Control _formBlock;
	private Control _annotationBlock;
	private TypewriterLabel _typewriter;
	private Button _continueButton;
	private Button _skipTypingButton;
	private Button _backButton;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);

		_workEdit = GetNode<TextEdit>("%WorkEdit");
		_relationEdit = GetNode<TextEdit>("%RelationEdit");
		_escapeEdit = GetNode<TextEdit>("%EscapeEdit");
		_submitButton = GetNode<Button>("%SubmitButton");
		_statusLabel = GetNode<Label>("%StatusLabel");
		_formBlock = GetNode<Control>("%FormBlock");
		_annotationBlock = GetNode<Control>("%AnnotationBlock");
		_typewriter = GetNode<TypewriterLabel>("%AnnotationTypewriter");
		_continueButton = GetNode<Button>("%ContinueVerdictButton");
		_skipTypingButton = GetNode<Button>("%SkipTypingButton");
		_backButton = GetNode<Button>("%BackButton");

		const int fieldFont = 32;
		foreach (var te in new[] { _workEdit, _relationEdit, _escapeEdit })
		{
			te.AddThemeFontSizeOverride("font_size", fieldFont);
			te.AddThemeConstantOverride("line_spacing", 6);
		}

		foreach (var b in new[] { _submitButton, _backButton, _skipTypingButton, _continueButton })
		{
			b.CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight);
			b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		}

		_statusLabel.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeCaption);

		_annotationBlock.Visible = false;
		_continueButton.Disabled = true;
		_submitButton.Pressed += OnSubmitPressed;
		_continueButton.Pressed += OnContinuePressed;
		_skipTypingButton.Pressed += OnSkipTyping;
		_backButton.Pressed += OnBackPressed;
		_typewriter.Finished += OnTypingFinished;

		Callable.From(EnsureApiConfiguredAsync).CallDeferred();
	}

	private async void EnsureApiConfiguredAsync()
	{
		if (ApiBridge.Instance != null && ApiBridge.Instance.IsConfigured) return;
		_statusLabel.Text = "未配置 API Key，无法继续。将前往设置。";
		_submitButton.Disabled = true;
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Settings);
	}

	private void OnTypingFinished()
	{
		_continueButton.Disabled = false;
		_skipTypingButton.Visible = false;
	}

	private void OnSkipTyping()
	{
		_typewriter?.SkipToEnd();
	}

	private async void OnSubmitPressed()
	{
		_submitButton.Disabled = true;
		var combined = $"{_workEdit.Text}\n{_relationEdit.Text}\n{_escapeEdit.Text}";
		if (CrisisKeywordGuard.ContainsCrisisContent(combined))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			_submitButton.Disabled = false;
			return;
		}

		_statusLabel.Text = "缮写档案中…";

		try
		{
			GameManager.Instance?.ResetSession();

			var profile = new SoulProfile
			{
				WorkText = _workEdit.Text.Trim(),
				RelationText = _relationEdit.Text.Trim(),
				EscapeText = _escapeEdit.Text.Trim()
			};

			SoulTagExtractor.ApplyKeywordTags(profile);
			await SoulTagExtractor.TryRefineWithLlmAsync(profile);
			GameManager.Instance?.ApplySoulProfile(profile);

			var cause = await DeathCauseGenerator.GenerateAsync(profile);

			// 必须顺序请求：并行会触发 ApiBridge 内互相 Cancel（单 HttpRequest），导致卡死或错包。
			var ann = await FetchAnnotationAsync(profile);
			var open = await FetchOpeningAsync(profile, cause);

			var session = GameManager.Instance?.Session;
			if (session != null)
			{
				session.DeathCauseId = cause.Id;
				session.DeathCauseText = cause.Text;
				session.ReaperAnnotation = ann;
				session.ReaperOpening = open;
			}

			var phase = GameManager.Phase.DeathRegistration.ToString();
			var log = GameManager.Instance?.ActivityLog;
			if (log != null)
			{
				log.AppendReaperDialogue(phase, "user",
					$"【入殓登记】工作：{profile.WorkText}\n关系：{profile.RelationText}\n逃避：{profile.EscapeText}");
				log.AppendReaperDialogue(phase, "reaper", $"【死因】{cause.Text}");
				log.AppendReaperDialogue(phase, "reaper", ann);
			}

			_formBlock.Visible = false;
			_annotationBlock.Visible = true;
			_continueButton.Disabled = true;
			_skipTypingButton.Visible = true;
			_statusLabel.Text = "";
			AudioManager.Instance?.PlaySfx(AudioManager.SfxPaper);
			_typewriter.StartTyping(session?.ReaperAnnotation ?? Phase1Copy.FallbackAnnotation());
		}
		catch (Exception e)
		{
			GD.PrintErr(e.ToString());
			_statusLabel.Text = "处理失败，请重试。";
			_formBlock.Visible = true;
			_annotationBlock.Visible = false;
		}
		finally
		{
			_submitButton.Disabled = false;
		}
	}

	private static async Task<string> FetchAnnotationAsync(SoulProfile profile)
	{
		var api = ApiBridge.Instance;
		var grim = PromptLoader.LoadSystem("grim_reaper");
		var taskSystem = PromptLoader.LoadSystem("annotation");
		var tmpl = PromptLoader.LoadUser("annotation");
		var vars = new Dictionary<string, string>
		{
			["work"] = profile.WorkText,
			["relation"] = profile.RelationText,
			["escape"] = profile.EscapeText
		};
		var user = PromptLoader.ApplyVars(tmpl, vars);
		var system = PromptLoader.Combine(grim, taskSystem);
		var fb = Phase1Copy.FallbackAnnotation();
		if (api == null || !api.IsConfigured) return fb;
		var r = await api.ChatTextAsync(system, user, fallback: fb);
		var text = r.Content?.Trim();
		if (string.IsNullOrEmpty(text)) return fb;
		return text;
	}

	private static async Task<string> FetchOpeningAsync(SoulProfile profile, DeathCause cause)
	{
		var api = ApiBridge.Instance;
		var grim = PromptLoader.LoadSystem("grim_reaper");
		var taskSystem = PromptLoader.LoadSystem("verdict_opening");
		var tmpl = PromptLoader.LoadUser("verdict_opening");
		var tagStr = string.Join("、", profile.Tags ?? []);
		var vars = new Dictionary<string, string>
		{
			["death_cause"] = cause.Text,
			["tags"] = tagStr,
			["work"] = profile.WorkText,
			["relation"] = profile.RelationText,
			["escape"] = profile.EscapeText
		};
		var user = PromptLoader.ApplyVars(tmpl, vars);
		var system = PromptLoader.Combine(grim, taskSystem);
		var fb = Phase1Copy.FallbackOpening(cause.Text);
		if (api == null || !api.IsConfigured) return fb;
		var r = await api.ChatTextAsync(system, user, fallback: fb);
		var text = r.Content?.Trim();
		if (string.IsNullOrEmpty(text)) return fb;
		return text;
	}

	private async void OnContinuePressed()
	{
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Verdict);
	}

	private async void OnBackPressed()
	{
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}
}
