using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>死神花钱：扣款 + death_spend_consequence LLM。</summary>
public static class DeathSpendService
{
	public static async Task<DeathSpendResult> ExecuteAsync(SpendOption option)
	{
		var opt = option ?? new SpendOption();
		var money = MoneySystem.Instance;
		if (money != null && opt.Amount > 0 && !money.TrySpend(opt.Amount))
		{
			return new DeathSpendResult
			{
				Narration = "余额不够，这笔钱没能花出去。",
				EffectLine = "余额不足",
				MemoryTag = "practical"
			};
		}

		var fallback =
			"{\"narration\":\"钱从指缝里溜走了，像终于承认了一次你在乎。\",\"effect_line\":\"这笔钱买到了一点迟来的体面。\",\"memory_tag\":\"gift\"}";
		var api = ApiBridge.Instance;
		if (api == null || !api.IsConfigured)
			return ParseResult(fallback);

		var sys = PromptLoader.LoadSystem("death_spend_consequence");
		var tmpl = PromptLoader.LoadUser("death_spend_consequence");
		var session = GameManager.Instance?.Session;
		var user = PromptLoader.ApplyVars(tmpl, new Dictionary<string, string>
		{
			["spend_name"] = opt.Name ?? "",
			["spend_description"] = opt.Description ?? "",
			["amount"] = opt.Amount.ToString(),
			["remaining_money"] = (money?.Yuan ?? 0).ToString(),
			["final_wish"] = session?.FinalWish ?? "",
			["story_summary"] = session?.World.NarrativeSummary ?? ""
		});

		var res = await api.ChatJsonAsync(sys, user, fallback);
		var text = res.Success && !string.IsNullOrWhiteSpace(res.Content) ? res.Content : fallback;
		return ParseResult(text);
	}

	private static DeathSpendResult ParseResult(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			return new DeathSpendResult
			{
				Narration = root.TryGetProperty("narration", out var n) ? n.GetString() ?? "" : "",
				EffectLine = root.TryGetProperty("effect_line", out var e) ? e.GetString() ?? "" : "",
				MemoryTag = root.TryGetProperty("memory_tag", out var m) ? m.GetString() ?? "" : ""
			};
		}
		catch
		{
			return new DeathSpendResult { Narration = "消费完成了。", EffectLine = "已消费", MemoryTag = "practical" };
		}
	}
}
