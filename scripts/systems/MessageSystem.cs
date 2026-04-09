using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 异步消息：发消息排队；聊天记录供 PhoneUI 与 message_reply。
/// </summary>
public partial class MessageSystem : Node
{
	public static MessageSystem Instance { get; private set; }

	private readonly List<PendingMessageEvent> _pending = new();
	private readonly List<string> _chatLines = new();
	private static readonly Random Rng = new();

	[Signal]
	public delegate void ReplyReceivedEventHandler(string replyText, string tone);

	[Signal]
	public delegate void ChatLogUpdatedEventHandler();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		CallDeferred(MethodName.AttachTime);
	}

	private void AttachTime()
	{
		var t = TimeManager.Instance;
		if (t != null)
			t.GameMinutesAdvanced += OnGameMinutesAdvanced;
	}

	public override void _ExitTree()
	{
		var t = TimeManager.Instance;
		if (t != null)
			t.GameMinutesAdvanced -= OnGameMinutesAdvanced;
	}

	private void OnGameMinutesAdvanced(int delta)
	{
		if (delta <= 0 || _pending.Count == 0) return;
		for (var i = _pending.Count - 1; i >= 0; i--)
		{
			var ev = _pending[i];
			ev.RemainingGameMinutes -= delta;
			if (ev.RemainingGameMinutes > 0) continue;
			_pending.RemoveAt(i);
			_ = DeliverAsync(ev);
		}
	}

	public void EnqueueSend(string latestMessage, string relationship, string personaHint)
	{
		var line = $"我：{latestMessage}";
		_chatLines.Add(line);
		EmitSignal(SignalName.ChatLogUpdated);

		var ev = new PendingMessageEvent
		{
			Id = Guid.NewGuid().ToString("N"),
			RemainingGameMinutes = 30 + Rng.Next(120),
			LatestMessage = latestMessage ?? "",
			Relationship = string.IsNullOrWhiteSpace(relationship) ? "朋友" : relationship,
			PersonaHint = string.IsNullOrWhiteSpace(personaHint) ? "克制、短句" : personaHint
		};
		_pending.Add(ev);
	}

	public IReadOnlyList<string> GetRecentChatLines() => _chatLines;

	/// <summary>新一局进入最后一天前清空聊天与待发队列。</summary>
	public void ClearRuntimeBuffers()
	{
		_pending.Clear();
		_chatLines.Clear();
	}

	public string FormatChatForPrompt()
	{
		if (_chatLines.Count == 0) return "（暂无）";
		return string.Join("\n", _chatLines.TakeLast(16));
	}

	private async Task DeliverAsync(PendingMessageEvent ev)
	{
		var api = ApiBridge.Instance;
		var world = GameManager.Instance?.Session.World;
		var summary = world?.NarrativeSummary ?? "";

		var fallback = "{\"reply\":\"我看到了。你先照顾好自己。\",\"tone\":\"plain\"}";
		if (api == null || !api.IsConfigured)
		{
			ParseAndEmitReply(fallback);
			return;
		}

		var sys = PromptLoader.LoadSystem("message_reply");
		var tmpl = PromptLoader.LoadUser("message_reply");
		var msgVars = new Dictionary<string, string>
		{
			["relationship"] = ev.Relationship,
			["persona_hint"] = ev.PersonaHint,
			["story_summary"] = summary,
			["chat_history"] = FormatChatForPrompt(),
			["latest_message"] = ev.LatestMessage
		};
		SoulPromptVars.AddSoulFields(msgVars, GameManager.Instance?.Soul);
		var user = PromptLoader.ApplyVars(tmpl, msgVars);

		var result = await api.ChatJsonAsync(sys, user, fallback);
		var text = result.Success && !string.IsNullOrWhiteSpace(result.Content) ? result.Content : fallback;
		ParseAndEmitReply(text);
	}

	private void ParseAndEmitReply(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var reply = root.TryGetProperty("reply", out var r) ? r.GetString() ?? "" : "";
			var tone = root.TryGetProperty("tone", out var t) ? t.GetString() ?? "plain" : "plain";
			if (string.IsNullOrWhiteSpace(reply)) reply = "嗯。";
			reply = ContentSafetyFilter.SanitizeDisplay(reply);
			_chatLines.Add($"对方：{reply}");
			EmitSignal(SignalName.ChatLogUpdated);
			GameManager.Instance?.Session.ActivityLog.AppendPhoneMessage("LastDay", "received", reply);
			EmitSignal(SignalName.ReplyReceived, reply, tone);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[MessageSystem] 解析回复 JSON 失败: {e.Message}");
			EmitSignal(SignalName.ReplyReceived, "消息似乎卡在半路了。", "plain");
		}
	}
}
