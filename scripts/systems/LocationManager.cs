using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// 推荐地点目录（8 个 canonical id）与别名：供快捷入口、展示名与本地兜底映射。
/// 不限制玩家输入目的地；无法映射时由 <see cref="EncounterFrame"/> 呈现开放地点。
/// </summary>
public partial class LocationManager : Node
{
	public static LocationManager Instance { get; private set; }

	public readonly record struct CanonicalLocation(
		string Id,
		string DisplayName,
		string[] Aliases,
		/// <summary>逗号分隔标签，用于 SVG / 叙事提示。</summary>
		string TagsCsv,
		/// <summary>气质关键词。</summary>
		string Mood);

	private static readonly CanonicalLocation[] Locations =
	[
		new("home", "家", ["家里", "回家", "住所", "公寓", "租房"], "私密,休息", "安静与倦意"),
		new("office", "公司", ["办公室", "单位", "工位", "上班", "写字楼", "职场"], "公共,工作", "紧绷与克制"),
		new("park", "公园", ["绿地", "广场散步"], "公共,自然", "空旷与停顿"),
		new("hospital", "医院", ["诊所", "急诊", "病房"], "公共,医疗", "消毒水与等待"),
		new("bar", "酒吧", ["酒馆", "夜店", "喝一杯"], "消费,逃避", "嘈杂与麻醉"),
		new("school", "学校", ["校园", "教室", "母校"], "纪念,关系", "旧日与走廊"),
		new("seaside", "海边", ["海滩", "沙滩", "海岸", "看海"], "开阔,告别", "风与潮声"),
		new("cemetery", "墓园", ["墓地", "陵园", "公墓"], "纪念,告别", "沉默与泥土")
	];

	public override void _EnterTree()
	{
		Instance = this;
	}

	public static IReadOnlyList<CanonicalLocation> All => Locations;

	public string GetDisplayName(string locationId)
	{
		foreach (var l in Locations)
		{
			if (l.Id == locationId) return l.DisplayName;
		}
		return locationId;
	}

	public string GetTags(string locationId)
	{
		foreach (var l in Locations)
		{
			if (l.Id == locationId) return l.TagsCsv;
		}
		return "场所";
	}

	public string GetMood(string locationId)
	{
		foreach (var l in Locations)
		{
			if (l.Id == locationId) return l.Mood;
		}
		return "平常";
	}

	public bool IsValidId(string locationId)
	{
		if (string.IsNullOrEmpty(locationId)) return false;
		foreach (var l in Locations)
		{
			if (l.Id == locationId) return true;
		}
		return false;
	}

	/// <summary>写入 last_day_location_resolve 的可用地点列表。</summary>
	public string FormatAvailableLocationsBlock()
	{
		var sb = new StringBuilder();
		foreach (var l in Locations)
		{
			sb.Append("- id: ").Append(l.Id).Append("，名称：").Append(l.DisplayName);
			if (l.Aliases.Length > 0)
				sb.Append("，常见说法：").Append(string.Join("、", l.Aliases.Take(6)));
			sb.Append('\n');
		}
		return sb.ToString().TrimEnd();
	}

	public string FormatLocationIdsComma()
	{
		return string.Join(",", Locations.Select(l => l.Id));
	}

	/// <summary>未配置 API 时的保守本地命中（子串匹配）。</summary>
	public string TryResolveLocal(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return "";
		foreach (var l in Locations)
		{
			if (text.Contains(l.DisplayName)) return l.Id;
			foreach (var a in l.Aliases)
			{
				if (text.Contains(a)) return l.Id;
			}
		}
		return "";
	}
}
