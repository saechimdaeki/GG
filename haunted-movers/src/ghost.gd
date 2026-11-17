extends Node3D
## The mansion ghost. Host-only brain; everyone sees the synced position.
## Floats through walls (it's a ghost), so it needs no navmesh; it drifts from room to random
## room of today's mansion.
## patrol → chase (someone within sight; anyone carrying scrap counts as closer) → attack (first
## catch knocks you down, a second catch the same day kills) → cooldown (drifts off to another
## room) → patrol. Sleeps for `start_delay` s each morning. An exorcism tape disables it.

const Tuning = preload("res://src/tuning.gd")

var main: Node
var state := "patrol"
var _goal := Vector3.ZERO
var _cooldown := 0.0
var _disabled := 0.0
var _lure_left := 0.0
var _lure_pos := Vector3.ZERO
var _flinch := 0.0
var _flee_from := Vector3.ZERO
var _arms: Array[Node3D] = []
var _head := Node3D.new()
var _strands: Array[Node3D] = []
var _moan := AudioStreamPlayer3D.new()


## Pure transition. `target_dist` is INF when nobody is in sight.
static func next_state(s: String, target_dist: float, cooldown_left: float, c: Dictionary) -> String:
	match s:
		"cooldown":
			return "patrol" if cooldown_left <= 0.0 else "cooldown"
		"patrol":
			return "chase" if target_dist < c.sight else "patrol"
		"chase":
			if target_dist >= c.sight:
				return "patrol"
			return "attack" if target_dist <= c.hit_range else "chase"
	return "patrol"


func _init() -> void:
	_build_look()
	var glow := OmniLight3D.new()  # casts its cold light on nearby walls
	glow.light_color = Color("#9fd8ff")
	glow.light_energy = 0.8
	glow.omni_range = 3.5
	glow.light_volumetric_fog_energy = 3.0  # a cold haze hangs around it
	add_child(glow)
	_moan.stream = preload("res://src/sfx.gd").cached("moan")
	_moan.unit_size = 3.0
	_moan.max_distance = 18.0
	_moan.autoplay = true
	add_child(_moan)

	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", ".:visible", ".:state"]:
		cfg.add_property(NodePath(p))
	var sync := MultiplayerSynchronizer.new()
	sync.name = "Sync"  # fixed name: auto names (@...@N) differ between peers
	sync.replication_config = cfg
	add_child(sync)


## Exorcism: the ghost fades out and does nothing for `sec` seconds.
func exorcise(sec: float) -> void:
	_disabled = sec
	_lure_left = 0.0
	visible = sec <= 0.0
	state = "patrol"


## Host: the truck horn calls the ghost to `pos` for up to `sec` seconds (or until it gets
## there). Resetting the day (exorcise(0)) cancels it.
func lure(pos: Vector3, sec: float) -> void:
	_lure_pos = pos
	_lure_left = sec


func _process(delta: float) -> void:
	# arms hang limp on patrol and rise toward you on the hunt; the head lolls
	var reach := 1.3 if state == "chase" else 0.15
	for a in _arms:
		a.rotation.x = lerpf(a.rotation.x, reach, delta * 3.0)
	var t := Time.get_ticks_msec() * 0.001
	_head.rotation.z = 0.22 + sin(t * 1.3) * 0.08
	for i in _strands.size():  # hair hangs straight down whatever the head does, and sways
		_strands[i].rotation.z = -_head.rotation.z
		_strands[i].rotation.x = sin(t * 1.7 + i * 0.9) * 0.08 + (0.15 if state == "chase" else 0.0)
	# Now and then she glitches: a jump sideways for a frame or two (cosmetic, every peer).
	var glitch := fmod(t + get_instance_id() % 7, 3.7) < 0.06
	_head.position.x = randf_range(-0.12, 0.12) if glitch else 0.0
	# everyone hears it; louder and a little higher when it's hunting (state is synced)
	_moan.volume_db = -60.0 if not visible else (-2.0 if state == "chase" else -10.0)
	_moan.pitch_scale = {"chase": 1.15, "flinch": 1.7}.get(state, 1.0)
	if state == "flinch":  # she shrieks: louder, and her arms fly up over her face
		_moan.volume_db = 2.0
		for a in _arms:
			a.rotation.x = 2.6


func _physics_process(delta: float) -> void:
	if main == null or not main.is_host_active() or main.run.phase != "day":
		return
	var c: Dictionary = Tuning.data.ghost
	if _disabled > 0.0:
		_disabled -= delta
		visible = _disabled <= 0.0
		return
	if Tuning.data.run.day_seconds - main.run.time_left < c.start_delay:
		return  # sleeps through the first seconds of each day so movers can get going
	_cooldown = maxf(0.0, _cooldown - delta)
	if _lure_left > 0.0:  # the truck horn: drop everything and drift toward it
		_lure_left -= delta
		state = "patrol"
		if _move_to(_lure_pos, c.speed * 1.3, delta):
			_lure_left = 0.0  # arrived: whoever is at the truck had better run
		return

	# A flashlight beam on her makes her shriek and back away (and eats that light's battery).
	for p in main.players.get_children():
		if lit_by(p):
			_flinch = c.flinch_seconds
			_flee_from = p.global_position
	if _flinch > 0.0:
		_flinch -= delta
		state = "flinch"
		var away := (global_position - _flee_from) * Vector3(1, 0, 1)
		global_position += away.normalized() * c.speed * delta
		return

	var target := _pick_target(c)
	var dist := INF
	if target:
		dist = global_position.distance_to(target.global_position + Vector3.UP)
	state = next_state(state, dist, _cooldown, c)

	match state:
		"patrol":
			if _goal == Vector3.ZERO or _move_to(_goal, c.speed * 0.6, delta):
				_goal = _random_room()
		"chase":
			_move_to(target.global_position + Vector3.UP, c.speed, delta)
		"attack":
			main.ghost_hit(target.name.to_int())
			_cooldown = c.cooldown
			state = "cooldown"
			_goal = _random_room()
		"cooldown":  # back off after a catch so it can't pin a downed mover
			if _move_to(_goal, c.speed * 0.6, delta):
				_goal = _random_room()
	position.y += sin(Time.get_ticks_msec() * 0.003) * 0.003  # bob


## True when `p`'s lit flashlight points at her (within `flash_range` m and `flash_angle`
## degrees of the beam) with no wall in between. Uses synced aim, so the host and the owner agree.
func lit_by(p: Node3D) -> bool:
	var c: Dictionary = Tuning.data.ghost
	if not visible or not p.flash_on or p.stunned or not p.is_inside_tree():
		return false
	var from: Vector3 = p.cam.global_position
	var to := global_position - from
	if to.length() > c.flash_range or rad_to_deg((-p.cam.global_basis.z).angle_to(to)) > c.flash_angle:
		return false
	var q := PhysicsRayQueryParameters3D.create(from, global_position)
	q.exclude = [p.get_rid()]
	return get_world_3d().direct_space_state.intersect_ray(q).is_empty()


## Host: she heard a board creak at `pos`: on patrol she drifts over to see.
func hear(pos: Vector3) -> void:
	if state == "patrol" and _lure_left <= 0.0:
		_goal = pos + Vector3.UP * 1.2


## Host: something scared her (a flashbang at `from`): she backs away for `sec` s.
func scare(from: Vector3, sec: float) -> void:
	_flinch = sec
	_flee_from = from


## A random room of today's mansion, at floating height.
func _random_room() -> Vector3:
	var rooms: Array = main.interior.maze.dist.keys()
	return main.interior.cell_center(rooms[randi() % rooms.size()]) + Vector3.UP * 1.2


## Nearest awake player in sight; someone carrying scrap counts as closer.
func _pick_target(c: Dictionary) -> Node3D:
	var best: Node3D
	var best_score := INF
	for p in main.players.get_children():
		if main.is_stunned(p.name.to_int()):
			continue
		var d := global_position.distance_to(p.global_position + Vector3.UP)
		if d >= c.sight:
			continue
		var score: float = d - (c.carrier_bias if p.slots.any(func(n: String) -> bool: return n != "") else 0.0)
		if score < best_score:
			best = p
			best_score = score
	return best


## Moves toward `goal`; true when there.
func _move_to(goal: Vector3, speed: float, delta: float) -> bool:
	var to := goal - global_position
	if to.length() < 0.2:
		return true
	global_position += to.limit_length(speed * delta)
	look_at(global_position + to * Vector3(1, 0, 1) + Vector3(0.001, 0, 0), Vector3.UP)
	return false


## 소복 귀신 (the mourning maid): a tall see-through white gown with a rippling hem, a pale
## face with black hollow eyes and a long screaming mouth, wet black hair hanging in strands
## that sway, and over-long jointed arms with long fingers that rise toward you when she hunts.
## Origin = her chest; she floats with the hem well off the floor.
func _build_look() -> void:
	var gown := ShaderMaterial.new()
	gown.shader = preload("res://src/spatial_char_ghost.gdshader")
	var skin := StandardMaterial3D.new()
	skin.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	skin.albedo_color = Color("#d6dde2")
	var hollow := StandardMaterial3D.new()
	hollow.albedo_color = Color.BLACK
	hollow.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	var hair := StandardMaterial3D.new()
	hair.albedo_color = Color("#050507")
	hair.roughness = 0.25  # wet sheen
	var dress := CylinderMesh.new()  # narrow at the shoulders, flared and torn at the hem
	dress.top_radius = 0.17
	dress.bottom_radius = 0.55
	dress.height = 1.8
	dress.radial_segments = 40
	dress.rings = 16
	_part(dress, Vector3(0, -0.7, 0), gown, self)
	_part(_sphere(0.2, 0.32), Vector3(0, 0.12, 0), gown, self)  # shoulders, rounded
	add_child(_head)
	_head.position = Vector3(0, 0.42, 0)
	var face := _part(_sphere(0.14, 0.4), Vector3.ZERO, skin, _head)
	face.scale = Vector3(0.85, 1.0, 0.8)  # a long, narrow face
	for sx in [-1, 1]:  # hollow eyes
		var e := _part(_sphere(0.035, 0.07), Vector3(sx * 0.05, 0.04, -0.108), hollow, _head)
		e.scale = Vector3(1.0, 1.4, 0.5)
	var mouth := _part(_sphere(0.04, 0.08), Vector3(0, -0.07, -0.1), hollow, _head)
	mouth.scale = Vector3(0.8, 2.2, 0.5)  # stretched open in a scream
	_part(_sphere(0.15, 0.34), Vector3(0, 0.07, 0.06), hair, _head).scale = Vector3(1.0, 1.0, 0.85)  # scalp, behind the face
	for i in 18:  # long strands, mostly behind and beside the face, two falling across it
		var a := PI * (0.05 + 0.9 * i / 17.0)
		var front := i == 8 or i == 10
		var strand := Node3D.new()
		strand.position = Vector3(cos(a) * 0.13, 0.1, (-0.1 if front else 0.02 + sin(a) * 0.1))
		_head.add_child(strand)
		var len_ := randf_range(0.8, 1.25)
		var m := CapsuleMesh.new()
		m.radius = 0.03
		m.height = len_
		m.radial_segments = 6
		_part(m, Vector3(0, -len_ / 2, 0), hair, strand)
		_strands.append(strand)
	var upper := CapsuleMesh.new()
	upper.radius = 0.035
	upper.height = 0.6
	var finger := CapsuleMesh.new()
	finger.radius = 0.012
	finger.height = 0.2
	for sx in [-1, 1]:
		var shoulder := Node3D.new()
		shoulder.position = Vector3(sx * 0.21, 0.15, 0)
		add_child(shoulder)
		_part(upper, Vector3(0, -0.3, 0), skin, shoulder)
		var elbow := Node3D.new()  # a second joint, bent a little the wrong way
		elbow.position = Vector3(0, -0.58, 0)
		elbow.rotation.x = -0.25
		shoulder.add_child(elbow)
		_part(upper, Vector3(0, -0.3, 0), skin, elbow)
		for f in 4:  # long fingers
			var fm := _part(finger, Vector3((f - 1.5) * 0.02, -0.68, 0), skin, elbow)
			fm.rotation.z = (f - 1.5) * 0.12
		_arms.append(shoulder)


func _sphere(r: float, h: float) -> SphereMesh:
	var m := SphereMesh.new()
	m.radius = r
	m.height = h
	m.radial_segments = 24
	m.rings = 12
	return m


func _part(m: Mesh, at: Vector3, mat: Material, parent: Node3D) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.material_override = mat
	mi.position = at
	parent.add_child(mi)
	return mi
