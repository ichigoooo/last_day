@tool
extends DialogicLayoutLayer

## 移动端档案输入层：Dialogic 原版未把 Theme 赋给根节点，导致字号不生效。
## 与 AppTheme.FontSizeBody（42）对齐，并显式为 Label / LineEdit / Button 设 font_size。

const _FallbackBodySize: int = 42


func _apply_export_overrides() -> void:
	var layer_theme: Theme = get(&"theme")
	if layer_theme == null:
		layer_theme = Theme.new()

	if get_global_setting(&"font", ""):
		layer_theme.default_font = load(get_global_setting(&"font", "") as String)
	if layer_theme.default_font == null:
		layer_theme.default_font = ThemeDB.fallback_font

	var fs: int = int(get_global_setting(&"font_size", 0))
	if fs <= 0:
		fs = int(get_parent().get_global_setting(&"global_font_size", _FallbackBodySize))
	if fs <= 0:
		fs = _FallbackBodySize

	layer_theme.default_font_size = fs

	var base_font: Font = layer_theme.default_font
	for type_name in [&"Label", &"LineEdit", &"Button"]:
		layer_theme.set_font_size(&"font_size", type_name, fs)
		if base_font != null:
			layer_theme.set_font(&"font", type_name, base_font)

	layer_theme.set_color(&"font_color", &"Label", Color(0.92, 0.93, 0.95, 1))
	layer_theme.set_color(&"font_color", &"LineEdit", Color(0.92, 0.93, 0.95, 1))
	layer_theme.set_color(&"font_placeholder_color", &"LineEdit", Color(0.55, 0.6, 0.68, 1))
	layer_theme.set_color(&"font_color", &"Button", Color(0.92, 0.93, 0.95, 1))
	layer_theme.set_color(&"font_hover_color", &"Button", Color(0.95, 0.88, 0.55, 1))

	set(&"theme", layer_theme)
