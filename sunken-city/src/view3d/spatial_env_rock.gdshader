shader_type spatial;
// Sunken City — rock walls (Claude, 2026-11-02). Forward+, desktop (Mac/Windows); one
// pass, no texture samples. Sunken-city rock: blocky "texel" noise on a 1/16-unit grid (one sprite pixel), so the
// 3D walls share the pixel scale of the 2D characters. Caustics ripple on submerged
// surfaces; ground the water has left turns warm, with moss on top faces.

render_mode cull_disabled;  // contour walls are emitted in either winding

const float TEXELS_PER_UNIT = 16.0;  // one tile = one world unit = 16 sprite pixels
const float CAUSTIC_DEPTH = 18.0;    // caustics fade out this many tiles below the surface
const vec2 BRICK = vec2(1.0, 0.5);   // carved city blocks on front faces: 1 tile wide, half a tile tall
const float MORTAR = 0.07;           // mortar line width, in brick-size fractions

group_uniforms colors;
uniform vec3 wet_color : source_color = vec3(0.32, 0.5, 0.5);
uniform vec3 dry_color : source_color = vec3(0.55, 0.47, 0.36);
uniform vec3 moss_color : source_color = vec3(0.36, 0.52, 0.22);
group_uniforms water;
uniform float surface_y = 0.0;   // world-space water surface (y up)
uniform float dry_amount = 0.0;  // 0 flooded .. 1 drained
uniform float caustic_strength = 0.55;

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
	vec3 col = mix(wet_color, dry_color, dry) * (0.78 + 0.35 * n) * COLOR.rgb;  // COLOR tints breakable chunks
	float top = smoothstep(0.55, 0.85, wnormal.y);
	col = mix(col, moss_color * (0.8 + 0.3 * n), top * dry);
	// front faces are the drowned city's masonry: offset brick rows with dark mortar
	vec2 b = wpos.xy / BRICK;
	b.x += step(1.0, mod(floor(b.y), 2.0)) * 0.5;
	vec2 f = fract(floor(b * TEXELS_PER_UNIT * BRICK) / (TEXELS_PER_UNIT * BRICK));
	float mortar = step(f.x, MORTAR) + step(f.y, MORTAR * 2.0);
	float front = smoothstep(0.6, 0.9, wnormal.z);
	col *= mix(1.0, 0.55, clamp(mortar, 0.0, 1.0) * front);
	col *= mix(1.0, 0.85 + 0.25 * hash(vec3(floor(b), 3.1)), front);  // each block a slightly different shade
	// sides (the depth of the rock) darker, so shapes read against the water
	col *= mix(0.5, 1.0, front);
	ALBEDO = col;
	ROUGHNESS = 0.95;
	float c = caustic(floor(wpos.xy * TEXELS_PER_UNIT) / TEXELS_PER_UNIT + wpos.z * 0.2, TIME);
	float facing = max(wnormal.y, 0.0) * 0.8 + max(wnormal.z, 0.0) * 0.4;
	float depth_fade = clamp(1.0 - (surface_y - wpos.y) / CAUSTIC_DEPTH, 0.0, 1.0);
	EMISSION = vec3(0.35, 0.75, 0.85) * c * facing * submerged * depth_fade * caustic_strength;
}
