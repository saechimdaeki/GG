class_name Hero
extends Unit
## Invading hero: walks the grid path from the entrance to the core and fights the demon lord
## or monsters that come within aggro range. Reaching the core damages it and removes the hero.

signal reached_core(hero: Hero)

var grid: DungeonGrid

var _path: PackedVector2Array
var _path_i: int = 0
var _repath: float = 0.0


func _ready() -> void:
	super()
	add_to_group("heroes")
	enemy_groups = [&"demon_lord", &"monsters"]


func _idle_velocity(delta: float) -> Vector2:
	var core := grid.core_position()
	if global_position.distance_to(core) < 10.0:
		reached_core.emit(self)
		queue_free()
		return Vector2.ZERO
	_repath -= delta
	if _repath <= 0.0 or _path_i >= _path.size():
		_path = grid.find_path(global_position, core)
		_path_i = 0
		_repath = 1.0
	while _path_i < _path.size() and global_position.distance_to(_path[_path_i]) < 3.0:
		_path_i += 1
	var goal := core if _path_i >= _path.size() else _path[_path_i]
	return (goal - global_position).normalized() * float(stats.speed)
