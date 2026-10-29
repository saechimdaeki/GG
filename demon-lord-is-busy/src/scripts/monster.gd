class_name Monster
extends Unit
## Player's monster minion: guards its home cell, attacks heroes in range, never strays past
## its leash. A placed monster that dies respawns at home after Units.MONSTER_RESPAWN_TIME
## (summons just vanish). Grows: XP from heroes dying nearby (or the 강화 tool) raises its
## level up to Units.MONSTER_MAX_LEVEL.

var kind: String
var home: Vector2
var level: int = 1
var xp: int = 0
## Reward-buff multipliers (hp, dmg) from the game, kept so a level-up can rescale.
var mult: Vector2 = Vector2.ONE
## Set by Game.place_monster; nest/split summons leave this false and are freed on death.
var respawns := false
## Seconds until respawn while down (0 = alive).
var down_t: float = 0.0


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


func _die() -> void:
	if not respawns:
		super()
		return
	# Down, not gone: out of the fight (no group, no collision) until the timer runs out.
	down_t = Units.MONSTER_RESPAWN_TIME
	remove_from_group("monsters")
	collision_layer = 0
	modulate.a = 0.3
	global_position = home
	_windup = -1.0
	_knockback = Vector2.ZERO


func _revive() -> void:
	down_t = 0.0
	health = max_health
	add_to_group("monsters")
	collision_layer = 2
	modulate.a = 1.0
	Fx.burst(get_parent(), global_position, Color(0.6, 1.0, 0.7), 14, 80.0)
	Fx.text(get_parent(), global_position + Vector2(0, -10), "부활!", Color(0.6, 1.0, 0.7))


func _physics_process(delta: float) -> void:
	if down_t > 0.0:
		down_t -= delta
		queue_redraw()
		if down_t <= 0.0:
			_revive()
		return
	super(delta)


## Full heal and back to the post; called at the start of every build phase.
func reset_for_wave() -> void:
	if down_t > 0.0:
		_revive()
	health = max_health
	_windup = -1.0
	_knockback = Vector2.ZERO
	global_position = home


func _attack() -> void:
	var hero := _target as Hero if is_instance_valid(_target) else null
	var base := damage
	if hero != null and kind == "goblin" and Game.has_perk(self, "goblin_hunter") \
			and (hero.slow_t > 0.0 or hero.burn_t > 0.0):
		damage *= 2
	super()
	damage = base
	if hero != null and is_instance_valid(hero) and kind == "slime" and Game.has_perk(self, "sticky_slime"):
		hero.slow(0.5, 2.0)


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
	if down_t > 0.0:
		var left := down_t / Units.MONSTER_RESPAWN_TIME
		draw_arc(Vector2.ZERO, radius + 4.0, -PI / 2.0, -PI / 2.0 + TAU * (1.0 - left), 24, Color(0.6, 1.0, 0.7), 2.0)
	if is_evolved():
		draw_arc(Vector2(0, radius - 1.0), radius + 3.0, 0.0, TAU, 24, Color(1.6, 0.6, 1.8, 0.8), 1.5)
	super()
	var crown := Sprites.tex("crown")
	if is_evolved() and crown != null:
		draw_texture(crown, Vector2(-4, -radius - 26.0))
	for i in level - 1:
		var p := Vector2(-3.0 + i * 6.0 - (level - 2) * 3.0, -radius - 18.0)
		draw_colored_polygon(PackedVector2Array([p + Vector2(0, -2.5), p + Vector2(2.2, 0), p + Vector2(0, 2.5), p + Vector2(-2.2, 0)]), Color(1, 0.85, 0.2))
