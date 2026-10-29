class_name ItemDrawer
## Draws a pawn item (see ItemGenerator.generate) onto any CanvasItem.
## Plain view = shaded silhouette in the plated color. Detail view (the loupe)
## adds the mark, the scratches and any bare seam, which is where the clues live.

## Mark size per kind, in unit coordinates.
const MARK_SIZE := {"medal": 0.16, "goblet": 0.13, "dagger": 0.09, "idol": 0.12, "crown": 0.11}
const SEAM_RADIUS := 0.07
## Optional sprite per kind (from tools/pixelate.py). Drawn tinted by the plated
## color in place of the shapes below; marks and seams still go on top.
const SPRITE_PATH := "res://assets/sprites/items/%s.png"

static var _sprites := {}


static func _sprite(kind: String) -> Texture2D:
	if not _sprites.has(kind):
		var path := SPRITE_PATH % kind
		_sprites[kind] = load(path) if ResourceLoader.exists(path) else null
	return _sprites[kind]


## [param unit] is the pixel size of 1.0 in the item's unit coordinates.
static func draw(canvas: CanvasItem, item: Dictionary, center: Vector2, unit: float, detail: bool) -> void:
	# A fake is plated to look like the claimed material; only a hint of the real metal shows.
	var claimed_col: Color = ItemData.MATERIALS[item["claimed_material"]]["color"]
	var true_col: Color = ItemData.MATERIALS[item["true_material"]]["color"]
	var col := claimed_col.lerp(true_col, ItemData.PLATING_SHOW_THROUGH)
	var edge := col.darkened(0.6)
	var lw := maxf(1.0, unit * 0.035)
	var tex := _sprite(item["kind"])
	if tex:
		# fit inside the 2x2 unit box, keeping the sprite's aspect
		var s := tex.get_size()
		var size := s * (unit * 2.0 / maxf(s.x, s.y))
		canvas.draw_texture_rect(tex, Rect2(center - size * 0.5, size), false, col.lightened(0.15))
	else:
		_draw_shape(canvas, item["kind"], center, unit, col, edge, lw)
	if not detail:
		return
	_draw_details(canvas, item, center, unit, col, true_col)


static func _draw_shape(canvas: CanvasItem, kind: String, center: Vector2, unit: float, col: Color, edge: Color, lw: float) -> void:
	var eye_white := Color(0.92, 0.88, 0.8)
	match kind:
		"medal":
			canvas.draw_circle(center + Vector2(0.05, 0.07) * unit, unit * 0.86, Color(0, 0, 0, 0.35))
			canvas.draw_circle(center, unit * 0.85, edge)
			canvas.draw_circle(center, unit * 0.8, col.darkened(0.15))
			canvas.draw_circle(center - Vector2(0.04, 0.04) * unit, unit * 0.72, col)
			canvas.draw_arc(center, unit * 0.62, 0.0, TAU, 48, col.darkened(0.3), lw)
			canvas.draw_arc(center, unit * 0.74, PI * 1.05, PI * 1.6, 16, col.lightened(0.45), lw * 1.4)
			# engraved tentacles curling toward the middle
			for i in 5:
				var a := TAU * i / 5.0
				var pts := PackedVector2Array()
				for s in 6:
					var r := 0.6 - s * 0.09
					pts.append(center + Vector2.from_angle(a + s * 0.35) * unit * r)
				canvas.draw_polyline(pts, col.darkened(0.25), lw * 0.8)
		"goblet":
			var bowl := [Vector2(-0.8, -0.9), Vector2(0.8, -0.9), Vector2(0.6, -0.1), Vector2(0.2, 0.15), Vector2(-0.2, 0.15), Vector2(-0.6, -0.1)]
			_poly(canvas, center, unit, bowl, col, edge, lw)
			_poly(canvas, center, unit, [Vector2(0.2, -0.88), Vector2(0.78, -0.88), Vector2(0.58, -0.12), Vector2(0.2, 0.13)], col.darkened(0.18), Color.TRANSPARENT, 0.0)
			_poly(canvas, center, unit, [Vector2(-0.8, -0.9), Vector2(0.8, -0.9), Vector2(0.76, -0.8), Vector2(-0.76, -0.8)], col.darkened(0.35), edge, lw)
			_poly(canvas, center, unit, [Vector2(-0.12, 0.1), Vector2(0.12, 0.1), Vector2(0.1, 0.65), Vector2(-0.1, 0.65)], col.darkened(0.1), edge, lw)
			_poly(canvas, center, unit, [Vector2(-0.5, 0.65), Vector2(0.5, 0.65), Vector2(0.6, 0.9), Vector2(-0.6, 0.9)], col, edge, lw)
			canvas.draw_line(center + Vector2(-0.6, -0.72) * unit, center + Vector2(-0.45, -0.2) * unit, col.lightened(0.5), lw * 1.5)
			# the eye set in the stem
			canvas.draw_circle(center + Vector2(0, 0.38) * unit, unit * 0.13, edge)
			canvas.draw_circle(center + Vector2(0, 0.38) * unit, unit * 0.11, eye_white)
			canvas.draw_circle(center + Vector2(0.03, 0.38) * unit, unit * 0.05, Color(0.6, 0.1, 0.1))
		"dagger":
			_poly(canvas, center, unit, [Vector2(0.0, -1.0), Vector2(0.28, -0.6), Vector2(0.22, 0.35), Vector2(-0.22, 0.35), Vector2(-0.28, -0.6)], col, edge, lw)
			_poly(canvas, center, unit, [Vector2(0.0, -1.0), Vector2(0.28, -0.6), Vector2(0.22, 0.35), Vector2(0.0, 0.35)], col.darkened(0.15), Color.TRANSPARENT, 0.0)
			canvas.draw_line(center + Vector2(0.0, -0.9) * unit, center + Vector2(0.0, 0.3) * unit, col.lightened(0.4), lw)
			_poly(canvas, center, unit, [Vector2(-0.55, 0.35), Vector2(0.55, 0.35), Vector2(0.55, 0.5), Vector2(-0.55, 0.5)], col.darkened(0.25), edge, lw)
			_poly(canvas, center, unit, [Vector2(-0.1, 0.5), Vector2(0.1, 0.5), Vector2(0.1, 0.95), Vector2(-0.1, 0.95)], Color(0.3, 0.18, 0.12), edge, lw)
			canvas.draw_circle(center + Vector2(0, 0.42) * unit, unit * 0.06, Color(0.75, 0.15, 0.2))
			canvas.draw_line(center + Vector2(-0.12, -0.3) * unit, center + Vector2(0.1, -0.05) * unit, Color(0.35, 0.05, 0.05, 0.7), lw * 1.5)
		"idol":
			# squatting thing with a bulbous head and a beard of tentacles
			_poly(canvas, center, unit, [Vector2(-0.55, 0.95), Vector2(0.55, 0.95), Vector2(0.45, 0.75), Vector2(-0.45, 0.75)], col.darkened(0.3), edge, lw)
			_poly(canvas, center, unit, [Vector2(-0.45, 0.78), Vector2(-0.5, 0.2), Vector2(-0.3, -0.1), Vector2(0.3, -0.1), Vector2(0.5, 0.2), Vector2(0.45, 0.78)], col, edge, lw)
			canvas.draw_circle(center + Vector2(0, -0.45) * unit, unit * 0.45, edge)
			canvas.draw_circle(center + Vector2(0, -0.45) * unit, unit * 0.42, col)
			canvas.draw_circle(center + Vector2(-0.12, -0.58) * unit, unit * 0.12, col.lightened(0.3))
			for i in 5:
				var x := -0.3 + i * 0.15
				var pts := PackedVector2Array()
				for s in 7:
					pts.append(center + Vector2(x + sin(s * 0.9 + i) * 0.06, -0.2 + s * 0.11) * unit)
				canvas.draw_polyline(pts, col.darkened(0.35), lw * 1.6)
			canvas.draw_circle(center + Vector2(-0.16, -0.48) * unit, unit * 0.07, Color(0.05, 0.02, 0.04))
			canvas.draw_circle(center + Vector2(0.16, -0.48) * unit, unit * 0.07, Color(0.05, 0.02, 0.04))
			# folded wings
			_poly(canvas, center, unit, [Vector2(0.5, 0.15), Vector2(0.9, -0.3), Vector2(0.75, 0.5)], col.darkened(0.2), edge, lw)
			_poly(canvas, center, unit, [Vector2(-0.5, 0.15), Vector2(-0.9, -0.3), Vector2(-0.75, 0.5)], col.darkened(0.2), edge, lw)
		"crown":
			var pts := [Vector2(-0.85, 0.45), Vector2(-0.85, -0.2), Vector2(-0.6, 0.05), Vector2(-0.4, -0.55), Vector2(-0.2, 0.0),
				Vector2(0.0, -0.75), Vector2(0.2, 0.0), Vector2(0.4, -0.55), Vector2(0.6, 0.05), Vector2(0.85, -0.2), Vector2(0.85, 0.45)]
			_poly(canvas, center, unit, pts, col, edge, lw)
			_poly(canvas, center, unit, [Vector2(-0.85, 0.3), Vector2(0.85, 0.3), Vector2(0.85, 0.45), Vector2(-0.85, 0.45)], col.darkened(0.3), edge, lw)
			for x in [-0.4, 0.0, 0.4]:
				canvas.draw_circle(center + Vector2(x, 0.37) * unit, unit * 0.06, Color(0.15, 0.55, 0.5))
			# barnacles
			for p in [Vector2(-0.6, 0.15), Vector2(0.25, -0.15), Vector2(0.65, 0.2)]:
				canvas.draw_circle(center + p * unit, unit * 0.05, col.darkened(0.45))


static func _draw_details(canvas: CanvasItem, item: Dictionary, center: Vector2, unit: float, col: Color, true_col: Color) -> void:
	for f in item["flaws"]:
		var p: Vector2 = center + (f["pos"] as Vector2) * unit * 0.8
		var d := Vector2.from_angle(f["angle"]) * unit * float(f["len"])
		canvas.draw_line(p - d, p + d, col.darkened(0.45), maxf(1.0, unit * 0.012))
	var seam: Vector2 = item["seam_pos"]
	if seam != Vector2.INF:
		var at := center + seam * unit
		var r := unit * SEAM_RADIUS
		canvas.draw_circle(at, r, true_col.darkened(0.1))
		canvas.draw_circle(at + Vector2(r * 0.7, r * 0.3), r * 0.6, true_col.darkened(0.1))
		canvas.draw_arc(at, r, 0.0, TAU, 16, col.darkened(0.5), maxf(1.0, unit * 0.01))
	if item["mark"] != "":
		var at: Vector2 = center + (item["mark_pos"] as Vector2) * unit
		var size: float = MARK_SIZE[item["kind"]] * unit
		var ink := col.darkened(0.55)
		if item["mark_blurred"]:
			_glyph(canvas, item["mark"], at + Vector2(size * 0.1, size * 0.07), size, ink, 0.55)
			_glyph(canvas, item["mark"], at - Vector2(size * 0.1, size * 0.06), size * 1.05, ink, 0.55)
		else:
			_glyph(canvas, item["mark"], at, size, ink, 1.0)


## Draws a mark on its own (used for the reference strip).
static func draw_mark(canvas: CanvasItem, mark: String, at: Vector2, size: float, color: Color) -> void:
	_glyph(canvas, mark, at, size, color, 1.0)


static func _poly(canvas: CanvasItem, center: Vector2, unit: float, pts: Array, fill: Color, edge: Color, lw: float) -> void:
	var out := PackedVector2Array()
	for p in pts:
		out.append(center + (p as Vector2) * unit)
	canvas.draw_colored_polygon(out, fill)
	if lw > 0.0:
		out.append(out[0])
		canvas.draw_polyline(out, edge, lw)


static func _glyph(canvas: CanvasItem, mark: String, at: Vector2, size: float, color: Color, alpha: float) -> void:
	var c := Color(color.r, color.g, color.b, alpha)
	var w := maxf(1.0, size * 0.14)
	match mark:
		"sun":
			canvas.draw_arc(at, size * 0.4, 0.0, TAU, 24, c, w)
			for i in 8:
				var a := TAU * i / 8.0
				canvas.draw_line(at + Vector2.from_angle(a) * size * 0.55, at + Vector2.from_angle(a) * size * 0.9, c, w)
		"moon":
			canvas.draw_arc(at, size * 0.7, deg_to_rad(70), deg_to_rad(290), 24, c, w)
			canvas.draw_arc(at + Vector2(size * 0.3, 0), size * 0.55, deg_to_rad(110), deg_to_rad(250), 16, c, w)
		"flame":
			var pts := PackedVector2Array([
				at + Vector2(0, -size), at + Vector2(size * 0.45, -size * 0.1), at + Vector2(size * 0.2, size * 0.8),
				at + Vector2(-size * 0.2, size * 0.8), at + Vector2(-size * 0.45, -size * 0.1), at + Vector2(0, -size)])
			canvas.draw_polyline(pts, c, w)
		"eye":
			canvas.draw_arc(at + Vector2(0, size * 0.35), size * 0.8, deg_to_rad(215), deg_to_rad(325), 12, c, w)
			canvas.draw_arc(at - Vector2(0, size * 0.35), size * 0.8, deg_to_rad(35), deg_to_rad(145), 12, c, w)
			canvas.draw_circle(at, size * 0.22, c)
