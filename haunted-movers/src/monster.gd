extends Node3D
## Basement monsters. Host-only brain; everyone sees the synced position/state. They walk the
## maze cell to cell (through doorways), so they never pass through walls. Three kinds:
##   statue — 마네킹: moves fast toward the nearest mover, but only while nobody alive is
##            looking at it. Touch = death. Keep eyes on it and back away together.
##   dust   — 먼지귀신: hangs on a ceiling. Walk under it and it drops onto your head: you are
##            blind, and dead in `latch_kill` s unless a teammate clicks you to pull it off.
##   boar   — 박제 멧돼지: wanders; sees a mover down a straight line → snorts, then charges in
##            that straight line. Hit = death. It slams into the wall and is dazed for a bit.
## Parked (hidden, far away) until `wake` spawns it.

const Tuning = preload("res://src/tuning.gd")
const Sfx = preload("res://src/sfx.gd")
const Basement = preload("res://src/basement.gd")

const PARKED := Vector3(0, -80, 0)
const KINDS := {statue = "마네킹", dust = "먼지귀신", boar = "박제 멧돼지"}

var main: Node
var kind := "statue"
## statue: "hunt" | dust: "ceiling", "latched" | boar: "wander", "windup", "charge", "dazed"
var state := "idle"
var _t := 0.0
var _goal_cell := Vector2i.ZERO
var _dir := Vector3.ZERO
var _victim := 0
var _cry := AudioStreamPlayer3D.new()
var _heard_state := "idle"


func setup(k: String) -> void:
	kind = k
	set_meta("label", KINDS[k])
	add_to_group("monsters")
	position = PARKED
	visible = false
	_build_look()
	_cry.stream = Sfx.cached("moan")
	_cry.unit_size = 4.0
	_cry.max_distance = 22.0
	_cry.pitch_scale = {statue = 1.6, dust = 2.2, boar = 0.55}[k]
	add_child(_cry)
	var cfg := SceneReplicationConfig.new()
	for p in [".:position", ".:rotation", ".:visible", ".:state"]:
		cfg.add_property(NodePath(p))
	var sync := MultiplayerSynchronizer.new()
	sync.name = "Sync"
	sync.replication_config = cfg
	add_child(sync)


## Host: appear at `at` (a basement cell floor centre).
func wake(at: Vector3) -> void:
	visible = true
	_victim = 0
	_t = 0.0
	match kind:
		"statue":
			state = "hunt"
			position = at
		"dust":
			state = "ceiling"
			position = at + Vector3(0, Basement.WALL_H - 0.35, 0)
		"boar":
			state = "wander"
			position = at
			_goal_cell = main.basement.cell_of(at)


## Host: back to storage (new day, or freed from a head).
func park() -> void:
	visible = false
	state = "idle"
	_victim = 0
	position = PARKED


## Every peer: cry when the synced state turns dangerous (drop, snort before a charge).
func _process(_delta: float) -> void:
	if state != _heard_state:
		_heard_state = state
		if state in ["latched", "windup"]:
			_cry.play()


func active() -> bool:
	return visible and state != "idle"


func _physics_process(delta: float) -> void:
	if main == null or not main.is_host_active() or not active() or main.run.phase != "day":
		return
	var c: Dictionary = Tuning.data.monsters[kind]
	_t += delta
	match kind:
		"statue":
			_statue(c, delta)
		"dust":
			_dust(c, delta)
		"boar":
			_boar(c, delta)


# --- statue -------------------------------------------------------------------

func _statue(c: Dictionary, delta: float) -> void:
	var target := _nearest_alive()
	if target == null or watched(c):
		return
	_walk_toward(target.global_position, c.speed, delta)
	if _flat_dist(target.global_position) < c.hit_range:
		main.kill_player(target.name.to_int())


## True if any living mover in the basement has this statue in view: within `sight` m,
## inside their view cone, and not behind a wall.
func watched(c: Dictionary) -> bool:
	for p in main.players.get_children():
		if main.is_stunned(p.name.to_int()) or p.blinded:
			continue
		var eye: Vector3 = p.cam.global_position
		var to := global_position + Vector3.UP * 1.0 - eye
		if to.length() > c.sight:
			continue
		var fwd: Vector3 = -p.cam.global_basis.z
		if fwd.dot(to.normalized()) < cos(deg_to_rad(c.view_angle)):
			continue
		if _clear_line(eye, global_position + Vector3.UP * 1.0, p):
			return true
	return false


# --- dust ----------------------------------------------------------------------

func _dust(c: Dictionary, _delta: float) -> void:
	if state == "ceiling":
		for p in main.players.get_children():
			var id: int = p.name.to_int()
			if main.is_stunned(id) or p.blinded:
				continue
			if _flat_dist(p.global_position) < c.drop_radius and absf(p.global_position.y - global_position.y) < 4.0:
				state = "latched"
				_victim = id
				_t = 0.0
				main.blind_player(id, true)
				return
	elif state == "latched":
		var p: Node3D = main.players.get_node_or_null(str(_victim))
		if p == null or main.is_stunned(_victim):
			main.blind_player(_victim, false)
			park()
			return
		global_position = p.global_position + Vector3.UP * 1.75  # sitting on the head
		if _t >= c.latch_kill:
			main.blind_player(_victim, false)
			main.kill_player(_victim)
			park()


## Host: a teammate pulled us off `id`'s head. True if we were on it.
func free_from(id: int) -> bool:
	if kind == "dust" and state == "latched" and _victim == id:
		main.blind_player(id, false)
		park()
		return true
	return false


# --- boar ----------------------------------------------------------------------

func _boar(c: Dictionary, delta: float) -> void:
	match state:
		"wander":
			var seen := _seen_mover(c)
			if seen:
				state = "windup"
				_t = 0.0
				_dir = ((seen.global_position - global_position) * Vector3(1, 0, 1)).normalized()
				look_at(global_position + _dir + Vector3(0.001, 0, 0), Vector3.UP)
				return
			if _arrive(main.basement.cell_center(_goal_cell), c.speed, delta):
				_goal_cell = _random_neighbour(_goal_cell)
		"windup":
			if _t >= c.windup:
				state = "charge"
				_t = 0.0
		"charge":
			var step: Vector3 = _dir * c.charge_speed * delta
			if _wall_ahead(step.length() + 0.6) or _t > c.charge_max:
				state = "dazed"
				_t = 0.0
				main.broadcast_sfx("thud")
				return
			global_position += step
			for p in main.players.get_children():
				var id: int = p.name.to_int()
				if not main.is_stunned(id) and _flat_dist(p.global_position) < c.hit_range \
						and absf(p.global_position.y - global_position.y) < 2.0:
					main.kill_player(id)
		"dazed":
			if _t >= c.daze:
				state = "wander"
				_goal_cell = main.basement.cell_of(global_position)


## A living mover in the basement the boar can see in a straight line within `sight` m.
func _seen_mover(c: Dictionary) -> Node3D:
	for p in main.players.get_children():
		if main.is_stunned(p.name.to_int()) or not main.basement.contains(p.global_position):
			continue
		if _flat_dist(p.global_position) < c.sight and _clear_line(global_position + Vector3.UP * 0.6, p.global_position + Vector3.UP * 0.9, p):
			return p
	return null


# --- shared movement and senses ------------------------------------------------------

## Step along the maze toward `goal`: through the next cell on the shortest path, then straight.
func _walk_toward(goal: Vector3, speed: float, delta: float) -> void:
	var bm: Node = main.basement
	var here: Vector2i = bm.cell_of(global_position)
	var there: Vector2i = bm.cell_of(goal)
	if here == there:
		_arrive(goal * Vector3(1, 0, 1) + Vector3(0, global_position.y, 0), speed, delta)
		return
	var next := next_cell(bm.maze, here, there)
	var aim: Vector3 = bm.cell_center(next)
	# go through the doorway's middle, not the corner: aim at the shared edge first
	var door: Vector3 = (bm.cell_center(here) + aim) / 2.0
	var lateral := (global_position - door) * Vector3(1, 0, 1)
	var across := Vector3(next.x - here.x, 0, next.y - here.y)
	if absf(lateral.dot(across.cross(Vector3.UP))) > 0.3:
		_arrive(door - across * 0.8, speed, delta)
	else:
		_arrive(aim, speed, delta)


## Pure: first cell on the shortest open path from `from` to `to` (or `from` if unreachable).
static func next_cell(maze: Dictionary, from: Vector2i, to: Vector2i) -> Vector2i:
	var prev := {from: from}
	var queue: Array[Vector2i] = [from]
	while not queue.is_empty():
		var c: Vector2i = queue.pop_front()
		if c == to:
			break
		for d in [Vector2i.RIGHT, Vector2i.LEFT, Vector2i.DOWN, Vector2i.UP]:
			var n: Vector2i = c + d
			if not prev.has(n) and maze.open.has(Basement.edge_key(c, n)):
				prev[n] = c
				queue.append(n)
	if not prev.has(to):
		return from
	var step := to
	while prev[step] != from:
		step = prev[step]
	return step


func _arrive(goal: Vector3, speed: float, delta: float) -> bool:
	var to := (goal - global_position) * Vector3(1, 0, 1)
	if to.length() < 0.15:
		return true
	global_position += to.limit_length(speed * delta)
	look_at(global_position + to + Vector3(0.001, 0, 0), Vector3.UP)
	return false


func _random_neighbour(c: Vector2i) -> Vector2i:
	var opts: Array[Vector2i] = []
	for d in [Vector2i.RIGHT, Vector2i.LEFT, Vector2i.DOWN, Vector2i.UP]:
		if main.basement.maze.open.has(Basement.edge_key(c, c + d)):
			opts.append(c + d)
	return c if opts.is_empty() else opts[randi() % opts.size()]


func _nearest_alive() -> Node3D:
	var best: Node3D
	for p in main.players.get_children():
		if main.is_stunned(p.name.to_int()) or not main.basement.contains(p.global_position):
			continue
		if best == null or _flat_dist(p.global_position) < _flat_dist(best.global_position):
			best = p
	return best


func _flat_dist(p: Vector3) -> float:
	return ((p - global_position) * Vector3(1, 0, 1)).length()


## No wall between `a` and `b` (ignores the mover `who` and furniture).
func _clear_line(a: Vector3, b: Vector3, who: Node) -> bool:
	var q := PhysicsRayQueryParameters3D.create(a, b)
	q.exclude = [who.get_rid()]
	var hit := get_world_3d().direct_space_state.intersect_ray(q)
	return hit.is_empty() or not (hit.collider is StaticBody3D)


func _wall_ahead(dist: float) -> bool:
	var from := global_position + Vector3.UP * 0.6
	var q := PhysicsRayQueryParameters3D.create(from, from + _dir * dist)
	var hit := get_world_3d().direct_space_state.intersect_ray(q)
	return not hit.is_empty() and hit.collider is StaticBody3D


func _build_look() -> void:
	match kind:
		"statue":
			var plaster := StandardMaterial3D.new()
			plaster.albedo_color = Color("#d8d0c4")
			plaster.roughness = 0.9
			_mesh(_capsule(0.22, 1.2), Vector3(0, 0.95, 0), plaster)        # torso and legs
			_mesh(_sphere(0.16), Vector3(0, 1.72, 0), plaster)              # blank head
			for sx in [-1, 1]:
				var arm := _mesh(_capsule(0.06, 0.7), Vector3(sx * 0.3, 1.15, -0.1), plaster)
				arm.rotation.x = -0.6                                        # reaching forward
		"dust":
			var fluff := StandardMaterial3D.new()
			fluff.albedo_color = Color("#2a2622")
			fluff.roughness = 1.0
			_mesh(_sphere(0.32), Vector3.ZERO, fluff)
			var glow := StandardMaterial3D.new()
			glow.emission_enabled = true
			glow.emission = Color("#ffef9a")
			glow.emission_energy_multiplier = 2.5
			for sx in [-1, 1]:
				_mesh(_sphere(0.04), Vector3(sx * 0.1, -0.12, -0.26), glow)
		"boar":
			var hide := StandardMaterial3D.new()
			hide.albedo_color = Color("#5a3a26")
			var tusk := StandardMaterial3D.new()
			tusk.albedo_color = Color("#efe6cc")
			var body := BoxMesh.new()
			body.size = Vector3(0.7, 0.7, 1.3)
			_mesh(body, Vector3(0, 0.55, 0), hide)
			var head := BoxMesh.new()
			head.size = Vector3(0.5, 0.45, 0.5)
			_mesh(head, Vector3(0, 0.6, -0.8), hide)
			for sx in [-1, 1]:
				var t := _mesh(_capsule(0.03, 0.3), Vector3(sx * 0.18, 0.45, -1.05), tusk)
				t.rotation.x = -1.2
			var eye := StandardMaterial3D.new()
			eye.emission_enabled = true
			eye.emission = Color("#ff3020")
			eye.emission_energy_multiplier = 3.0
			for sx in [-1, 1]:
				_mesh(_sphere(0.04), Vector3(sx * 0.16, 0.72, -1.05), eye)


func _mesh(m: Mesh, at: Vector3, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = m
	mi.material_override = mat
	mi.position = at
	add_child(mi)
	return mi


func _sphere(r: float) -> SphereMesh:
	var m := SphereMesh.new()
	m.radius = r
	m.height = r * 2.0
	return m


func _capsule(r: float, h: float) -> CapsuleMesh:
	var m := CapsuleMesh.new()
	m.radius = r
	m.height = h
	return m
