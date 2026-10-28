class_name Hero
extends Unit
## Invading hero: walks the grid path from its entrance to the core and fights the demon lord
## or monsters that come within aggro range. Greedy: detours to loot nearby lures first.
## Water slows it. Reaching the core damages it and removes the hero.

signal reached_core(hero: Hero)
## Boss calls in reinforcements; the game spawns them so wave counting stays right.
signal summon_requested(kind: String, pos: Vector2)

var grid: DungeonGrid
var elite: bool = false

var _path: PackedVector2Array
var _path_i: int = 0
var _repath: float = 0.0
var _looted: Array[BuildObject] = []
var _loot: BuildObject
var _loot_t: float = 0.0
## Skill gauge: counts down from skill_cd only while fighting; the skill fires at 0.
var _skill_cd: float = 0.0
var _boss_step: int = 0
var _charge_left: float = 0.0
var _charge_hit: Array[Node2D] = []


## Turns this hero into an elite: bigger, tougher, faster, worth more gold.
func make_elite() -> void:
	elite = true
	stats = stats.duplicate()
	stats.speed = float(stats.speed) * float(Units.ELITE.speed)
	stats.gold = int(stats.gold) * int(Units.ELITE.gold)
	max_health = roundi(max_health * float(Units.ELITE.hp))
	health = max_health
	damage = maxi(damage + 1, roundi(damage * float(Units.ELITE.damage)))
	base_scale = Vector2.ONE * float(Units.ELITE.scale)
	scale = base_scale


func _ready() -> void:
	super()
	add_to_group("heroes")
	enemy_groups = [&"demon_lord", &"monsters"]
	collision_mask |= 8  # closed iron gates
	_skill_cd = float(stats.get("skill_cd", 0.0))  # gauge starts empty: no skill on first sight
	if elite:
		add_child(Fx.light(Color(1.0, 0.8, 0.2), 1.2, 0.7))


func _modify_damage(amount: int) -> int:
	if randf() < float(stats.get("dodge", 0.0)):
		return 0
	return maxi(1, roundi(amount * (1.0 - float(stats.get("armor", 0.0)))))


func _tick_skill(delta: float) -> void:
	if _charge_left > 0.0:
		_charge_left -= delta
		for g in enemy_groups:
			for node in get_tree().get_nodes_in_group(g):
				var n := node as Node2D
				if not _charge_hit.has(n) and n.global_position.distance_to(global_position) < radius + 10.0:
					_charge_hit.append(n)
					n.call("take_damage", damage + 1, _knockback.normalized())
	var skill := String(stats.get("skill", ""))
	if skill == "":
		return
	if _target != null:
		_skill_cd -= delta
	if _skill_cd > 0.0:
		return
	match skill:
		"charge":
			if _target == null or global_position.distance_to(_target.global_position) > 110.0:
				return
			_charge(_target.global_position, 360.0, 0.35)
		"volley":
			if _target == null:
				return
			var aim := (_target.global_position - global_position).normalized()
			for k in [-1, 0, 1]:
				_spawn_bolt(aim.rotated(deg_to_rad(14.0) * k))
			Fx.text(get_parent(), global_position, "연사!", Color(0.5, 0.8, 1.0))
		"blink":
			if _target == null:
				return
			# Behind the target, else either flank; skip if all three are walls.
			var away: Vector2 = (_target.global_position - global_position).normalized() * 16.0
			var behind := Vector2.INF
			for turn: float in [0.0, PI / 2.0, -PI / 2.0]:
				var spot: Vector2 = _target.global_position + away.rotated(turn)
				if not grid.is_wall_at(spot):
					behind = spot
					break
			if behind == Vector2.INF:
				return
			Fx.burst(get_parent(), global_position, Color(0.5, 0.3, 0.7), 12, 60.0)
			global_position = behind
			_windup = 0.0  # strike right away from behind
			Fx.text(get_parent(), global_position, "기습!", Color(0.8, 0.5, 1.0))
		"slam":
			if _target == null or global_position.distance_to(_target.global_position) > 40.0:
				return
			var tg := Telegraph.new()
			tg.position = global_position
			tg.radius = 34.0
			tg.delay = 0.5
			tg.damage = damage + 1
			tg.color = Color(0.6, 0.8, 1.0)
			get_parent().add_child(tg)
			Fx.text(get_parent(), global_position, "강타!", Color(0.6, 0.8, 1.0))
		"heal":
			var hurt := false
			for node in get_tree().get_nodes_in_group("heroes"):
				var h := node as Unit
				if h.global_position.distance_to(global_position) < 60.0 and h.health < h.max_health:
					h.heal(3)
					hurt = true
			if not hurt:
				return
			Fx.ring(get_parent(), global_position, 60.0, Color(0.4, 1.0, 0.5))
		"fireball":
			if _target == null:
				return
			var tg := Telegraph.new()
			tg.position = _target.global_position
			tg.damage = damage + 1
			get_parent().add_child(tg)
		"boss":
			_boss_step = (_boss_step + 1) % 3
			match _boss_step:
				0:
					for i in 16:
						_spawn_bolt(Vector2.from_angle(TAU * i / 16.0 + _anim_t))
				1:
					if _target == null:
						return
					_charge(_target.global_position, 420.0, 0.45)
				2:
					for k in 2:
						summon_requested.emit("warrior", global_position + Vector2.from_angle(TAU * k / 2.0) * 18.0)
					Fx.ring(get_parent(), global_position, 40.0, Color(1.0, 0.8, 0.3))
	_skill_cd = float(stats.skill_cd)


## Dashes toward `to`, hitting each enemy it touches once (damage + 1).
func _charge(to: Vector2, speed: float, time: float) -> void:
	_knockback = (to - global_position).normalized() * speed
	_charge_left = time
	_charge_hit.clear()
	Fx.text(get_parent(), global_position, "돌진!", Color(1, 0.5, 0.2))


func _speed_mult() -> float:
	return float(Units.TERRAIN.water.slow) if grid.terrain_at(global_position) == "water" else 1.0


func _idle_velocity(delta: float) -> Vector2:
	var core := grid.core_position()
	if global_position.distance_to(core) < 10.0:
		reached_core.emit(self)
		queue_free()
		return Vector2.ZERO
	if _loot != null and not is_instance_valid(_loot):
		_loot = null
	if _loot == null:
		_loot = _pick_lure()
		if _loot != null:
			_repath = 0.0
	elif global_position.distance_to(_loot.global_position) < 8.0:
		_loot_t += delta
		if _loot_t >= float(_loot.data.loot_time):
			if _loot.kind == "mine" and not _loot.robbed:
				_loot.robbed = true
				_loot.queue_redraw()
				Fx.text(get_parent(), _loot.global_position, "광산 약탈!", Color(1, 0.4, 0.4))
			_looted.append(_loot)
			_loot = null
			_loot_t = 0.0
			_repath = 0.0
		return Vector2.ZERO
	var dest := core if _loot == null else _loot.global_position
	_repath -= delta
	if _repath <= 0.0 or _path_i >= _path.size():
		_path = grid.find_path(global_position, dest)
		_path_i = 0
		_repath = 1.0
	while _path_i < _path.size() and global_position.distance_to(_path[_path_i]) < 3.0:
		_path_i += 1
	var goal := dest if _path_i >= _path.size() else _path[_path_i]
	return (goal - global_position).normalized() * _speed()


func _pick_lure() -> BuildObject:
	for node in get_tree().get_nodes_in_group("lures"):
		var lure := node as BuildObject
		if _looted.has(lure):
			continue
		if global_position.distance_to(lure.global_position) <= float(lure.data.reach) * DungeonGrid.TILE:
			return lure
	return null


func _draw() -> void:
	if elite:
		draw_arc(Vector2(0, radius - 1.0), radius + 2.0, 0.0, TAU, 20, Color(1.0, 0.8, 0.2, 0.7), 1.0)
	super()
	if stats.has("skill_cd"):
		# Skill gauge under the feet; glows when the skill is ready.
		var k := clampf(1.0 - _skill_cd / float(stats.skill_cd), 0.0, 1.0)
		var gy := radius + 3.0
		draw_rect(Rect2(-radius, gy, radius * 2.0, 2.0), Color(0, 0, 0, 0.6))
		var col := Color(2.2, 1.6, 0.4) if k >= 1.0 else Color(0.4, 0.75, 1.0)
		draw_rect(Rect2(-radius, gy, radius * 2.0 * k, 2.0), col)
	if _loot != null and _loot_t > 0.0:
		draw_arc(Vector2(0, -radius - 12.0), 3.0, -PI / 2.0, -PI / 2.0 + TAU * _loot_t / float(_loot.data.loot_time), 12, Color(1, 0.85, 0.2), 1.5)
