shader_type spatial;
// Sunken City — rock walls (Claude, 2026-11-02). Forward+, desktop (Mac/Windows); one
// pass, two texture samples (wet/dry stone). Sunken-city rock: blocky "texel" noise on a 1/16-unit grid (one sprite pixel), so the
// 3D walls share the pixel scale of the 2D characters. Caustics ripple on submerged
// surfaces; ground the water has left turns warm, with moss on top faces.

render_mode cull_disabled;  // contour walls are emitted in either winding

const float TEXELS_PER_UNIT = 16.0;  // one tile = one world unit = 16 sprite pixels
const float CAUSTIC_DEPTH = 18.0;    // caustics fade out this many tiles below the surface

const float STONE_TILES = 4.0;       // one stone texture repeat covers this many tiles

group_uniforms textures;
uniform sampler2D stone_wet : source_color, filter_nearest, repeat_enable;
uniform sampler2D stone_dry : source_color, filter_nearest, repeat_enable;
group_uniforms colors;
uniform vec3 wet_color : source_color = vec3(0.4, 0.33, 0.27);  // warm stone: a different hue from the blue water
uniform vec3 dry_color : source_color = vec3(0.55, 0.47, 0.36);
uniform vec3 moss_color : source_color = vec3(0.36, 0.52, 0.22);
group_uniforms water;
uniform float surface_y = 0.0;   // world-space water surface (y up)
uniform float dry_amount = 0.0;  // 0 flooded .. 1 drained
uniform float caustic_strength = 0.55;
uniform float self_light = 0.45;  // share of the stone color added as emission on camera-facing faces

varying vec3 wpos;
varying vec3 wnormal;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float caustic(vec2 p, float t) {
	vec2 q = p * 1.3;
	float a = sin(q.x * 2.1 + t * 1.1 + sin(q.y * 1.7 + t * 0.7));
	float b = sin(q.y * 2.3 - t * 0.9 + sin(q.x * 1.5 - t * 0.6));
	return pow(clamp(1.0 - abs(a + b) * 0.5, 0.0, 1.0), 6.0);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnormal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 texel = floor(wpos * TEXELS_PER_UNIT) / TEXELS_PER_UNIT;
	float n = hash(floor(wpos * 4.0)) * 0.6 + hash(texel) * 0.4;
	float submerged = step(wpos.y, surface_y);
	float dry = (1.0 - submerged) * dry_amount;
	// painted stone on the faces toward the camera, plain darker color on the sides
	vec2 uv = wpos.xy / STONE_TILES;
	vec3 stone = mix(texture(stone_wet, uv).rgb * vec3(0.85, 0.92, 0.95), texture(stone_dry, uv).rgb, dry);  // a cool cast underwater
	vec3 side = mix(wet_color, dry_color, dry) * (0.6 + 0.3 * n);
	float front = smoothstep(0.5, 0.85, wnormal.z);
	vec3 col = mix(side, stone * 1.15, front) * COLOR.rgb;  // COLOR tints breakable chunks
	float top = smoothstep(0.55, 0.85, wnormal.y);
	col = mix(col, moss_color * (0.8 + 0.3 * n), top * dry);
	ALBEDO = col;
	ROUGHNESS = 0.95;
	float c = caustic(floor(wpos.xy * TEXELS_PER_UNIT) / TEXELS_PER_UNIT + wpos.z * 0.2, TIME);
	float facing = max(wnormal.y, 0.0) * 0.8 + max(wnormal.z, 0.0) * 0.4;
	float depth_fade = clamp(1.0 - (surface_y - wpos.y) / CAUSTIC_DEPTH, 0.0, 1.0);
	EMISSION = vec3(0.35, 0.75, 0.85) * c * facing * submerged * depth_fade * caustic_strength;
	// the play terrain must read in any light: its face glows faintly with its own stone
	EMISSION += col * front * self_light;
}
