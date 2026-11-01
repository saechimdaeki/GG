class_name Diver
extends RefCounted
## The player: movement (swim below the surface, walk and jump above it), oxygen,
## tool hits, pickups and fainting. Input arrives as plain values so tests can drive it.

signal fainted
signal picked(kind: String)
signal hurt

var pos: Vector2
var vel := Vector2.ZERO
var facing := Vector2i.RIGHT
var oxygen := Tuning.OXYGEN_MAX
var inventory := {"scrap": 0, "pipe": 0, "gear": 0}
var on_floor := false
var _invuln := 0.0
var _hit_cd := 0.0


func _init(at: Vector2) -> void:
	pos = at


## move: -1..1 per axis. jump: pressed this frame. use_tool: held.
func step(world: World, move: Vector2, jump: bool, use_tool: bool, dt: float) -> void:
	_invuln = maxf(_invuln - dt, 0.0)
	_hit_cd = maxf(_hit_cd - dt, 0.0)
	if move.x != 0:
		facing = Vector2i(int(signf(move.x)), 0)
	elif move.y != 0:
		facing = Vector2i(0, int(signf(move.y)))

	var wet := world.in_water(pos)
	if wet:
		_swim(move, dt)
		if jump and pos.y - world.surface_y < Tuning.SURFACE_JUMP_RANGE:
			vel.y = -Tuning.JUMP_SPEED
	else:
		_walk(move, jump, dt)
	_move(world, dt)

	if wet:
		oxygen -= Tuning.OXYGEN_DRAIN * dt
	else:
		oxygen = Tuning.OXYGEN_MAX
	for a in world.air_plants:
		if pos.distance_to(a) < Tuning.TILE:
			oxygen = Tuning.OXYGEN_MAX

	if use_tool and _hit_cd == 0.0:
		_hit_cd = Tuning.HIT_COOLDOWN
		world.hit(_tile() + facing)

	for i in range(world.pickups.size() - 1, -1, -1):
		var p: Dictionary = world.pickups[i]
		if pos.distance_to(p["pos"]) < Tuning.PICKUP_RANGE:
			inventory[p["kind"]] += 1
			world.pickups.remove_at(i)
			picked.emit(p["kind"])

	for e in world.eels:
		if e.alive and _invuln == 0.0 and e.touches(pos, Tuning.DIVER_SIZE):
			oxygen -= Tuning.EEL_BITE
			_invuln = Tuning.HURT_INVULN
			vel = (pos - e.pos).normalized() * Tuning.KNOCKBACK
			hurt.emit()

	if oxygen <= 0.0:
		_faint(world)


func near_pump(world: World) -> bool:
	return pos.distance_to(world.pump) < Tuning.PUMP_RANGE


func invulnerable() -> bool:
	return _invuln > 0.0


func _tile() -> Vector2i:
	return Vector2i(floori(pos.x / Tuning.TILE), floori(pos.y / Tuning.TILE))


func _swim(move: Vector2, dt: float) -> void:
	if move != Vector2.ZERO:
		vel += move.normalized() * Tuning.SWIM_ACCEL * dt
	else:
		vel -= vel * minf(Tuning.SWIM_DRAG * dt, 1.0)
		vel.y += Tuning.SWIM_SINK * dt
	# ease down to swim speed instead of clamping, so a surface jump keeps its push for a few frames
	vel = vel.move_toward(vel.limit_length(Tuning.SWIM_MAX), Tuning.SWIM_ACCEL * 1.5 * dt)


func _walk(move: Vector2, jump: bool, dt: float) -> void:
	vel.x = move.x * Tuning.WALK_SPEED
	vel.y = minf(vel.y + Tuning.GRAVITY * dt, Tuning.FALL_MAX)
	if jump and on_floor:
		vel.y = -Tuning.JUMP_SPEED


## Axis-separated move so the diver slides along walls.
func _move(world: World, dt: float) -> void:
	var dx := Vector2(vel.x * dt, 0)
	if world.box_hits(pos + dx, Tuning.DIVER_SIZE):
		vel.x = 0
	else:
		pos += dx
	var dy := Vector2(0, vel.y * dt)
	on_floor = false
	if world.box_hits(pos + dy, Tuning.DIVER_SIZE):
		on_floor = vel.y > 0
		vel.y = 0
	else:
		pos += dy


func _faint(world: World) -> void:
	for k in inventory:
		inventory[k] = int(inventory[k] * Tuning.FAINT_KEEP)
	pos = world.start
	vel = Vector2.ZERO
	oxygen = Tuning.OXYGEN_MAX
	fainted.emit()
