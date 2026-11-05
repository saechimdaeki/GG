shader_type sky;
// Compress the sky gradient into the narrow side-view camera's 18-degree band.
uniform vec4 top_color : source_color = vec4(0.18, 0.53, 0.92, 1.0);
uniform vec4 horizon_color : source_color = vec4(0.82, 0.91, 1.0, 1.0);
void sky() {
	float height = smoothstep(-0.14, 0.16, EYEDIR.y);
	COLOR = mix(horizon_color.rgb, top_color.rgb, height);
}
