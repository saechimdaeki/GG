class_name Unit
extends CharacterBody2D
## Shared body for heroes and monsters: HP, knockback, and a telegraphed melee or ranged
## attack against the nearest node in `enemy_groups`. Subclasses steer when idle.

signal died(unit: Unit)

const BOLT_SCRIPT := preload("res://src/scripts/magic_bolt.gd")

var stats: Dictionary
var max_health: int = 1
var health: int = 1
var damage: int = 1
var radius: float = 7.0
var enemy_groups: Array[StringName] = []

var _windup: float = -1.0
var _cooldown: float = 0.0
var _knockback: Vector2 = Vector2.ZERO
var _hurt_flash: float = 0.0
var _target: Node2D
var _face_left: bool = false
var _anim_t: float = randf() * 10.0
var _moving: bool = false
var _stun: float = 0.0
## Seconds left during which hits deal damage but no knockback.
var _kb_immune: float = 0.0
## Resting scale (elites are bigger); hit squash returns here.
var base_scale: Vector2 = Vector2.ONE


## Copies tuning values from Units data; multipliers scale HP and damage.
func setup(s: Dictionary, hp_mult: float = 1.0, dmg_mult: float = 1.0) -> void:
	stats = s
	max_health = maxi(1, roundi(float(s.hp) * hp_mult))
	health = max_health
	damage = maxi(1, roundi(float(s.damage) * dmg_mult))
	radius = float(s.radius)


func _ready() -> void:
	collision_layer = 2
	collision_mask = 4
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = radius
	shape.shape = circle
	add_child(shape)
	# ponytail: one small light per unit keeps sprites readable on the dark floor
	add_child(Fx.light(Color(1.0, 0.9, 0.75), 0.7, 0.5))


## Applies damage and knockback; frees the unit at 0 HP. Returns false if already dead.
func take_damage(amount: int, dir: Vector2) -> bool:
	if health <= 0:
		return false
	amount = _modify_damage(amount)
	if amount <= 0:
		Fx.text(get_parent(), global_position, "MISS", Color(0.8, 0.8, 1.0))
		return true
	health -= amount
	_hurt_flash = 0.1
	# Attacks in progress are not cancelled, and after one knockback the unit shrugs off more
	# for a moment, so spamming melee can no longer juggle enemies in place.
	if _kb_immune <= 0.0:
		_knockback = dir * Units.KNOCKBACK_SPEED
		_kb_immune = Units.KNOCKBACK_IMMUNE
	var col: Color = stats.color
	Fx.damage_number(get_parent(), global_position, amount, Color(1, 0.9, 0.5) if is_in_group("heroes") else Color(1, 0.4, 0.4))
	Fx.burst(get_parent(), global_position, col.lightened(0.3), 6, 80.0)
	scale = base_scale * Vector2(1.3, 0.75)
	create_tween().tween_property(self, "scale", base_scale, 0.12)
	if health <= 0:
		Audio.play("hero_die" if is_in_group("heroes") else "monster_die", -3.0)
		Fx.burst(get_parent(), global_position, col, 18, 140.0)
		Fx.burst(get_parent(), global_position, Color(0.7, 0.05, 0.1), 10, 110.0)
		var lord := get_tree().get_first_node_in_group("demon_lord") as DemonLord
		if lord != null:
			lord.shake(2.5)
		died.emit(self)
		queue_free()
	return true


## Incoming damage after armor/dodge. Hero overrides; 0 means the hit missed.
func _modify_damage(amount: int) -> int:
	return amount


## Restores HP (capped) with a green number.
func heal(amount: int) -> void:
	if health <= 0 or health >= max_health:
		return
	health = mini(health + amount, max_health)
	Fx.damage_number(get_parent(), global_position, amount, Color(0.4, 1.0, 0.4))


## Per-frame hook for special abilities (Hero skills). Runs unless stunned.
func _tick_skill(_delta: float) -> void:
	pass


## Stops attacking and moving for t seconds (cancels a wind-up).
func stun(t: float) -> void:
	_stun = maxf(_stun, t)
	_windup = -1.0


## Movement speed after terrain effects. Hero overrides _speed_mult for water.
func _speed() -> float:
	return float(stats.speed) * _speed_mult()


func _speed_mult() -> float:
	return 1.0


func _physics_process(delta: float) -> void:
	_cooldown -= delta
	_hurt_flash -= delta
	_anim_t += delta
	_knockback = _knockback.move_toward(Vector2.ZERO, 600.0 * delta)
	_kb_immune -= delta
	if _stun > 0.0:
		_stun -= delta
		_moving = false
		velocity = _knockback
		move_and_slide()
		queue_redraw()
		return
	_target = _find_target()
	_tick_skill(delta)
	var move := Vector2.ZERO
	if _windup >= 0.0:
		_windup += delta
		if _windup >= float(stats.windup):
			_windup = -1.0
			_cooldown = float(stats.cooldown)
			_attack()
	elif _target != null and global_position.distance_to(_target.global_position) <= _reach(_target):
		if _cooldown <= 0.0:
			_windup = 0.0
	elif _target != null and _can_chase(_target):
		move = (_target.global_position - global_position).normalized() * _speed()
	else:
		move = _idle_velocity(delta)
	velocity = move + _knockback
	_moving = move.length() > 1.0
	if absf(move.x) > 1.0:
		_face_left = move.x < 0.0
	elif _target != null:
		_face_left = _target.global_position.x < global_position.x
	move_and_slide()
	queue_redraw()


## Steering when there is nothing to fight. Overridden by Hero and Monster.
func _idle_velocity(_delta: float) -> Vector2:
	return Vector2.ZERO


## Whether this unit may leave its post to chase target. Overridden by Monster.
func _can_chase(_t: Node2D) -> bool:
	return true


func _reach(t: Node2D) -> float:
	return float(stats.range) + radius + float(t.get("radius"))


func _find_target() -> Node2D:
	var best: Node2D = null
	var best_d := float(stats.aggro)
	for g in enemy_groups:
		for node in get_tree().get_nodes_in_group(g):
			var n := node as Node2D
			var d := global_position.distance_to(n.global_position)
			if d <= best_d:
				best = n
				best_d = d
	return best


func _attack() -> void:
	if _target == null or not is_instance_valid(_target):
		return
	var dir := (_target.global_position - global_position).normalized()
	if stats.get("ranged", false):
		var burst := int(stats.get("burst", 1))
		var spread := int(stats.get("spread", 1))
		for i in burst:
			for k in spread:
				_spawn_bolt(dir.rotated(TAU * i / burst + deg_to_rad(14.0) * (k - (spread - 1) / 2.0)))
	else:
		_knockback = dir * Units.LUNGE_SPEED
		if global_position.distance_to(_target.global_position) <= _reach(_target) + Units.LUNGE_REACH:
			_target.call("take_damage", damage, dir)


func _spawn_bolt(dir: Vector2) -> void:
	var bolt: Node2D = BOLT_SCRIPT.new()
	bolt.direction = dir
	bolt.damage = damage
	bolt.speed = Units.BOLT_SPEED_ENEMY
	bolt.target_groups = enemy_groups
	bolt.color = stats.color
	bolt.global_position = global_position + dir * (radius + 3.0)
	get_parent().add_child(bolt)


func _draw_body(col: Color) -> void:
	draw_circle(Vector2.ZERO, radius, col)
	draw_circle(Vector2(-2.5, -1.5), 1.3, Color.BLACK)
	draw_circle(Vector2(2.5, -1.5), 1.3, Color.BLACK)


func _draw() -> void:
	var col: Color = stats.color
	if _hurt_flash > 0.0:
		col = Color.WHITE
	elif _windup >= 0.0:
		col = col.lerp(Color(1.0, 0.2, 0.2), 0.6)
	draw_set_transform(Vector2(0, radius - 1.0), 0.0, Vector2(1.0, 0.4))
	draw_circle(Vector2.ZERO, radius, Color(0, 0, 0, 0.45))
	draw_set_transform(Vector2.ZERO)
	var t := Sprites.tex(String(stats.get("sprite", "")))
	var walk := Sprites.tex(String(stats.get("sprite", "")) + "_walk")
	var tint: Color = stats.get("tint", Color.WHITE)
	if t == null and stats.has("fallback"):
		t = Sprites.tex(String(stats.fallback))
		walk = Sprites.tex(String(stats.fallback) + "_walk")
		tint = stats.tint
	if t != null:
		if _hurt_flash > 0.0:
			tint = Color(4, 4, 4)
		elif _windup >= 0.0:
			tint = Color(1.5, 0.85, 0.85)
		var wind := _windup / float(stats.windup) if _windup >= 0.0 else 0.0
		Sprites.draw_on(self, t, radius + 1.0, _face_left, tint, _anim_t, _moving, wind, walk)
	else:
		_draw_body(col)
	if _windup >= 0.0:
		draw_arc(Vector2.ZERO, radius + 3.0, 0.0, TAU * (_windup / float(stats.windup)), 16, Color(1, 0.3, 0.2), 1.5)
	if _stun > 0.0:
		for i in 3:
			var a := _anim_t * 6.0 + i * TAU / 3.0
			draw_circle(Vector2(cos(a) * 5.0, -radius - 14.0 + sin(a) * 1.5), 1.2, Color(1, 1, 0.4))
	var w := radius * 2.0
	var bar_y := -radius - 5.0 if t == null else radius + 1.0 - t.get_size().y - 3.0
	draw_rect(Rect2(-radius, bar_y, w, 2.0), Color(0.2, 0.2, 0.2))
	draw_rect(Rect2(-radius, bar_y, w * float(health) / max_health, 2.0), Color(0.9, 0.2, 0.2))
