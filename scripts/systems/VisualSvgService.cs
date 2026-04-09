using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// brief → SVG → Texture；按内容哈希缓存。场景与人物共用生成管线，失败时分别降级。
/// </summary>
public static class VisualSvgService
{
	private const string Palette =
		"#0c0d11,#12141a,#3a4a5c,#b89a5a,#8a4a52";

	public static string SceneCacheKey(string brief)
	{
		return "scene::" + HashBrief(brief ?? "");
	}

	public static string CharacterCacheKey(string role, string brief)
	{
		var r = (role ?? "").Trim();
		return "character::" + r + "::" + HashBrief(brief ?? "");
	}

	public static async Task<Texture2D> GetSceneTextureAsync(string visualBrief, float scale = 1.8f)
	{
		var world = GameManager.Instance?.Session.World;
		var key = SceneCacheKey(visualBrief);
		var brief = string.IsNullOrWhiteSpace(visualBrief) ? "安静、低饱和的场景剪影" : visualBrief.Trim();

		var svg = "";
		if (world != null && world.SceneVisualSvgCache.TryGetValue(key, out var cached) && !string.IsNullOrEmpty(cached))
			svg = cached;
		else
		{
			svg = await FetchSceneSvgAsync(brief) ?? MinimalSceneSvg();
			if (world != null)
				world.SceneVisualSvgCache[key] = svg;
		}

		return SvgToTexture(svg, scale) ?? SolidFallbackTexture();
	}

	public static async Task<Texture2D> GetCharacterTextureAsync(string role, string visualBrief, float scale = 1.8f)
	{
		var world = GameManager.Instance?.Session.World;
		var key = CharacterCacheKey(role, visualBrief);
		var brief = string.IsNullOrWhiteSpace(visualBrief) ? "侧身轮廓，面部不清" : visualBrief.Trim();

		var svg = "";
		if (world != null && world.CharacterVisualSvgCache.TryGetValue(key, out var cached) && !string.IsNullOrEmpty(cached))
			svg = cached;
		else
		{
			svg = await FetchCharacterSvgAsync(role ?? "人物", brief) ?? MinimalCharacterSvg();
			if (world != null)
				world.CharacterVisualSvgCache[key] = svg;
		}

		return SvgToTexture(svg, scale) ?? SolidFallbackTexture();
	}

	public static Texture2D SolidFallbackTexture()
	{
		var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
		img.Fill(new Color(0.07f, 0.08f, 0.1f));
		return ImageTexture.CreateFromImage(img);
	}

	private static string HashBrief(string brief)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(brief));
		return Convert.ToHexString(bytes.AsSpan(0, 8));
	}

	private static async Task<string> FetchSceneSvgAsync(string brief)
	{
		var api = ApiBridge.Instance;
		if (api == null || !api.IsConfigured)
			return null;

		var sys = PromptLoader.LoadSystem("scene_visual_svg");
		var tmpl = PromptLoader.LoadUser("scene_visual_svg");
		var user = PromptLoader.ApplyVars(tmpl, new Dictionary<string, string>
		{
			["visual_brief"] = brief,
			["view_box"] = "0 0 256 256",
			["palette"] = Palette,
			["max_paths"] = "24"
		});

		var res = await api.ChatTextAsync(sys, user, MinimalSceneSvg());
		if (!res.Success || string.IsNullOrWhiteSpace(res.Content))
			return null;
		return StripMarkdownFences(res.Content.Trim());
	}

	private static async Task<string> FetchCharacterSvgAsync(string role, string brief)
	{
		var api = ApiBridge.Instance;
		if (api == null || !api.IsConfigured)
			return null;

		var sys = PromptLoader.LoadSystem("character_visual_svg");
		var tmpl = PromptLoader.LoadUser("character_visual_svg");
		var user = PromptLoader.ApplyVars(tmpl, new Dictionary<string, string>
		{
			["character_role"] = role,
			["visual_brief"] = brief,
			["view_box"] = "0 0 256 256",
			["palette"] = Palette,
			["max_paths"] = "22"
		});

		var res = await api.ChatTextAsync(sys, user, MinimalCharacterSvg());
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
			var end = s.LastIndexOf("```", StringComparison.Ordinal);
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
		catch (Exception e)
		{
			GD.PrintErr($"[VisualSvg] SVG 栅格化失败: {e.Message}");
			return null;
		}
	}

	private static string MinimalSceneSvg()
	{
		return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 256 256\"><rect fill=\"#0c0d11\" width=\"256\" height=\"256\"/><path d=\"M64 180 L128 60 L192 180 Z\" fill=\"none\" stroke=\"#3a4a5c\" stroke-width=\"8\"/></svg>";
	}

	private static string MinimalCharacterSvg()
	{
		return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 256 256\"><rect fill=\"#0c0d11\" width=\"256\" height=\"256\"/><circle cx=\"128\" cy=\"88\" r=\"36\" fill=\"none\" stroke=\"#b89a5a\" stroke-width=\"6\"/><path d=\"M128 130 L128 210 M88 170 L168 170\" fill=\"none\" stroke=\"#8a4a52\" stroke-width=\"6\"/></svg>";
	}
}
