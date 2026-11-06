extends RigidBody3D
## A movable piece of furniture. The host simulates it; clients keep it frozen
## (kinematic) and follow the synced transform.

const Tuning = preload("res://src/tuning.gd")

## Host: a "buck" curse just kicked; main releases everyone holding it.
signal bucked(body: RigidBody3D)
## Host: took impact damage (main plays a thud).
signal hit

## How many players hold it right now. Set by the host, synced to clients.
var holders := 0
## Payout when delivered intact.
var value := 0.0
var size := Vector3.ONE
## 1 = pristine, 0 = broken (pays nothing). Host lowers it on hard impacts; synced.
var durability := 1.0:
	set(v):
		durability = v
		if _mat:
			_mat.albedo_color = _color.lerp(Color("#222222"), (1.0 - v) * 0.7)

var _mat := StandardMaterial3D.new()
var _color := Color.WHITE
var _prev_vel := Vector3.ZERO
## "" | "runaway" (hops away from nearby players while nobody holds it)
## | "buck" (every few seconds while held, jumps and shakes everyone off)
var curse := ""
var _curse_t := 0.0


## Builds collision, mesh and network sync from one entry of tuning.json "furniture".
func setup(d: Dictionary) -> void:
	name = d.name
	mass = d.mass
	value = d.value
	curse = d.get("curse", "")
	position = Vector3(d.pos[0], d.pos[1], d.pos[2])
	freeze_mode = RigidBody3D.FREEZE_MODE_KINEMATIC
	size = Vector3(d.size[0], d.size[1], d.size[2])

	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	add_child(col)

	var mesh := BoxMesh.new()
	mesh.size = size
	_color = Color(d.color)
	_mat.albedo_color = _color
	mesh.material = _mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	add_child(mi)

	_add_face()

	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", ".:holders", ".:durability"]:
		cfg.add_property(NodePath(p))
	var sync := MultiplayerSynchronizer.new()
	sync.replication_config = cfg
	add_child(sync)


## World-space box around the furniture (used for truck packing).
func world_aabb() -> AABB:
	return global_transform * AABB(-size / 2, size)


## Host: a sudden velocity change (a hit or a drop) above the threshold costs
## durability; curses act.
func _physics_process(delta: float) -> void:
	var dv := (linear_velocity - _prev_vel).length()
	_prev_vel = linear_velocity
	if freeze:
		return
	var t := Tuning.data
	if durability > 0.0 and dv > t.impact_threshold:
		durability = maxf(0.0, durability - (dv - t.impact_threshold) * t.damage_per_impact)
		hit.emit()
	if curse != "":
		_curse_step(delta, t.curses[curse])


func _curse_step(delta: float, c: Dictionary) -> void:
	_curse_t += delta
	if curse == "runaway":
		if holders > 0 or _curse_t < c.hop_interval:
			return
		var near := _nearest_player(c.radius)
		if near:
			_curse_t = 0.0
			var away := (global_position - near.global_position) * Vector3(1, 0, 1)
			apply_central_impulse((away.normalized() * c.hop_speed + Vector3.UP * c.hop_up) * mass)
	elif curse == "buck":
		if holders == 0:
			_curse_t = 0.0  # the countdown starts when someone grabs it
		elif _curse_t >= c.interval:
			_curse_t = 0.0
			apply_central_impulse(Vector3.UP * c.kick_up * mass)
			apply_torque_impulse(Vector3(0, 0, c.kick_spin * mass))
			bucked.emit(self)


func _nearest_player(radius: float) -> Node3D:
	var best: Node3D
	var best_d := radius
	for p in get_tree().get_nodes_in_group("players"):
		var d := global_position.distance_to(p.global_position)
		if d < best_d:
			best = p
			best_d = d
	return best


## Two eyes and a mouth on the -z face (toward the front door).
func _add_face() -> void:
	var white := StandardMaterial3D.new()
	white.albedo_color = Color("#f4ecd8")
	var black := StandardMaterial3D.new()
	black.albedo_color = Color("#1a1218")
	var front := -size.z / 2
	var spread := minf(0.2, size.x * 0.25)
	for sx in [-1, 1]:
		_face_part(Vector3(0.12, 0.12, 0.02), Vector3(sx * spread, size.y * 0.25, front - 0.011), white)
		_face_part(Vector3(0.06, 0.06, 0.02), Vector3(sx * spread, size.y * 0.25 - 0.02, front - 0.021), black)
	_face_part(Vector3(0.18, 0.04, 0.02), Vector3(0, size.y * 0.25 - 0.14, front - 0.011), black)


func _face_part(box: Vector3, pos: Vector3, mat: Material) -> void:
	var m := BoxMesh.new()
	m.size = box
	m.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.position = pos
	add_child(mi)
