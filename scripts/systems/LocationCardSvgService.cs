using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>场所卡片 SVG：LLM → Image.LoadSvgFromString → Texture2D；失败用极简兜底。</summary>
public static class LocationCardSvgService
{
	private const string Palette =
		"#0c0d11,#12141a,#3a4a5c,#b89a5a,#8a4a52";

	/// <summary>右侧「心境」占位小图（无 LLM，本地 SVG）。</summary>
	public static Texture2D GetMoodPlaceholderTexture()
	{
		return SvgToTexture(MinimalFallbackSvg("park"), 1.2f) ?? SolidFallbackTexture();
	}

	public static async Task<Texture2D> GetCardTextureAsync(string locationId, float scale = 1.8f)
	{
		var world = GameManager.Instance?.Session.World;
		var loc = LocationManager.Instance;
		if (loc == null || !loc.IsValidId(locationId))
			locationId = "home";

		var svg = "";
		if (world != null && world.LocationSvgCache.TryGetValue(locationId, out var cached) && !string.IsNullOrEmpty(cached))
			svg = cached;
		else
		{
			svg = await FetchSvgAsync(locationId) ?? MinimalFallbackSvg(locationId);
			if (world != null)
				world.LocationSvgCache[locationId] = svg;
		}

		return SvgToTexture(svg, scale) ?? SolidFallbackTexture();
	}

	private static async Task<string> FetchSvgAsync(string locationId)
	{
		var api = ApiBridge.Instance;
		var loc = LocationManager.Instance;
		if (api == null || !api.IsConfigured || loc == null)
			return null;

		var sys = PromptLoader.LoadSystem("location_card_svg");
		var tmpl = PromptLoader.LoadUser("location_card_svg");
		var user = PromptLoader.ApplyVars(tmpl, new Dictionary<string, string>
		{
			["location_id"] = locationId,
			["location_name"] = loc.GetDisplayName(locationId),
			["location_tags"] = loc.GetTags(locationId),
			["location_mood"] = loc.GetMood(locationId),
			["view_box"] = "0 0 256 256",
			["palette"] = Palette,
			["max_paths"] = "24"
		});

		var res = await api.ChatTextAsync(sys, user, MinimalFallbackSvg(locationId));
		if (!res.Success || string.IsNullOrWhiteSpace(res.Content))
			return null;
		return StripMarkdownFences(res.Content.Trim());
	}

	private static string StripMarkdownFences(string raw)
	{
		var s = raw.Trim();
		if (s.StartsWith("```"))
		{
			var idx = s.IndexOf('\n');
			if (idx > 0) s = s[(idx + 1)..];
			var end = s.LastIndexOf("```", System.StringComparison.Ordinal);
			if (end > 0) s = s[..end];
		}
		return s.Trim();
	}

	private static Texture2D SvgToTexture(string svg, float scale)
	{
		if (string.IsNullOrWhiteSpace(svg)) return null;
		try
		{
			var img = new Image();
			var err = img.LoadSvgFromString(svg, scale);
			if (err != Error.Ok || img.GetWidth() <= 0)
				return null;
			return ImageTexture.CreateFromImage(img);
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"[LocationCardSvg] SVG 栅格化失败: {e.Message}");
			return null;
		}
	}

	private static Texture2D SolidFallbackTexture()
	{
		var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
		img.Fill(new Color(0.07f, 0.08f, 0.1f));
		return ImageTexture.CreateFromImage(img);
	}

	private static string MinimalFallbackSvg(string locationId)
	{
		string stroke = "#b89a5a";
		string d = "M64 180 L128 60 L192 180 Z";
		switch (locationId)
		{
			case "seaside":
				stroke = "#3a4a5c";
				d = "M40 180 Q128 40 216 180";
				break;
			case "cemetery":
				stroke = "#8a4a52";
				d = "M128 60 L160 200 L96 200 Z";
				break;
			case "hospital":
				stroke = "#3a4a5c";
				d = "M128 50 L128 200 M80 120 L176 120";
				break;
		}
		return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 256 256\"><rect fill=\"#0c0d11\" width=\"256\" height=\"256\"/><path d=\"{d}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"8\"/></svg>";
	}
}
