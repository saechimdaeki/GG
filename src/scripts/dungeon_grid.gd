class_name DungeonGrid
extends Node2D
## Tile grid of the dungeon, generated fresh every run: rooms joined by corridors, a core room
## on the right, three entrances (left/top/bottom), terrain (water slows heroes, lava burns them),
## pillars and decor. Owns wall collision, hero pathfinding (AStarGrid2D), trap and lava ticks.

const TILE := 16
const W := 60
const H := 36
const WALL_TOP := Color(0.2, 0.18, 0.26)
const VOID := Color(0.03, 0.025, 0.05)
const EDGE := Color(0.75, 0.68, 0.85, 0.55)
const FLOOR_TINT := Color(1.3, 1.22, 1.15)
const DIRS: Array[Vector2i] = [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]

## Set before add_child for a reproducible map; 0 = random.
var seed_value: int = 0
var entrances: Array[Vector2i] = []
var core := Vector2i.ZERO
var rooms: Array[Rect2i] = []
## Vector2i -> "water" | "lava" (keys of Units.TERRAIN).
var terrain: Dictionary = {}
## Vector2i -> "bones" | "crate" | "skull" (cosmetic, walkable).
var decor: Dictionary = {}
## Vector2i -> trap kind (key of Units.TRAPS).
var traps: Dictionary = {}
## Traps and lava deal damage only while true (raid phase).
var traps_active := false
## Extra damage on every trap (shop upgrade).
var trap_bonus: int = 0
## Entrances heroes will use this wave (indices into entrances), highlighted during build.
var active_entrances: Array[int] = [0]
## Build cursor, drawn as an outline while hover_visible.
var hover_cell := Vector2i(-1, -1)
var hover_ok := false
var hover_visible := false
## Hero routes from each active entrance to the core, dotted during build.
var preview_paths: Array[PackedVector2Array] = []
## Core HP fraction for the ring drawn around the gem (set by the game).
var core_ratio: float = 1.0

var floor_tex: Texture2D = Sprites.tex("floor")
var wall_tex: Texture2D = Sprites.tex("wall")
var _torches: Array[PointLight2D] = []
var _time: float = 0.0
var _floor: PackedByteArray
var _astar := AStarGrid2D.new()
var _walls: StaticBody2D
var _trap_clock: Dictionary = {}
var _lava_clock: float = 0.0


func _ready() -> void:
	add_to_group("dungeon")
	_floor.resize(W * H)
	_walls = StaticBody2D.new()
	_walls.collision_layer = 4
	_walls.collision_mask = 0
	add_child(_walls)
	_astar.region = Rect2i(0, 0, W, H)
	_astar.cell_size = Vector2(TILE, TILE)
	_astar.offset = position + Vector2(TILE, TILE) / 2.0
	_astar.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	_astar.update()
	_generate()
	_rebuild()
	_add_lights()


# --- generation -----------------------------------------------------------

func _generate() -> void:
	var rng := RandomNumberGenerator.new()
	if seed_value != 0:
		rng.seed = seed_value
	else:
		rng.randomize()
	var cw := rng.randi_range(7, 8)
	var ch := rng.randi_range(7, 9)
	var core_room := Rect2i(rng.randi_range(W - 14, W - 2 - cw), rng.randi_range(4, H - 4 - ch), cw, ch)
	rooms.append(core_room)
	for i in 1200:
		if rooms.size() >= 13:
			break
		var r := Rect2i(rng.randi_range(2, W - 18), rng.randi_range(2, H - 8),
			rng.randi_range(5, 10), rng.randi_range(4, 8))
		if r.end.x > W - 2 or r.end.y > H - 2:
			continue
		var free := true
		for o in rooms:
			if o.grow(2).intersects(r):
				free = false
				break
		if free:
			rooms.append(r)
	for r in rooms:
		_carve(r)
	# Prim-style: link each room to the closest already-linked one, then add one loop.
	var linked: Array[int] = [0]
	var rest: Array[int] = []
	for i in range(1, rooms.size()):
		rest.append(i)
	while not rest.is_empty():
		var best := Vector2i(-1, -1)
		var best_d := INF
		for a in linked:
			for b in rest:
				var d := Vector2(_center(a)).distance_to(Vector2(_center(b)))
				if d < best_d:
					best_d = d
					best = Vector2i(a, b)
		_corridor(_center(best.x), _center(best.y), rng)
		linked.append(best.y)
		rest.erase(best.y)
	if rooms.size() > 3:
		_corridor(_center(rng.randi_range(1, rooms.size() - 1)), _center(rng.randi_range(1, rooms.size() - 1)), rng)
	core = _center(0)
	for side in 3:
		var e: Vector2i
		match side:
			0: e = Vector2i(1, rng.randi_range(4, H - 5))
			1: e = Vector2i(rng.randi_range(4, W - 24), 1)
			_: e = Vector2i(rng.randi_range(4, W - 24), H - 2)
		var nearest := 1
		for i in range(1, rooms.size()):
			if Vector2(_center(i)).distance_to(Vector2(e)) < Vector2(_center(nearest)).distance_to(Vector2(e)):
				nearest = i
		_corridor(e, _center(nearest), rng)
		entrances.append(e)
	_sync_astar()
	for i in range(1, rooms.size()):
		_decorate(rooms[i], rng)


func _center(i: int) -> Vector2i:
	return rooms[i].position + rooms[i].size / 2


func _carve(r: Rect2i) -> void:
	for y in range(maxi(r.position.y, 1), mini(r.end.y, H - 1)):
		for x in range(maxi(r.position.x, 1), mini(r.end.x, W - 1)):
			_floor[y * W + x] = 1


## Two-wide L-shaped corridor between two cells.
func _corridor(a: Vector2i, b: Vector2i, rng: RandomNumberGenerator) -> void:
	var corner := Vector2i(b.x, a.y) if rng.randf() < 0.5 else Vector2i(a.x, b.y)
	for seg: Array in [[a, corner], [corner, b]]:
		var p: Vector2i = seg[0]
		var q: Vector2i = seg[1]
		_carve(Rect2i(mini(p.x, q.x), mini(p.y, q.y), absi(p.x - q.x) + 2, absi(p.y - q.y) + 2))


func _decorate(r: Rect2i, rng: RandomNumberGenerator) -> void:
	var inner := r.grow(-1)
	var roll := rng.randf()
	if roll < 0.28 and inner.size.x >= 2 and inner.size.y >= 2:
		_pool("water", inner, rng, Vector2i(rng.randi_range(2, 4), rng.randi_range(2, 3)))
	elif roll < 0.48 and inner.size.x >= 2 and inner.size.y >= 2:
		_pool("lava", inner, rng, Vector2i(rng.randi_range(1, 2), rng.randi_range(1, 2)))
	elif roll < 0.75 and r.size.x >= 5 and r.size.y >= 4:
		for p: Vector2i in [inner.position, Vector2i(inner.end.x - 1, inner.position.y),
				Vector2i(inner.position.x, inner.end.y - 1), inner.end - Vector2i.ONE]:
			_try_pillar(p)
	for i in rng.randi_range(1, 3):
		var c := Vector2i(rng.randi_range(r.position.x, r.end.x - 1), rng.randi_range(r.position.y, r.end.y - 1))
		if is_buildable_floor(c) and not terrain.has(c):
			decor[c] = ["bones", "crate", "skull"][rng.randi_range(0, 2)]


func _pool(kind: String, inner: Rect2i, rng: RandomNumberGenerator, size: Vector2i) -> void:
	size = size.min(inner.size)
	var o := Vector2i(rng.randi_range(inner.position.x, inner.end.x - size.x), rng.randi_range(inner.position.y, inner.end.y - size.y))
	for y in size.y:
		for x in size.x:
			var c := o + Vector2i(x, y)
			if is_buildable_floor(c):
				terrain[c] = kind


func _try_pillar(c: Vector2i) -> void:
	if not is_buildable_floor(c) or terrain.has(c):
		return
	_floor[c.y * W + c.x] = 0
	_astar.set_point_solid(c, true)
	if not all_connected():
		_floor[c.y * W + c.x] = 1
		_astar.set_point_solid(c, false)


func _sync_astar() -> void:
	for y in H:
		for x in W:
			_astar.set_point_solid(Vector2i(x, y), is_wall(Vector2i(x, y)))


func _add_lights() -> void:
	var core_light := Fx.light(Color(1.0, 0.25, 0.35), 2.0, 2.6)
	core_light.position = cell_center(core) - global_position
	add_child(core_light)
	for e in entrances:
		var gate_light := Fx.light(Color(0.35, 0.55, 1.0), 1.0, 1.0)
		gate_light.position = cell_center(e) - global_position
		add_child(gate_light)
	var lit := {}
	for c: Vector2i in terrain:
		if terrain[c] == "lava" and not lit.has(c / 2):
			lit[c / 2] = true
			var glow := Fx.light(Color(1.0, 0.45, 0.1), 1.3, 0.8)
			glow.position = cell_center(c) - global_position
			add_child(glow)
	for y in H:
		for x in W:
			var c := Vector2i(x, y)
			if (x + y * 3) % 7 == 0 and is_wall(c) and in_bounds(c + Vector2i.DOWN) and not is_wall(c + Vector2i.DOWN):
				var torch := Fx.light(Color(1.0, 0.6, 0.25), 1.0, 1.1)
				torch.position = Vector2(c) * TILE + Vector2(TILE / 2.0, TILE)
				add_child(torch)
				_torches.append(torch)


# --- queries --------------------------------------------------------------

## Grid cell under a world position.
func cell_at(world: Vector2) -> Vector2i:
	return Vector2i(((world - global_position) / TILE).floor())


## World position of a cell's center.
func cell_center(c: Vector2i) -> Vector2:
	return global_position + Vector2(c) * TILE + Vector2(TILE, TILE) / 2.0


func in_bounds(c: Vector2i) -> bool:
	return c.x >= 0 and c.y >= 0 and c.x < W and c.y < H


func is_wall(c: Vector2i) -> bool:
	return not in_bounds(c) or _floor[c.y * W + c.x] == 0


func is_wall_at(world: Vector2) -> bool:
	return is_wall(cell_at(world))


func core_position() -> Vector2:
	return cell_center(core)


func entrance_position(i: int = 0) -> Vector2:
	return cell_center(entrances[i])


## "water", "lava" or "" under a world position.
func terrain_at(world: Vector2) -> String:
	return terrain.get(cell_at(world), "")


## Floor cell that is neither an entrance nor the core: where builds may go.
func is_buildable_floor(c: Vector2i) -> bool:
	return not is_wall(c) and not entrances.has(c) and c != core


## True when every entrance can still reach the core.
func all_connected() -> bool:
	for e in entrances:
		if _astar.get_id_path(e, core).is_empty():
			return false
	return true


## World-space path between two points, empty if unreachable.
func find_path(from_world: Vector2, to_world: Vector2) -> PackedVector2Array:
	var a := cell_at(from_world)
	var b := cell_at(to_world)
	if is_wall(a) or is_wall(b):
		return PackedVector2Array()
	return _astar.get_point_path(a, b)


## Recomputes the dotted hero routes for the active entrances (call after any build).
func refresh_preview() -> void:
	preview_paths.clear()
	for i in active_entrances:
		preview_paths.append(_astar.get_point_path(entrances[i], core))
	queue_redraw()


# --- building -------------------------------------------------------------

## Filling a floor cell back into wall is refused if it would cut any entrance off the core.
func can_fill(c: Vector2i) -> bool:
	if not is_buildable_floor(c) or traps.has(c):
		return false
	_astar.set_point_solid(c, true)
	var ok := all_connected()
	_astar.set_point_solid(c, false)
	return ok


func fill(c: Vector2i) -> void:
	_floor[c.y * W + c.x] = 0
	terrain.erase(c)
	decor.erase(c)
	_rebuild()


func can_place_trap(c: Vector2i) -> bool:
	return is_buildable_floor(c) and not traps.has(c)


func place_trap(c: Vector2i, kind: String) -> void:
	traps[c] = kind
	_trap_clock[c] = 0.0
	queue_redraw()


func _rebuild() -> void:
	for child in _walls.get_children():
		child.free()
	for y in H:
		for x in W:
			var c := Vector2i(x, y)
			var wall := is_wall(c)
			_astar.set_point_solid(c, wall)
			if wall and _touches_floor(c):
				var shape := CollisionShape2D.new()
				var rect := RectangleShape2D.new()
				rect.size = Vector2(TILE, TILE)
				shape.shape = rect
				shape.position = Vector2(c) * TILE + Vector2(TILE, TILE) / 2.0
				_walls.add_child(shape)
	queue_redraw()


func _touches_floor(c: Vector2i) -> bool:
	for d in DIRS:
		if in_bounds(c + d) and not is_wall(c + d):
			return true
	return false


# --- ticking --------------------------------------------------------------

func _physics_process(delta: float) -> void:
	_time += delta
	for i in _torches.size():
		_torches[i].energy = 0.9 + 0.15 * sin(_time * 9.0 + i * 1.7) + randf_range(-0.05, 0.05)
	queue_redraw()
	if not traps_active:
		return
	for c: Vector2i in traps:
		var data: Dictionary = Units.TRAPS[traps[c]]
		_trap_clock[c] += delta
		if _trap_clock[c] < float(data.interval):
			continue
		_trap_clock[c] = 0.0
		_hurt_heroes_on(func(hc: Vector2i) -> bool: return hc == c, int(data.damage) + trap_bonus)
	_lava_clock += delta
	var lava: Dictionary = Units.TERRAIN.lava
	if _lava_clock >= float(lava.interval):
		_lava_clock = 0.0
		_hurt_heroes_on(func(hc: Vector2i) -> bool: return terrain.get(hc, "") == "lava", int(lava.damage))


func _hurt_heroes_on(match_cell: Callable, dmg: int) -> void:
	for node in get_tree().get_nodes_in_group("heroes"):
		var hero := node as Hero
		if match_cell.call(cell_at(hero.global_position)):
			hero.take_damage(dmg, Vector2.ZERO)


# --- drawing --------------------------------------------------------------

## Draws the 16x16 slice of a large repeating texture that belongs to cell c.
func _draw_tiled(t: Texture2D, c: Vector2i, r: Rect2, tint: Color = Color.WHITE) -> void:
	var n := Vector2i(t.get_size()) / TILE
	var src := Rect2(Vector2(posmod(c.x, n.x), posmod(c.y, n.y)) * TILE, Vector2(TILE, TILE))
	draw_texture_rect_region(t, r, src, tint)


func _draw() -> void:
	for y in H:
		for x in W:
			var c := Vector2i(x, y)
			var r := Rect2(Vector2(c) * TILE, Vector2(TILE, TILE))
			if is_wall(c):
				# Walls read as solid blocks: flat dark top, brick front face where floor is below.
				var front := in_bounds(c + Vector2i.DOWN) and not is_wall(c + Vector2i.DOWN)
				if front:
					if wall_tex != null:
						_draw_tiled(wall_tex, c, r, Color(0.85, 0.8, 0.9))
					else:
						draw_rect(r, Color(0.33, 0.31, 0.36))
					draw_rect(Rect2(r.position, Vector2(TILE, 4)), WALL_TOP)
				elif _touches_floor(c):
					draw_rect(r, WALL_TOP)
				else:
					draw_rect(r, VOID)
				continue
			if floor_tex != null:
				_draw_tiled(floor_tex, c, r, FLOOR_TINT)
			else:
				var shade := 0.22 if (x + y) % 2 == 0 else 0.19
				draw_rect(r, Color(shade, shade, shade + 0.03))
			if is_wall(c + Vector2i.UP):
				draw_rect(Rect2(r.position, Vector2(TILE, 5)), Color(0, 0, 0, 0.4))
			for d in DIRS:
				if is_wall(c + d):
					var a := r.get_center() + Vector2(d) * TILE / 2.0 + Vector2(d).orthogonal() * TILE / 2.0
					var b := r.get_center() + Vector2(d) * TILE / 2.0 - Vector2(d).orthogonal() * TILE / 2.0
					draw_line(a, b, EDGE, 1.0)
	_draw_terrain()
	_draw_decor()
	for i in entrances.size():
		var er := Rect2(Vector2(entrances[i]) * TILE, Vector2(TILE, TILE))
		draw_rect(er, Color(0.25, 0.45, 0.9, 0.55))
		if hover_visible and active_entrances.has(i):
			var blink := 0.6 + 0.4 * sin(_time * 6.0)
			draw_rect(er.grow(1), Color(1.0, 0.25, 0.2, blink), false, 2.0)
			draw_string(ThemeDB.fallback_font, er.position + Vector2(5, -3), "!", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(1, 0.3, 0.2, blink))
	var core_c := Vector2(core) * TILE + Vector2(TILE, TILE) / 2.0
	var pulse := 1.0 + 0.15 * sin(_time * 3.0)
	draw_circle(core_c, 22.0 * pulse, Color(0.9, 0.1, 0.25, 0.2))
	draw_set_transform(core_c + Vector2(0, 14), 0.0, Vector2(1.0, 0.35))
	draw_circle(Vector2.ZERO, 14.0, Color(0, 0, 0, 0.5))
	draw_set_transform(Vector2.ZERO)
	var core_tex := Sprites.tex("core")
	var bob := Vector2(0, sin(_time * 2.0) * 2.0)
	if core_tex != null:
		draw_texture(core_tex, core_c + bob - core_tex.get_size() / 2.0 - Vector2(0, 4))
	else:
		draw_circle(core_c + bob, 12.0, Color(1.0, 0.25, 0.35))
	var ring_col := Color(0.3, 1.0, 0.45).lerp(Color(1.0, 0.2, 0.2), 1.0 - core_ratio)
	draw_arc(core_c, 26.0, -PI / 2.0, -PI / 2.0 + TAU * core_ratio, 40, ring_col, 2.0)
	var torch_tex := Sprites.tex("torch")
	for t in _torches:
		var tp := t.position
		if torch_tex != null:
			draw_texture(torch_tex, tp - Vector2(torch_tex.get_size().x / 2.0, torch_tex.get_size().y + 1.0))
			continue
		draw_rect(Rect2(tp + Vector2(-1, -8), Vector2(2, 5)), Color(0.4, 0.25, 0.1))
		var flick := 1.0 + 0.3 * sin(_time * 12.0 + tp.x)
		draw_circle(tp + Vector2(0, -9), 2.2 * flick, Color(1.0, 0.55, 0.15))
		draw_circle(tp + Vector2(0, -9.5), 1.1 * flick, Color(1.0, 0.95, 0.6))
	if hover_visible:
		for path in preview_paths:
			for i in path.size() - 1:
				if i % 2 == 0:
					draw_line(path[i] - global_position, path[i + 1] - global_position, Color(1.0, 0.85, 0.3, 0.55), 1.0)
	for c: Vector2i in traps:
		var col: Color = Units.TRAPS[traps[c]].color
		var o := Vector2(c) * TILE
		var trap_tex := Sprites.tex(String(Units.TRAPS[traps[c]].sprite))
		if trap_tex != null:
			draw_texture(trap_tex, o)
			continue
		for i in 3:
			var bx := o.x + 2.0 + i * 4.5
			draw_colored_polygon(PackedVector2Array([
				Vector2(bx, o.y + 13), Vector2(bx + 2, o.y + 4), Vector2(bx + 4, o.y + 13)]), col)
	if hover_visible and in_bounds(hover_cell):
		var hc := Color(0.3, 1.0, 0.4) if hover_ok else Color(1.0, 0.3, 0.3)
		draw_rect(Rect2(Vector2(hover_cell) * TILE, Vector2(TILE, TILE)), hc, false, 1.0)


func _draw_terrain() -> void:
	for c: Vector2i in terrain:
		var r := Rect2(Vector2(c) * TILE, Vector2(TILE, TILE))
		if terrain[c] == "water":
			draw_rect(r, Color(0.15, 0.35, 0.6, 0.75))
			var wy := 5.0 + 2.0 * sin(_time * 2.0 + c.x)
			draw_line(r.position + Vector2(3, wy), r.position + Vector2(9, wy), Color(0.6, 0.85, 1.0, 0.6), 1.0)
		else:
			var glow := 0.75 + 0.25 * sin(_time * 3.0 + c.x * 0.7 + c.y)
			draw_rect(r, Color(0.9 * glow, 0.3 * glow, 0.05))
			draw_circle(r.position + Vector2(5 + 3 * sin(_time + c.y), 8), 1.5, Color(1.0, 0.85, 0.3))


func _draw_decor() -> void:
	for c: Vector2i in decor:
		var o := Vector2(c) * TILE
		match decor[c]:
			"bones":
				draw_line(o + Vector2(3, 11), o + Vector2(12, 8), Color(0.85, 0.82, 0.72), 1.5)
				draw_line(o + Vector2(5, 6), o + Vector2(10, 12), Color(0.85, 0.82, 0.72), 1.5)
			"skull":
				draw_circle(o + Vector2(8, 9), 3.5, Color(0.88, 0.85, 0.76))
				draw_rect(Rect2(o + Vector2(6, 8), Vector2(1.5, 1.5)), Color.BLACK)
				draw_rect(Rect2(o + Vector2(9, 8), Vector2(1.5, 1.5)), Color.BLACK)
			"crate":
				draw_rect(Rect2(o + Vector2(3, 4), Vector2(10, 9)), Color(0.45, 0.3, 0.15))
				draw_rect(Rect2(o + Vector2(3, 4), Vector2(10, 9)), Color(0.25, 0.15, 0.08), false, 1.0)
				draw_line(o + Vector2(3, 4), o + Vector2(13, 13), Color(0.25, 0.15, 0.08), 1.0)
