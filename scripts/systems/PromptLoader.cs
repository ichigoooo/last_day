using Godot;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 从 res://resources/prompts/ 读取模板并做简单占位符替换。
/// 占位符：{{key}}。
/// </summary>
public static class PromptLoader
{
	private const string PromptDir = "res://resources/prompts/";

	public static string Load(string resPath)
	{
		if (!Godot.FileAccess.FileExists(resPath))
		{
			GD.PrintErr($"[PromptLoader] 不存在: {resPath}");
			return "";
		}
		using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
		return f?.GetAsText() ?? "";
	}

	public static string LoadSystem(string promptId)
	{
		return Load($"{PromptDir}{promptId}.system.txt");
	}

	public static string LoadUser(string promptId)
	{
		return Load($"{PromptDir}{promptId}.user.txt");
	}

	public static string Combine(params string[] parts)
	{
		if (parts == null || parts.Length == 0) return "";
		var sb = new StringBuilder();
		foreach (var part in parts)
		{
			if (string.IsNullOrWhiteSpace(part)) continue;
			if (sb.Length > 0) sb.Append("\n\n");
			sb.Append(part.Trim());
		}
		return sb.ToString();
	}

	public static string ApplyVars(string template, IReadOnlyDictionary<string, string> vars)
	{
		if (string.IsNullOrEmpty(template) || vars == null) return template ?? "";
		var sb = new StringBuilder(template);
		foreach (var kv in vars)
		{
			sb.Replace("{{" + kv.Key + "}}", kv.Value ?? "");
		}
		return sb.ToString();
	}
}
