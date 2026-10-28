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
	var base: Dictionary = Units.MONSTERS[kind]
	if is_evolved():
		base = base.duplicate()
		base.merge(Units.EVOLUTIONS[kind], true)
	setup(base, m.x * pow(Units.MONSTER_LEVEL_HP, lv), m.y * pow(Units.MONSTER_LEVEL_DMG, lv))


## True at max level for kinds that have an evolution (Units.EVOLUTIONS).
func is_evolved() -> bool:
	return level >= Units.MONSTER_MAX_LEVEL and Units.EVOLUTIONS.has(kind)


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
	if is_evolved():
		Fx.text(get_parent(), global_position + Vector2(0, -14), "진화! %s" % stats.name, Color(1, 0.5, 1.0))
		Fx.ring(get_parent(), global_position, 40.0, Color(1, 0.5, 1.0))
		Audio.play("level_up")
	else:
		Fx.text(get_parent(), global_position + Vector2(0, -10), "LV %d!" % level, Color(1, 0.9, 0.3))
	Fx.burst(get_parent(), global_position, Color(1, 0.9, 0.3), 16, 90.0)


func _ready() -> void:
	super()
	add_to_group("monsters")
	enemy_groups = [&"heroes"]
	died.connect(_on_died)


## King slime splits into two small slimes that fight on where it fell.
func _on_died(_u: Unit) -> void:
	if not (is_evolved() and stats.get("ability", "") == "split"):
		return
	for k in 2:
		var s := Monster.new()
		s.kind = "slime"
		s.setup(Units.MONSTERS.slime, 0.5, 1.0)
		s.home = global_position + Vector2(k * 12 - 6, 0)
		s.position = s.home
		s.add_to_group("summons")  # cleared when the next build phase starts
		get_parent().add_child.call_deferred(s)


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
	if is_evolved():
		draw_arc(Vector2(0, radius - 1.0), radius + 3.0, 0.0, TAU, 24, Color(1.6, 0.6, 1.8, 0.8), 1.5)
	super()
	for i in level - 1:
		var p := Vector2(-3.0 + i * 6.0 - (level - 2) * 3.0, -radius - 18.0)
		draw_colored_polygon(PackedVector2Array([p + Vector2(0, -2.5), p + Vector2(2.2, 0), p + Vector2(0, 2.5), p + Vector2(-2.2, 0)]), Color(1, 0.85, 0.2))
