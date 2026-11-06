shader_type spatial;
// spatial_env_surface — haunted-movers. Purpose: procedural house/furniture surfaces with
// no texture files. Projects onto the world-space plane each face lies in, so plain boxes
// need no UVs. Target: desktop Forward+, 0 texture samples, a few value-noise calls.
// `mode` is a per-material uniform, so the branch is uniform (coherent), not per-pixel:
//   0 = wood planks (floors), 1 = wall: wallpaper + wainscot inside the house footprint,
//   clapboard siding outside it, 2 = grass, 3 = wood grain (furniture).

group_uniforms look;
uniform int mode : hint_range(0, 3) = 0;
uniform vec3 base_color : source_color = vec3(0.45, 0.3, 0.2);
uniform vec3 accent_color : source_color = vec3(0.3, 0.2, 0.14);
group_uniforms layout;
uniform float plank_width : hint_range(0.05, 0.5) = 0.18;   // m, floor board width
uniform float plank_length : hint_range(0.5, 4.0) = 1.6;    // m, before a butt joint
uniform float wainscot_height : hint_range(0.0, 2.0) = 0.95; // m above floor_y
uniform float floor_y = 0.0;                                 // ground floor height for the wainscot
uniform float storey_height : hint_range(2.0, 5.0) = 3.0;    // wainscot repeats every storey
uniform vec4 interior_rect = vec4(-5.95, 0.05, 5.95, 9.95);     // x0, z0, x1, z1 of the inside; outside = siding
uniform vec3 siding_color : source_color = vec3(0.36, 0.42, 0.40);
group_uniforms;

const float SEAM_WIDTH = 0.012;  // m, dark gap between boards

varying vec3 wpos;
varying vec3 wnrm;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 col = base_color;
	float rough = 0.8;
	vec3 n = abs(wnrm);
	// pick the plane the face lies in
	vec2 uv = n.y > 0.5 ? wpos.xz : (n.x > 0.5 ? wpos.zy : wpos.xy);

	if (mode == 0) {
		float row = floor(uv.y / plank_width);
		float along = uv.x + hash(vec2(row, 3.0)) * 4.0;
		float seg = floor(along / plank_length);
		float tint = hash(vec2(row, seg)) * 0.25 - 0.12;
		float grain = noise(vec2(along * 1.5, uv.y * 60.0)) * 0.18;
		float seam = smoothstep(0.0, SEAM_WIDTH, fract(uv.y / plank_width) * plank_width)
			* smoothstep(0.0, SEAM_WIDTH, fract(along / plank_length) * plank_length);
		col = base_color * (1.0 + tint + grain) * mix(0.45, 1.0, seam);
		rough = 0.55;
	} else if (mode == 1) {
		float h = mod(wpos.y - floor_y, storey_height);
		// wallpaper: two-tone stripes, faint motif, blotchy age stains
		float stripe = step(0.5, fract(uv.x / 0.22));
		float motif = smoothstep(0.55, 0.75, noise(uv * vec2(9.0, 7.0)));
		vec3 paper = mix(base_color, base_color * 0.82, stripe) * (1.0 - motif * 0.12)
			* (0.93 + noise(uv * 2.0) * 0.12);
		// wainscot: panelled boards with a lighter rail on top
		float panel = smoothstep(0.0, 0.02, abs(fract(uv.x / 0.6) - 0.5) - 0.44);
		vec3 wains = accent_color * (1.0 - panel * 0.35) * (0.95 + noise(uv * 30.0) * 0.1)
			* mix(1.0, 1.25, step(wainscot_height - 0.05, h));
		vec3 inside_col = mix(paper, wains, step(h, wainscot_height));
		// siding: overlapping horizontal boards, shadow line under each
		float board = fract(wpos.y / 0.2);
		vec3 siding = siding_color * mix(0.7, 1.0, smoothstep(0.0, 0.15, board)) * (0.92 + noise(uv * vec2(1.0, 25.0)) * 0.12);
		// inside the footprint (shrunk a little so the wall's outer face reads as outside)
		float inside = step(interior_rect.x, wpos.x) * step(wpos.x, interior_rect.z)
			* step(interior_rect.y, wpos.z) * step(wpos.z, interior_rect.w);
		col = mix(siding, inside_col, inside);
		rough = mix(0.85, 0.7, inside);
	} else if (mode == 2) {
		float g = noise(uv * 3.0) * 0.5 + noise(uv * 17.0) * 0.5;
		col = mix(base_color, accent_color, g);
		rough = 0.95;
	} else {
		float grain = noise(vec2(uv.x * 2.0, uv.y * 40.0)) * 0.12 + noise(uv * 6.0) * 0.06;
		col = base_color * (0.92 + grain);
		rough = 0.65;
	}
	ALBEDO = col;
	ROUGHNESS = rough;
}
