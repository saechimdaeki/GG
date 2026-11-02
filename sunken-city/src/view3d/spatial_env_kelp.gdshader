shader_type spatial;
// Sunken City — kelp strands (Claude, 2026-11-02). Forward+, desktop; one pass, no
// texture samples. Each strand is a vertical quad strip (UV.y 0 at the root, 1 at the
// tip) that sways more toward the tip; UV2.x is the root's world y. Kelp the water has left behind withers away.
render_mode cull_disabled;

const float SWAY_SPEED = 1.3;
const float SWAY_AMOUNT = 0.35;   // world units at the tip

group_uniforms colors;
uniform vec3 root_color : source_color = vec3(0.12, 0.32, 0.18);
uniform vec3 tip_color : source_color = vec3(0.35, 0.62, 0.3);
group_uniforms water;
uniform float surface_y = 0.0;

varying float root_above;

void vertex() {
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	root_above = step(surface_y, UV2.x);  // UV2.x carries the strand's root height (sea_view.gd)
	float t = UV.y * UV.y;  // the root stays put, the tip swings
	float phase = w.x * 0.7 + w.z * 1.3;
	VERTEX.x += sin(TIME * SWAY_SPEED + phase) * SWAY_AMOUNT * t;
}

void fragment() {
	if (root_above > 0.5) {
		discard;  // a strand whose root is out of the water is gone
	}
	ALBEDO = mix(root_color, tip_color, UV.y);
	ROUGHNESS = 0.8;
}
