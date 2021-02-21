shader_type canvas_item;
render_mode blend_mul;

uniform sampler2D light_values;

void fragment()
{
	vec4 light = texture(light_values, UV);
	COLOR = vec4(light.rgb, 1);
}