using Godot;

/// <summary>
/// Dialogic 运行时桥接：负责启动时间线、读写变量和清理布局节点。
/// 变量路径使用点号嵌套（如 archive.work）；须在项目 Dialogic Variables 中预置默认值。
/// </summary>
public static class DialogicRuntime
{
	public const string IntroBootTimeline = "res://resources/dialogic/timelines/intro_boot.dtl";
	public const string IntroArchiveTimeline = "res://resources/dialogic/timelines/intro_archive.dtl";
	public const string VerdictReleaseTimeline = "res://resources/dialogic/timelines/verdict_release.dtl";

	public static Node GetHandler(Node host)
	{
		return host?.GetNodeOrNull<Node>("/root/Dialogic");
	}

	public static void StartTimeline(Node host, string timelinePath)
	{
		var handler = GetHandler(host);
		handler?.Call("start", timelinePath);
	}

	public static void EndTimeline(Node host, bool skipEnding = true)
	{
		var handler = GetHandler(host);
		handler?.Call("end_timeline", skipEnding);
	}

	public static void SetVariable(Node host, string key, Variant value)
	{
		var vars = GetHandler(host)?.GetNodeOrNull<Node>("VAR");
		vars?.Call("set_variable", key, value);
	}

	public static string GetString(Node host, string key, string fallback = "")
	{
		var vars = GetHandler(host)?.GetNodeOrNull<Node>("VAR");
		if (vars == null) return fallback;
		var value = vars.Call("get_variable", key, fallback);
		return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
	}

	public static void ConnectTimelineEnded(Node host, Callable callable)
	{
		var handler = GetHandler(host);
		if (handler == null || handler.IsConnected("timeline_ended", callable)) return;
		handler.Connect("timeline_ended", callable);
	}

	public static void DisconnectTimelineEnded(Node host, Callable callable)
	{
		var handler = GetHandler(host);
		if (handler == null || !handler.IsConnected("timeline_ended", callable)) return;
		handler.Disconnect("timeline_ended", callable);
	}
}
