using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 单局「一日」时间线：与死神对话、选项、手机消息、到访场景等，供后续 LLM 生成悼词/总结。
/// 挂在 <see cref="GameSession.ActivityLog"/>，随 <see cref="GameManager.ResetSession"/> 清空（新开一局时）。
/// </summary>
public class SessionActivityLog
{
	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	/// <summary>本局日志开始时间（首条事件前写入）。</summary>
	[JsonPropertyName("started_unix_ms")]
	public long StartedUnixMs { get; set; }

	[JsonPropertyName("entries")]
	public List<ActivityEntry> Entries { get; set; } = new();

	public void Clear()
	{
		Entries.Clear();
		StartedUnixMs = 0;
	}

	public void AppendReaperDialogue(string phase, string role, string text)
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.ReaperDialogue,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			Role = NormalizeRole(role),
			Text = text ?? ""
		});
	}

	public void AppendChoice(string phase, string context, string optionKey, string displayText)
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.Choice,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			Context = context ?? "",
			OptionKey = optionKey ?? "",
			Text = displayText ?? ""
		});
	}

	public void AppendPhoneMessage(string phase, string direction, string body)
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.PhoneMessage,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			Direction = NormalizePhoneDirection(direction),
			Text = body ?? ""
		});
	}

	public void AppendLocationVisit(string phase, string locationId, string locationName, string note = "")
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.LocationVisit,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			LocationId = locationId ?? "",
			LocationName = locationName ?? "",
			Text = note ?? ""
		});
	}

	public void AppendLastDayTurn(string phase, string locationId, string userInput, string narration, string intentSummary)
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.LastDayTurn,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			LocationId = locationId ?? "",
			Text = $"输入：{userInput}\n旁白：{narration}\n意图：{intentSummary}"
		});
	}

	/// <summary>现场面对面对话：opening / npc_line / player_reply / end_player / end_system。</summary>
	public void AppendFaceToFaceDialogue(string phase, string sessionId, string locationId, string dialogueEvent,
		string speaker, string speakerDisplayName, string text)
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.FaceToFaceDialogue,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			SessionId = sessionId ?? "",
			LocationId = locationId ?? "",
			DialogueEvent = dialogueEvent ?? "",
			Speaker = speaker ?? "",
			SpeakerDisplayName = speakerDisplayName ?? "",
			Text = text ?? ""
		});
	}

	public void AppendNote(string phase, string text)
	{
		EnsureStarted();
		Entries.Add(new ActivityEntry
		{
			Kind = ActivityKinds.Note,
			UnixMs = NowUnixMs(),
			Phase = phase ?? "",
			Text = text ?? ""
		});
	}

	/// <summary>提交给大模型做一日总结 / 悼词的 JSON 文本。</summary>
	public string ToJsonString()
	{
		return JsonSerializer.Serialize(this, JsonOpts);
	}

	private void EnsureStarted()
	{
		if (StartedUnixMs == 0)
			StartedUnixMs = NowUnixMs();
	}

	private static long NowUnixMs()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	private static string NormalizeRole(string role)
	{
		var r = (role ?? "").Trim().ToLowerInvariant();
		return r is "user" or "reaper" ? r : "user";
	}

	private static string NormalizePhoneDirection(string direction)
	{
		var d = (direction ?? "").Trim().ToLowerInvariant();
		if (d is "sent" or "out" or "outbound") return "sent";
		if (d is "received" or "in" or "inbound") return "received";
		return string.IsNullOrEmpty(d) ? "sent" : d;
	}
}

public static class ActivityKinds
{
	public const string ReaperDialogue = "reaper_dialogue";
	public const string Choice = "choice";
	public const string PhoneMessage = "phone_message";
	public const string LocationVisit = "location_visit";
	public const string LastDayTurn = "last_day_turn";
	public const string FaceToFaceDialogue = "face_to_face_dialogue";
	public const string Note = "note";
}

/// <summary>单条时间线记录；按 <see cref="Kind"/> 使用对应字段，其余可为空。</summary>
public class ActivityEntry
{
	[JsonPropertyName("kind")]
	public string Kind { get; set; } = "";

	[JsonPropertyName("unix_ms")]
	public long UnixMs { get; set; }

	/// <summary>游戏阶段名，如 MainMenu、LastDay（与 GameManager.Phase 一致）。</summary>
	[JsonPropertyName("phase")]
	public string Phase { get; set; } = "";

	/// <summary>reaper_dialogue：user / reaper。</summary>
	[JsonPropertyName("role")]
	public string Role { get; set; } = "";

	/// <summary>通用正文：对话、短信内容、备注等。</summary>
	[JsonPropertyName("text")]
	public string Text { get; set; } = "";

	/// <summary>choice：选项所在界面或逻辑说明。</summary>
	[JsonPropertyName("context")]
	public string Context { get; set; } = "";

	/// <summary>choice：选项 id 或内部键。</summary>
	[JsonPropertyName("option_key")]
	public string OptionKey { get; set; } = "";

	/// <summary>phone_message：sent / received。</summary>
	[JsonPropertyName("direction")]
	public string Direction { get; set; } = "";

	[JsonPropertyName("location_id")]
	public string LocationId { get; set; } = "";

	[JsonPropertyName("location_name")]
	public string LocationName { get; set; } = "";

	/// <summary>face_to_face_dialogue：会话 id。</summary>
	[JsonPropertyName("session_id")]
	public string SessionId { get; set; } = "";

	/// <summary>face_to_face_dialogue：opening / npc_line / player_reply / end_player / end_system。</summary>
	[JsonPropertyName("dialogue_event")]
	public string DialogueEvent { get; set; } = "";

	/// <summary>face_to_face_dialogue：player / npc / system。</summary>
	[JsonPropertyName("speaker")]
	public string Speaker { get; set; } = "";

	/// <summary>face_to_face_dialogue：说话者展示名（NPC 名或「你」）。</summary>
	[JsonPropertyName("speaker_display_name")]
	public string SpeakerDisplayName { get; set; } = "";
}
