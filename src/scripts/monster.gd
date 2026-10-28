class_name Monster
extends Unit
## Player's monster minion: guards its home cell, attacks heroes in range, never strays past
## its leash. Death is permanent for the run.

var kind: String
var home: Vector2


func _ready() -> void:
	super()
	add_to_group("monsters")
	enemy_groups = [&"heroes"]


## Full heal and back to the post; called at the start of every build phase.
func reset_for_wave() -> void:
	health = max_health
	_windup = -1.0
	_knockback = Vector2.ZERO
	global_position = home


func _can_chase(_t: Node2D) -> bool:
	return global_position.distance_to(home) < float(stats.leash)


func _idle_velocity(_delta: float) -> Vector2:
	var to_home := home - global_position
	return Vector2.ZERO if to_home.length() < 2.0 else to_home.normalized() * float(stats.speed)


func _draw_body(col: Color) -> void:
	draw_rect(Rect2(-radius, -radius, radius * 2.0, radius * 2.0), col)
	draw_rect(Rect2(-radius + 2.0, -2.0, 2.0, 2.0), Color.BLACK)
	draw_rect(Rect2(radius - 4.0, -2.0, 2.0, 2.0), Color.BLACK)
