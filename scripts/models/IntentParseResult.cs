using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// 「最后一天」意图解析阶段输出：理解玩家想去哪、想做什么，不直接结算资源。
/// </summary>
public sealed class IntentParseResult
{
	public string DestinationText { get; set; } = "";
	public bool IsTravelIntent { get; set; }
	public string IntentType { get; set; } = "other";
	public string LocationFit { get; set; } = "current_ok";
	public string SuggestedLocationId { get; set; } = "";
	public string TargetPerson { get; set; } = "";
	public string CostBand { get; set; } = "short";
	public string ScreenUsage { get; set; } = "brief";
	public string MoneyBand { get; set; } = "none";
	public string Risk { get; set; } = "low";
	public string WishRelevance { get; set; } = "medium";
	public string SceneFrame { get; set; } = "ordinary_moment";
	public string Summary { get; set; } = "";
	public string RejectReason { get; set; } = "";

	public static IntentParseResult CreateDefault(string playerInput)
	{
		var t = (playerInput ?? "").Trim();
		return new IntentParseResult
		{
			DestinationText = t,
			IsTravelIntent = LooksLikeTravelHeuristic(t),
			IntentType = "other",
			LocationFit = "current_ok",
			Summary = string.IsNullOrEmpty(t) ? "继续度过今天。" : "你在琢磨接下来要做的事。"
		};
	}

	private static bool LooksLikeTravelHeuristic(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return false;
		return Regex.IsMatch(text, @"去|前往|赶往|想去|出门|回家|上学|上班|走到|赶到|抵达|回到|回公司|回老家");
	}

	public static IntentParseResult Parse(string json, string playerInputFallback)
	{
		var fallback = CreateDefault(playerInputFallback);
		if (string.IsNullOrWhiteSpace(json)) return fallback;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var dest = ReadStr(root, "destination_text", "");
			if (string.IsNullOrWhiteSpace(dest))
				dest = ReadStr(root, "destination", "");
			if (string.IsNullOrWhiteSpace(dest))
				dest = (playerInputFallback ?? "").Trim();

			var travel = ReadBool(root, "is_travel_intent", fallback.IsTravelIntent);
			if (!root.TryGetProperty("is_travel_intent", out _))
				travel = LooksLikeTravelHeuristic(playerInputFallback ?? dest);

			var r = new IntentParseResult
			{
				DestinationText = string.IsNullOrWhiteSpace(dest) ? fallback.DestinationText : dest.Trim(),
				IsTravelIntent = travel,
				IntentType = NormalizeIntentType(ReadStr(root, "intent_type", fallback.IntentType)),
				LocationFit = NormalizeLocationFit(ReadStr(root, "location_fit", fallback.LocationFit)),
				SuggestedLocationId = ReadStr(root, "suggested_location_id", ""),
				TargetPerson = ReadStr(root, "target_person", ""),
				CostBand = NormalizeCostBand(ReadStr(root, "cost_band", fallback.CostBand)),
				ScreenUsage = NormalizeScreenUsage(ReadStr(root, "screen_usage", fallback.ScreenUsage)),
				MoneyBand = NormalizeMoneyBand(ReadStr(root, "money_band", fallback.MoneyBand)),
				Risk = NormalizeRisk(ReadStr(root, "risk", fallback.Risk)),
				WishRelevance = NormalizeWishRel(ReadStr(root, "wish_relevance", fallback.WishRelevance)),
				SceneFrame = NormalizeSceneFrame(ReadStr(root, "scene_frame", fallback.SceneFrame)),
				Summary = ReadStr(root, "summary", fallback.Summary),
				RejectReason = ReadStr(root, "reject_reason", "")
			};
			if (string.IsNullOrWhiteSpace(r.Summary))
				r.Summary = fallback.Summary;
			return r;
		}
		catch
		{
			return fallback;
		}
	}

	/// <summary>API 未配置或请求失败时的占位 JSON。故意<strong>不写</strong> <c>is_travel_intent</c>，以便 <see cref="Parse"/> 在缺省字段时用 <see cref="CreateDefault"/> 的出行启发式（否则固定 false 会导致「前往海边」等永不移动）。</summary>
	public static string BuildFallbackJson()
	{
		return "{\"destination_text\":\"\",\"intent_type\":\"other\",\"location_fit\":\"current_ok\",\"suggested_location_id\":\"\",\"target_person\":\"\",\"cost_band\":\"short\",\"screen_usage\":\"brief\",\"money_band\":\"none\",\"risk\":\"low\",\"wish_relevance\":\"medium\",\"scene_frame\":\"ordinary_moment\",\"summary\":\"继续度过今天。\",\"reject_reason\":\"\"}";
	}

	private static string ReadStr(JsonElement root, string name, string def)
	{
		return root.TryGetProperty(name, out var el) ? el.GetString() ?? def : def;
	}

	private static bool ReadBool(JsonElement root, string name, bool def)
	{
		if (!root.TryGetProperty(name, out var el)) return def;
		return el.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.String => el.GetString() is "true" or "1" or "yes",
			JsonValueKind.Number => el.TryGetInt32(out var n) && n != 0,
			_ => def
		};
	}

	private static string NormalizeIntentType(string v) => v switch
	{
		"talk" or "apologize" or "confess" or "goodbye" or "buy" or "eat" or "wait" or "walk" or "look" or "remember" or "rest" or "post" or "message" or "charge" or "other" => v,
		_ => "other"
	};

	private static string NormalizeLocationFit(string v) => v switch
	{
		"current_ok" or "move_recommended" or "move_required" or "not_possible_today" => v,
		_ => "current_ok"
	};

	private static string NormalizeCostBand(string v) => v switch
	{
		"instant" or "short" or "medium" or "long" or "xlong" => v,
		_ => "short"
	};

	private static string NormalizeScreenUsage(string v) => v switch
	{
		"none" or "brief" or "active" => v,
		_ => "brief"
	};

	private static string NormalizeMoneyBand(string v) => v switch
	{
		"none" or "low" or "medium" or "high" => v,
		_ => "none"
	};

	private static string NormalizeRisk(string v) => v switch
	{
		"low" or "medium" or "high" => v,
		_ => "low"
	};

	private static string NormalizeWishRel(string v) => v switch
	{
		"low" or "medium" or "high" => v,
		_ => "medium"
	};

	private static string NormalizeSceneFrame(string v) => v switch
	{
		"unfinished_relation" or "unfinished_work" or "ordinary_moment" or "private_pause" or "ritual_farewell" or "escape_loop" or "self_repair" or "boundary_refusal" or "none" => v,
		_ => "ordinary_moment"
	};
}
