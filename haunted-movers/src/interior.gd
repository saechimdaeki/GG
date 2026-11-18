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
## Hung on walls (CC0 Poly Haven models): two kinds of framed picture and a clock.
const WALL_PROPS := ["hanging_picture_frame_01", "hanging_picture_frame_02", "hanging_picture_frame_01", "wall_clock"]
## Room wallpaper themes (tints): [paper, wainscot].
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
## Double doors hung in some doorways today: [{at, along_x, open, hinges: [Node3D, Node3D]}].
## Index = the `room_door` meta on each leaf. Same layout on every peer; open/shut is synced
## by main (set_room_door).
var room_doors: Array = []
## Doorways knocked through into one big hall today: {edge_key: true} (no wall at all).
var merged := {}
## Floor traps today: [{at, kind ("creak" | "bear"), node}]. Same on every peer.
var traps: Array = []
## The locked vault room (a dead end) and its door, or empty: {room, at, node}.
var vault := {}
## Floor footprints (xz) of today's furniture; loot spots avoid them.
var blocked: Array[Rect2] = []
var _built: Node3D
var _papers: Array = []
var _trim := Models.grain(Color("#2a1c14"))
var _floor := Models.pbr("dark_wooden_planks", Color(0.8, 0.75, 0.7), 2.5)
var _ceiling := Models.pbr("damaged_plaster", Color(0.45, 0.42, 0.4), 3.0)
var _bulb := Color("#ffc888")
var _hollow := Models.matte(Color("#050404"))
var _door_wood := Models.grain(Color("#4a3020"))
var _carpet := Models.pbr("dirty_carpet", Color(0.75, 0.6, 0.6), 1.5)
var _panel := Models.pbr("dark_wooden_planks", Color(0.45, 0.35, 0.3), 1.0)  # wainscot boards
var _wood := Models.grain(Color("#3a2618"))
var _sheet := Models.matte(Color("#c9c4b6"))


func _init() -> void:
	for t in THEMES:  # one worn wallpaper texture, tinted per room theme
		_papers.append(Models.pbr("decrepit_wallpaper", Color(t[0]).lerp(Color.WHITE, 0.45), 2.0))


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
			if Vector2i(x, y) != Vector2i.ZERO and (vault.is_empty() or Vector2i(x, y) != vault.room):
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


## Rebuilds rooms, decor, lights and doors for (seed, day, site). Same inputs → same house on
## every peer. The site (cottage / mansion / hospital) changes its size, wallpaper and bulbs.
func build(seed: int, day: int, site := "mansion") -> void:
	var c: Dictionary = Tuning.data.interior
	var st: Dictionary = Tuning.data.run.sites.get(site, Tuning.data.run.sites.mansion)
	var sizes: Array = c.sizes
	var sz: Array = sizes[clampi(day, 1, sizes.size()) - 1]
	var cols := maxi(3, int(sz[0]) + int(st.size))
	var rows := maxi(3, int(sz[1]) + int(st.size))
	maze = generate(hash([seed, day, "interior"]), cols, rows, float(c.cell), float(c.extra_openings))
	_bulb = Color(st.light)
	for i in THEMES.size():
		_papers[i].albedo_color = Color(THEMES[i][0]).lerp(Color.WHITE, 0.45) * Color(st.paper)
	if _built:
		_built.free()
	_built = Node3D.new()
	add_child(_built)
	lights.clear()
	room_kinds.clear()
	blocked.clear()
	containers.clear()
	traps.clear()
	vault = {}
	merged.clear()
	room_doors.clear()
	var door_spots: Array = []
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
	# Some doorways become no wall at all: two rooms read as one long hall (same wallpaper).
	for key: String in maze.open:
		var ab: Array = _edge_rooms(key)
		if Vector2i.ZERO in ab or maze.exit in ab or rng.randf() >= float(c.merge_chance):
			continue
		merged[key] = true
		theme[ab[1]] = theme[ab[0]]
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
				if not outside and merged.has(edge_key(here, n)):
					continue  # one hall: no wall between these two
				var gap: float = 0.0 if outside or not maze.open.has(edge_key(here, n)) else float(c.door_width)
				_wall(mid, dir, s, gap, theme[here], -1 if outside else theme[n])
				if gap > 0.0:
					door_spots.append([mid, dir.y != 0])
	for spot in door_spots:  # some doorways get a pair of doors, starting shut
		if rng.randf() < float(c.room_doors.chance):
			_room_door(spot[0], spot[1])
	_pick_vault(rng)
	for x in maze.cols:
		for y in maze.rows:
			var room := Vector2i(x, y)
			if not vault.is_empty() and room == vault.room:
				room_kinds[room] = "vault"  # bare: only what's locked in it
				continue
			_dress_room(room, rng, c)
			if room != Vector2i.ZERO and room != maze.exit and rng.randf() < float(c.traps.chance):
				_trap(room, rng, c)
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
		for side in ([-1.0, 1.0] if far_theme >= 0 else [-1.0]):
			Models.box(_built, skin, at + normal * 0.11 * side, _papers[near_theme if side < 0 else far_theme])
			_mouldings(at + normal * 0.11 * side, normal * side, len_, along_x)
	if gap > 0.0:  # lintel over the doorway, and a moulded casing round it on both faces
		var lintel := Vector3(gap, h - 2.3, 0.2) if along_x else Vector3(0.2, h - 2.3, gap)
		_box(lintel, mid + Vector3(0, 2.3 + (h - 2.3) / 2, 0), _trim)
		var across := Vector3.RIGHT if along_x else Vector3.BACK
		for side in [-1.0, 1.0]:
			var face: Vector3 = mid + normal * 0.13 * side
			for e in [-1.0, 1.0]:  # side jambs
				Models.box(_built, _sized(Vector3(0.12, 2.35, 0.05), along_x), face + across * e * (gap / 2 + 0.06) + Vector3(0, 1.175, 0), _wood)
			Models.box(_built, _sized(Vector3(gap + 0.36, 0.14, 0.06), along_x), face + Vector3(0, 2.37, 0), _wood)  # head


## A pair of panelled doors in the doorway at `mid` (wall along x or z), hinged at the jambs.
func _room_door(mid: Vector3, along_x: bool) -> void:
	var w := float(Tuning.data.interior.door_width)
	var across := Vector3.RIGHT if along_x else Vector3.BACK
	var i := room_doors.size()
	var hinges: Array = []
	for side in [-1.0, 1.0]:
		var hinge := Node3D.new()
		hinge.position = mid + across * side * (w / 2) + Vector3(0, ORIGIN.y - mid.y, 0)
		_built.add_child(hinge)
		var leaf := StaticBody3D.new()
		leaf.set_meta("room_door", i)
		leaf.position = -across * side * (w / 4) + Vector3(0, 1.15, 0)
		var col := CollisionShape3D.new()
		var shape := BoxShape3D.new()
		shape.size = _sized(Vector3(w / 2 + 0.01, 2.3, 0.07), along_x)  # the two leaves meet: no crack to see through
		col.shape = shape
		leaf.add_child(col)
		Models.box(leaf, shape.size, Vector3.ZERO, _door_wood)
		for py in [-0.5, 0.45]:  # two sunken panels on each face
			Models.box(leaf, _sized(Vector3(w / 2 - 0.25, 0.75, 0.09), along_x), Vector3(0, py, 0), _wood)
		Models.box(leaf, _sized(Vector3(0.05, 0.05, 0.14), along_x), -across * side * (w / 4 - 0.12) + Vector3(0, 0.0, 0), Models.metal())  # knob
		hinge.add_child(leaf)
		hinge.set_meta("side", side)
		hinges.append(hinge)
	room_doors.append({at = Vector3(mid.x, ORIGIN.y, mid.z), along_x = along_x, open = false, hinges = hinges})


## Every peer: swing door pair `i` open (into the rooms, 95°) or shut.
func set_room_door(i: int, open: bool) -> void:
	if i >= room_doors.size():
		return
	room_doors[i].open = open
	for h: Node3D in room_doors[i].hinges:
		h.rotation.y = (deg_to_rad(95.0) * float(h.get_meta("side"))) if open else 0.0


## The two rooms an edge key joins.
func _edge_rooms(key: String) -> Array:
	for x in maze.cols:
		for y in maze.rows:
			for d in [Vector2i.RIGHT, Vector2i.DOWN]:
				if edge_key(Vector2i(x, y), Vector2i(x, y) + d) == key:
					return [Vector2i(x, y), Vector2i(x, y) + d]
	return [Vector2i(-1, -1), Vector2i(-1, -1)]


## Every peer: container `i` has been searched — its front now gapes open (dark inside).
func mark_searched(i: int) -> void:
	if i >= containers.size() or not is_instance_valid(containers[i].node):
		return
	var c: Dictionary = containers[i]
	var hole := Models.box(c.node, Vector3(c.size.x * 0.7, c.size.y * 0.55, 0.02), Vector3(0, 0.05, -c.size.z / 2 - 0.015), _hollow)
	hole.name = "Opened"


## The vault: the deepest dead-end room (one doorway) that isn't the entrance or the fire exit.
## Its doorway gets a heavy locked door (meta "vault"); main.unlock_vault opens it.
func _pick_vault(rng: RandomNumberGenerator) -> void:
	var best := Vector2i(-1, -1)
	for room: Vector2i in maze.dist:
		if room == Vector2i.ZERO or room == maze.exit:
			continue
		var ways: Array = []
		for dir in [Vector2i.RIGHT, Vector2i.DOWN, Vector2i.LEFT, Vector2i.UP]:
			if maze.open.has(edge_key(room, room + dir)):
				ways.append(dir)
		if ways.size() == 1 and not merged.has(edge_key(room, room + ways[0])) and (best.x < 0 or maze.dist[room] > maze.dist[best]):
			best = room
			vault = {room = room, dir = ways[0]}
	if best.x < 0:
		vault = {}
		return
	var out := Vector3(vault.dir.x, 0, vault.dir.y)
	var at: Vector3 = cell_center(best) + out * (maze.cell / 2)
	var door := StaticBody3D.new()
	door.position = at + Vector3(0, 1.15, 0)
	door.set_meta("vault", true)
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	var along_x := absf(out.z) > 0.5
	shape.size = _sized(Vector3(float(Tuning.data.interior.door_width), 2.3, 0.25), along_x)
	col.shape = shape
	door.add_child(col)
	var iron := Models.metal()
	iron.albedo_color = Color("#4a4a50")
	Models.box(door, shape.size, Vector3.ZERO, iron)
	for k in 3:  # rivet bands
		Models.box(door, _sized(Vector3(shape.size.x if along_x else shape.size.z, 0.08, 0.3), along_x), Vector3(0, -0.8 + k * 0.8, 0), Models.metal())
	_built.add_child(door)
	vault.at = at
	vault.node = door


## One floor trap somewhere in `room`, clear of furniture: a creaky board (calls the ghost)
## or a rusty bear trap (snaps shut: stuck for a few seconds).
func _trap(room: Vector2i, rng: RandomNumberGenerator, c: Dictionary) -> void:
	var center := cell_center(room)
	for attempt in 8:
		var p := center + Vector3(rng.randf_range(-2.0, 2.0), 0, rng.randf_range(-2.0, 2.0))
		if blocked.any(func(b: Rect2) -> bool: return b.grow(0.4).has_point(Vector2(p.x, p.z))):
			continue
		var bear := rng.randf() < float(c.traps.bear_share)
		var node := Node3D.new()
		node.position = p
		_built.add_child(node)
		if bear:  # two toothed jaws, open
			var rust := Models.metal()
			rust.albedo_color = Color("#5a4030")
			Models.cylinder(node, 0.22, 0.22, 0.03, Vector3(0, 0.015, 0), rust)
			for sx in [-1, 1]:
				var jaw := Models.box(node, Vector3(0.44, 0.1, 0.03), Vector3(0, 0.05, sx * 0.2), rust)
				jaw.set_meta("jaw", sx)
		else:  # a loose, darker board
			Models.box(node, Vector3(1.2, 0.025, 0.22), Vector3(0, 0.012, 0), Models.grain(Color("#2a1a10")))
		traps.append({at = p, kind = "bear" if bear else "creak", node = node})
		blocked.append(Rect2(p.x - 0.35, p.z - 0.35, 0.7, 0.7))  # no loot on top of it
		return


## Every peer: the vault door swings away (opened with the key).
func open_vault() -> void:
	if not vault.is_empty() and is_instance_valid(vault.node):
		vault.node.queue_free()


## Every peer: bear trap `i` snaps shut (jaws stand up).
func snap_trap(i: int) -> void:
	if i < traps.size() and is_instance_valid(traps[i].node):
		for j in traps[i].node.get_children():
			if j.has_meta("jaw"):
				j.rotation.x = j.get_meta("jaw") * 1.2
				j.position.z *= 0.2


## Old-house wall trim on one wall face (`at` = the face's centre at mid-height, `out` = the
## direction it faces): dark wood wainscot to 1 m with a rail, a skirting board and a crown
## moulding under the ceiling. Mesh only.
func _mouldings(at: Vector3, out: Vector3, len_: float, along_x: bool) -> void:
	var h := height()
	var base := Vector3(at.x, ORIGIN.y, at.z)
	var l := len_ - 0.04
	Models.box(_built, _sized(Vector3(l, 1.0, 0.03), along_x), base + out * 0.02 + Vector3(0, 0.5, 0), _panel)
	Models.box(_built, _sized(Vector3(l, 0.06, 0.06), along_x), base + out * 0.04 + Vector3(0, 1.0, 0), _wood)   # rail
	Models.box(_built, _sized(Vector3(l, 0.16, 0.05), along_x), base + out * 0.04 + Vector3(0, 0.08, 0), _wood)  # skirting
	Models.box(_built, _sized(Vector3(l, 0.14, 0.08), along_x), base + out * 0.05 + Vector3(0, h - 0.07, 0), _wood)  # crown


## A box sized for a wall running along x (as given) or along z (x and z swapped).
static func _sized(v: Vector3, along_x: bool) -> Vector3:
	return v if along_x else Vector3(v.z, v.y, v.x)


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
		Models.box(_built, Vector3(2.6, 0.02, 1.8), center + Vector3(0, 0.01, 0), _carpet)
	var exit: bool = room == maze.exit
	if room == Vector2i.ZERO or exit or rng.randf() < c.light_chance:
		var l := OmniLight3D.new()
		l.position = center + Vector3(0, height() - 1.0, 0)  # just under the chandelier, so it does not shadow itself
		l.omni_range = s * 0.95
		l.light_energy = 1.0 if not exit else 0.9
		l.light_color = Color("#ff2a1a") if exit else _bulb
		l.shadow_enabled = true
		l.set_meta("emergency", exit)
		_built.add_child(l)
		lights.append(l)
		var lamp: Node3D = (load("res://assets/models/polyhaven/chinese_chandelier/chinese_chandelier.gltf") as PackedScene).instantiate()
		lamp.position = Vector3(center.x, ORIGIN.y + height() - 0.75, center.z)  # hangs from the ceiling
		_built.add_child(lamp)


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
		containers.append({at = Vector3(at.x, ORIGIN.y, at.z), front = front * (size.z / 2 + 0.1), label = item.label, node = sb, size = tall})
	_built.add_child(sb)
	var r := maxf(tall.x, tall.z) / 2  # ponytail: a square around the piece, fine for spawn checks
	blocked.append(Rect2(at.x - r, at.z - r, r * 2, r * 2))


## A framed painting on the wall facing into the room: dark oils in a gilt frame.
func _painting(at: Vector3, out: Vector3, rng: RandomNumberGenerator) -> void:
	var pick: String = WALL_PROPS[rng.randi_range(0, WALL_PROPS.size() - 1)]
	var m: Node3D = (load("res://assets/models/polyhaven/%s/%s.gltf" % [pick, pick]) as PackedScene).instantiate()
	m.position = Vector3(at.x, ORIGIN.y + rng.randf_range(1.5, 1.8), at.z) - out * 0.02
	m.rotation.y = atan2(-out.x, -out.z)  # model front (+z) faces into the room
	_built.add_child(m)


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
