extends Control
## Truck radio screen, full screen: the house (left) and today's basement maze (right), drawn
## from synced positions — movers (ring = upstairs, X = dead), the ghost, monsters, furniture
## still in play, doors and the truck. Shown only to a mover in radio mode (Tab at the truck).
## Pure drawing, no game logic.

## House-side area shown: x -7..7, z -9..13 (house x -6..6 z 0..10, truck in front, back yard).
const HOUSE_AREA := Rect2(-7, -9, 14, 22)
const UPSTAIRS_Y := 2.5
const BG := Color("#141a14")
const WALL := Color("#7a9a7a")

var main: Node
## Seconds until the horn can lure again; the host sets it on every peer when the horn sounds.
var lure_ready_in := 0.0


func _process(delta: float) -> void:
	lure_ready_in = maxf(0.0, lure_ready_in - delta)
	if visible:
		queue_redraw()


func _draw() -> void:
	if main == null:
		return
	draw_rect(Rect2(Vector2.ZERO, size), Color(0, 0, 0, 0.8))
	var font := get_theme_default_font()
	var help := "트럭 무전 — ● 1층  ○ 2층  ✕ 사망  · 빨강 유령/몬스터 · 네모 가구 · Tab 닫기 · H 경적(유령을 트럭으로 유인)"
	help += "" if lure_ready_in <= 0.0 else "  [경적 %d초 뒤]" % ceili(lure_ready_in)
	draw_string(font, Vector2(24, 36), help, HORIZONTAL_ALIGNMENT_LEFT, size.x - 48, 20, Color.WHITE)
	var top := 56.0
	var gap := 24.0
	var avail := Rect2(24, top, size.x - 48, size.y - top - 24)
	var bm: Node = main.basement
	var bw: float = bm.maze.cols * bm.maze.cell if not bm.maze.is_empty() else 1.0
	var bd: float = bm.maze.rows * bm.maze.cell if not bm.maze.is_empty() else 1.0
	var b_area := Rect2(bm.ORIGIN.x - 1, bm.ORIGIN.z - 1, bw + 2, bd + 2)
	# Same metres-to-pixels scale on both panels so the basement's real size reads at a glance.
	var s := minf((avail.size.x - gap) / (HOUSE_AREA.size.x + b_area.size.x),
		minf(avail.size.y / HOUSE_AREA.size.y, avail.size.y / b_area.size.y))
	var house_px := Rect2(avail.position, HOUSE_AREA.size * s)
	var base_px := Rect2(avail.position + Vector2(house_px.size.x + gap, 0), b_area.size * s)
	_panel(house_px, "집 (1·2층)")
	_panel(base_px, "지하 창고 — %d일차 배치" % main.run.day)
	_draw_house(house_px, s)
	_draw_basement(base_px, b_area, s)
	for f in main.furniture.get_children():
		if f.in_play:
			_dot_furniture(f, house_px, base_px, b_area, s)
	for m in _monsters():
		_dot_monster(m, house_px, base_px, b_area, s)
	for p in main.players.get_children():
		_dot_player(p, house_px, base_px, b_area, s)


func _panel(r: Rect2, title: String) -> void:
	draw_rect(r, BG)
	draw_rect(r, Color(WALL, 0.4), false, 1.0)
	draw_string(get_theme_default_font(), r.position + Vector2(8, 22), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color(WALL, 0.9))


func _draw_house(px: Rect2, s: float) -> void:
	_wire(Rect2(-6, 0, 12, 10), px, HOUSE_AREA, s, 3.0)
	_line(Vector3(-6, 0, 5.5), Vector3(-3.6, 0, 5.5), px, HOUSE_AREA, s)   # inner wall with its door
	_line(Vector3(-2.4, 0, 5.5), Vector3(6, 0, 5.5), px, HOUSE_AREA, s)
	draw_rect(_rect(Rect2(-0.8, -0.15, 1.6, 0.3), px, HOUSE_AREA, s), BG)   # front door gap
	var bed: AABB = main.truck.bed
	draw_rect(_rect(Rect2(bed.position.x, bed.position.z, bed.size.x, bed.size.z), px, HOUSE_AREA, s), Color("#e07a2e", 0.5))
	_text(_pt(bed.get_center(), px, HOUSE_AREA, s), "트럭", Color("#ffb070"))
	for dd in main.basement.doors:
		if not main.basement.contains(dd.at):
			_door(dd, px, HOUSE_AREA, s)


func _draw_basement(px: Rect2, area: Rect2, s: float) -> void:
	var bm: Node = main.basement
	if bm.maze.is_empty():
		return
	var m: Dictionary = bm.maze
	var c: float = m.cell
	var o: Vector3 = bm.ORIGIN
	_wire(Rect2(o.x, o.z, m.cols * c, m.rows * c), px, area, s, 3.0)
	for x in m.cols:
		for y in m.rows:
			var here := Vector2i(x, y)
			if x + 1 < m.cols and not m.open.has(bm.edge_key(here, here + Vector2i.RIGHT)):
				_line(o + Vector3((x + 1) * c, 0, y * c), o + Vector3((x + 1) * c, 0, (y + 1) * c), px, area, s)
			if y + 1 < m.rows and not m.open.has(bm.edge_key(here, here + Vector2i.DOWN)):
				_line(o + Vector3(x * c, 0, (y + 1) * c), o + Vector3((x + 1) * c, 0, (y + 1) * c), px, area, s)
	for dd in bm.doors:
		if bm.contains(dd.at):
			_door(dd, px, area, s)


func _door(dd: Dictionary, px: Rect2, area: Rect2, s: float) -> void:
	var at := _pt(dd.at, px, area, s)
	var fire: bool = dd.label.begins_with("비상구")
	draw_rect(Rect2(at - Vector2(6, 6), Vector2(12, 12)), Color("#ff6a50") if fire else Color("#ffe0b0"))
	_text(at + Vector2(8, 16), dd.label, Color("#ff8a70") if fire else Color("#ffe0b0"))


func _dot_furniture(f: Node, hp: Rect2, bp: Rect2, area: Rect2, s: float) -> void:
	var at := _where(f.global_position, hp, bp, area, s)
	draw_rect(Rect2(at - Vector2(5, 5), Vector2(10, 10)), Color("#b0a890"))


func _dot_monster(m: Node3D, hp: Rect2, bp: Rect2, area: Rect2, s: float) -> void:
	var at := _where(m.global_position, hp, bp, area, s)
	draw_circle(at, 10.0, Color("#ff4040"))
	_text(at + Vector2(12, 5), m.get_meta("label", "유령"), Color("#ff8080"))


func _dot_player(p: Node, hp: Rect2, bp: Rect2, area: Rect2, s: float) -> void:
	var at := _where(p.global_position, hp, bp, area, s)
	var col := Color.from_hsv(fmod(p.name.to_int() * 0.37, 1.0), 0.6, 0.95)
	if p.dead:
		draw_line(at - Vector2(8, 8), at + Vector2(8, 8), col, 3.0)
		draw_line(at + Vector2(-8, 8), at + Vector2(8, -8), col, 3.0)
	elif not main.basement.contains(p.global_position) and p.global_position.y > UPSTAIRS_Y:
		draw_arc(at, 8.0, 0.0, TAU, 20, col, 3.0)
	else:
		draw_circle(at, 8.0, col)
	_text(at + Vector2(11, -6), "나" if p.name.to_int() == main.multiplayer.get_unique_id() else "동료", col)


## Ghost plus every monster that is out (monsters put themselves in the "monsters" group).
func _monsters() -> Array:
	var out: Array = get_tree().get_nodes_in_group("monsters").filter(func(m: Node) -> bool: return m.visible)
	if main.ghost.visible:
		out.append(main.ghost)
	return out


## A world point on whichever panel it belongs to.
func _where(p: Vector3, hp: Rect2, bp: Rect2, area: Rect2, s: float) -> Vector2:
	return _pt(p, bp, area, s) if main.basement.contains(p) else _pt(p, hp, HOUSE_AREA, s)


## World (x, z) → panel pixels. +z (deeper in) is up on screen.
func _pt(p: Vector3, px: Rect2, area: Rect2, s: float) -> Vector2:
	return Vector2(px.position.x + (p.x - area.position.x) * s, px.end.y - (p.z - area.position.y) * s)


func _rect(r: Rect2, px: Rect2, area: Rect2, s: float) -> Rect2:
	var a := _pt(Vector3(r.position.x, 0, r.end.y), px, area, s)
	var b := _pt(Vector3(r.end.x, 0, r.position.y), px, area, s)
	return Rect2(a, b - a)


func _wire(r: Rect2, px: Rect2, area: Rect2, s: float, w: float) -> void:
	draw_rect(_rect(r, px, area, s), WALL, false, w)


func _line(a: Vector3, b: Vector3, px: Rect2, area: Rect2, s: float) -> void:
	draw_line(_pt(a, px, area, s), _pt(b, px, area, s), WALL, 3.0)


func _text(at: Vector2, t: String, col: Color) -> void:
	draw_string(get_theme_default_font(), at, t, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, col)
