extends Node3D
## The mansion's inside (저택 내부), rebuilt every day from the run seed: a grid of dark rooms
## joined by doorways, each furnished as a living room, bedroom, kitchen, study, storeroom or bath. It sits far below the
## yard; doors you use with E move you between it and the outside:
##   front door of the mansion  ⇄ entrance room (0, 0)
##   side "비상구" of the mansion ⇄ the room farthest from the entrance
## Walls/floor/decor are built identically on every peer from (seed, day), so nothing is synced.

const Models = preload("res://src/models.gd")
const Tuning = preload("res://src/tuning.gd")

## Grid corner (room (0, 0) starts here, +x columns, +z rows).
const ORIGIN := Vector3(-20, -40, 30)
## Room wallpaper themes: [paper, wainscot].
const THEMES := [["#4a6a46", "#2a1e16"], ["#7a3036", "#2a1a14"], ["#3a4a78", "#1e1a18"],
	["#8a7440", "#2e2218"], ["#6a4a6a", "#22181c"]]

## Last generated layout (see `generate`). Empty until `build`.
var maze := {}
## Doors: [{at, inward, to, face, yaw, label}]. `at` is on the wall; E on its slab uses it.
var doors: Array = []
## Interior lights (die at the blackout, except the fire-exit lamp).
var lights: Array[OmniLight3D] = []
## Room → its type ("living", "bedroom", …), for today's layout.
var room_kinds := {}
## Searchable furniture today: [{at (its centre on the floor), front (centre → just past its face), label}].
## Index = the `container` meta on its StaticBody; same order on every peer.
var containers: Array = []
## Floor footprints (xz) of today's furniture; loot spots avoid them.
var blocked: Array[Rect2] = []
var _built: Node3D
var _papers: Array = []
var _trim := Models.grain(Color("#2a1c14"))
var _floor := Models.surface(0, Color("#4a3424"), Color.BLACK)
var _ceiling := Models.matte(Color("#1a1716"))
var _sheet := Models.matte(Color("#c9c4b6"))
var _gilt := Models.matte(Color("#6a5228"))


func _init() -> void:
	for t in THEMES:
		var m := Models.surface(1, Color(t[0]), Color(t[1]), ORIGIN.y)
		m.set_shader_parameter("interior_rect", Vector4(-1e4, -1e4, 1e4, 1e4))  # always wallpaper
		_papers.append(m)


static func height() -> float:
	return float(Tuning.data.interior.height)


## Pure maze: random depth-first spanning tree over cols × rows rooms, then `extra` of the
## remaining walls opened for loops. Returns {cols, rows, cell, open: {key: true}, exit: Vector2i,
## dist: {Vector2i: int}}. Every room is reachable from (0, 0).
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


## World position of a room's floor centre.
func cell_center(c: Vector2i) -> Vector3:
	var s: float = maze.cell
	return ORIGIN + Vector3((c.x + 0.5) * s, 0, (c.y + 0.5) * s)


## True if `p` is inside the mansion volume.
func contains(p: Vector3) -> bool:
	if maze.is_empty():
		return false
	var s: float = maze.cell
	return p.y < ORIGIN.y + height() + 1.0 and p.y > ORIGIN.y - 2.0 \
		and p.x >= ORIGIN.x and p.z >= ORIGIN.z and p.x <= ORIGIN.x + maze.cols * s and p.z <= ORIGIN.z + maze.rows * s


## Which room `p` is in (clamped).
func cell_of(p: Vector3) -> Vector2i:
	var s: float = maze.cell
	return Vector2i(clampi(floori((p.x - ORIGIN.x) / s), 0, maze.cols - 1), clampi(floori((p.z - ORIGIN.z) / s), 0, maze.rows - 1))


## Floor spots for `n` pieces of loot, each [position, room], shuffled by `seed`. Spots keep to
## the middle of a room (decor stands along the walls) and never land on each other.
func loot_spots(seed: int, n: int) -> Array:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	var rooms: Array = []
	for x in maze.cols:
		for y in maze.rows:
			if Vector2i(x, y) != Vector2i.ZERO:
				rooms.append(Vector2i(x, y))
	var out := []
	var i := 0
	while out.size() < n and i < n * 60:
		if i % rooms.size() == 0:
			for k in range(rooms.size() - 1, 0, -1):  # Fisher-Yates, a fresh pass each lap
				var j := rng.randi_range(0, k)
				var t = rooms[k]
				rooms[k] = rooms[j]
				rooms[j] = t
		var room: Vector2i = rooms[i % rooms.size()]
		i += 1
		var p := cell_center(room) + Vector3(rng.randf_range(-2.2, 2.2), 0, rng.randf_range(-2.2, 2.2))
		var clear := blocked.all(func(b: Rect2) -> bool: return not b.grow(0.35).has_point(Vector2(p.x, p.z)))
		if clear and out.all(func(o: Array) -> bool: return o[0].distance_to(p) > 1.2):
			out.append([p, room])
	return out


## Rebuilds rooms, decor, lights and doors for (seed, day). Same inputs → same mansion on every peer.
func build(seed: int, day: int) -> void:
	var c: Dictionary = Tuning.data.interior
	var sizes: Array = c.sizes
	var sz: Array = sizes[clampi(day, 1, sizes.size()) - 1]
	maze = generate(hash([seed, day, "interior"]), int(sz[0]), int(sz[1]), float(c.cell), float(c.extra_openings))
	if _built:
		_built.free()
	_built = Node3D.new()
	add_child(_built)
	lights.clear()
	room_kinds.clear()
	blocked.clear()
	containers.clear()
	var s: float = maze.cell
	var h := height()
	var w: float = maze.cols * s
	var d: float = maze.rows * s
	var rng := RandomNumberGenerator.new()
	rng.seed = hash([seed, day, "rooms"])
	var theme := {}
	for x in maze.cols:
		for y in maze.rows:
			theme[Vector2i(x, y)] = rng.randi_range(0, _papers.size() - 1)
	_box(Vector3(w, 0.2, d), ORIGIN + Vector3(w / 2, -0.1, d / 2), _floor)
	_box(Vector3(w, 0.2, d), ORIGIN + Vector3(w / 2, h + 0.1, d / 2), _ceiling)
	# Every wall segment: a collision box, plus a thin wallpaper skin on each face in that
	# side's room theme. Outer walls have one room side.
	for x in maze.cols:
		for y in maze.rows:
			var here := Vector2i(x, y)
			for dir in [Vector2i.RIGHT, Vector2i.DOWN, Vector2i.LEFT, Vector2i.UP]:
				var n: Vector2i = here + dir
				var outside: bool = n.x < 0 or n.y < 0 or n.x >= maze.cols or n.y >= maze.rows
				if not outside and (dir == Vector2i.LEFT or dir == Vector2i.UP):
					continue  # inner walls are built once, from their left/top room
				var mid := cell_center(here) + Vector3(dir.x, 0, dir.y) * (s / 2)
				var gap: float = 0.0 if outside or not maze.open.has(edge_key(here, n)) else float(c.door_width)
				_wall(mid, dir, s, gap, theme[here], -1 if outside else theme[n])
	for x in maze.cols:
		for y in maze.rows:
			_dress_room(Vector2i(x, y), rng, c)
	_make_doors()


## One wall between `mid - dir` and `mid + dir` sides, `s` long, with a doorway `gap` wide.
func _wall(mid: Vector3, dir: Vector2i, s: float, gap: float, near_theme: int, far_theme: int) -> void:
	var h := height()
	var along_x := dir.y != 0  # wall runs along x when it separates rows
	var normal := Vector3(dir.x, 0, dir.y)
	var pieces: Array = [[0.0, s]] if gap <= 0.0 else [[-(s + gap) / 4.0, (s - gap) / 2.0], [(s + gap) / 4.0, (s - gap) / 2.0]]
	for pc in pieces:
		var off: Vector3 = (Vector3.RIGHT if along_x else Vector3.BACK) * float(pc[0])
		var len_: float = pc[1]
		var size := Vector3(len_, h, 0.2) if along_x else Vector3(0.2, h, len_)
		var at: Vector3 = mid + off + Vector3(0, h / 2, 0)
		_box(size, at, _trim)
		var skin := Vector3(len_ - 0.02, h, 0.02) if along_x else Vector3(0.02, h, len_ - 0.02)
		Models.box(_built, skin, at - normal * 0.11, _papers[near_theme])
		if far_theme >= 0:
			Models.box(_built, skin, at + normal * 0.11, _papers[far_theme])
	if gap > 0.0:  # lintel over the doorway
		var lintel := Vector3(gap, h - 2.3, 0.2) if along_x else Vector3(0.2, h - 2.3, gap)
		_box(lintel, mid + Vector3(0, 2.3 + (h - 2.3) / 2, 0), _trim)


## Furnishes one room by its type (living room, bedroom, kitchen, study, storage, bath — see
## `room_types` in tuning): furniture packed along each closed wall, maybe a table with chairs
## in the middle, paintings, a rug and maybe a dim bulb. Everything solid is also recorded in
## `blocked`, so loot never spawns inside furniture.
func _dress_room(room: Vector2i, rng: RandomNumberGenerator, c: Dictionary) -> void:
	var s: float = maze.cell
	var center := cell_center(room)
	var kind := "hall" if room == Vector2i.ZERO else _room_type(rng)  # the entrance is a clear hall
	var t: Dictionary = Tuning.data.room_types[kind]
	room_kinds[room] = kind
	var closed: Array[Vector2i] = []
	for dir in [Vector2i.RIGHT, Vector2i.DOWN, Vector2i.LEFT, Vector2i.UP]:
		var n: Vector2i = room + dir
		var outside: bool = n.x < 0 or n.y < 0 or n.x >= maze.cols or n.y >= maze.rows
		if (outside or not maze.open.has(edge_key(room, n))) and not _door_wall(room, dir):
			closed.append(dir)
	for dir in closed:
		var out := Vector3(dir.x, 0, dir.y)
		var side := out.cross(Vector3.UP)
		var room_left: float = (s - 0.4) * float(c.wall_fill)
		var cursor := -(s - 0.4) / 2.0 + rng.randf_range(0.0, 0.6)
		var placed_any := false
		while true:
			var item := _decor(t.walls[rng.randi_range(0, t.walls.size() - 1)])
			var size := Vector3(item.size[0], item.size[1], item.size[2])
			if size.x > room_left or cursor + size.x > (s - 0.4) / 2.0:
				break
			var stack: bool = rng.randf() < float(t.get("stack", 0.0)) and str(item.name).begins_with("boxes")
			var at := center + out * (s / 2 - 0.12 - size.z / 2) + side * (cursor + size.x / 2)
			_solid(item, size, at, atan2(out.x, out.z), rng, c, stack)
			placed_any = true
			cursor += size.x + rng.randf_range(0.1, 0.5)
			room_left -= size.x
		if rng.randf() < float(c.painting_chance) or not placed_any:
			_painting(center + out * (s / 2 - 0.13) + side * rng.randf_range(-1.6, 1.6), out, rng)
	if str(t.center) != "":  # ponytail: monsters walk through it (they don't collide); offset it if that shows
		var item := _decor(t.center)
		var size := Vector3(item.size[0], item.size[1], item.size[2])
		var yaw := rng.randi_range(0, 1) * PI / 2
		_solid(item, size, center, yaw, rng, c, false)
		var chair := _decor("chair")
		var csize := Vector3(chair.size[0], chair.size[1], chair.size[2])
		for i in int(t.chairs):  # chairs around it, facing in
			var a := yaw + i * TAU / maxf(t.chairs, 1.0) + rng.randf_range(-0.2, 0.2)
			var off := Vector3(sin(a), 0, cos(a)) * (maxf(size.x, size.z) / 2 + 0.35)
			_solid(chair, csize, center + off, a, rng, c, false)
	if rng.randf() < 0.5:
		var rug := Models.matte(Color(THEMES[rng.randi_range(0, THEMES.size() - 1)][0]).darkened(0.2))
		Models.box(_built, Vector3(2.6, 0.02, 1.8), center + Vector3(0, 0.01, 0), rug)
	var exit: bool = room == maze.exit
	if room == Vector2i.ZERO or exit or rng.randf() < c.light_chance:
		var l := OmniLight3D.new()
		l.position = center + Vector3(0, height() - 0.5, 0)
		l.omni_range = s * 0.95
		l.light_energy = 1.0 if not exit else 0.9
		l.light_color = Color("#ff2a1a") if exit else Color("#ffc888")
		l.shadow_enabled = true
		l.set_meta("emergency", exit)
		_built.add_child(l)
		lights.append(l)
		var bulb := StandardMaterial3D.new()
		bulb.emission_enabled = true
		bulb.emission = l.light_color
		bulb.emission_energy_multiplier = 3.0
		Models.cylinder(_built, 0.07, 0.07, 0.14, l.position + Vector3(0, 0.15, 0), bulb)
		Models.cylinder(_built, 0.01, 0.01, 0.35, l.position + Vector3(0, 0.38, 0), _ceiling)


## True if one of the two teleport doors sits on this room's wall `dir`.


func _room_type(rng: RandomNumberGenerator) -> String:
	var types: Dictionary = Tuning.data.room_types
	var total := 0.0
	for k in types:
		total += float(types[k].weight)
	var roll := rng.randf() * total
	for k in types:
		roll -= float(types[k].weight)
		if roll <= 0.0:
			return k
	return types.keys()[0]


func _decor(name: String) -> Dictionary:
	for d: Dictionary in Tuning.data.decor:
		if d.name == name:
			return d
	return Tuning.data.decor[0]


## One solid piece of furniture standing on the floor at `at` (xz), turned `yaw` (its front,
## -z, faces away from its back). Sometimes under a dust sheet; boxes may be stacked two high.
func _solid(item: Dictionary, size: Vector3, at: Vector3, yaw: float, rng: RandomNumberGenerator,
		c: Dictionary, stack: bool) -> void:
	var tall := size * Vector3(1, 2 if stack else 1, 1)
	var sb := StaticBody3D.new()
	sb.position = Vector3(at.x, ORIGIN.y + tall.y / 2, at.z)
	sb.rotation.y = yaw
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = tall
	col.shape = shape
	sb.add_child(col)
	if rng.randf() < float(c.sheet_chance) and not stack:
		Models.box(sb, size + Vector3(0.06, 0.03, 0.06), Vector3(0, 0.015, 0), _sheet)   # dust sheet
		Models.box(sb, Vector3(size.x + 0.1, 0.25, size.z + 0.1), Vector3(0, -size.y / 2 + 0.12, 0), _sheet)  # its hem
	else:
		for k in (2 if stack else 1):
			var m := Models.furniture(item.name, size, Color(item.color).darkened(0.25), item.get("model", ""), item.get("yaw", 0.0))
			m.position.y = -tall.y / 2 + size.y / 2 + k * size.y
			m.rotation.y = k * rng.randf_range(-0.4, 0.4)
			sb.add_child(m)
	if item.get("label", "") != "":  # searchable: E opens it (main.search)
		sb.set_meta("container", containers.size())
		var front := Vector3(-sin(yaw), 0, -cos(yaw))  # its -z, out into the room
		containers.append({at = Vector3(at.x, ORIGIN.y, at.z), front = front * (size.z / 2 + 0.1), label = item.label})
	_built.add_child(sb)
	var r := maxf(tall.x, tall.z) / 2  # ponytail: a square around the piece, fine for spawn checks
	blocked.append(Rect2(at.x - r, at.z - r, r * 2, r * 2))


## A framed painting on the wall facing into the room: dark oils in a gilt frame.
func _painting(at: Vector3, out: Vector3, rng: RandomNumberGenerator) -> void:
	var w := rng.randf_range(0.6, 1.2)
	var h := rng.randf_range(0.5, 0.9)
	var along_x := absf(out.z) > 0.5
	var y := ORIGIN.y + rng.randf_range(1.6, 1.9)
	var frame := Vector3(w + 0.12, h + 0.12, 0.05) if along_x else Vector3(0.05, h + 0.12, w + 0.12)
	var canvas := Vector3(w, h, 0.06) if along_x else Vector3(0.06, h, w)
	Models.box(_built, frame, Vector3(at.x, y, at.z), _gilt)
	var oil := Models.matte(Color.from_hsv(rng.randf(), rng.randf_range(0.3, 0.6), rng.randf_range(0.15, 0.35)))
	Models.box(_built, canvas, Vector3(at.x, y, at.z) - out * 0.01, oil)


func _door_wall(room: Vector2i, dir: Vector2i) -> bool:
	return (room == Vector2i.ZERO and dir == Vector2i.LEFT) or (room == maze.exit and dir == _exit_dir())


func _exit_dir() -> Vector2i:
	var e: Vector2i = maze.exit
	if e.x == maze.cols - 1:
		return Vector2i.RIGHT
	if e.y == maze.rows - 1:
		return Vector2i.DOWN
	if e.y == 0:
		return Vector2i.UP
	return Vector2i.LEFT


## Doors: mansion front ⇄ entrance room (its west wall), side fire door ⇄ the exit room.
func _make_doors() -> void:
	var c: Dictionary = Tuning.data.interior
	var fd: Array = c.front_door
	var fe: Array = c.fire_door
	var front := Vector3(fd[0], 0, fd[1])
	var fire := Vector3(fe[0], 0, fe[1])
	var fire_out := Vector3(fe[2], 0, fe[3])
	var s: float = maze.cell
	var ent := cell_center(Vector2i.ZERO) + Vector3(-s / 2 + 0.12, 0, 0)
	var ed := _exit_dir()
	var out := Vector3(ed.x, 0, ed.y)
	var ex := cell_center(maze.exit) + out * (s / 2 - 0.12)
	doors = [
		_door(front, Vector3.FORWARD, ent + Vector3.RIGHT * 1.2, Vector3.RIGHT, "저택으로 들어가기"),
		_door(ent, Vector3.RIGHT, front + Vector3.FORWARD * 1.2, Vector3.FORWARD, "밖으로 나가기 (현관)"),
		_door(fire, fire_out, ex - out * 1.2, -out, "비상구로 들어가기"),
		_door(ex, -out, fire + fire_out * 1.2, fire_out, "비상구로 나가기"),
	]
	for i in doors.size():
		_door_slab(i)


## `at` on a wall, room/yard side toward `inward`; using it sends you to `to`, facing `face`.
func _door(at: Vector3, inward: Vector3, to: Vector3, face: Vector3, label: String) -> Dictionary:
	return {at = at, inward = inward, to = to, face = face, yaw = atan2(-face.x, -face.z), label = label}


## A solid door slab you can aim at (meta "door" = index), with a frame and a small lamp.
func _door_slab(i: int) -> void:
	var dd: Dictionary = doors[i]
	var at: Vector3 = dd.at
	var inward: Vector3 = dd.inward
	var flat := Vector3(1.3, 2.2, 0.1) if absf(inward.z) > 0.5 else Vector3(0.1, 2.2, 1.3)
	var fire: bool = dd.label.begins_with("비상구")
	var sb := StaticBody3D.new()
	sb.position = at + inward * 0.1 + Vector3(0, 1.1, 0)
	sb.set_meta("door", i)
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = flat
	col.shape = shape
	sb.add_child(col)
	Models.box(sb, flat, Vector3.ZERO, Models.matte(Color("#6a1e14") if fire else Color("#3a2618")))
	var knob := inward.cross(Vector3.UP).normalized() * 0.45 + inward * 0.07
	Models.cylinder(sb, 0.04, 0.04, 0.06, knob, Models.metal())
	_built.add_child(sb)
	var frame := Vector3(1.5, 2.35, 0.08) if absf(inward.z) > 0.5 else Vector3(0.08, 2.35, 1.5)
	Models.box(_built, frame, at + inward * 0.06 + Vector3(0, 1.175, 0), _trim)
	var lamp := StandardMaterial3D.new()
	lamp.emission_enabled = true
	lamp.emission = Color("#ff3a20") if fire else Color("#ffd9a0")
	lamp.emission_energy_multiplier = 2.5
	Models.box(_built, Vector3(0.3, 0.1, 0.1), at + inward * 0.12 + Vector3(0, 2.55, 0), lamp)


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
