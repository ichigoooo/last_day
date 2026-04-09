using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 最后一天：意图解析 → 系统结算 → 遭遇帧渲染（EncounterFrame）→ 校验与兜底。
/// </summary>
public static class LastDayDirector
{
	private static readonly JsonSerializerOptions ResolvedJsonOpts = new()
	{
		PropertyNamingPolicy = null
	};

	/// <summary>遭遇帧 user 提示：本回合要求尽量出现可同框的他人（与 system 里「同框人物优先」规则配对）。</summary>
	private const string EncounterPresenceHintFavorNpc =
		"【同框人物优先】本回合系统抽中：宜出现一名具体他人。你必须将 show_character_frame 设为 true，并在 narration 里写出与此人的相遇或同框（哪怕只是一瞥、背影、柜台后一声回应），"
		+ "同时填写与旁白一致的 character_name、character_role、character_visual_brief。"
		+ "唯一例外：旁白必须描写该空间已确认绝对无人、且不可能有他者在场（例如独自在空屋反复确认无人）时，才可把 show_character_frame 设为 false。";

	/// <summary>遭遇帧 user 提示：偏独处/无人直接同框（仍为软性参考）。</summary>
	private const string EncounterPresenceHintFavorSolo =
		"【独处氛围优先】可优先呈现独处、无人与你直接同框，或他人仅在远处、门外、画外音式存在；若该场所或剧情下几乎必然出现具体他人（如服务台、医护、同事工位），仍以常识与合理性为准，"
		+ "可设置 show_character_frame 为 true 并写好 character_*，不必为「独处氛围」违背真实感。";

	public static bool LooksLikeTravel(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return false;
		return System.Text.RegularExpressions.Regex.IsMatch(text,
			@"去|前往|赶往|想去|出门|回家|上学|上班|走到|赶到|抵达|回到|回公司|回老家");
	}

	public static async Task<LastDayTurnResult> RunTurnAsync(string playerInput)
	{
		var result = new LastDayTurnResult { Ok = true };
		var gm = GameManager.Instance;
		if (gm == null)
		{
			result.Ok = false;
			result.Error = "GameManager 未就绪";
			return result;
		}

		var session = gm.Session;
		var world = session.World;
		var input = (playerInput ?? "").Trim();
		if (string.IsNullOrEmpty(input))
		{
			result.Ok = false;
			result.Error = "请输入你想做的事。";
			return result;
		}

		var locMgr = LocationManager.Instance;
		var currentId = world.CurrentLocationId;
		if (string.IsNullOrEmpty(currentId) || !locMgr.IsValidId(currentId))
			currentId = "home";

		var intent = await ParseIntentAsync(input, currentId, session, world);
		result.IntentSummary = intent.Summary;

		var resolved = BuildResolvedAction(intent, currentId, locMgr, input);
		result.Resolved = resolved;

		ApplyToSystems(resolved, intent, world, input);

		world.ActionTurnCount++;
		world.LastDayFreeformDestination = string.IsNullOrEmpty(resolved.FreeformDestinationText)
			? intent.DestinationText ?? ""
			: resolved.FreeformDestinationText;

		var frame = await RenderEncounterFrameAsync(session, world, input, intent, resolved);
		EncounterFrame.Normalize(frame, resolved.RejectNote ?? "");
		if (!string.IsNullOrEmpty(resolved.RejectNote) &&
		    (string.IsNullOrWhiteSpace(frame.Narration) || frame.Narration.Length < 4))
			frame.Narration = ContentSafetyFilter.SanitizeDisplay(resolved.RejectNote);
		EncounterFrame.SanitizeForDisplay(frame);

		world.CurrentEncounterFrame = frame;
		world.LastDayDisplayPlaceName = frame.PlaceName ?? "";

		result.Encounter = frame;
		result.Narrative = frame.ToLegacyNarrativeTurn();

		session.ActivityLog.AppendLastDayTurn("LastDay", world.CurrentLocationId, input, frame.Narration, intent.Summary);

		var locLabel = string.IsNullOrEmpty(world.LastDayDisplayPlaceName)
			? locMgr.GetDisplayName(world.CurrentLocationId)
			: world.LastDayDisplayPlaceName;
		PushRecentLine(world, $"{locLabel}｜{input}｜{frame.Narration}");

		if (world.ActionTurnCount > 0 && world.ActionTurnCount % 3 == 0)
			await MaybeCompressStoryAsync(session, world);

		return result;
	}

	private static void PushRecentLine(WorldState world, string line)
	{
		world.RecentTurnLines.Add(line);
		while (world.RecentTurnLines.Count > 6)
			world.RecentTurnLines.RemoveAt(0);
	}

	private static async Task<IntentParseResult> ParseIntentAsync(string playerInput, string currentId, GameSession session,
		WorldState world)
	{
		var api = ApiBridge.Instance;
		var locMgr = LocationManager.Instance;
		var fallbackJson = IntentParseResult.BuildFallbackJson();

		if (api == null || !api.IsConfigured)
			return IntentParseResult.Parse(fallbackJson, playerInput);

		var sys = PromptLoader.LoadSystem("last_day_intent_parse");
		var tmpl = PromptLoader.LoadUser("last_day_intent_parse");
		var intentVars = new Dictionary<string, string>
		{
			["current_location"] = $"{locMgr.GetDisplayName(currentId)}（{currentId}）",
			["available_location_ids"] = locMgr.FormatLocationIdsComma(),
			["current_time"] = TimeManager.Instance?.GetClockDisplay() ?? "--:--",
			["current_money"] = MoneySystem.Instance?.Yuan.ToString() ?? "0",
			["current_battery"] = BatterySystem.Instance?.Percent.ToString("F0") ?? "0",
			["final_wish"] = session.FinalWish ?? "",
			["story_summary"] = world.NarrativeSummary ?? "",
			["player_input"] = playerInput
		};
		SoulPromptVars.AddSoulFields(intentVars, session.Soul);
		var user = PromptLoader.ApplyVars(tmpl, intentVars);

		var res = await api.ChatJsonAsync(sys, user, fallbackJson);
		var json = res.Success && !string.IsNullOrWhiteSpace(res.Content) ? res.Content : fallbackJson;
		return IntentParseResult.Parse(json, playerInput);
	}

	private static ResolvedAction BuildResolvedAction(IntentParseResult intent, string currentId, LocationManager locMgr,
		string playerInput)
	{
		var ra = new ResolvedAction
		{
			IntentType = intent.IntentType,
			GameMinutesCost = CostBandToMinutes(intent.CostBand),
			MoneyDeltaYuan = MoneyBandToYuan(intent.MoneyBand),
			ScreenActive = intent.ScreenUsage is "active" or "brief",
			EnqueueMessageSend = intent.IntentType == "message",
			MessageTargetHint = intent.TargetPerson,
			SystemNote = intent.Summary
		};

		if (intent.LocationFit == "not_possible_today")
		{
			ra.RejectNote = string.IsNullOrEmpty(intent.RejectReason) ? "这件事今天很难成立。" : intent.RejectReason;
			ra.GameMinutesCost = Math.Max(10, ra.GameMinutesCost / 2);
			ra.FreeformDestinationText = intent.DestinationText ?? "";
			return ra;
		}

		string TryResolveCanonical()
		{
			if (locMgr.IsValidId(intent.SuggestedLocationId))
				return intent.SuggestedLocationId;
			var d = (intent.DestinationText ?? "").Trim();
			if (!string.IsNullOrEmpty(d))
			{
				var hit = locMgr.TryResolveLocal(d);
				if (locMgr.IsValidId(hit)) return hit;
			}
			var hitInput = locMgr.TryResolveLocal(playerInput ?? "");
			if (locMgr.IsValidId(hitInput)) return hitInput;
			return null;
		}

		var wantMove = intent.IsTravelIntent
		               || intent.LocationFit == "move_recommended"
		               || intent.LocationFit == "move_required";

		var targetId = TryResolveCanonical();
		if (wantMove && !string.IsNullOrEmpty(targetId) && targetId != currentId)
		{
			ra.LocationChanged = true;
			ra.NewLocationId = targetId;
			ra.GameMinutesCost += 25;
			return ra;
		}

		if (wantMove && string.IsNullOrEmpty(targetId))
			ra.FreeformDestinationText = (intent.DestinationText ?? "").Trim().Length > 0
				? intent.DestinationText!.Trim()
				: (playerInput ?? "").Trim();

		return ra;
	}

	private static void ApplyToSystems(ResolvedAction ra, IntentParseResult intent, WorldState world, string playerInput)
	{
		var tm = TimeManager.Instance;
		var money = MoneySystem.Instance;
		var bat = BatterySystem.Instance;

		if (ra.LocationChanged && !string.IsNullOrEmpty(ra.NewLocationId) &&
		    LocationManager.Instance.IsValidId(ra.NewLocationId))
		{
			world.CurrentLocationId = ra.NewLocationId;
			world.MarkVisited(ra.NewLocationId);
			GameManager.Instance?.Session.ActivityLog.AppendLocationVisit("LastDay", ra.NewLocationId,
				LocationManager.Instance.GetDisplayName(ra.NewLocationId), "移动");
		}

		tm?.AdvanceGameMinutes(ra.GameMinutesCost);
		if (ra.MoneyDeltaYuan > 0)
			money?.TrySpend(ra.MoneyDeltaYuan);

		bat?.ApplyTurnDrain(ra.ScreenActive ? intent.ScreenUsage : "none");

		if (intent.IntentType == "charge")
			bat?.ChargeMinutes(ra.GameMinutesCost);

		if (ra.EnqueueMessageSend)
		{
			var rel = MapRelationship(intent.TargetPerson);
			GameManager.Instance?.Session.ActivityLog.AppendPhoneMessage("LastDay", "sent", playerInput ?? "");
			MessageSystem.Instance?.EnqueueSend(playerInput ?? "", rel, "短句、克制");
		}
	}

	private static string MapRelationship(string targetPerson)
	{
		var t = (targetPerson ?? "").Trim().ToLowerInvariant();
		if (t.Contains("家人") || t.Contains("父母") || t == "family") return "家人";
		if (t.Contains("同事") || t == "colleague") return "同事";
		if (t == "relation" || t.Contains("前任") || t.Contains("恋人")) return "重要的人";
		if (t.Contains("自己") || t == "self") return "自己";
		return "朋友";
	}

	private static int CostBandToMinutes(string band)
	{
		return band switch
		{
			"instant" => 5,
			"short" => 20,
			"medium" => 45,
			"long" => 90,
			"xlong" => 150,
			_ => 25
		};
	}

	private static int MoneyBandToYuan(string band)
	{
		return band switch
		{
			"none" => 0,
			"low" => 40,
			"medium" => 120,
			"high" => 380,
			_ => 0
		};
	}

	private static async Task<EncounterFrame> RenderEncounterFrameAsync(GameSession session, WorldState world,
		string playerInput, IntentParseResult intent, ResolvedAction ra)
	{
		var api = ApiBridge.Instance;
		var locMgr = LocationManager.Instance;
		var fallbackJson = EncounterFrame.BuildFallbackJson();
		var rejectFallback = string.IsNullOrEmpty(ra.RejectNote) ? "时间从指缝里滑过去，你没再多说什么。" : ra.RejectNote;

		if (api == null || !api.IsConfigured)
			return EncounterFrame.Parse(fallbackJson, rejectFallback);

		var sys = PromptLoader.LoadSystem("last_day_encounter_frame_render");
		var tmpl = PromptLoader.LoadUser("last_day_encounter_frame_render");
		var resolvedJson = JsonSerializer.Serialize(ra, ResolvedJsonOpts);
		var narrVars = new Dictionary<string, string>
		{
			["current_location"] = $"{locMgr.GetDisplayName(world.CurrentLocationId)}（{world.CurrentLocationId}）",
			["current_time"] = TimeManager.Instance?.GetClockDisplay() ?? "--:--",
			["current_money"] = MoneySystem.Instance?.Yuan.ToString() ?? "0",
			["current_battery"] = BatterySystem.Instance?.Percent.ToString("F0") ?? "0",
			["final_wish"] = session.FinalWish ?? "",
			["story_summary"] = world.NarrativeSummary ?? "",
			["player_input"] = playerInput,
			["destination_text"] = intent.DestinationText ?? "",
			["is_travel_intent"] = intent.IsTravelIntent ? "是" : "否",
			["freeform_destination_text"] = ra.FreeformDestinationText ?? "",
			["intent_summary"] = intent.Summary ?? "",
			["resolved_action_json"] = resolvedJson
		};
		SoulPromptVars.AddSoulFields(narrVars, session.Soul);
		var rollFavorNpc = GD.Randf() < 0.5f;
		narrVars["encounter_presence_hint"] = rollFavorNpc
			? EncounterPresenceHintFavorNpc
			: EncounterPresenceHintFavorSolo;

		var user = PromptLoader.ApplyVars(tmpl, narrVars);

		var res = await api.ChatJsonAsync(sys, user, fallbackJson);
		var json = res.Success && !string.IsNullOrWhiteSpace(res.Content) ? res.Content : fallbackJson;
		var frame = EncounterFrame.Parse(json, rejectFallback);
		ApplyNpcPresenceRollGuarantee(frame, rollFavorNpc, rejectFallback);
		return frame;
	}

	/// <summary>
	/// 本回合若系统抽中「同框人物优先」，则保证 UI 会呈现人物框；模型偶发输出 show_character_frame=false 时在此补全最小 character_* 并再次 Normalize。
	/// </summary>
	private static void ApplyNpcPresenceRollGuarantee(EncounterFrame frame, bool rollFavorNpc, string narrationFallback)
	{
		if (frame == null || !rollFavorNpc || frame.ShowCharacterFrame)
			return;
		frame.ShowCharacterFrame = true;
		if (string.IsNullOrWhiteSpace(frame.CharacterName))
			frame.CharacterName = "路人";
		if (string.IsNullOrWhiteSpace(frame.CharacterRole))
			frame.CharacterRole = "路人";
		if (string.IsNullOrWhiteSpace(frame.CharacterVisualBrief))
			frame.CharacterVisualBrief = "侧身轮廓，面部不清，克制剪影";
		EncounterFrame.Normalize(frame, narrationFallback);
	}

	private static async Task MaybeCompressStoryAsync(GameSession session, WorldState world)
	{
		var api = ApiBridge.Instance;
		var lines = string.Join("\n", world.RecentTurnLines);
		const string fallback = "{\"summary\":\"\",\"arc_state\":\"stalled\",\"open_threads\":[]}";

		if (api == null || !api.IsConfigured)
			return;

		var sys = PromptLoader.LoadSystem("story_summary");
		var tmpl = PromptLoader.LoadUser("story_summary");
		var sumVars = new Dictionary<string, string>
		{
			["final_wish"] = session.FinalWish ?? "",
			["previous_summary"] = world.NarrativeSummary ?? "",
			["recent_turns"] = string.IsNullOrEmpty(lines) ? "（暂无）" : lines
		};
		SoulPromptVars.AddSoulFields(sumVars, session.Soul);
		var user = PromptLoader.ApplyVars(tmpl, sumVars);

		var res = await api.ChatJsonAsync(sys, user, fallback);
		var json = res.Success && !string.IsNullOrWhiteSpace(res.Content) ? res.Content : fallback;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.TryGetProperty("summary", out var s))
				world.NarrativeSummary = ContentSafetyFilter.SanitizeDisplay(s.GetString() ?? world.NarrativeSummary ?? "");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[LastDayDirector] story_summary 解析失败: {e.Message}");
		}
	}
}
