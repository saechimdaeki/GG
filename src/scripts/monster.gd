class_name Monster
extends Unit
## Player's monster minion: guards its home cell, attacks heroes in range, never strays past
## its leash. Death is permanent for the run. Grows: XP from heroes dying nearby (or the 강화
## tool) raises its level up to Units.MONSTER_MAX_LEVEL.

var kind: String
var home: Vector2
var level: int = 1
var xp: int = 0
## Reward-buff multipliers (hp, dmg) from the game, kept so a level-up can rescale.
var mult: Vector2 = Vector2.ONE


## Re-derives HP/damage from data, run buffs and level. Full heal.
func refresh(m: Vector2) -> void:
	mult = m
	var lv := level - 1
	setup(Units.MONSTERS[kind], m.x * pow(Units.MONSTER_LEVEL_HP, lv), m.y * pow(Units.MONSTER_LEVEL_DMG, lv))


## Adds XP; levels up when the threshold for the current level is reached.
func add_xp(n: int) -> void:
	if level >= Units.MONSTER_MAX_LEVEL:
		return
	xp += n
	if xp >= Units.MONSTER_XP[level - 1]:
		level_up()


## One level up (max Units.MONSTER_MAX_LEVEL): stronger and fully healed.
func level_up() -> void:
	if level >= Units.MONSTER_MAX_LEVEL:
		return
	level += 1
	refresh(mult)
	base_scale = Vector2.ONE * (1.0 + 0.1 * (level - 1))
	scale = base_scale
	Fx.text(get_parent(), global_position + Vector2(0, -10), "LV %d!" % level, Color(1, 0.9, 0.3))
	Fx.burst(get_parent(), global_position, Color(1, 0.9, 0.3), 16, 90.0)


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
	return Vector2.ZERO if to_home.length() < 2.0 else to_home.normalized() * _speed()


func _draw_body(col: Color) -> void:
	draw_rect(Rect2(-radius, -radius, radius * 2.0, radius * 2.0), col)
	draw_rect(Rect2(-radius + 2.0, -2.0, 2.0, 2.0), Color.BLACK)
	draw_rect(Rect2(radius - 4.0, -2.0, 2.0, 2.0), Color.BLACK)


func _draw() -> void:
	super()
	for i in level - 1:
		var p := Vector2(-3.0 + i * 6.0 - (level - 2) * 3.0, -radius - 18.0)
		draw_colored_polygon(PackedVector2Array([p + Vector2(0, -2.5), p + Vector2(2.2, 0), p + Vector2(0, 2.5), p + Vector2(-2.2, 0)]), Color(1, 0.85, 0.2))
