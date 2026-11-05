class_name Crab
extends RefCounted
## Rubble crab: buried in the seabed while it is flooded, out walking once the water
## has left its spot. Patrols its floor, turning at walls and ledges. World.step drives it.

var pos: Vector2
var dir := 1.0
var active := false


func _init(at: Vector2) -> void:
	pos = at


func step(world: World, dt: float) -> void:
	active = not world.in_water(pos)
	if not active:
		return
	var next := pos + Vector2(dir * Tuning.CRAB_SPEED * dt, 0)
	var ahead := next + Vector2(dir * Tuning.CRAB_SIZE.x / 2.0, Tuning.TILE)  # floor under the leading edge
	if world.box_hits(next, Tuning.CRAB_SIZE) or not world.solid_at(ahead):
		dir = -dir
	else:
		pos = next


func touches(center: Vector2, size: Vector2) -> bool:
	return active and absf(center.x - pos.x) * 2 < size.x + Tuning.CRAB_SIZE.x \
		and absf(center.y - pos.y) * 2 < size.y + Tuning.CRAB_SIZE.y
