class_name DemonLord
extends CharacterBody2D
## Player-controlled demon lord: 8-way move, invulnerable dash, melee with the equipped weapon
## (Arsenal.WEAPONS, swapped by walking over drops), mouse-aimed magic bolts that recharge,
## and two skill slots on Q / E (Arsenal.SKILLS, learned from rewards).

signal died

const BOLT_SCRIPT := preload("res://src/scripts/magic_bolt.gd")
const SWING_TIME := 0.12

@export var move_speed: float = 140.0
@export var max_health: int = 6
@export var dash_speed: float = 420.0
@export var dash_time: float = 0.15
@export var dash_cooldown: float = 0.6
@export var max_bolts: int = 3
@export var bolt_recharge_time: float = 1.5
@export var bolt_damage: int = 2
@export var radius: float = 7.0

## Movement is clamped to this rect (screen edge); walls are handled by physics.
var bounds: Rect2 = Rect2(-10000, -10000, 20000, 20000)
## Attacks and skills only fire while true (raid phase).
var combat_enabled: bool = true
## Shaken on hits; injected by the game.
var camera: Camera2D
var health: int
var bolts: int
var weapon_id: String = "claws"
var weapon: Dictionary = Arsenal.WEAPONS.claws
## Flat melee damage added on top of any weapon (from rewards).
var melee_bonus: int = 0
## Skill ids for Q and E ("" = empty slot) and their remaining cooldowns.
var skills: Array[String] = ["hellfire", ""]
var skill_cd: Array[float] = [0.0, 0.0]

var _dash_left: float = 0.0
var _dash_cd: float = 0.0
var _dash_dir: Vector2 = Vector2.RIGHT
var _melee_cd: float = 0.0
var _swing_show: float = 0.0
var _swing_dir: Vector2 = Vector2.RIGHT
var _recharge: float = 0.0
var _hurt_flash: float = 0.0
var _shake: float = 0.0
var _anim_t: float = 0.0


func _ready() -> void:
	add_to_group("demon_lord")
	collision_layer = 1
	collision_mask = 4
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = radius
	shape.shape = circle
	add_child(shape)
	add_child(Fx.light(Color(0.75, 0.5, 1.0), 1.2, 1.8))
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
	Audio.play("hurt")
	shake(5.0)
	Fx.damage_number(get_parent(), global_position, amount, Color(1, 0.3, 0.3))
	Fx.burst(get_parent(), global_position, Color(0.8, 0.1, 0.2), 10, 100.0)
	HitFeedback.hitstop(get_tree(), 0.04)
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


## Switches the melee weapon (key of Arsenal.WEAPONS).
func equip(id: String) -> void:
	weapon_id = id
	weapon = Arsenal.WEAPONS[id]


## Puts a skill in the empty slot, or replaces E when both are full.
func learn(id: String) -> void:
	var slot := skills.find("")
	if slot == -1:
		slot = 1
	skills[slot] = id
	skill_cd[slot] = 0.0


func _physics_process(delta: float) -> void:
	_dash_cd -= delta
	_melee_cd -= delta
	_swing_show -= delta
	_hurt_flash -= delta
	_anim_t += delta
	for i in skill_cd.size():
		skill_cd[i] = maxf(skill_cd[i] - delta, 0.0)

	var input := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if Input.is_action_just_pressed("dash") and _dash_cd <= 0.0:
		_dash_dir = input if input != Vector2.ZERO else _aim_dir()
		_dash_left = dash_time
		_dash_cd = dash_cooldown
		Audio.play("dash", -4.0)
	if _dash_left > 0.0:
		_dash_left -= delta
		velocity = _dash_dir * dash_speed
		Fx.afterimage(get_parent(), global_position, radius, Color(0.6, 0.3, 1.0, 0.5))
	else:
		velocity = input * move_speed
	move_and_slide()
	global_position = global_position.clamp(bounds.position, bounds.end)

	if combat_enabled:
		if Input.is_action_pressed("melee") and _melee_cd <= 0.0:
			_swing()
		if Input.is_action_just_pressed("bolt") and bolts > 0:
			_fire_bolt(_aim_dir())
		for i in 2:
			if Input.is_action_just_pressed(["skill_q", "skill_e"][i]):
				try_skill(i)
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
	_melee_cd = float(weapon.cooldown)
	_swing_dir = _aim_dir()
	_swing_show = SWING_TIME
	Audio.play("swing", -8.0)
	var hit := false
	var reach := float(weapon.range)
	for node in get_tree().get_nodes_in_group("heroes"):
		var hero := node as Unit
		var to_hero := hero.global_position - global_position
		if to_hero.length() > reach + hero.radius:
			continue
		if float(weapon.arc) < 360.0 and absf(rad_to_deg(_swing_dir.angle_to(to_hero))) > float(weapon.arc) / 2.0:
			continue
		hero.take_damage(int(weapon.damage) + melee_bonus, to_hero.normalized() * float(weapon.knockback))
		Fx.burst(get_parent(), hero.global_position, Color(1.0, 0.9, 0.5), 8, 120.0)
		hit = true
	if hit:
		Audio.play("hit")
		shake(float(weapon.shake))
		HitFeedback.hitstop(get_tree(), 0.06)


func _fire_bolt(dir: Vector2) -> void:
	if bolts > 0:
		bolts -= 1
	Audio.play("bolt", -6.0)
	_spawn_bolt(dir)


func _spawn_bolt(dir: Vector2) -> void:
	var bolt: Node2D = BOLT_SCRIPT.new()
	bolt.direction = dir
	bolt.damage = bolt_damage
	bolt.from_player = true
	bolt.global_position = global_position + dir * (radius + 4.0)
	get_parent().add_child(bolt)


## Casts the skill in slot i if it is ready. Returns true when cast.
func try_skill(i: int) -> bool:
	if skills[i] == "" or skill_cd[i] > 0.0:
		return false
	var id := skills[i]
	skill_cd[i] = float(Arsenal.SKILLS[id].cooldown)
	Audio.play("skill", -2.0)
	var col: Color = Arsenal.SKILLS[id].color
	match id:
		"hellfire":
			Fx.ring(get_parent(), global_position, 56.0, col)
			Fx.burst(get_parent(), global_position, col, 40, 220.0)
			for node in get_tree().get_nodes_in_group("heroes"):
				var hero := node as Unit
				var to_hero := hero.global_position - global_position
				if to_hero.length() <= 56.0 + hero.radius:
					hero.take_damage(6, to_hero.normalized() * 2.0)
			shake(9.0)
			HitFeedback.hitstop(get_tree(), 0.08)
		"storm":
			for k in 16:
				_spawn_bolt(Vector2.from_angle(TAU * k / 16.0))
			Fx.ring(get_parent(), global_position, 30.0, col)
			shake(5.0)
		"fear":
			Fx.ring(get_parent(), global_position, 90.0, col)
			for node in get_tree().get_nodes_in_group("heroes"):
				var hero := node as Unit
				if hero.global_position.distance_to(global_position) <= 90.0:
					hero.stun(2.5)
			shake(6.0)
		"imps":
			for k in 3:
				var imp := Monster.new()
				imp.setup(Units.SUMMONS.imp)
				imp.kind = "imp"
				imp.home = global_position + Vector2.from_angle(TAU * k / 3.0) * 14.0
				imp.position = imp.home
				get_parent().add_child(imp)
				Fx.burst(get_parent(), imp.home, col, 10, 70.0)
				get_tree().create_timer(12.0).timeout.connect(func() -> void:
					if is_instance_valid(imp):
						Fx.burst(imp.get_parent(), imp.global_position, col, 8, 50.0)
						imp.queue_free())
	return true


func _draw() -> void:
	var body := Color(0.55, 0.2, 0.75)
	if _hurt_flash > 0.0:
		body = Color.WHITE
	elif is_invulnerable():
		body = Color(0.55, 0.2, 0.75, 0.4)
	draw_set_transform(Vector2(0, radius - 1.0), 0.0, Vector2(1.0, 0.4))
	draw_circle(Vector2.ZERO, radius, Color(0, 0, 0, 0.45))
	draw_set_transform(Vector2.ZERO)
	var aim := _aim_dir()
	var side := aim.orthogonal()
	var tex := Sprites.tex("demon_lord")
	if tex != null:
		var tint := Color(4, 4, 4) if _hurt_flash > 0.0 else Color(1, 1, 1, body.a)
		Sprites.draw_on(self, tex, radius + 1.0, aim.x < 0.0, tint, _anim_t, velocity.length() > 1.0, 0.0, Sprites.tex("demon_lord_walk"))
	else:
		for s: float in [-1.0, 1.0]:
			var base := Vector2(s * 4.0, -radius + 2.0)
			draw_colored_polygon(PackedVector2Array([base + Vector2(-2, 0), base + Vector2(s * 3.0, -6.0), base + Vector2(2, 0)]), Color(0.95, 0.85, 0.6))
		draw_circle(Vector2.ZERO, radius, body)
		draw_circle(aim * 3.0 + side * 2.0, 1.4, Color(1.0, 0.3, 0.3))
		draw_circle(aim * 3.0 - side * 2.0, 1.4, Color(1.0, 0.3, 0.3))
	_draw_weapon(aim)
	if _swing_show > 0.0:
		var a := _swing_dir.angle()
		var arc := deg_to_rad(float(weapon.arc))
		var half := minf(arc, TAU) / 2.0
		var reach := float(weapon.range)
		var t := _swing_show / SWING_TIME
		var pts := PackedVector2Array([Vector2.ZERO])
		for i in 17:
			pts.append(Vector2.from_angle(a - half + 2.0 * half * i / 16.0) * reach)
		draw_colored_polygon(pts, Color(1.0, 0.8, 0.4, 0.3 * t))
		draw_arc(Vector2.ZERO, reach, a - half, a + half, 24, Color(1.0, 0.95, 0.7, t), 3.0)


## Weapon held toward the aim; during a swing it sweeps across the arc.
func _draw_weapon(aim: Vector2) -> void:
	var ang := aim.angle()
	if _swing_show > 0.0:
		var half := minf(deg_to_rad(float(weapon.arc)), TAU) / 2.0
		ang = _swing_dir.angle() + lerpf(half, -half, 1.0 - _swing_show / SWING_TIME)
	var dir := Vector2.from_angle(ang)
	var hand := dir * (radius - 1.0) + Vector2(0, 1)
	var tip := hand + dir * float(weapon.len)
	var col: Color = weapon.color
	match String(weapon.shape):
		"claw":
			for k: float in [-0.35, 0.0, 0.35]:
				draw_line(hand, hand + Vector2.from_angle(ang + k) * float(weapon.len), col, 1.0)
		"blade":
			draw_line(hand, tip, Color(0.1, 0.08, 0.12), 4.0)
			draw_line(hand, tip, col, 2.0)
			draw_line(hand + dir.orthogonal() * 3.0, hand - dir.orthogonal() * 3.0, Color(0.6, 0.45, 0.2), 2.0)
		"spear":
			draw_line(hand - dir * 4.0, tip, Color(0.45, 0.3, 0.15), 1.5)
			draw_colored_polygon(PackedVector2Array([tip + dir * 5.0, tip + dir.orthogonal() * 2.0, tip - dir.orthogonal() * 2.0]), col)
		"hammer":
			draw_line(hand, tip, Color(0.45, 0.3, 0.15), 2.0)
			draw_rect(Rect2(tip - Vector2(4, 4), Vector2(8, 8)), col)
