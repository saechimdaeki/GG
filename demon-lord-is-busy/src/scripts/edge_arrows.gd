class_name EdgeArrows
extends Control
## Red arrows on the screen edge pointing at heroes outside the view (raid), and at the
## entrances heroes will use (build), so the player always knows where they come from.

var game: Game


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _process(_delta: float) -> void:
	queue_redraw()


func _draw() -> void:
	if game == null:
		return
	var targets: Array[Vector2] = []
	if game.phase == Game.Phase.RAID:
		for h in get_tree().get_nodes_in_group("heroes"):
			targets.append((h as Node2D).global_position)
	elif game.phase == Game.Phase.BUILD:
		for i in game.grid.active_entrances:
			targets.append(game.grid.entrance_position(i))
	var view := get_viewport_rect().size
	var xf := get_viewport().get_canvas_transform()
	var inner := Rect2(Vector2.ZERO, view).grow(-24.0)
	var blink := 0.65 + 0.35 * sin(Time.get_ticks_msec() / 150.0)
	for w in targets:
		var p := xf * w
		if Rect2(Vector2.ZERO, view).has_point(p):
			continue
		var center := view / 2.0
		var dir := (p - center).normalized()
		# Scale the direction so the arrow sits on the inner border.
		var k := minf(absf((inner.size.x / 2.0) / dir.x) if dir.x != 0.0 else INF,
			absf((inner.size.y / 2.0) / dir.y) if dir.y != 0.0 else INF)
		var at := center + dir * k
		var side := dir.orthogonal()
		draw_colored_polygon(PackedVector2Array([at + dir * 12.0, at - dir * 4.0 + side * 9.0, at - dir * 4.0 - side * 9.0]),
			Color(1.0, 0.25, 0.2, blink))
