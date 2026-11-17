extends Control
## Truck radio screen: a top-down map of the house drawn from synced positions — movers
## (ring = upstairs, X = dead), the ghost, furniture still in play, and the truck bed.
## Shown only to a mover in radio mode (Tab at the truck). Pure drawing, no game logic.

## World area shown: x -7..7, z -9..11 (house x -6..6 z 0..10, truck in front at z < 0).
const WORLD := Rect2(-7, -9, 14, 20)
const UPSTAIRS_Y := 2.5

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
	var box := _map_rect()
	draw_rect(Rect2(Vector2.ZERO, size), Color(0, 0, 0, 0.55))
	draw_rect(box, Color("#141a14"))
	draw_rect(_to_map_rect(Rect2(-6, 0, 12, 10), box), Color("#3c4a3c"), false, 3.0)  # house walls
	draw_rect(_to_map_rect(Rect2(-0.8, -0.1, 1.6, 0.2), box), Color("#141a14"))      # front door gap
	var bed: AABB = main.truck.bed
	draw_rect(_to_map_rect(Rect2(bed.position.x, bed.position.z, bed.size.x, bed.size.z), box), Color("#e07a2e", 0.5))
	for f in main.furniture.get_children():
		if f.in_play:
			draw_rect(Rect2(_to_map(f.global_position, box) - Vector2(5, 5), Vector2(10, 10)), Color("#b0a890"))
	if main.ghost.visible:
		var g := _to_map(main.ghost.global_position, box)
		draw_circle(g, 11.0, Color("#ff4040"))
		draw_string(get_theme_default_font(), g + Vector2(14, 6), "유령" + (" (2층)" if main.ghost.global_position.y > UPSTAIRS_Y + 1.0 else ""),
			HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color("#ff8080"))
	for p in main.players.get_children():
		var at := _to_map(p.global_position, box)
		var col := Color.from_hsv(fmod(p.name.to_int() * 0.37, 1.0), 0.6, 0.95)
		if p.dead:
			draw_line(at - Vector2(8, 8), at + Vector2(8, 8), col, 3.0)
			draw_line(at + Vector2(-8, 8), at + Vector2(8, -8), col, 3.0)
		elif p.global_position.y > UPSTAIRS_Y:
			draw_arc(at, 8.0, 0.0, TAU, 20, col, 3.0)
		else:
			draw_circle(at, 8.0, col)
		var label := "나" if p.name.to_int() == main.multiplayer.get_unique_id() else "동료"
		draw_string(get_theme_default_font(), at + Vector2(11, -6), label, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, col)
	var help := "트럭 무전 — 지도: ● 1층 ○ 2층 ✕ 사망 · 빨강 유령 · Tab 닫기 · H 경적(유령을 트럭으로 유인)"
	help += "" if lure_ready_in <= 0.0 else "  [%d초 뒤 다시]" % ceili(lure_ready_in)
	draw_string(get_theme_default_font(), Vector2(box.position.x, box.position.y - 12), help,
		HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color.WHITE)


## Largest WORLD-shaped rect centred in this control, with a margin for the help line.
func _map_rect() -> Rect2:
	var avail := size - Vector2(40, 90)
	var s := minf(avail.x / WORLD.size.x, avail.y / WORLD.size.y)
	var dim := WORLD.size * s
	return Rect2((size - dim) / 2.0 + Vector2(0, 15), dim)


## World (x, z) → map pixels. +z (deeper into the house) is up on screen.
func _to_map(world: Vector3, box: Rect2) -> Vector2:
	var u := (world.x - WORLD.position.x) / WORLD.size.x
	var v := 1.0 - (world.z - WORLD.position.y) / WORLD.size.y
	return box.position + Vector2(u, v) * box.size


func _to_map_rect(r: Rect2, box: Rect2) -> Rect2:
	var a := _to_map(Vector3(r.position.x, 0, r.end.y), box)
	var b := _to_map(Vector3(r.end.x, 0, r.position.y), box)
	return Rect2(a, b - a)
