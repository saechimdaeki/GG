class_name DemonLord
extends CharacterBody2D
## Player-controlled demon lord: 8-way move, invulnerable dash, melee swing (main damage),
## mouse-aimed magic bolts that recharge one at a time.

signal died

const BOLT_SCRIPT := preload("res://src/scripts/magic_bolt.gd")

@export var move_speed: float = 140.0
@export var max_health: int = 10
@export var dash_speed: float = 420.0
@export var dash_time: float = 0.15
@export var dash_cooldown: float = 0.6
@export var melee_damage: int = 3
@export var melee_range: float = 34.0
@export var melee_arc_deg: float = 120.0
@export var melee_cooldown: float = 0.3
@export var max_bolts: int = 3
@export var bolt_recharge_time: float = 1.5
@export var bolt_damage: int = 2
@export var radius: float = 7.0

## Movement is clamped to this rect (screen edge); walls are handled by physics.
var bounds: Rect2 = Rect2(-10000, -10000, 20000, 20000)
## Attacks only fire while true (raid phase).
var combat_enabled: bool = true
## Shaken on hits; injected by the game.
var camera: Camera2D
var health: int
var bolts: int

var _dash_left: float = 0.0
var _dash_cd: float = 0.0
var _dash_dir: Vector2 = Vector2.RIGHT
var _melee_cd: float = 0.0
var _swing_show: float = 0.0
var _swing_dir: Vector2 = Vector2.RIGHT
var _recharge: float = 0.0
var _hurt_flash: float = 0.0
var _shake: float = 0.0


func _ready() -> void:
	add_to_group("demon_lord")
	collision_layer = 1
	collision_mask = 4
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = radius
	shape.shape = circle
	add_child(shape)
	health = max_health
	bolts = max_bolts


## True while dashing: incoming damage is ignored.
func is_invulnerable() -> bool:
	return _dash_left > 0.0


## Applies damage unless dashing; flashes, shakes and emits died at 0 HP.
## Returns false when the hit was ignored (dashing or already dead).
func take_damage(amount: int, _dir: Vector2 = Vector2.ZERO) -> bool:
	if is_invulnerable() or health <= 0:
		return false
	health = max(health - amount, 0)
	_hurt_flash = 0.15
	shake(4.0)
	if health == 0:
		died.emit()
	return true


## Restores full HP and bolts (used on respawn).
func reset_stats() -> void:
	health = max_health
	bolts = max_bolts


## Kicks the camera; decays over a few frames.
func shake(strength: float) -> void:
	_shake = max(_shake, strength)


func _physics_process(delta: float) -> void:
	_dash_cd -= delta
	_melee_cd -= delta
	_swing_show -= delta
	_hurt_flash -= delta

	var input := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if Input.is_action_just_pressed("dash") and _dash_cd <= 0.0:
		_dash_dir = input if input != Vector2.ZERO else _aim_dir()
		_dash_left = dash_time
		_dash_cd = dash_cooldown
	if _dash_left > 0.0:
		_dash_left -= delta
		velocity = _dash_dir * dash_speed
	else:
		velocity = input * move_speed
	move_and_slide()
	global_position = global_position.clamp(bounds.position, bounds.end)

	if combat_enabled and Input.is_action_pressed("melee") and _melee_cd <= 0.0:
		_swing()
	if combat_enabled and Input.is_action_just_pressed("bolt") and bolts > 0:
		_fire_bolt()
	if bolts < max_bolts:
		_recharge += delta
		if _recharge >= bolt_recharge_time:
			_recharge = 0.0
			bolts += 1

	_shake = move_toward(_shake, 0.0, 30.0 * delta)
	if camera != null:
		camera.offset = Vector2(randf_range(-_shake, _shake), randf_range(-_shake, _shake))
	queue_redraw()


func _aim_dir() -> Vector2:
	var to_mouse := get_global_mouse_position() - global_position
	return to_mouse.normalized() if to_mouse.length() > 0.1 else Vector2.RIGHT


func _swing() -> void:
	_melee_cd = melee_cooldown
	_swing_dir = _aim_dir()
	_swing_show = 0.1
	var hit := false
	for node in get_tree().get_nodes_in_group("heroes"):
		var hero := node as Unit
		var to_hero := hero.global_position - global_position
		if to_hero.length() > melee_range + hero.radius:
			continue
		if absf(rad_to_deg(_swing_dir.angle_to(to_hero))) > melee_arc_deg / 2.0:
			continue
		hero.take_damage(melee_damage, _swing_dir)
		hit = true
	if hit:
		shake(3.0)
		HitFeedback.hitstop(get_tree(), 0.05)


func _fire_bolt() -> void:
	bolts -= 1
	var bolt: Node2D = BOLT_SCRIPT.new()
	bolt.direction = _aim_dir()
	bolt.damage = bolt_damage
	bolt.from_player = true
	bolt.global_position = global_position + bolt.direction * (radius + 4.0)
	get_parent().add_child(bolt)


func _draw() -> void:
	var body := Color(0.55, 0.2, 0.75)
	if _hurt_flash > 0.0:
		body = Color.WHITE
	elif is_invulnerable():
		body = Color(0.55, 0.2, 0.75, 0.4)
	draw_circle(Vector2.ZERO, radius, body)
	draw_circle(_aim_dir() * 4.0, 2.0, Color(1.0, 0.3, 0.3))
	if _swing_show > 0.0:
		var a := _swing_dir.angle()
		var half := deg_to_rad(melee_arc_deg) / 2.0
		draw_arc(Vector2.ZERO, melee_range, a - half, a + half, 16, Color(1.0, 0.85, 0.4), 4.0)
