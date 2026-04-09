shader_type canvas_item;
render_mode unshaded, blend_mix;

/// 随当日进度降低饱和度、略偏冷色；death_pulse 为切入死亡前强对比脉冲。
uniform float day_progress : hint_range(0.0, 1.0) = 0.0;
uniform float death_pulse : hint_range(0.0, 1.0) = 0.0;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

void fragment() {
	vec4 src = texture(screen_texture, SCREEN_UV);
	float luma = dot(src.rgb, vec3(0.299, 0.587, 0.114));
	float desat = day_progress * 0.42 + death_pulse * 0.38;
	vec3 rgb = mix(src.rgb, vec3(luma), clamp(desat, 0.0, 1.0));
	vec3 cold_tint = vec3(0.88, 0.93, 1.0);
	rgb *= mix(vec3(1.0), cold_tint, day_progress * 0.14);
	rgb = mix(rgb, vec3(0.02, 0.02, 0.04), death_pulse * 0.62);
	COLOR = vec4(rgb, 1.0);
}
