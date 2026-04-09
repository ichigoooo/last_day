using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>加载 resources/spend_options.json。</summary>
public static class SpendOptionsStore
{
	private const string Path = "res://resources/spend_options.json";

	public static List<SpendOption> Load()
	{
		if (!FileAccess.FileExists(Path))
			return DefaultList();
		using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
		var json = f?.GetAsText() ?? "[]";
		try
		{
			var list = JsonSerializer.Deserialize<List<SpendOption>>(json);
			return list is { Count: > 0 } ? list : DefaultList();
		}
		catch
		{
			return DefaultList();
		}
	}

	private static List<SpendOption> DefaultList()
	{
		return new List<SpendOption>
		{
			new SpendOption
			{
				Id = "flowers",
				Name = "给妈妈订一束花",
				Description = "让花在日落前送到门口",
				Amount = 268
			},
			new SpendOption
			{
				Id = "donation",
				Name = "捐一笔小额善款",
				Description = "以你的名字，匿名",
				Amount = 200
			},
			new SpendOption
			{
				Id = "meal",
				Name = "最后一顿像样一点的饭",
				Description = "点一份你平时舍不得的菜",
				Amount = 380
			},
			new SpendOption
			{
				Id = "taxi",
				Name = "打车穿过半座城",
				Description = "不为省时，只为少淋一点雨",
				Amount = 120
			}
		};
	}
}
