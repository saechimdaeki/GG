shader_type canvas_item;
// Warm VHS look: slight colour split, scanlines, warm tint, vignette.

uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform float split = 0.0015;
uniform float scanline = 0.05;
uniform float noise = 0.0;  // 0..1, set by main.gd as the ghost gets close

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

void fragment() {
	vec2 uv = SCREEN_UV;
	float row = floor(FRAGCOORD.y / 4.0);
	uv.x += (hash(vec2(row, floor(TIME * 20.0))) - 0.5) * 0.02 * noise;
	vec3 c = vec3(texture(screen, uv + vec2(split, 0.0)).r,
	              texture(screen, uv).g,
	              texture(screen, uv - vec2(split, 0.0)).b);
	c *= 1.0 - scanline * (0.5 + 0.5 * sin(FRAGCOORD.y * 3.14159));
	c *= vec3(1.06, 1.0, 0.88);
	vec2 d = uv - 0.5;
	c *= 1.0 - 0.6 * dot(d, d);
	c = mix(c, vec3(hash(FRAGCOORD.xy + fract(TIME) * 100.0)), 0.35 * noise);
	COLOR = vec4(c, 1.0);
}
