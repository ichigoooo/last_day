extends Node2D
## 插件 merovi.svgtexture2d 探测：从文件读入 SVG 字符串 → SVGTexture2D → SVGSprite2D 栅格化显示。
## 运行：在编辑器中打开 svg_plugin_test.tscn 按 F6，或临时把 project.godot run/main_scene 指向本场景。

const SvgTexture2DRes = preload("res://addons/merovi.svgtexture2d/svg_texture_2d.gd")

@onready var _sprite: Sprite2D = $SVGSprite2D

func _ready() -> void:
	var cam := $Camera2D as Camera2D
	if cam:
		cam.make_current()

	var path := "res://resources/svg_tests/demo_llm_generated.svg"
	if not FileAccess.file_exists(path):
		push_error("[svg_plugin_test] 缺少文件: " + path)
		return
	var svg_text := FileAccess.get_file_as_string(path)
	var tex: Resource = SvgTexture2DRes.new()
	tex.svg_data_frames = [svg_text]
	# 先设分辨率与尺寸，再赋 SVG，避免 setter 链式触发多次空栅格化
	_sprite.set("Resolution", 1.0)
	_sprite.set("sprite_size", 2.0)
	_sprite.set("SVGTexture", tex)
	_sprite.position = get_viewport_rect().size * 0.5
	print("[svg_plugin_test] SVG 已载入，帧数=", tex.svg_data_frames.size())
