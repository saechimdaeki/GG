shader_type spatial;
// spatial_char_ghost — haunted-movers. Purpose: the house ghost's body: see-through middle,
// glowing rim (fresnel), a hem that ripples like cloth, and a slow inner shimmer.
// Target: desktop Forward+, no textures, unshaded (it is its own light), alpha blended.
render_mode unshaded, blend_mix, depth_draw_opaque, cull_back;

group_uniforms look;
uniform vec3 body_color : source_color = vec3(0.86, 0.94, 1.0);
uniform vec3 rim_color : source_color = vec3(0.62, 0.85, 1.0);
uniform float core_alpha : hint_range(0.0, 1.0) = 0.35;  // alpha facing the camera
uniform float rim_power : hint_range(0.5, 8.0) = 2.5;    // fresnel falloff; higher = thinner rim
group_uniforms motion;
uniform float hem_y = -0.15;                              // local y below which the hem ripples
uniform float hem_amp : hint_range(0.0, 0.3) = 0.08;      // m of sideways ripple at the bottom
uniform float hem_speed : hint_range(0.0, 10.0) = 3.0;
group_uniforms;

const float HEM_WAVES = 6.0;  // ripples around the hem

void vertex() {
	// ripple strength grows toward the bottom of the hem
	float below = clamp((hem_y - VERTEX.y) / 0.5, 0.0, 1.0);
	float ang = atan(VERTEX.z, VERTEX.x);
	float wave = sin(ang * HEM_WAVES + TIME * hem_speed);
	VERTEX.xz += normalize(VERTEX.xz + vec2(1e-4)) * wave * hem_amp * below;
	VERTEX.y += wave * hem_amp * 0.6 * below;
}

void fragment() {
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), rim_power);
	float shimmer = 0.9 + 0.1 * sin(TIME * 2.0 + FRAGCOORD.y * 0.05);
	ALBEDO = mix(body_color, rim_color * 1.6, fres) * shimmer;
	ALPHA = clamp(core_alpha + fres * 0.65, 0.0, 1.0);
}
