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


## Applies damage and knockback; frees the unit at 0 HP. Returns false if already dead.
func take_damage(amount: int, dir: Vector2) -> bool:
	if health <= 0:
		return false
	health -= amount
	_hurt_flash = 0.1
	_knockback = dir * 140.0
	_windup = -1.0
	if health <= 0:
		died.emit(self)
		queue_free()
	return true


func _physics_process(delta: float) -> void:
	_cooldown -= delta
	_hurt_flash -= delta
	_knockback = _knockback.move_toward(Vector2.ZERO, 600.0 * delta)
	_target = _find_target()
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
		move = (_target.global_position - global_position).normalized() * float(stats.speed)
	else:
		move = _idle_velocity(delta)
	velocity = move + _knockback
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
		for i in burst:
			_spawn_bolt(dir.rotated(TAU * i / burst))
	elif global_position.distance_to(_target.global_position) <= _reach(_target) + 4.0:
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


func _draw() -> void:
	var col: Color = stats.color
	if _hurt_flash > 0.0:
		col = Color.WHITE
	elif _windup >= 0.0:
		col = col.lerp(Color(1.0, 0.2, 0.2), 0.6)
	_draw_body(col)
	if _windup >= 0.0:
		draw_arc(Vector2.ZERO, radius + 3.0, 0.0, TAU * (_windup / float(stats.windup)), 16, Color(1, 0.3, 0.2), 1.5)
	var w := radius * 2.0
	draw_rect(Rect2(-radius, -radius - 5.0, w, 2.0), Color(0.2, 0.2, 0.2))
	draw_rect(Rect2(-radius, -radius - 5.0, w * float(health) / max_health, 2.0), Color(0.9, 0.2, 0.2))
