using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 由 LLM 根据灵魂画像生成死因；失败或未配置 API 时用本地兜底。
/// </summary>
public static class DeathCauseGenerator
{
	public static string StableIdFromText(string text)
	{
		var raw = text ?? "";
		if (string.IsNullOrEmpty(raw)) return "empty";
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
		return "gen_" + Convert.ToHexString(bytes)[..16].ToLowerInvariant();
	}

	public static async Task<DeathCause> GenerateAsync(SoulProfile profile)
	{
		var fbText = Phase1Copy.FallbackDeathCauseBody(profile);
		var fb = new DeathCause
		{
			Text = fbText,
			Id = StableIdFromText("fb:" + fbText)
		};

		var api = ApiBridge.Instance;
		if (api == null || !api.IsConfigured) return fb;

		var grim = PromptLoader.LoadSystem("grim_reaper");
		var taskSystem = PromptLoader.LoadSystem("death_cause_generation");
		var tmpl = PromptLoader.LoadUser("death_cause_generation");
		var vars = new Dictionary<string, string>
		{
			["tags"] = string.Join("、", profile?.Tags ?? []),
			["work"] = profile?.WorkText ?? "",
			["relation"] = profile?.RelationText ?? "",
			["escape"] = profile?.EscapeText ?? "",
			["tone_hint"] = SoulPromptVars.ToneHintForDeathCause(profile ?? new SoulProfile())
		};
		var user = PromptLoader.ApplyVars(tmpl, vars);
		var system = PromptLoader.Combine(grim, taskSystem);

		var r = await api.ChatJsonAsync(system, user, fallback: null);
		if (!r.Success || string.IsNullOrWhiteSpace(r.Content)) return fb;

		try
		{
			using var doc = JsonDocument.Parse(r.Content);
			if (!doc.RootElement.TryGetProperty("text", out var el)) return fb;
			var text = el.GetString()?.Trim();
			if (string.IsNullOrEmpty(text)) return fb;
			text = ContentSafetyFilter.PolishDeathCauseBody(text);
			return new DeathCause { Text = text, Id = StableIdFromText(text) };
		}
		catch (Exception e)
		{
			GD.PrintErr($"[DeathCauseGenerator] JSON 解析失败: {e.Message}");
			return fb;
		}
	}
}
