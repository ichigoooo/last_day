using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class NarrativeOption
{
	[JsonPropertyName("slot")]
	public string Slot { get; set; } = "";

	[JsonPropertyName("label")]
	public string Label { get; set; } = "";
}

/// <summary>
/// 单段旁白 + 三选项 + 自定义提示，驱动 LastDay UI。
/// </summary>
public class NarrativeTurn
{
	[JsonPropertyName("narration")]
	public string Narration { get; set; } = "";

	[JsonPropertyName("options")]
	public List<NarrativeOption> Options { get; set; } = new();

	[JsonPropertyName("custom_hint")]
	public string CustomHint { get; set; } = "";

	/// <summary>左/右主视觉：仅场景 / 场景+角色。</summary>
	[JsonPropertyName("show_character_frame")]
	public bool ShowCharacterFrame { get; set; } = true;

	/// <summary>ambient / dialogue，与 <see cref="EncounterFrame.PresentationMode"/> 对齐。</summary>
	[JsonPropertyName("presentation_mode")]
	public string PresentationMode { get; set; } = "ambient";

	/// <summary>是否为现场对话呈现（回应语义）。</summary>
	[JsonIgnore]
	public bool DialogueMode { get; set; }

	public static NarrativeTurn Parse(string json)
	{
		var nt = new NarrativeTurn();
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			nt.Narration = root.TryGetProperty("narration", out var n) ? n.GetString() ?? "" : "";
			nt.CustomHint = root.TryGetProperty("custom_hint", out var h) ? h.GetString() ?? "" : "";
			if (root.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
			{
				foreach (var el in opts.EnumerateArray())
				{
					var slot = el.TryGetProperty("slot", out var s) ? s.GetString() ?? "" : "";
					var label = el.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
					if (!string.IsNullOrEmpty(label))
						nt.Options.Add(new NarrativeOption { Slot = slot, Label = label });
				}
			}
			if (root.TryGetProperty("show_character_frame", out var scf))
			{
				if (scf.ValueKind == JsonValueKind.True) nt.ShowCharacterFrame = true;
				else if (scf.ValueKind == JsonValueKind.False) nt.ShowCharacterFrame = false;
			}
			nt.CustomHint = ContentSafetyFilter.ClampLastDayCustomHint(nt.CustomHint ?? "");
		}
		catch
		{
			nt.Narration = "时间从指缝里滑过去，你没再多说什么。";
			nt.Options =
			[
				new NarrativeOption { Slot = "face", Label = "继续把事做完" },
				new NarrativeOption { Slot = "deflect", Label = "先转开注意力" },
				new NarrativeOption { Slot = "pause", Label = "停下来发呆一会" }
			];
			nt.CustomHint = "也可以自己输入真正想做的事。";
		}
		return nt;
	}
}
