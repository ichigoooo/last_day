using Godot;
using System.Text;

/// <summary>讣告卡：死因、轨迹、消费摘要、墓志铭；支持复制全文。</summary>
public partial class ShareCard : PanelContainer
{
	private Label _bodyLabel;

	public override void _Ready()
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.08f, 0.1f, 1f),
			BorderColor = new Color(0.35f, 0.38f, 0.45f, 0.9f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 22,
			ContentMarginTop = 22,
			ContentMarginRight = 22,
			ContentMarginBottom = 22
		};
		AddThemeStyleboxOverride("panel", style);

		_bodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_bodyLabel.AddThemeFontSizeOverride("font_size", AppTheme.FontSizeBodySmall);
		// 动态节点可能未继承父级 Theme，字色与深底同色时会「看似空白」
		_bodyLabel.AddThemeColorOverride("font_color", AppTheme.TextPrimary);
		AddChild(_bodyLabel);
	}

	public void ApplyFromSession(GameSession session, int yuanRemaining, float batteryPercent, int phoneMsgCount)
	{
		var text = BuildShareText(session, yuanRemaining, batteryPercent, phoneMsgCount);
		void Apply()
		{
			if (_bodyLabel != null)
				_bodyLabel.Text = text;
			else
				Callable.From(Apply).CallDeferred();
		}
		Apply();
	}

	public static string BuildShareText(GameSession session, int yuanRemaining, float batteryPercent, int phoneMsgCount)
	{
		var sb = new StringBuilder();
		sb.AppendLine("—— 讣告卡 ——");
		sb.AppendLine();
		sb.AppendLine("死因裁定");
		sb.AppendLine(session?.DeathCauseText ?? "——");
		sb.AppendLine();
		sb.AppendLine("到访轨迹");
		sb.AppendLine(ClosurePromptVars.FormatVisitedLine(session));
		sb.AppendLine();
		var spent = MoneySystem.StartingYuan - Mathf.Max(0, yuanRemaining);
		sb.AppendLine($"现金结余：{yuanRemaining} 元（本局约花费 {spent} 元）");
		sb.AppendLine($"手机电量：{batteryPercent:0.#}%");
		sb.AppendLine($"消息往来：约 {phoneMsgCount} 条记录");
		sb.AppendLine();
		sb.AppendLine("墓志铭");
		sb.AppendLine(session?.Epitaph ?? "——");
		return sb.ToString().Trim();
	}

	public string GetShareText() => _bodyLabel?.Text ?? "";

	public void CopyToClipboard()
	{
		var t = GetShareText();
		if (string.IsNullOrEmpty(t)) return;
		DisplayServer.ClipboardSet(t);
	}
}
