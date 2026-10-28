class_name BuildObject
extends Node2D
## Placeable dungeon object (Units.OBJECTS):
## - lure: greedy heroes within `reach` tiles walk over and loot it (loot_time s, once per hero).
## - nest: during raids spawns a weak slime every spawn_every s, up to max_alive at once.

var kind: String
var data: Dictionary
## Nest only: raids tick only while true.
var active: bool = false

var _clock: float = 0.0
var _spawned: Array[Monster] = []
var _t: float = 0.0


## kind is a key of Units.OBJECTS.
func setup(k: String) -> void:
	kind = k
	data = Units.OBJECTS[k]


func _ready() -> void:
	add_to_group("lures" if kind == "lure" else "nests")
	add_child(Fx.light(data.color, 0.9, 0.6))


## Nest: drops summoned slimes from the last raid.
func end_raid() -> void:
	active = false
	_clock = 0.0
	for m in _spawned:
		if is_instance_valid(m):
			m.queue_free()
	_spawned.clear()


func _physics_process(delta: float) -> void:
	_t += delta
	queue_redraw()
	if kind != "nest" or not active:
		return
	_spawned = _spawned.filter(func(m: Monster) -> bool: return is_instance_valid(m))
	_clock += delta
	if _clock < float(data.spawn_every) or _spawned.size() >= int(data.max_alive):
		return
	_clock = 0.0
	var m := Monster.new()
	m.setup(Units.MONSTERS.slime, 0.6, 1.0)
	m.kind = "slime"
	m.home = global_position + Vector2(randf_range(-8, 8), randf_range(-8, 8))
	m.position = global_position
	get_parent().add_child(m)
	_spawned.append(m)
	Fx.burst(get_parent(), global_position, data.color, 10, 60.0)


func _draw() -> void:
	var col: Color = data.color
	if kind == "lure":
		var bob := sin(_t * 3.0) * 1.5
		draw_rect(Rect2(-6, -4 + bob, 12, 8), Color(0.5, 0.3, 0.1))
		draw_rect(Rect2(-6, -6 + bob, 12, 3), Color(0.65, 0.4, 0.15))
		draw_rect(Rect2(-6, -4 + bob, 12, 8), Color(0.2, 0.1, 0.05), false, 1.0)
		draw_rect(Rect2(-1, -3 + bob, 2, 3), col)
		for i in 3:
			var a := _t * 2.0 + i * TAU / 3.0
			draw_circle(Vector2(cos(a) * 7.0, -8.0 + sin(a) * 2.0 + bob), 1.0, col)
	else:
		draw_circle(Vector2.ZERO, 7.0, Color(0.15, 0.3, 0.15))
		draw_circle(Vector2.ZERO, 5.0 + sin(_t * 2.5), col.darkened(0.3))
		draw_circle(Vector2(-2, -2), 1.5, col.lightened(0.4))
