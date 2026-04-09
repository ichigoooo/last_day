using System;
using System.Text.Json;
using Godot;

/// <summary>面对面对话续聊一轮的解析结果（<c>last_day_face_dialogue_continue</c>）。</summary>
public sealed class DialogueContinuationResult
{
	public DialogueTurn Turn { get; set; } = new();
	public bool ParseOk { get; set; } = true;

	public static DialogueContinuationResult Parse(string json, string playerReplyFallback)
	{
		var turn = new DialogueTurn();
		var def = CreateFallbackTurn();
		if (string.IsNullOrWhiteSpace(json))
		{
			turn = def;
			return new DialogueContinuationResult { Turn = turn, ParseOk = false };
		}

		try
		{
			var work = EncounterFrame.PrepareLooseObjectJson(json);
			for (var peel = 0; peel < 8; peel++)
			{
				using var doc = JsonDocument.Parse(work);
				var root = doc.RootElement;
				if (root.ValueKind == JsonValueKind.String)
				{
					work = root.GetString() ?? "";
					if (string.IsNullOrWhiteSpace(work)) break;
					continue;
				}

				if (root.ValueKind == JsonValueKind.Array)
				{
					if (root.GetArrayLength() == 0) break;
					work = root[0].GetRawText();
					continue;
				}

				if (root.ValueKind != JsonValueKind.Object)
					break;

				turn.NpcSpokenLine = ReadStr(root, "npc_line", def.NpcSpokenLine);
				turn.StageDirection = ReadStr(root, "stage_direction", "");
				turn.DialogueEnded = ReadBool(root, "dialogue_ended", false);
				turn.ClosingAmbientNote = ReadStr(root, "closing_ambient_note", "");
				turn.CustomReplyHint = ReadStr(root, "custom_reply_hint", def.CustomReplyHint);
				ReadReplyOptions(root, turn, def);
				if (string.IsNullOrWhiteSpace(turn.NpcSpokenLine) && !turn.DialogueEnded)
					turn.NpcSpokenLine = def.NpcSpokenLine;
				ContentDialogueTurn(turn, playerReplyFallback);
				return new DialogueContinuationResult { Turn = turn, ParseOk = true };
			}

			throw new InvalidOperationException("no json object");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[DialogueContinuationResult] JSON 解析失败: {e.Message}");
			return new DialogueContinuationResult { Turn = def, ParseOk = false };
		}
	}

	private static DialogueTurn CreateFallbackTurn()
	{
		return new DialogueTurn
		{
			NpcSpokenLine = "对方看着你，沉默了一瞬，像是在等你的下一句话。",
			ReplyOptions =
			[
				new NarrativeOption { Slot = "a", Label = "嗯，我在听。" },
				new NarrativeOption { Slot = "b", Label = "先别说这个。" },
				new NarrativeOption { Slot = "c", Label = "让我想一想。" }
			],
			CustomReplyHint = "用一句话回应对方",
			DialogueEnded = false
		};
	}

	private static void ReadReplyOptions(JsonElement root, DialogueTurn turn, DialogueTurn def)
	{
		turn.ReplyOptions.Clear();
		if (!root.TryGetProperty("player_reply_options", out var opts) || opts.ValueKind != JsonValueKind.Array)
		{
			foreach (var o in def.ReplyOptions)
				turn.ReplyOptions.Add(new NarrativeOption { Slot = o.Slot, Label = o.Label });
			return;
		}

		var i = 0;
		foreach (var el in opts.EnumerateArray())
		{
			if (el.ValueKind == JsonValueKind.String)
			{
				var lab = el.GetString()?.Trim() ?? "";
				if (!string.IsNullOrEmpty(lab))
					turn.ReplyOptions.Add(new NarrativeOption { Slot = $"r{i}", Label = lab });
			}
			else if (el.ValueKind == JsonValueKind.Object)
			{
				var slot = el.TryGetProperty("slot", out var s) ? s.GetString() ?? "" : "";
				var label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
				if (!string.IsNullOrEmpty(label))
					turn.ReplyOptions.Add(new NarrativeOption { Slot = slot, Label = label });
			}

			i++;
		}

		while (turn.ReplyOptions.Count < 3)
		{
			var d = def.ReplyOptions[turn.ReplyOptions.Count % def.ReplyOptions.Count];
			turn.ReplyOptions.Add(new NarrativeOption { Slot = d.Slot, Label = d.Label });
		}

		if (turn.ReplyOptions.Count > 3)
			turn.ReplyOptions.RemoveRange(3, turn.ReplyOptions.Count - 3);
	}

	private static void ContentDialogueTurn(DialogueTurn turn, string playerReplyFallback)
	{
		turn.NpcSpokenLine = ContentSafetyFilter.SanitizeDisplay(turn.NpcSpokenLine ?? "");
		turn.StageDirection = ContentSafetyFilter.SanitizeDisplay(turn.StageDirection ?? "");
		turn.ClosingAmbientNote = ContentSafetyFilter.SanitizeDisplay(turn.ClosingAmbientNote ?? "");
		turn.CustomReplyHint = ContentSafetyFilter.ClampLastDayCustomHint(turn.CustomReplyHint ?? "");
		foreach (var o in turn.ReplyOptions)
			o.Label = ContentSafetyFilter.SanitizeDisplay(o.Label ?? "");
		if (turn.DialogueEnded)
			return;
		if (string.IsNullOrWhiteSpace(turn.NpcSpokenLine))
			turn.NpcSpokenLine = "空气里只剩呼吸与停顿。";
	}

	private static string ReadStr(JsonElement root, string name, string def)
	{
		if (!root.TryGetProperty(name, out var el)) return def;
		return el.ValueKind switch
		{
			JsonValueKind.String => el.GetString() ?? def,
			JsonValueKind.Number => el.GetRawText(),
			JsonValueKind.True => "true",
			JsonValueKind.False => "false",
			_ => el.ValueKind == JsonValueKind.Null ? def : el.GetRawText()
		};
	}

	private static bool ReadBool(JsonElement root, string name, bool def)
	{
		if (!root.TryGetProperty(name, out var el)) return def;
		return el.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.String => el.GetString() is "true" or "1",
			JsonValueKind.Number => el.TryGetInt32(out var n) && n != 0,
			_ => def
		};
	}
}
