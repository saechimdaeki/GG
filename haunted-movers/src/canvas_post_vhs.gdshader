shader_type canvas_item;
// canvas_post_vhs — haunted-movers. Purpose: full-screen "cheap camcorder" look over the 3D
// view: chunky low-res pixels, a small dithered palette, colour split, scanlines, warm tint,
// vignette, and static/tearing that main.gd drives as the ghost gets close.
// Target: desktop Forward+, 3 screen samples per pixel, no loops.

group_uniforms pixels;
uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform float pixel_size : hint_range(1.0, 8.0) = 3.0;  // screen pixels per "game" pixel
uniform float color_levels : hint_range(4.0, 64.0) = 20.0;  // steps per channel before dithering
group_uniforms tape;
uniform float split : hint_range(0.0, 0.01) = 0.0015;   // red/blue offset, in UV
uniform float scanline : hint_range(0.0, 0.3) = 0.05;
uniform float noise : hint_range(0.0, 1.0) = 0.0;       // ghost proximity, set by main.gd
group_uniforms;

const float TEAR_ROWS = 4.0;     // screen rows per tearing band
const float TEAR_AMOUNT = 0.02;  // max horizontal tear, in UV

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

// 4x4 ordered (Bayer) dither threshold in 0..1 for a block coordinate
float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0));
	int y = int(mod(p.y, 4.0));
	int idx = x + y * 4;
	const float M[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return M[idx] / 16.0;
}

void fragment() {
	// snap to the centre of a pixel_size block → chunky low-res pixels
	vec2 block = floor(FRAGCOORD.xy / pixel_size);
	vec2 uv = (block * pixel_size + pixel_size * 0.5) * SCREEN_PIXEL_SIZE;
	float row = floor(FRAGCOORD.y / (TEAR_ROWS * pixel_size));
	uv.x += (hash(vec2(row, floor(TIME * 20.0))) - 0.5) * TEAR_AMOUNT * noise;
	vec3 c = vec3(texture(screen, uv + vec2(split, 0.0)).r,
	              texture(screen, uv).g,
	              texture(screen, uv - vec2(split, 0.0)).b);
	// small palette: quantize each channel, dithering the rounding per block
	c = floor(c * color_levels + bayer4(block)) / color_levels;
	c *= 1.0 - scanline * (0.5 + 0.5 * sin(block.y * 3.14159));
	c *= vec3(0.97, 1.0, 1.04);  // cold night tint
	vec2 d = SCREEN_UV - 0.5;
	c *= 1.0 - 0.6 * dot(d, d);  // vignette
	c = mix(c, vec3(hash(block + fract(TIME) * 100.0)), 0.35 * noise);
	COLOR = vec4(c, 1.0);
}
