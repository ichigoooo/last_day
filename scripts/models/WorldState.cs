using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// 最后一天「有限世界」运行时状态（由系统维护，不由 LLM 直接改写）。
/// </summary>
public class WorldState
{
	[JsonPropertyName("current_location_id")]
	public string CurrentLocationId { get; set; } = "home";

	[JsonPropertyName("visited_location_ids")]
	public List<string> VisitedLocationIds { get; set; } = new();

	/// <summary>story_summary 产出的长期叙事线索。</summary>
	[JsonPropertyName("narrative_summary")]
	public string NarrativeSummary { get; set; } = "";

	/// <summary>本阶段内已完成行动回合数（用于每 3 回合压缩摘要）。</summary>
	[JsonPropertyName("action_turn_count")]
	public int ActionTurnCount { get; set; }

	[JsonPropertyName("wish_progress_level")]
	public int WishProgressLevel { get; set; }

	[JsonPropertyName("hesitation_level")]
	public int HesitationLevel { get; set; }

	/// <summary>进入最后一天场景时是否已初始化现金/电量/时钟（每局一次）。</summary>
	[JsonPropertyName("last_day_systems_initialized")]
	public bool LastDaySystemsInitialized { get; set; }

	/// <summary>供 story_summary 压缩用的最近回合短句（每条约一行）。</summary>
	[JsonPropertyName("recent_turn_lines")]
	public List<string> RecentTurnLines { get; set; } = new();

	/// <summary>朋友圈式短记录（本局）。</summary>
	[JsonPropertyName("moments_lines")]
	public List<string> MomentsLines { get; set; } = new();

	/// <summary>开放输入下玩家声明的目的地原文（本回合或最近一轮）。</summary>
	[JsonPropertyName("last_day_freeform_destination")]
	public string LastDayFreeformDestination { get; set; } = "";

	/// <summary>当前回合用于标题栏展示的地点名（来自遭遇帧，可与 canonical id 解耦）。</summary>
	[JsonIgnore]
	public string LastDayDisplayPlaceName { get; set; } = "";

	/// <summary>当前回合遭遇帧运行时引用（不入存档）。</summary>
	[JsonIgnore]
	public EncounterFrame CurrentEncounterFrame { get; set; }

	/// <summary>场所 id → 已生成的 SVG 源码（内存缓存，不入存档）。旧路径兼容。</summary>
	[JsonIgnore]
	public Dictionary<string, string> LocationSvgCache { get; } = new();

	/// <summary>场景 brief 哈希键 → SVG 源码。</summary>
	[JsonIgnore]
	public Dictionary<string, string> SceneVisualSvgCache { get; } = new();

	/// <summary>人物 brief 哈希键 → SVG 源码。</summary>
	[JsonIgnore]
	public Dictionary<string, string> CharacterVisualSvgCache { get; } = new();

	public void MarkVisited(string locationId)
	{
		if (string.IsNullOrEmpty(locationId)) return;
		if (!VisitedLocationIds.Contains(locationId))
			VisitedLocationIds.Add(locationId);
	}

	public void ResetForNewSession()
	{
		CurrentLocationId = "home";
		VisitedLocationIds.Clear();
		NarrativeSummary = "";
		ActionTurnCount = 0;
		WishProgressLevel = 0;
		HesitationLevel = 0;
		LastDaySystemsInitialized = false;
		RecentTurnLines.Clear();
		MomentsLines.Clear();
		LastDayFreeformDestination = "";
		LastDayDisplayPlaceName = "";
		CurrentEncounterFrame = null;
		LocationSvgCache.Clear();
		SceneVisualSvgCache.Clear();
		CharacterVisualSvgCache.Clear();
	}
}
