using Godot;
using System;
using System.Threading.Tasks;

/// <summary>最后一天：布局、遭遇帧主视觉、手机与花钱叠层、日终进死亡。</summary>
public partial class LastDayScreen : Control
{
	private const int LockdownMinute = 23 * 60 + 50;

	private StatusBar _statusBar;
	private TextureRect _sceneCard;
	private TextureRect _portraitCard;
	private Label _locationTitle;
	private Label _characterCaption;
	private RichTextLabel _narration;
	private LineEdit _input;
	private Button _submit;
	private Button _opt1;
	private Button _opt2;
	private Button _opt3;
	private Label _customHintLabel;
	private Label _phoneToast;
	private Button _btnMap;
	private Button _btnEndDialogue;
	private Button _btnPhone;
	private Button _btnSpend;
	private ColorRect _bgTint;
	private LastDayDyingFx _dyingFx;
	private Control _mainVisual;
	private Control _visualRow;

	private PhoneUI _phone;
	private DeathSpendUI _spend;
	private LocationMapOverlay _map;

	private bool _busy;
	private bool _ending;
	private int _visualGeneration;

	private CanvasLayer _loadLayer;
	private Label _loadLabel;
	private int _loadDepth;
	private string _loadMessage = "";
	private double _loadDotPhase;
	private bool _lockdownTriggered;
	private bool _lockdownTimeFrozen;

	public override void _Ready()
	{
		AppTheme.ApplyTo(this);

		_bgTint = GetNodeOrNull<ColorRect>("%BgTint");
		_dyingFx = GetNodeOrNull<LastDayDyingFx>("%DyingOverlay");
		_statusBar = GetNodeOrNull<StatusBar>("%StatusBar");
		_sceneCard = GetNodeOrNull<TextureRect>("%SceneCard");
		_portraitCard = GetNodeOrNull<TextureRect>("%PortraitCard");
		_characterCaption = GetNodeOrNull<Label>("%CharacterCaption");
		_locationTitle = GetNodeOrNull<Label>("%LocationTitle");
		_narration = GetNodeOrNull<RichTextLabel>("%NarrationLabel");
		if (_narration != null)
		{
			// 避免模型旁白中含 […] 时 BBCode 解析导致整块不可见。
			_narration.BbcodeEnabled = false;
		}
		_input = GetNodeOrNull<LineEdit>("%PlayerInput");
		_submit = GetNodeOrNull<Button>("%SubmitButton");
		_opt1 = GetNodeOrNull<Button>("%OptionButton1");
		_opt2 = GetNodeOrNull<Button>("%OptionButton2");
		_opt3 = GetNodeOrNull<Button>("%OptionButton3");
		_customHintLabel = GetNodeOrNull<Label>("%OptionCustom");
		_phoneToast = GetNodeOrNull<Label>("%PhoneToast");
		_btnMap = GetNodeOrNull<Button>("%BtnOpenMap");
		_btnEndDialogue = GetNodeOrNull<Button>("%BtnEndDialogue");
		_btnPhone = GetNodeOrNull<Button>("%BtnOpenPhone");
		_btnSpend = GetNodeOrNull<Button>("%BtnOpenSpend");
		_mainVisual = GetNodeOrNull<Control>("%MainVisual");
		_visualRow = GetNodeOrNull<Control>("%VisualRow");
		if (_portraitCard != null)
			_portraitCard.Visible = false;

		_phone = GetNodeOrNull<PhoneUI>("%PhoneLayer");
		_spend = GetNodeOrNull<DeathSpendUI>("%SpendLayer");
		_map = GetNodeOrNull<LocationMapOverlay>("%MapLayer");

		var back = GetNodeOrNull<Button>("%BackButton");
		if (back != null) back.Pressed += OnBack;
		var next = GetNodeOrNull<Button>("%NextButton");
		if (next != null) next.Pressed += OnNext;

		if (_submit != null) _submit.Pressed += OnSubmit;
		if (_opt1 != null) _opt1.Pressed += () => OnOption(_opt1);
		if (_opt2 != null) _opt2.Pressed += () => OnOption(_opt2);
		if (_opt3 != null) _opt3.Pressed += () => OnOption(_opt3);

		if (_btnMap != null) _btnMap.Pressed += () => _map?.Open();
		if (_btnEndDialogue != null) _btnEndDialogue.Pressed += OnEndDialoguePressed;
		if (_btnPhone != null) _btnPhone.Pressed += () => _phone?.Open();
		if (_btnSpend != null) _btnSpend.Pressed += () => _spend?.Open();

		if (_map != null)
			_map.LocationPicked += OnLocationPicked;

		EnsureLastDayResources();

		var msg = MessageSystem.Instance;
		if (msg != null)
			msg.ReplyReceived += OnPhoneReply;

		var tm = TimeManager.Instance;
		if (tm != null)
			tm.DaytimeDepleted += OnDaytimeDepleted;

		EnsureLoadOverlay();
		_ = RefreshAmbientVisualsAsync();
		ApplyPresentationModeUi();
		SetProcess(true);

		TryHydrateOpeningEncounterUi();
	}

	/// <summary>
	/// 首局进入场景时尚未跑过 <see cref="LastDayDirector.RunTurnAsync"/>，<c>ApplyNarrativeOnly</c> 不会被调用；
	/// 用与 API 失败时一致的 fallback 遭遇帧填充旁白与三选项，避免界面只剩占位说明。
	/// </summary>
	private void TryHydrateOpeningEncounterUi()
	{
		var world = GameManager.Instance?.Session.World;
		if (world == null) return;
		if (world.ActionTurnCount > 0) return;
		if (world.CurrentEncounterFrame != null) return;

		var json = EncounterFrame.BuildFallbackJson();
		var frame = EncounterFrame.Parse(json, "");
		EncounterFrame.SanitizeForDisplay(frame);
		ApplyNarrativeOnly(frame.ToLegacyNarrativeTurn());
	}

	public override void _Process(double delta)
	{
		_statusBar?.Refresh();
		UpdateAmbient();
		UpdateLoadingDots(delta);
		if (_ending) return;
		if (!_lockdownTriggered && TimeManager.Instance != null &&
		    TimeManager.Instance.GameMinuteFromMidnight >= LockdownMinute)
		{
			_ = BeginLockdownAsync();
			return;
		}
		if (BatterySystem.Instance != null && BatterySystem.Instance.Percent <= 0.05f)
			_ = ForceEndAsync("电量耗尽。屏幕暗下去之前，你仍听得见自己的呼吸。");
	}

	private void UpdateAmbient()
	{
		if (_bgTint == null) return;
		var tm = TimeManager.Instance;
		if (tm != null)
			_bgTint.Color = tm.GetAmbientTint();
	}

	public override void _ExitTree()
	{
		ReleaseLockdownFreeze();
		var msg = MessageSystem.Instance;
		if (msg != null)
			msg.ReplyReceived -= OnPhoneReply;
		var tm = TimeManager.Instance;
		if (tm != null)
			tm.DaytimeDepleted -= OnDaytimeDepleted;
		if (_map != null)
			_map.LocationPicked -= OnLocationPicked;
	}

	private void OnDaytimeDepleted()
	{
		if (_lockdownTriggered) return;
		_ = ForceEndAsync("时间到了。今天不会再给你多一分钟。");
	}

	private async Task ForceEndAsync(string line)
	{
		if (_ending) return;
		_ending = true;
		ReleaseLockdownFreeze();
		_dyingFx?.TriggerDeathPulse();
		if (_narration != null)
			_narration.Text = line;
		await ToSignal(GetTree().CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Death);
	}

	private async Task BeginLockdownAsync()
	{
		if (_lockdownTriggered || _ending) return;
		_lockdownTriggered = true;
		FreezeForLockdown();
		SetLastDayInteractionLocked(true);
		_btnEndDialogue?.Hide();
		_phone?.Hide();
		_spend?.Hide();
		_map?.Hide();
		_dyingFx?.TriggerDeathPulse();

		if (_narration != null)
			_narration.Text = "系统正在收回交互权限。";
		await ToSignal(GetTree().CreateTimer(1.4), SceneTreeTimer.SignalName.Timeout);

		if (_narration != null)
			_narration.Text = "你发现了一件残酷的事。最后一天并不会因为它是最后一天，就忽然变得更会说话。";
		await ToSignal(GetTree().CreateTimer(2.8), SceneTreeTimer.SignalName.Timeout);

		if (_narration != null)
			_narration.Text = "该删掉的字还是删掉了。该没发出去的话，还是停在发送前一秒。";
		await ToSignal(GetTree().CreateTimer(2.4), SceneTreeTimer.SignalName.Timeout);

		if (_narration != null)
			_narration.Text = "时间不等你了。";
		await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);

		await ForceEndAsync("23:59。你低头看了一眼状态栏。这个数字，比任何隐喻都准确。");
	}

	private void FreezeForLockdown()
	{
		if (_lockdownTimeFrozen) return;
		TimeManager.Instance?.PushGameTimeFreeze();
		_lockdownTimeFrozen = true;
	}

	private void ReleaseLockdownFreeze()
	{
		if (!_lockdownTimeFrozen) return;
		TimeManager.Instance?.PopGameTimeFreeze();
		_lockdownTimeFrozen = false;
	}

	private void OnPhoneReply(string reply, string _tone)
	{
		if (_phoneToast != null)
			_phoneToast.Text = $"手机：{reply}";
	}

	private void EnsureLastDayResources()
	{
		var w = GameManager.Instance?.Session.World;
		if (w == null) return;
		if (w.LastDaySystemsInitialized) return;

		MessageSystem.Instance?.ClearRuntimeBuffers();
		TimeManager.Instance?.ResetForLastDay();
		MoneySystem.Instance?.ResetForSession();
		BatterySystem.Instance?.ResetForSession();
		w.LastDaySystemsInitialized = true;
	}

	private async void OnLocationPicked(string locationId)
	{
		if (GameManager.Instance?.Session.World.ActiveDialogueSession != null) return;
		if (_busy || _ending || IsLoadBlocking()) return;
		var loc = LocationManager.Instance;
		if (loc == null || !loc.IsValidId(locationId)) return;

		var label = loc.GetDisplayName(locationId);
		var text = $"前往{label}";
		if (_input != null) _input.Text = text;
		await SubmitLastDayTurnAsync(text);
	}

	private async Task RefreshAmbientVisualsAsync()
	{
		var world = GameManager.Instance?.Session.World;
		if (world == null) return;
		var id = world.CurrentLocationId;
		var loc = LocationManager.Instance;
		if (loc == null) return;

		PushLoad("正在绘制开场画面，请稍候…");
		try
		{
			_visualGeneration++;
			var token = _visualGeneration;
			var brief = $"{loc.GetTags(id)}，气质：{loc.GetMood(id)}";
			if (_sceneCard != null)
				_sceneCard.Texture = await VisualSvgService.GetSceneTextureAsync(brief);
			if (token != _visualGeneration) return;
			ApplyMainVisualLayout(null);
			RefreshTitles();
		}
		finally
		{
			PopLoad();
		}
	}

	private void RefreshTitles()
	{
		var gm = GameManager.Instance;
		if (gm == null) return;
		var world = gm.Session.World;
		var frame = world.CurrentEncounterFrame;
		if (_locationTitle != null)
		{
			if (frame != null && !string.IsNullOrEmpty(frame.PlaceName))
				_locationTitle.Text = frame.PlaceName;
			else if (!string.IsNullOrEmpty(world.LastDayDisplayPlaceName))
				_locationTitle.Text = world.LastDayDisplayPlaceName;
			else
				_locationTitle.Text = LocationManager.Instance?.GetDisplayName(world.CurrentLocationId) ?? "";
		}
	}

	private async void OnSubmit()
	{
		if (_busy || _ending || _input == null || IsLoadBlocking()) return;
		await SubmitLastDayTurnAsync(_input.Text?.Trim() ?? "");
	}

	/// <summary>统一入口：选项、输入框、地图共用；选项路径不依赖 LineEdit 与 OnSubmit 的异步竞态。</summary>
	private async Task SubmitLastDayTurnAsync(string text)
	{
		if (_busy || _ending || IsLoadBlocking()) return;
		var t = (text ?? "").Trim();
		if (string.IsNullOrEmpty(t)) return;
		if (CrisisKeywordGuard.ContainsCrisisContent(t))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			return;
		}

		await RunPlayerTurnAsync(t);
		if (_input != null)
			_input.Text = "";
	}

	private async Task RunPlayerTurnAsync(string text)
	{
		PushLoad("正在请求叙事与结算（可能需要数十秒）…");
		_busy = true;
		SetLastDayInteractionLocked(true);
		if (_narration != null)
			_narration.Text = "正在生成这一回合的遭遇与旁白…";

		try
		{
			var result = await LastDayDirector.RunTurnAsync(text);
			if (!result.Ok)
			{
				if (_narration != null) _narration.Text = result.Error;
			}
			else
			{
				await ApplyEncounterUiAsync(result);
				AudioManager.Instance?.PlaySfx(AudioManager.SfxUiSoft);
			}
		}
		finally
		{
			PopLoad();
			_busy = false;
			SetLastDayInteractionLocked(false);
			if (_submit != null) _submit.Disabled = false;
			RefreshTitles();
			ApplyPresentationModeUi();
		}
	}

	private async void OnEndDialoguePressed()
	{
		if (_busy || _ending || IsLoadBlocking()) return;
		if (GameManager.Instance?.Session.World.ActiveDialogueSession == null) return;
		_busy = true;
		SetLastDayInteractionLocked(true);
		try
		{
			var result = LastDayDirector.EndFaceDialoguePlayerInitiated();
			if (!result.Ok)
			{
				if (_narration != null) _narration.Text = result.Error;
				return;
			}

			await ApplyEncounterUiAsync(result);
			AudioManager.Instance?.PlaySfx(AudioManager.SfxUiSoft);
		}
		finally
		{
			_busy = false;
			SetLastDayInteractionLocked(false);
			ApplyPresentationModeUi();
		}
	}

	/// <summary>根据是否处于现场对话会话，切换底部入口语义与禁用规则。</summary>
	private void ApplyPresentationModeUi()
	{
		var dlg = GameManager.Instance?.Session.World.ActiveDialogueSession != null;
		if (_btnEndDialogue != null)
		{
			_btnEndDialogue.Visible = dlg;
			_btnEndDialogue.Disabled = false;
		}

		if (_btnMap != null)
		{
			_btnMap.Visible = true;
			_btnMap.Disabled = dlg;
		}

		if (_btnPhone != null)
			_btnPhone.Disabled = dlg;
		if (_btnSpend != null)
			_btnSpend.Disabled = dlg;

		if (_input != null)
		{
			_input.PlaceholderText = dlg ? "输入你的回应…" : "输入你想做的事…";
		}

		if (_submit != null)
			_submit.Text = dlg ? "回应" : "行动";

		if (_portraitCard != null)
			_portraitCard.SelfModulate = dlg ? new Color(1.06f, 1.04f, 1.1f, 1f) : Colors.White;
	}

	private async void OnOption(Button b)
	{
		if (_busy || _ending || b == null || IsLoadBlocking()) return;
		var label = b.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(label)) return;
		if (_input != null) _input.Text = label;
		await SubmitLastDayTurnAsync(label);
	}

	private void ClearEncounterVisualState()
	{
		_visualGeneration++;
		if (_portraitCard != null)
			_portraitCard.Visible = false;
		if (_characterCaption != null)
			_characterCaption.Text = "";
	}

	private void ApplyMainVisualLayout(EncounterFrame f)
	{
		var row = _visualRow as HBoxContainer;
		if (row == null) return;

		if (f == null)
		{
			row.Visible = true;
			if (_sceneCard != null) _sceneCard.Visible = true;
			if (_portraitCard != null)
				_portraitCard.Visible = false;
			if (_characterCaption != null)
			{
				_characterCaption.Visible = false;
				_characterCaption.Text = "";
			}
			return;
		}

		var showS = f.ShowSceneImage;
		var showC = f.ShowCharacterFrame;

		if (!showS && !showC)
		{
			row.Visible = false;
			if (_characterCaption != null) _characterCaption.Visible = false;
			return;
		}

		row.Visible = true;
		if (_characterCaption != null)
			_characterCaption.Visible = showC;

		if (_sceneCard != null)
			_sceneCard.Visible = showS;
		if (_portraitCard != null)
			_portraitCard.Visible = showC;
	}

	private async Task ApplyEncounterUiAsync(LastDayTurnResult result)
	{
		var frame = result.Encounter;
		if (frame == null)
		{
			ApplyMainVisualLayout(null);
			if (_characterCaption != null)
			{
				_characterCaption.Visible = false;
				_characterCaption.Text = "";
			}
			ApplyNarrativeOnly(result.Narrative);
			return;
		}

		ClearEncounterVisualState();
		var token = _visualGeneration;

		ApplyMainVisualLayout(frame);

		if (_characterCaption != null)
		{
			if (frame.ShowCharacterFrame &&
			    (!string.IsNullOrEmpty(frame.CharacterName) || !string.IsNullOrEmpty(frame.CharacterRole)))
			{
				var name = frame.CharacterName?.Trim() ?? "";
				var role = frame.CharacterRole?.Trim() ?? "";
				_characterCaption.Text = string.IsNullOrEmpty(name)
					? role
					: string.IsNullOrEmpty(role)
						? name
						: $"{name} · {role}";
			}
			else
				_characterCaption.Text = "";
		}

		ApplyNarrativeOnly(result.Narrative);

		if (frame.ShowSceneImage)
		{
			if (_sceneCard != null)
				_sceneCard.Texture = await VisualSvgService.GetSceneTextureAsync(frame.SceneVisualBrief);
			if (token != _visualGeneration) return;
		}

		if (frame.ShowCharacterFrame)
		{
			if (_portraitCard != null)
				_portraitCard.Texture =
					await VisualSvgService.GetCharacterTextureAsync(frame.CharacterRole, frame.CharacterVisualBrief);
		}

		ApplyPresentationModeUi();
	}

	private void ApplyNarrativeOnly(NarrativeTurn n)
	{
		if (n == null) return;
		if (_narration != null)
			_narration.Text = n.Narration;

		var opts = n.Options;
		SetOptionButton(_opt1, opts.Count > 0 ? opts[0].Label : "");
		SetOptionButton(_opt2, opts.Count > 1 ? opts[1].Label : "");
		SetOptionButton(_opt3, opts.Count > 2 ? opts[2].Label : "");
		if (_customHintLabel != null)
		{
			_customHintLabel.Visible = true;
			_customHintLabel.Text = string.IsNullOrEmpty(n.CustomHint) ? "自定义输入…" : n.CustomHint;
		}
	}

	private static void SetOptionButton(Button b, string label)
	{
		if (b == null) return;
		var ok = !string.IsNullOrWhiteSpace(label);
		b.Visible = ok;
		b.Text = ok ? label : "";
	}

	private async void OnBack()
	{
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.MainMenu);
	}

	private async void OnNext()
	{
		_dyingFx?.TriggerDeathPulse();
		if (SceneSwitcher.Instance != null)
			await SceneSwitcher.Instance.SwitchToAsync(GameManager.Phase.Death);
	}

	private bool IsLoadBlocking() => _loadDepth > 0;

	private void EnsureLoadOverlay()
	{
		if (_loadLayer != null) return;
		_loadLayer = new CanvasLayer { Layer = 120 };
		AddChild(_loadLayer);

		var root = new Control();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Stop;
		_loadLayer.AddChild(root);

		var dim = new ColorRect();
		dim.SetAnchorsPreset(LayoutPreset.FullRect);
		dim.MouseFilter = Control.MouseFilterEnum.Stop;
		dim.Color = new Color(0.04f, 0.05f, 0.07f, 0.62f);
		root.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		root.AddChild(center);

		_loadLabel = new Label();
		_loadLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_loadLabel.VerticalAlignment = VerticalAlignment.Center;
		_loadLabel.AddThemeFontSizeOverride("font_size", 22);
		_loadLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_loadLabel.CustomMinimumSize = new Vector2(280, 0);
		_loadLabel.Text = "处理中…";
		center.AddChild(_loadLabel);

		_loadLayer.Visible = false;
	}

	private void PushLoad(string message)
	{
		EnsureLoadOverlay();
		_loadDepth++;
		_loadMessage = message ?? "处理中…";
		_loadDotPhase = 0;
		if (_loadLabel != null)
			_loadLabel.Text = DotSuffix(_loadMessage, 0);
		if (_loadLayer != null)
			_loadLayer.Visible = true;
	}

	private void PopLoad()
	{
		_loadDepth = Math.Max(0, _loadDepth - 1);
		if (_loadDepth == 0 && _loadLayer != null)
			_loadLayer.Visible = false;
	}

	private static string DotSuffix(string msg, int phase)
	{
		var dots = (phase % 4) switch
		{
			0 => "",
			1 => " ·",
			2 => " ··",
			_ => " ···"
		};
		return msg + dots;
	}

	private void UpdateLoadingDots(double delta)
	{
		if (_loadLayer == null || !_loadLayer.Visible || _loadDepth <= 0 || _loadLabel == null) return;
		_loadDotPhase += delta;
		if (_loadDotPhase < 0.35) return;
		_loadDotPhase = 0;
		var step = (int)((Time.GetTicksMsec() / 350) % 4);
		_loadLabel.Text = DotSuffix(_loadMessage, step);
	}

	private void SetLastDayInteractionLocked(bool locked)
	{
		if (_submit != null) _submit.Disabled = locked;
		var dlg = GameManager.Instance?.Session.World.ActiveDialogueSession != null;
		if (_btnMap != null) _btnMap.Disabled = locked || dlg;
		if (_btnEndDialogue != null) _btnEndDialogue.Disabled = locked;
		if (_btnPhone != null) _btnPhone.Disabled = locked || dlg;
		if (_btnSpend != null) _btnSpend.Disabled = locked || dlg;
		foreach (var b in new[] { _opt1, _opt2, _opt3 })
		{
			if (b != null) b.Disabled = locked;
		}
		if (_input != null) _input.Editable = !locked;
		var back = GetNodeOrNull<Button>("%BackButton");
		var next = GetNodeOrNull<Button>("%NextButton");
		if (back != null) back.Disabled = locked;
		if (next != null) next.Disabled = locked;
	}
}
