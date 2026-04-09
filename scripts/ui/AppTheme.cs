using Godot;

/// <summary>
/// 全局 UI：深色底、冷灰蓝 + 克制金、单一强调色；字号按竖屏 1080 基准放大，便于触控与阅读。
/// </summary>
public static class AppTheme
{
	// —— 色板（避免纯黑、避免多枚高饱和强调色）——
	public static readonly Color BgDeep = new(0.08f, 0.085f, 0.1f);
	public static readonly Color Panel = new(0.12f, 0.13f, 0.16f);
	public static readonly Color TextPrimary = new(0.88f, 0.9f, 0.93f);
	public static readonly Color TextMuted = new(0.55f, 0.6f, 0.68f);
	public static readonly Color AccentCoolBlue = new(0.45f, 0.58f, 0.72f);
	public static readonly Color AccentGold = new(0.72f, 0.62f, 0.38f);
	public static readonly Color AccentMutedRed = new(0.55f, 0.32f, 0.34f);

	/// <summary>主菜单/章节大标题。</summary>
	public const int FontSizeDisplay = 66;
	/// <summary>页面标题（登记、宣判等）。</summary>
	public const int FontSizeTitle = 54;
	/// <summary>次级标题、状态栏主数字。</summary>
	public const int FontSizeSection = 45;
	/// <summary>正文默认：场景内多数说明、叙事、表单标签。</summary>
	public const int FontSizeBody = 42;
	/// <summary>按钮、选项、长文本辅助。</summary>
	public const int FontSizeBodySmall = 39;
	/// <summary>次要说明、副标题、状态栏相位。</summary>
	public const int FontSizeCaption = 33;
	/// <summary>极次要脚注（尽量少用）。</summary>
	public const int FontSizeMicro = 30;

	/// <summary>主要按钮最小高度（约 48dp+，适配手指）。</summary>
	public const int MinButtonHeight = 54;

	private static Theme _cached;

	public static Theme GetGameTheme()
	{
		if (_cached != null) return _cached;
		_cached = CreateGameTheme();
		return _cached;
	}

	public static void ApplyTo(Control root)
	{
		if (root == null) return;
		root.Theme = GetGameTheme();
	}

	private static Theme CreateGameTheme()
	{
		var t = new Theme();
		var font = ThemeDB.FallbackFont;
		t.DefaultFont = font;
		t.DefaultFontSize = FontSizeBody;

		void SetLabelLike(string type, int size)
		{
			t.SetFontSize("font_size", type, size);
			t.SetFont("font", type, font);
		}

		SetLabelLike("Label", FontSizeBody);
		t.SetColor("font_color", "Label", TextPrimary);
		t.SetColor("font_shadow_color", "Label", Colors.Black);
		t.SetConstant("shadow_offset_x", "Label", 1);
		t.SetConstant("shadow_offset_y", "Label", 1);

		var btnFont = FontSizeBodySmall;
		SetLabelLike("Button", btnFont);
		t.SetColor("font_color", "Button", TextPrimary);
		t.SetColor("font_hover_color", "Button", AccentGold);
		t.SetColor("font_pressed_color", "Button", TextPrimary);
		t.SetColor("font_disabled_color", "Button", new Color(TextPrimary, 0.45f));

		int padH = 22;
		int padV = 16;
		t.SetStylebox("normal", "Button", MakeFlat(Panel, AccentCoolBlue, 2, padH, padV, 8));
		t.SetStylebox("hover", "Button", MakeFlat(Lighten(Panel, 0.04f), AccentGold, 2, padH, padV, 8));
		t.SetStylebox("pressed", "Button", MakeFlat(Panel.Darkened(0.08f), AccentMutedRed, 2, padH, padV, 8));
		t.SetStylebox("disabled", "Button", MakeFlat(Panel.Darkened(0.12f), new Color(AccentCoolBlue, 0.35f), 1, padH, padV, 8));
		var focusSb = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = AccentGold
		};
		focusSb.SetBorderWidthAll(2);
		focusSb.SetCornerRadiusAll(8);
		focusSb.SetExpandMarginAll(2);
		t.SetStylebox("focus", "Button", focusSb);

		SetLabelLike("LineEdit", FontSizeBody);
		t.SetColor("font_color", "LineEdit", TextPrimary);
		t.SetColor("caret_color", "LineEdit", AccentGold);
		t.SetColor("font_placeholder_color", "LineEdit", new Color(TextPrimary, 0.42f));
		t.SetStylebox("normal", "LineEdit", MakeFlat(BgDeep, new Color(AccentCoolBlue, 0.55f), 1, 16, 14, 6));
		t.SetConstant("minimum_character_width", "LineEdit", 4);

		SetLabelLike("TextEdit", FontSizeBody);
		t.SetColor("font_color", "TextEdit", TextPrimary);
		t.SetColor("caret_color", "TextEdit", AccentGold);
		t.SetStylebox("normal", "TextEdit", MakeFlat(BgDeep, new Color(AccentCoolBlue, 0.45f), 1, 16, 16, 8));
		t.SetConstant("line_spacing", "TextEdit", 4);

		foreach (var key in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size" })
			t.SetFontSize(key, "RichTextLabel", FontSizeBody);
		t.SetFont("normal_font", "RichTextLabel", font);
		t.SetColor("default_color", "RichTextLabel", TextPrimary);

		SetLabelLike("ItemList", FontSizeBodySmall);
		t.SetColor("font_color", "ItemList", TextPrimary);
		t.SetColor("font_hovered_color", "ItemList", AccentGold);
		t.SetColor("font_selected_color", "ItemList", TextPrimary);
		t.SetStylebox("bg", "ItemList", MakeFlat(BgDeep, new Color(AccentCoolBlue, 0.25f), 1, 10, 8, 6));

		SetLabelLike("CheckBox", FontSizeBodySmall);
		SetLabelLike("CheckButton", FontSizeBodySmall);

		// 进度条略增高，便于扫视电量。
		var track = MakeFlat(new Color(BgDeep.R, BgDeep.G, BgDeep.B, 0.95f), new Color(AccentCoolBlue, 0.35f), 1, 4, 4, 4);
		var fill = MakeFlat(new Color(AccentCoolBlue.R, AccentCoolBlue.G, AccentCoolBlue.B, 0.55f), AccentGold, 0, 4, 4, 4);
		t.SetStylebox("background", "ProgressBar", track);
		t.SetStylebox("fill", "ProgressBar", fill);

		t.SetStylebox("panel", "PanelContainer",
			MakeFlat(Panel, new Color(AccentCoolBlue.R, AccentCoolBlue.G, AccentCoolBlue.B, 0.35f), 1, 18, 16, 8));
		t.SetStylebox("panel", "Panel",
			MakeFlat(Panel, new Color(AccentCoolBlue.R, AccentCoolBlue.G, AccentCoolBlue.B, 0.28f), 1, 18, 16, 8));

		return t;
	}

	private static Color Lighten(Color c, float amount)
	{
		return new Color(
			Mathf.Clamp(c.R + amount, 0f, 1f),
			Mathf.Clamp(c.G + amount, 0f, 1f),
			Mathf.Clamp(c.B + amount, 0f, 1f),
			c.A);
	}

	private static StyleBoxFlat MakeFlat(Color bg, Color border, int width, int padH, int padV, int radius)
	{
		var sb = new StyleBoxFlat { BgColor = bg };
		sb.SetBorderWidthAll(width);
		sb.BorderColor = border;
		sb.SetCornerRadiusAll(radius);
		sb.ContentMarginLeft = padH;
		sb.ContentMarginRight = padH;
		sb.ContentMarginTop = padV;
		sb.ContentMarginBottom = padV;
		return sb;
	}
}
