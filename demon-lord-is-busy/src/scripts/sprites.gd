class_name Sprites
## Optional textures from res://assets/sprites/<name>.png (made by tools/pixelate.py).
## null means "no sprite yet": callers draw their placeholder shape instead.

static var _cache: Dictionary = {}


static func tex(name: String) -> Texture2D:
	if not _cache.has(name):
		var path := "res://assets/sprites/%s.png" % name
		_cache[name] = load(path) if ResourceLoader.exists(path) else null
	return _cache[name]


## Draws tex standing on foot_y, centered on x, optionally mirrored, with a procedural
## animation: walking = bounce + lean + squash/stretch, idle = breathing, windup = rear back (0..1).
## walk: optional `<name>_walk` strip (square frames side by side); played while moving.
static func draw_on(ci: CanvasItem, t: Texture2D, foot_y: float, flip: bool, tint: Color,
		anim_t: float = 0.0, moving: bool = false, windup: float = 0.0, walk: Texture2D = null) -> void:
	var size := t.get_size()
	var src := Rect2(Vector2.ZERO, size)
	if moving and walk != null:
		size = Vector2(walk.get_height(), walk.get_height())
		var n := int(walk.get_width() / size.x)
		src = Rect2(Vector2(size.x * (int(anim_t * 8.0) % n), 0), size)
		t = walk
	var bob := 0.0
	var lean := 0.0
	var sq := Vector2.ONE
	if moving:
		var step := sin(anim_t * 11.0)
		var k := 0.4 if walk != null else 1.0  # real walk frames need less fake bounce
		bob = absf(step) * 2.0 * k
		lean = step * 0.07 * k
		sq = Vector2(1.0 + 0.05 * absf(step) * k, 1.0 - 0.05 * absf(step) * k)
	else:
		var breath := sin(anim_t * 3.0)
		sq = Vector2(1.0 - 0.015 * breath, 1.0 + 0.03 * breath)
	if windup > 0.0:
		sq *= Vector2(1.0 - 0.1 * windup, 1.0 + 0.14 * windup)
		lean -= 0.12 * windup
	var fx := -1.0 if flip else 1.0
	ci.draw_set_transform(Vector2(0.0, foot_y - bob), lean * fx, Vector2(fx * sq.x, sq.y))
	ci.draw_texture_rect_region(t, Rect2(Vector2(-size.x / 2.0, -size.y), size), src, tint)
	ci.draw_set_transform(Vector2.ZERO)
