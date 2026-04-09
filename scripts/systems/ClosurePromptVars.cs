using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>阶段 3 葬礼/冥想 Prompt 占位符与快照聚合。</summary>
public static class ClosurePromptVars
{
	public const int MaxActivityLogJsonChars = 12000;

	public static Dictionary<string, string> BuildForEulogyAndMeditation(GameSession session)
	{
		var logJson = session?.ActivityLog?.ToJsonString() ?? "{}";
		if (logJson.Length > MaxActivityLogJsonChars)
			logJson = logJson[..MaxActivityLogJsonChars] + "…";

		return new Dictionary<string, string>
		{
			["work"] = session?.Soul?.WorkText ?? "",
			["relation"] = session?.Soul?.RelationText ?? "",
			["escape"] = session?.Soul?.EscapeText ?? "",
			["archive_summary"] = session?.ArchiveSummary ?? "",
			["final_wish"] = session?.FinalWish ?? "",
			["death_cause"] = session?.DeathCauseText ?? "",
			["session_summary"] = BuildSessionSummaryBrief(session),
			["activity_log_json"] = logJson
		};
	}

	public static Dictionary<string, string> BuildForEpitaph(GameSession session)
	{
		var d = BuildForEulogyAndMeditation(session);
		return d;
	}

	public static string BuildSessionSummaryBrief(GameSession session)
	{
		if (session == null) return "";
		var w = session.World;
		var sb = new StringBuilder();
		if (!string.IsNullOrWhiteSpace(w.NarrativeSummary))
			sb.AppendLine($"叙事摘要：{w.NarrativeSummary.Trim()}");
		if (w.RecentTurnLines is { Count: > 0 })
			sb.AppendLine("近期回合：" + string.Join(" | ", w.RecentTurnLines.Take(12)));
		if (w.MomentsLines is { Count: > 0 })
			sb.AppendLine("片刻：" + string.Join(" | ", w.MomentsLines.Take(8)));
		var ff = 0;
		if (session.ActivityLog?.Entries != null)
		{
			foreach (var e in session.ActivityLog.Entries)
			{
				if (e.Kind == ActivityKinds.FaceToFaceDialogue) ff++;
			}
		}

		if (ff > 0)
			sb.AppendLine($"现场面对面对话记录条数：{ff}");
		sb.AppendLine($"到访场所数：{w.VisitedLocationIds?.Count ?? 0}");
		return sb.ToString().Trim();
	}

	public static int CountPhoneMessages(GameSession session)
	{
		if (session?.ActivityLog?.Entries == null) return 0;
		var n = 0;
		foreach (var e in session.ActivityLog.Entries)
		{
			if (e.Kind == ActivityKinds.PhoneMessage) n++;
		}
		return n;
	}

	public static string FormatVisitedLine(GameSession session)
	{
		if (session?.World?.VisitedLocationIds == null || session.World.VisitedLocationIds.Count == 0)
			return "（无）";
		var lm = LocationManager.Instance;
		var parts = new List<string>();
		foreach (var id in session.World.VisitedLocationIds)
		{
			var name = lm != null ? lm.GetDisplayName(id) : id;
			parts.Add($"{name}");
		}
		return string.Join(" → ", parts);
	}
}
