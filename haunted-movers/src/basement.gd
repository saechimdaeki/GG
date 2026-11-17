extends Node3D
## Underground storage maze (지하 창고), rebuilt every day from the run seed so each day is a
## different layout. It sits far below the yard; doors teleport between it and the house:
##   house "지하실" door (ground floor, left wall) ⇄ entrance cell (0, 0)
##   back-yard "비상구" (behind the house)        ⇄ the cell farthest from the entrance
## Walls/floor are built identically on every peer from (seed, day), so nothing is synced.

const Models = preload("res://src/models.gd")
const Tuning = preload("res://src/tuning.gd")

## Maze corner (cell (0, 0) starts here, +x columns, +z rows).
const ORIGIN := Vector3(-20, -40, 30)
const WALL_H := 3.0

## Last generated layout (see `generate`). Empty until `build`.
var maze := {}
## Teleport doors: [{zone: AABB, to: Vector3, yaw: float, label: String}]
var doors: Array = []
var _built: Node3D
var _wall := Models.surface(1, Color("#5a5e58"), Color("#2e302c"))
var _floor := Models.surface(0, Color("#3a3830"), Color.BLACK)
var _ceiling := Models.matte(Color("#1c1c1a"))


## Pure maze: random depth-first spanning tree over cols × rows cells, then `extra` of the
## remaining walls opened for loops. Returns {cols, rows, cell, open: {key: true}, exit: Vector2i,
## dist: {Vector2i: int}}. Every cell is reachable from (0, 0).
static func generate(seed: int, cols: int, rows: int, cell: float, extra: float) -> Dictionary:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	var open := {}
	var seen := {Vector2i.ZERO: true}
	var stack: Array[Vector2i] = [Vector2i.ZERO]
	var dirs := [Vector2i.RIGHT, Vector2i.LEFT, Vector2i.DOWN, Vector2i.UP]
	while not stack.is_empty():
		var c: Vector2i = stack.back()
		var next: Array[Vector2i] = []
		for d in dirs:
			var n: Vector2i = c + d
			if n.x >= 0 and n.y >= 0 and n.x < cols and n.y < rows and not seen.has(n):
				next.append(n)
		if next.is_empty():
			stack.pop_back()
			continue
		var n := next[rng.randi_range(0, next.size() - 1)]
		open[edge_key(c, n)] = true
		seen[n] = true
		stack.append(n)
	for x in cols:
		for y in rows:
			for d in [Vector2i.RIGHT, Vector2i.DOWN]:
				var n: Vector2i = Vector2i(x, y) + d
				if n.x < cols and n.y < rows and rng.randf() < extra:
					open[edge_key(Vector2i(x, y), n)] = true
	var dist := {Vector2i.ZERO: 0}
	var queue: Array[Vector2i] = [Vector2i.ZERO]
	var far := Vector2i.ZERO
	while not queue.is_empty():
		var c: Vector2i = queue.pop_front()
		var edge := c.x == 0 or c.y == 0 or c.x == cols - 1 or c.y == rows - 1
		if edge and dist[c] > dist[far]:  # the fire exit needs an outer wall
			far = c
		for d in dirs:
			var n: Vector2i = c + d
			if open.has(edge_key(c, n)) and not dist.has(n):
				dist[n] = dist[c] + 1
				queue.append(n)
	return {cols = cols, rows = rows, cell = cell, open = open, exit = far, dist = dist}


static func edge_key(a: Vector2i, b: Vector2i) -> String:
	var lo := a if a < b else b
	var hi := b if a < b else a
	return "%d,%d|%d,%d" % [lo.x, lo.y, hi.x, hi.y]


## World position of a cell's floor centre.
func cell_center(c: Vector2i) -> Vector3:
	var s: float = maze.cell
	return ORIGIN + Vector3((c.x + 0.5) * s, 0, (c.y + 0.5) * s)


## True if `p` is inside the maze volume (used by the radio map and monsters).
func contains(p: Vector3) -> bool:
	if maze.is_empty():
		return false
	var s: float = maze.cell
	return p.y < ORIGIN.y + WALL_H + 1.0 and p.y > ORIGIN.y - 2.0 \
		and p.x >= ORIGIN.x and p.z >= ORIGIN.z and p.x <= ORIGIN.x + maze.cols * s and p.z <= ORIGIN.z + maze.rows * s


## Which cell `p` is in (clamped).
func cell_of(p: Vector3) -> Vector2i:
	var s: float = maze.cell
	return Vector2i(clampi(floori((p.x - ORIGIN.x) / s), 0, maze.cols - 1), clampi(floori((p.z - ORIGIN.z) / s), 0, maze.rows - 1))


## Floor spots for furniture: every cell centre except the entrance and fire-exit cells,
## shuffled by `seed`. Each is [x, y, z, yaw] like the house spawns.
func spawn_points(seed: int) -> Array:
	var out := []
	for x in maze.cols:
		for y in maze.rows:
			var c := Vector2i(x, y)
			if c != Vector2i.ZERO and c != maze.exit:
				var p := cell_center(c)
				out.append([p.x, p.y, p.z, 0])
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	for i in range(out.size() - 1, 0, -1):
		var j := rng.randi_range(0, i)
		var t = out[i]
		out[i] = out[j]
		out[j] = t
	return out


## Rebuilds walls, floor, lights and doors for (seed, day). Same inputs → same maze on every peer.
func build(seed: int, day: int) -> void:
	var c: Dictionary = Tuning.data.basement
	var sizes: Array = c.sizes
	var sz: Array = sizes[clampi(day, 1, sizes.size()) - 1]
	maze = generate(hash([seed, day, "basement"]), int(sz[0]), int(sz[1]), float(c.cell), float(c.extra_openings))
	if _built:
		_built.free()
	_built = Node3D.new()
	add_child(_built)
	var s: float = maze.cell
	var w: float = maze.cols * s
	var d: float = maze.rows * s
	var door: float = c.door_width
	_box(Vector3(w, 0.2, d), ORIGIN + Vector3(w / 2, -0.1, d / 2), _floor)
	_box(Vector3(w, 0.2, d), ORIGIN + Vector3(w / 2, WALL_H + 0.1, d / 2), _ceiling)
	# Outer walls.
	_box(Vector3(w + 0.2, WALL_H, 0.2), ORIGIN + Vector3(w / 2, WALL_H / 2, 0), _wall)
	_box(Vector3(w + 0.2, WALL_H, 0.2), ORIGIN + Vector3(w / 2, WALL_H / 2, d), _wall)
	_box(Vector3(0.2, WALL_H, d), ORIGIN + Vector3(0, WALL_H / 2, d / 2), _wall)
	_box(Vector3(0.2, WALL_H, d), ORIGIN + Vector3(w, WALL_H / 2, d / 2), _wall)
	# Inner walls: between each pair of neighbours, solid or with a doorway in the middle.
	for x in maze.cols:
		for y in maze.rows:
			var here := Vector2i(x, y)
			if x + 1 < maze.cols:
				_inner_wall(here, here + Vector2i.RIGHT, Vector3((x + 1) * s, 0, (y + 0.5) * s), false, s, door)
			if y + 1 < maze.rows:
				_inner_wall(here, here + Vector2i.DOWN, Vector3((x + 0.5) * s, 0, (y + 1) * s), true, s, door)
	# A few weak work lights so it isn't pitch black (entrance always lit).
	var rng := RandomNumberGenerator.new()
	rng.seed = hash([seed, day, "lights"])
	for x in maze.cols:
		for y in maze.rows:
			var cc := Vector2i(x, y)
			if cc == Vector2i.ZERO or cc == maze.exit or rng.randf() < c.light_chance:
				var l := OmniLight3D.new()
				l.position = cell_center(cc) + Vector3(0, WALL_H - 0.4, 0)
				l.omni_range = s * 0.9
				l.light_energy = 0.35
				l.light_color = Color("#ff3b2a") if cc == maze.exit else Color("#d8c89a")
				_built.add_child(l)
	_make_doors()


func _inner_wall(a: Vector2i, b: Vector2i, mid: Vector3, along_x: bool, s: float, door: float) -> void:
	var size := Vector3(s, WALL_H, 0.2) if along_x else Vector3(0.2, WALL_H, s)
	if not maze.open.has(edge_key(a, b)):
		_box(size, ORIGIN + mid + Vector3(0, WALL_H / 2, 0), _wall)
		return
	var side := (s - door) / 2.0
	for sgn in [-1, 1]:
		var off: float = (door / 2.0 + side / 2.0) * sgn
		var piece: Vector3 = Vector3(side, WALL_H, 0.2) if along_x else Vector3(0.2, WALL_H, side)
		var at: Vector3 = mid + (Vector3(off, 0, 0) if along_x else Vector3(0, 0, off))
		_box(piece, ORIGIN + at + Vector3(0, WALL_H / 2, 0), _wall)


## Doors: house ⇄ entrance (west wall of cell 0,0), back yard ⇄ fire exit (outer wall of the
## exit cell). Each door is a zone you walk into and the spot you come out at.
func _make_doors() -> void:
	var c: Dictionary = Tuning.data.basement
	var hd: Array = c.house_door        # [x, z] on the house's left inner wall
	var yd: Array = c.yard_door         # [x, z] on the back of the house
	var house_door := Vector3(hd[0], 0, hd[1])
	var yard_door := Vector3(yd[0], 0, yd[1])
	var s: float = maze.cell
	var ent := cell_center(Vector2i.ZERO) + Vector3(-s / 2 + 0.1, 0, 0)  # on the west outer wall
	var e: Vector2i = maze.exit
	var out := Vector3.RIGHT if e.x == maze.cols - 1 else (Vector3.BACK if e.y == maze.rows - 1 \
		else (Vector3.FORWARD if e.y == 0 else Vector3.LEFT))
	var ex := cell_center(e) + out * (s / 2 - 0.1)
	var ex_in := -out
	doors = [
		_door(house_door, Vector3.RIGHT, ent + Vector3.RIGHT * 1.6, Vector3.RIGHT, "지하실"),
		_door(ent, Vector3.RIGHT, house_door + Vector3.RIGHT * 1.6, Vector3.RIGHT, "← 집으로"),
		_door(yard_door, Vector3.BACK, ex + ex_in * 1.6, ex_in, "비상구"),
		_door(ex, ex_in, yard_door + Vector3.BACK * 1.6, Vector3.BACK, "비상구 → 뒷마당"),
	]
	for dd in doors:
		_door_mesh(dd)


## A door zone at `at` (on a wall, its room side toward `inward`); walking in sends you to
## `to`, facing `face`.
func _door(at: Vector3, inward: Vector3, to: Vector3, face: Vector3, label: String) -> Dictionary:
	var zone := AABB(at - Vector3(0.7, 0, 0.7), Vector3(1.4, 2.2, 1.4))
	return {zone = zone, to = to, face = face, yaw = atan2(-face.x, -face.z), label = label, at = at, inward = inward}


func _door_mesh(dd: Dictionary) -> void:
	var at: Vector3 = dd.at
	var inward: Vector3 = dd.inward
	var flat := Vector3(1.2, 2.1, 0.08) if absf(inward.z) > 0.5 else Vector3(0.08, 2.1, 1.2)
	var mat := Models.grain(Color("#5a2a1e") if dd.label.begins_with("비상구") else Color("#3a2618"))
	Models.box(_built, flat, at + inward * 0.12 + Vector3(0, 1.05, 0), mat)
	var sign := Label3D.new()
	sign.text = dd.label
	sign.font_size = 56
	sign.outline_size = 10
	sign.modulate = Color("#ff8a70") if dd.label.begins_with("비상구") else Color("#ffe0b0")
	sign.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	sign.position = at + inward * 0.3 + Vector3(0, 2.4, 0)
	_built.add_child(sign)


## If `p` (feet) is in a door zone, where it comes out and facing yaw; else {}.
func teleport_for(p: Vector3) -> Dictionary:
	for dd in doors:
		if dd.zone.has_point(p + Vector3.UP * 0.5):
			return {to = dd.to, yaw = dd.yaw, face = dd.face}
	return {}


func _box(size: Vector3, pos: Vector3, mat: Material) -> void:
	var sb := StaticBody3D.new()
	sb.position = pos
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	sb.add_child(col)
	Models.box(sb, size, Vector3.ZERO, mat)
	_built.add_child(sb)
