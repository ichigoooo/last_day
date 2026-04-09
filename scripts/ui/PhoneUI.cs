using Godot;
using System.Text;

/// <summary>叠层手机：聊天记录、充电宝、朋友圈列表。</summary>
public partial class PhoneUI : CanvasLayer
{
	private Control _root;
	private RichTextLabel _chat;
	private LineEdit _send;
	private ItemList _moments;
	private LineEdit _momentsInput;
	private Label _hint;

	public override void _Ready()
	{
		_root = GetNodeOrNull<Control>("%PhoneRoot");
		if (_root != null)
			AppTheme.ApplyTo(_root);
		_chat = GetNodeOrNull<RichTextLabel>("%PhoneChat");
		_send = GetNodeOrNull<LineEdit>("%PhoneSendInput");
		_moments = GetNodeOrNull<ItemList>("%MomentsList");
		_momentsInput = GetNodeOrNull<LineEdit>("%MomentsInput");
		_hint = GetNodeOrNull<Label>("%PhoneHint");

		var close = GetNodeOrNull<Button>("%PhoneClose");
		if (close != null) close.Pressed += () => Visible = false;

		var sendBtn = GetNodeOrNull<Button>("%PhoneSendBtn");
		if (sendBtn != null) sendBtn.Pressed += OnSendPressed;

		var power = GetNodeOrNull<Button>("%PowerBankBtn");
		if (power != null) power.Pressed += OnPowerBank;

		var mp = GetNodeOrNull<Button>("%MomentsPost");
		if (mp != null) mp.Pressed += OnMomentsPost;

		var msg = MessageSystem.Instance;
		if (msg != null)
		{
			msg.ReplyReceived += OnReply;
			msg.ChatLogUpdated += RefreshChat;
		}

		Visible = false;
	}

	public override void _ExitTree()
	{
		var msg = MessageSystem.Instance;
		if (msg != null)
		{
			msg.ReplyReceived -= OnReply;
			msg.ChatLogUpdated -= RefreshChat;
		}
	}

	public void Open()
	{
		Visible = true;
		BatterySystem.Instance?.ApplyPhoneOpenedDrain();
		RefreshChat();
		RefreshMoments();
		if (_send != null)
			_send.GrabFocus();
	}

	private void OnReply(string reply, string tone)
	{
		if (Visible)
			RefreshChat();
		if (_hint != null)
			_hint.Text = $"新回复（{tone}）";
	}

	private async void OnSendPressed()
	{
		var text = _send?.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(text)) return;
		if (CrisisKeywordGuard.ContainsCrisisContent(text))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			return;
		}
		var soul = GameManager.Instance?.Soul;
		var (rel, hint) = SoulPromptVars.PickChatPersona(soul);
		MessageSystem.Instance?.EnqueueSend(text, rel, hint);
		_send.Text = "";
		RefreshChat();
	}

	private void OnPowerBank()
	{
		BatterySystem.Instance?.ApplyPowerBankCharge();
		if (_hint != null)
			_hint.Text = "已用充电宝（消耗约 1 小时游戏时间）。";
	}

	private async void OnMomentsPost()
	{
		var t = _momentsInput?.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(t)) return;
		if (CrisisKeywordGuard.ContainsCrisisContent(t))
		{
			await CrisisHelpOverlay.ShowBlockingAsync(GetTree());
			return;
		}
		var world = GameManager.Instance?.Session.World;
		if (world == null) return;
		world.MomentsLines.Add(t);
		if (_momentsInput != null) _momentsInput.Text = "";
		RefreshMoments();
		GameManager.Instance?.Session.ActivityLog.AppendNote("LastDay", $"朋友圈：{t}");
	}

	private void RefreshChat()
	{
		if (_chat == null) return;
		var sb = new StringBuilder();
		var ms = MessageSystem.Instance;
		if (ms == null) return;
		foreach (var line in ms.GetRecentChatLines())
		{
			sb.AppendLine(line);
		}
		_chat.Text = sb.ToString().TrimEnd();
	}

	private void RefreshMoments()
	{
		if (_moments == null) return;
		_moments.Clear();
		var world = GameManager.Instance?.Session.World;
		if (world == null) return;
		foreach (var m in world.MomentsLines)
			_moments.AddItem(m);
	}
}
