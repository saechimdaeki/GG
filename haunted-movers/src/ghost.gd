extends Node3D
## The house ghost. Host-only brain; everyone sees the synced position.
## Floats through walls (it's a ghost), so it needs no navmesh.
## patrol → chase (someone within sight; carriers count as closer) → attack (stun)
## → cooldown → patrol. Sleeps for `start_delay` s each morning. An exorcism tape disables it.

const Tuning = preload("res://src/tuning.gd")

var main: Node
var state := "patrol"
var _wp := 0
var _cooldown := 0.0
var _disabled := 0.0
var _lure_left := 0.0
var _lure_pos := Vector3.ZERO
var _body := MeshInstance3D.new()
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
	var m := SphereMesh.new()
	m.radius = 0.45
	m.height = 1.3
	m.radial_segments = 24
	m.rings = 16
	var mat := ShaderMaterial.new()
	mat.shader = preload("res://src/spatial_char_ghost.gdshader")
	m.material = mat
	_body.mesh = m
	add_child(_body)
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
	var black := StandardMaterial3D.new()
	black.albedo_color = Color.BLACK
	for sx in [-1, 1]:
		var eye := MeshInstance3D.new()
		var em := SphereMesh.new()
		em.radius = 0.07
		em.height = 0.14
		em.material = black
		eye.mesh = em
		eye.position = Vector3(sx * 0.15, 0.15, -0.4)
		add_child(eye)

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


func _process(_delta: float) -> void:
	# everyone hears it; louder and a little higher when it's hunting (state is synced)
	_moan.volume_db = -60.0 if not visible else (-2.0 if state == "chase" else -10.0)
	_moan.pitch_scale = 1.15 if state == "chase" else 1.0


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

	var target := _pick_target(c)
	var dist := INF
	if target:
		dist = global_position.distance_to(target.global_position + Vector3.UP)
	state = next_state(state, dist, _cooldown, c)

	match state:
		"patrol":
			var wp: Array = c.patrol[_wp]
			var goal := Vector3(wp[0], wp[1], wp[2])
			if _move_to(goal, c.speed * 0.6, delta):
				_wp = (_wp + 1) % c.patrol.size()
		"chase":
			_move_to(target.global_position + Vector3.UP, c.speed, delta)
		"attack":
			main.kill_player(target.name.to_int())
			_cooldown = c.cooldown
			state = "cooldown"
	position.y += sin(Time.get_ticks_msec() * 0.003) * 0.003  # bob


## Nearest awake player in sight; someone carrying furniture counts as closer.
func _pick_target(c: Dictionary) -> Node3D:
	var best: Node3D
	var best_score := INF
	for p in main.players.get_children():
		if main.is_stunned(p.name.to_int()):
			continue
		var d := global_position.distance_to(p.global_position + Vector3.UP)
		if d >= c.sight:
			continue
		var score: float = d - (c.carrier_bias if main.grabs.has(p.name.to_int()) else 0.0)
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
