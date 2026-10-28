class_name DungeonGrid
extends Node2D
## Tile grid of the dungeon: walls, floor, entrance, core and traps.
## Owns wall collision, hero pathfinding (AStarGrid2D) and trap damage ticks.

const TILE := 16
const W := 40
const H := 22

var entrance := Vector2i(1, 11)
var core := Vector2i(37, 11)
## Vector2i -> trap kind (key of Units.TRAPS).
var traps: Dictionary = {}
## Traps deal damage only while true (raid phase).
var traps_active := false
## Build cursor, drawn as an outline while hover_visible.
var hover_cell := Vector2i(-1, -1)
var hover_ok := false
var hover_visible := false

var _floor: PackedByteArray
var _astar := AStarGrid2D.new()
var _walls: StaticBody2D
var _trap_clock: Dictionary = {}


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


func _generate() -> void:
	_carve(Rect2i(1, 8, 4, 7))     # entrance room
	_carve(Rect2i(33, 7, 6, 9))    # core room
	_carve(Rect2i(5, 10, 8, 2))    # winding corridor
	_carve(Rect2i(11, 4, 2, 8))
	_carve(Rect2i(11, 4, 14, 2))
	_carve(Rect2i(23, 4, 2, 13))
	_carve(Rect2i(23, 15, 10, 2))
	_carve(Rect2i(31, 10, 2, 7))


func _carve(r: Rect2i) -> void:
	for y in range(r.position.y, r.end.y):
		for x in range(r.position.x, r.end.x):
			_floor[y * W + x] = 1


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


func entrance_position() -> Vector2:
	return cell_center(entrance)


## Floor cell that is neither entrance nor core: where traps and monsters may go.
func is_buildable_floor(c: Vector2i) -> bool:
	return not is_wall(c) and c != entrance and c != core


## World-space path between two points, empty if unreachable.
func find_path(from_world: Vector2, to_world: Vector2) -> PackedVector2Array:
	var a := cell_at(from_world)
	var b := cell_at(to_world)
	if is_wall(a) or is_wall(b):
		return PackedVector2Array()
	return _astar.get_point_path(a, b)


## Digging: interior wall cells only (the outer border stays).
func can_dig(c: Vector2i) -> bool:
	return c.x > 0 and c.y > 0 and c.x < W - 1 and c.y < H - 1 and is_wall(c)


func dig(c: Vector2i) -> void:
	_floor[c.y * W + c.x] = 1
	_rebuild()


## Filling a floor cell back into wall is refused if it would cut entrance from core.
func can_fill(c: Vector2i) -> bool:
	if not is_buildable_floor(c) or traps.has(c):
		return false
	_astar.set_point_solid(c, true)
	var ok := not _astar.get_id_path(entrance, core).is_empty()
	_astar.set_point_solid(c, false)
	return ok


func fill(c: Vector2i) -> void:
	_floor[c.y * W + c.x] = 0
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
	for d: Vector2i in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		if in_bounds(c + d) and not is_wall(c + d):
			return true
	return false


func _physics_process(delta: float) -> void:
	if hover_visible:
		queue_redraw()
	if not traps_active:
		return
	for c: Vector2i in traps:
		var data: Dictionary = Units.TRAPS[traps[c]]
		_trap_clock[c] += delta
		if _trap_clock[c] < float(data.interval):
			continue
		_trap_clock[c] = 0.0
		for node in get_tree().get_nodes_in_group("heroes"):
			var hero := node as Hero
			if cell_at(hero.global_position) == c:
				hero.take_damage(int(data.damage), Vector2.ZERO)


func _draw() -> void:
	for y in H:
		for x in W:
			var c := Vector2i(x, y)
			var r := Rect2(Vector2(c) * TILE, Vector2(TILE, TILE))
			if is_wall(c):
				draw_rect(r, Color(0.2, 0.19, 0.23))
				if in_bounds(c + Vector2i.DOWN) and not is_wall(c + Vector2i.DOWN):
					draw_rect(Rect2(r.position + Vector2(0, 10), Vector2(TILE, 6)), Color(0.33, 0.31, 0.36))
			else:
				var shade := 0.12 if (x + y) % 2 == 0 else 0.1
				draw_rect(r, Color(shade, shade, shade + 0.02))
	var e := Rect2(Vector2(entrance) * TILE, Vector2(TILE, TILE))
	draw_rect(e, Color(0.25, 0.45, 0.9, 0.6))
	var core_c := Vector2(core) * TILE + Vector2(TILE, TILE) / 2.0
	draw_circle(core_c, 11.0, Color(0.9, 0.1, 0.25, 0.25))
	draw_circle(core_c, 6.0, Color(1.0, 0.25, 0.35))
	for c: Vector2i in traps:
		var col: Color = Units.TRAPS[traps[c]].color
		var o := Vector2(c) * TILE
		for i in 3:
			var bx := o.x + 2.0 + i * 4.5
			draw_colored_polygon(PackedVector2Array([
				Vector2(bx, o.y + 13), Vector2(bx + 2, o.y + 4), Vector2(bx + 4, o.y + 13)]), col)
	if hover_visible and in_bounds(hover_cell):
		var hc := Color(0.3, 1.0, 0.4) if hover_ok else Color(1.0, 0.3, 0.3)
		draw_rect(Rect2(Vector2(hover_cell) * TILE, Vector2(TILE, TILE)), hc, false, 1.0)
