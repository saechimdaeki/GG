class_name Eel
extends RefCounted
## Lantern eel: swims left and right and turns at walls. Dies if the water leaves it (World.step).

var pos: Vector2
var dir := 1.0
var alive := true


func _init(at: Vector2) -> void:
	pos = at


func step(world: World, dt: float) -> void:
	var next := pos + Vector2(dir * Tuning.EEL_SPEED * dt, 0)
	if world.box_hits(next, Tuning.EEL_SIZE):
		dir = -dir
	else:
		pos = next


func touches(center: Vector2, size: Vector2) -> bool:
	return absf(center.x - pos.x) * 2 < size.x + Tuning.EEL_SIZE.x \
		and absf(center.y - pos.y) * 2 < size.y + Tuning.EEL_SIZE.y
