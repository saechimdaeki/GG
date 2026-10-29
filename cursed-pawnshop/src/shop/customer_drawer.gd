class_name CustomerDrawer
## Draws a monster behind the counter. Uses its portrait sprite when one exists
## (tools/pixelate.py output); otherwise a code-drawn stand-in. Either way the mood
## shows on top: sweat, red flush, anger vein, shaking.

const OUTLINE := Color(0.06, 0.03, 0.05)
const PORTRAIT_PATH := "res://assets/sprites/portraits/%s.png"
## Portrait box size (pixels) and where its top sits relative to the head point.
const PORTRAIT_SIZE := 150.0
const PORTRAIT_TOP := -70.0

## Eye layout per species: [offset, radius]. Empty = no eyes.
const EYES := {
	"deep_one": [[Vector2(-20, -6), 8.0], [Vector2(20, -6), 8.0]],
	"faceless": [],
	"tentacle": [[Vector2(-11, -8), 5.0], [Vector2(11, -8), 5.0]],
	"eyes": [[Vector2(-14, -10), 7.0], [Vector2(12, -14), 5.0], [Vector2(0, 2), 9.0], [Vector2(-20, 8), 4.0],
		[Vector2(20, 4), 6.0], [Vector2(-6, -24), 4.0], [Vector2(14, 18), 4.0]],
	"ghoul": [[Vector2(-11, -6), 5.0], [Vector2(11, -6), 5.0]],
	"drowned": [[Vector2(-11, -4), 6.0], [Vector2(11, -4), 6.0]],
	"spider": [[Vector2(-9, -6), 4.5], [Vector2(9, -6), 4.5], [Vector2(-17, -12), 2.5], [Vector2(17, -12), 2.5],
		[Vector2(-5, -15), 2.0], [Vector2(5, -15), 2.0]],
	"goblin": [[Vector2(-12, -4), 6.0], [Vector2(12, -4), 6.0]],
}

static var _portraits := {}


static func _portrait(species: String) -> Texture2D:
	if not _portraits.has(species):
		var path := PORTRAIT_PATH % species
		_portraits[species] = load(path) if ResourceLoader.exists(path) else null
	return _portraits[species]


## [param at] is the head center. [param t] drives idle sway, sweat and shaking.
static func draw(canvas: CanvasItem, v: Dictionary, at: Vector2, t: float) -> void:
	var m: Dictionary = v["member"]
	var sp: String = m["species"]
	var mood: String = v["mood"]
	var shake := Vector2(sin(t * 60.0) * 1.5, 0) if mood == "angry" else Vector2.ZERO
	var head := at + Vector2(0, sin(t * 1.8) * 1.5) + shake

	var tex := _portrait(sp)
	if tex:
		var tint := Color(1, 0.75, 0.75) if mood == "angry" else Color.WHITE
		# 2x pixel scale, bottom on the counter, centered on the head point
		var size := tex.get_size() * (PORTRAIT_SIZE / tex.get_size().y)
		canvas.draw_texture_rect(tex, Rect2(head + Vector2(-size.x / 2, PORTRAIT_TOP), size), false, tint)
	else:
		_draw_body(canvas, m, at + shake)
		_draw_head(canvas, sp, head, mood, t)
	_draw_mood_fx(canvas, m, head, mood, t)


static func _draw_body(canvas: CanvasItem, m: Dictionary, at: Vector2) -> void:
	var cl: Color = m["clothes"]
	_poly(canvas, [at + Vector2(-36, 26), at + Vector2(36, 26), at + Vector2(58, 98), at + Vector2(-58, 98)], cl)
	_poly(canvas, [at + Vector2(-36, 26), at + Vector2(-4, 26), at + Vector2(-12, 98), at + Vector2(-58, 98)], cl.lightened(0.08))
	canvas.draw_line(at + Vector2(0, 30), at + Vector2(0, 98), cl.darkened(0.4), 2)
	# a hanging amulet on a cord
	canvas.draw_line(at + Vector2(-12, 28), at + Vector2(0, 52), Color(0.3, 0.25, 0.2), 1)
	canvas.draw_line(at + Vector2(12, 28), at + Vector2(0, 52), Color(0.3, 0.25, 0.2), 1)
	canvas.draw_circle(at + Vector2(0, 54), 4, Color(0.2, 0.55, 0.5))


static func _draw_head(canvas: CanvasItem, sp: String, head: Vector2, mood: String, t: float) -> void:
	var skin: Color = CustomerData.SPECIES[sp]["skin"]
	match sp:
		"faceless":
			canvas.draw_circle(head + Vector2(0, -4), 40, OUTLINE)
			canvas.draw_circle(head + Vector2(0, -4), 38, Color(0.14, 0.1, 0.12))
			_ellipse(canvas, head + Vector2(0, 4), Vector2(20, 26), skin)
			_ellipse(canvas, head + Vector2(-4, 0), Vector2(14, 20), skin.lightened(0.1))
		"eyes":
			for i in 6:
				var a := TAU * i / 6.0 + sin(t + i) * 0.1
				canvas.draw_circle(head + Vector2.from_angle(a) * 30, 14, OUTLINE)
				canvas.draw_circle(head + Vector2.from_angle(a) * 30, 12, skin.darkened(0.1))
			canvas.draw_circle(head, 34, OUTLINE)
			canvas.draw_circle(head, 32, skin)
		"spider":
			canvas.draw_circle(head + Vector2(0, -6), 40, Color(0.06, 0.04, 0.06))
			for i in 4:
				var y := -10.0 + i * 12.0
				canvas.draw_polyline(PackedVector2Array([head + Vector2(-34, y), head + Vector2(-58, y - 16), head + Vector2(-66, y + 14)]), OUTLINE, 3)
				canvas.draw_polyline(PackedVector2Array([head + Vector2(34, y), head + Vector2(58, y - 16), head + Vector2(66, y + 14)]), OUTLINE, 3)
			_ellipse(canvas, head, Vector2(26, 31), OUTLINE)
			_ellipse(canvas, head, Vector2(24, 29), skin)
		_:
			if sp == "deep_one":
				_poly(canvas, [head + Vector2(-8, -30), head + Vector2(0, -52), head + Vector2(8, -30)], skin.darkened(0.2))
				_poly(canvas, [head + Vector2(-20, -24), head + Vector2(-22, -42), head + Vector2(-8, -30)], skin.darkened(0.2))
				_poly(canvas, [head + Vector2(20, -24), head + Vector2(22, -42), head + Vector2(8, -30)], skin.darkened(0.2))
			if sp == "goblin":
				_poly(canvas, [head + Vector2(24, -8), head + Vector2(58, -24), head + Vector2(28, 10)], skin)
				_poly(canvas, [head + Vector2(-24, -8), head + Vector2(-58, -24), head + Vector2(-28, 10)], skin)
			var size := Vector2(36, 30) if sp == "deep_one" else Vector2(30, 33)
			_ellipse(canvas, head, size + Vector2(2, 2), OUTLINE)
			_ellipse(canvas, head, size, skin.darkened(0.12))
			_ellipse(canvas, head + Vector2(-3, -3), size - Vector2(4, 4), skin)
			canvas.draw_circle(head + Vector2(-13, -16), 4, skin.lightened(0.3))

	match sp:
		"deep_one":
			for i in 3:
				canvas.draw_arc(head + Vector2(-26, 4 + i * 6), 6, -0.6, 0.6, 6, OUTLINE, 1.5)
				canvas.draw_arc(head + Vector2(26, 4 + i * 6), 6, PI - 0.6, PI + 0.6, 6, OUTLINE, 1.5)
		"tentacle":
			for i in 4:
				var x := -12.0 + i * 8.0
				var pts := PackedVector2Array()
				for s in 8:
					pts.append(head + Vector2(x + sin(t * 2.0 + s * 0.6 + i) * (2.0 + s), 10 + s * 6))
				canvas.draw_polyline(pts, OUTLINE, 6)
				canvas.draw_polyline(pts, skin.darkened(0.1), 4)
		"ghoul":
			_poly(canvas, [head + Vector2(-16, 12), head + Vector2(16, 12), head + Vector2(10, 34), head + Vector2(-10, 34)], skin.darkened(0.1))
		"drowned":
			for i in 7:
				var x := -26.0 + i * 8.5
				var pts := PackedVector2Array([head + Vector2(x, -28), head + Vector2(x - 3, -6), head + Vector2(x + sin(t * 1.5 + i) * 3, 24 + (i % 3) * 8)])
				canvas.draw_polyline(pts, Color(0.18, 0.32, 0.2), 3)

	_draw_eyes(canvas, sp, head, mood, t)
	_draw_mouth(canvas, sp, head + Vector2(0, 14), mood)


static func _draw_eyes(canvas: CanvasItem, sp: String, head: Vector2, mood: String, t: float) -> void:
	var look := Vector2(3, 1) if mood == "nervous" else Vector2.ZERO
	if sp == "eyes":
		look = Vector2(sin(t * 1.3), cos(t * 0.9)) * 2.0 + look
	for e in EYES.get(sp, EYES["goblin"]):
		var at: Vector2 = head + (e[0] as Vector2)
		var r: float = e[1]
		match sp:
			"ghoul":
				canvas.draw_circle(at, r + 2, Color(0.05, 0.03, 0.03))
				canvas.draw_circle(at + look * 0.5, 1.5, Color(1, 0.85, 0.2))
			"spider":
				canvas.draw_circle(at, r, Color(0.7, 0.05, 0.1))
				canvas.draw_circle(at - Vector2(r * 0.3, r * 0.3), r * 0.3, Color(1, 0.6, 0.6))
			"tentacle":
				canvas.draw_circle(at, r, Color(0.85, 0.95, 0.75))
			"drowned":
				canvas.draw_circle(at, r, Color(0.88, 0.92, 0.9))
				canvas.draw_circle(at + look, r * 0.25, Color(0.5, 0.6, 0.62))
			_:
				if mood == "happy" and sp == "goblin":
					canvas.draw_arc(at + Vector2(0, 2), r, PI, TAU, 8, OUTLINE, 2.5)
					continue
				canvas.draw_circle(at, r + 1, OUTLINE)
				canvas.draw_circle(at, r, Color(0.95, 0.9, 0.75))
				canvas.draw_circle(at + look, r * 0.45, Color(0.1, 0.05, 0.05))
	if sp in ["deep_one", "goblin", "drowned", "ghoul"]:
		var le := head + Vector2(-12, -4)
		var re := head + Vector2(12, -4)
		match mood:
			"annoyed", "angry":
				canvas.draw_line(le + Vector2(-8, -11), le + Vector2(6, -7), OUTLINE, 3)
				canvas.draw_line(re + Vector2(8, -11), re + Vector2(-6, -7), OUTLINE, 3)
			"sad":
				canvas.draw_line(le + Vector2(-8, -7), le + Vector2(6, -11), OUTLINE, 2.5)
				canvas.draw_line(re + Vector2(8, -7), re + Vector2(-6, -11), OUTLINE, 2.5)


static func _draw_mouth(canvas: CanvasItem, sp: String, mouth: Vector2, mood: String) -> void:
	if sp == "tentacle":
		return
	if sp == "deep_one":
		mouth += Vector2(0, 2)
		canvas.draw_line(mouth + Vector2(-16, 0), mouth + Vector2(16, 0), OUTLINE, 3)
	match mood:
		"happy":
			canvas.draw_arc(mouth + Vector2(0, -4), 9, 0.2, PI - 0.2, 10, OUTLINE, 2.5)
		"angry":
			_ellipse(canvas, mouth + Vector2(0, 2), Vector2(9, 6), Color(0.15, 0.02, 0.04))
			for i in 4:
				_poly(canvas, [mouth + Vector2(-7 + i * 4.5, -3), mouth + Vector2(-5 + i * 4.5, 2), mouth + Vector2(-3 + i * 4.5, -3)], Color(0.95, 0.92, 0.85))
		"sad":
			canvas.draw_arc(mouth + Vector2(0, 6), 8, PI + 0.4, TAU - 0.4, 10, OUTLINE, 2.5)
		"nervous":
			canvas.draw_polyline(PackedVector2Array([mouth + Vector2(-8, 0), mouth + Vector2(-3, -2), mouth + Vector2(2, 1), mouth + Vector2(8, -1)]), OUTLINE, 2)
		_:
			if sp != "deep_one":
				canvas.draw_line(mouth + Vector2(-8, 0), mouth + Vector2(8, 0), OUTLINE, 2.5)
	if sp in ["ghoul", "spider"]:
		_poly(canvas, [mouth + Vector2(-6, 0), mouth + Vector2(-4, 8), mouth + Vector2(-2, 0)], Color(0.95, 0.92, 0.85))
		_poly(canvas, [mouth + Vector2(6, 0), mouth + Vector2(4, 8), mouth + Vector2(2, 0)], Color(0.95, 0.92, 0.85))


static func _draw_mood_fx(canvas: CanvasItem, m: Dictionary, head: Vector2, mood: String, t: float) -> void:
	if mood == "angry":
		canvas.draw_circle(head, 34, Color(1, 0, 0, 0.15))
		var vein := head + Vector2(-24, -26)
		canvas.draw_line(vein + Vector2(-5, -5), vein + Vector2(5, 5), Color(0.9, 0.1, 0.1), 2)
		canvas.draw_line(vein + Vector2(5, -5), vein + Vector2(-5, 5), Color(0.9, 0.1, 0.1), 2)
	var sweaty: bool = CustomerData.PERSONALITIES[m["personality"]]["sweaty"]
	if mood == "nervous" or (sweaty and mood != "happy") or m["species"] == "drowned":
		var drop := head + Vector2(32, -20 + fmod(t * 14.0, 18.0))
		_poly(canvas, [drop + Vector2(0, -7), drop + Vector2(4, 0), drop + Vector2(-4, 0)], Color(0.6, 0.85, 1.0))
		canvas.draw_circle(drop + Vector2(0, 1), 4, Color(0.6, 0.85, 1.0))
	if mood == "happy":
		canvas.draw_circle(head + Vector2(-18, 8), 5, Color(1, 0.4, 0.5, 0.35))
		canvas.draw_circle(head + Vector2(18, 8), 5, Color(1, 0.4, 0.5, 0.35))


static func _ellipse(canvas: CanvasItem, at: Vector2, radii: Vector2, color: Color) -> void:
	var pts := PackedVector2Array()
	for i in 24:
		pts.append(at + Vector2(cos(TAU * i / 24.0) * radii.x, sin(TAU * i / 24.0) * radii.y))
	canvas.draw_colored_polygon(pts, color)


static func _poly(canvas: CanvasItem, pts: Array, fill: Color) -> void:
	var out := PackedVector2Array(pts)
	canvas.draw_colored_polygon(out, fill)
	out.append(out[0])
	canvas.draw_polyline(out, OUTLINE, 2.0)
