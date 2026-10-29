class_name Hero
extends Unit
## Invading hero: walks the grid path from its entrance to the core and fights the demon lord
## or monsters that come within aggro range. Greedy: detours to loot nearby lures first.
## Water slows it. At the core it stands and hits it every Units.CORE_ATTACK_INTERVAL until killed.

signal attacked_core(hero: Hero, amount: int)
## A trap / kick combo landed on this hero (lava dive, oil explosion, wall slam, ...).
signal combo(name: String, at: Vector2)
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
var _core_t: float = 0.0
# Status effects (seconds left). See Units.STATUS and on_trap().
var burn_t: float = 0.0
var oil_t: float = 0.0
var slow_t: float = 0.0
var slow_mult: float = 1.0
var _burn_tick: float = 0.0
var _air_t: float = 0.0
var _air_hit: Array[Node2D] = []
var _slammed := false


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


## Chase only what it can see: chasing straight at the lord through a wall pinned heroes
## against it for the rest of the raid. Out of sight, it keeps walking its path.
func _can_chase(t: Node2D) -> bool:
	return grid.has_line_of_sight(global_position, t.global_position)


func _speed_mult() -> float:
	var m := float(Units.TERRAIN.water.slow) if grid.terrain_at(global_position) == "water" else 1.0
	return m * (slow_mult if slow_t > 0.0 else 1.0)


## A trap fired on this hero. dmg already includes the dungeon's trap bonus; push_to is
## where a push trap throws it (DungeonGrid.push_dir).
func on_trap(kind: String, data: Dictionary, dmg: int, push_to: Vector2) -> void:
	if dmg > 0:
		take_damage(dmg, Vector2.ZERO)
	match kind:
		"fire":
			ignite()
		"glue":
			slow(float(data.slow), float(data.slow_time))
		"oil":
			if oil_t <= 0.0:
				Fx.text(get_parent(), global_position, "기름 범벅", Color(0.7, 0.6, 0.4))
			oil_t = float(data.oil_time)
		"push":
			var speed := float(data.speed) * (1.4 if Game.has_perk(self, "lava_pump") else 1.0)
			launch(push_to * speed, float(data.time))


## Sets the hero on fire; an oiled hero explodes instead (combo).
func ignite() -> void:
	if oil_t > 0.0:
		oil_t = 0.0
		explode()
		return
	burn_t = float(Units.STATUS.burn.time)


## Oil + fire: damages every hero nearby (this one included).
func explode() -> void:
	var ex: Dictionary = Units.STATUS.explosion
	Fx.ring(get_parent(), global_position, float(ex.radius), Color(1.0, 0.5, 0.1))
	Fx.burst(get_parent(), global_position, Color(1.0, 0.6, 0.15), 30, 200.0)
	Audio.play("explosion", -2.0)
	combo.emit("기름 폭발", global_position)
	var chain := Game.has_perk(self, "chain_fire")
	for node in get_tree().get_nodes_in_group("heroes"):
		var h := node as Hero
		if h.global_position.distance_to(global_position) <= float(ex.radius) + h.radius:
			if chain and h != self:
				h.ignite.call_deferred()
			h.take_damage(int(ex.damage), (h.global_position - global_position).normalized())


func slow(mult: float, time: float) -> void:
	slow_mult = minf(mult, slow_mult) if slow_t > 0.0 else mult
	slow_t = maxf(slow_t, time)


## Thrown through the air (push trap, demon lord kick): can't act until it lands. Hitting a
## wall or another hero, or landing in lava / on a trap, are the combos.
func launch(vel: Vector2, time: float) -> void:
	_knockback = vel
	_kb_immune = time
	_air_t = time
	_air_hit.clear()
	_slammed = false
	stun(time)


func airborne() -> bool:
	return _air_t > 0.0


func _physics_process(delta: float) -> void:
	_tick_status(delta)
	if health <= 0 or not is_inside_tree():
		return
	super(delta)
	if _air_t > 0.0:
		_tick_air(delta)


func _tick_status(delta: float) -> void:
	slow_t -= delta
	oil_t -= delta
	if burn_t > 0.0:
		burn_t -= delta
		_burn_tick -= delta
		if _burn_tick <= 0.0:
			_burn_tick = float(Units.STATUS.burn.tick)
			Fx.burst(get_parent(), global_position + Vector2(0, -4), Color(1.0, 0.5, 0.1), 3, 40.0)
			take_damage(int(Units.STATUS.burn.damage), Vector2.ZERO)


func _tick_air(delta: float) -> void:
	_air_t -= delta
	rotation += 18.0 * delta  # tumbling through the air
	# Wall: the only thing heroes collide with is walls and closed gates.
	if not _slammed and get_slide_collision_count() > 0 and _knockback.length() > 60.0:
		_slammed = true
		rotation = 0.0
		var ws: Dictionary = Units.STATUS.wall_slam
		var mult := 2 if Game.has_perk(self, "wall_breaker") else 1
		_knockback = Vector2.ZERO
		_air_t = 0.0
		stun(float(ws.stun))
		Fx.ring(get_parent(), global_position, 14.0, Color(0.9, 0.9, 1.0))
		combo.emit("벽꽝", global_position)
		take_damage(int(ws.damage) * mult, Vector2.ZERO)
		return
	# Bowling: other heroes in the flight path get hit once each.
	var bowl := int(Units.STATUS.bowling.damage) * (2 if Game.has_perk(self, "bowling") else 1)
	for node in get_tree().get_nodes_in_group("heroes"):
		var h := node as Hero
		if h == self or _air_hit.has(h) or h.global_position.distance_to(global_position) > radius + h.radius:
			continue
		_air_hit.append(h)
		combo.emit("볼링", h.global_position)
		h.take_damage(bowl, _knockback.normalized())
		if Game.has_perk(self, "bowling"):
			h.stun(1.0)
	if _air_t <= 0.0:
		_land()


func _land() -> void:
	rotation = 0.0
	var c := grid.cell_at(global_position)
	match grid.terrain_at(global_position):
		"lava":
			Fx.burst(get_parent(), global_position, Color(1.0, 0.4, 0.05), 30, 160.0)
			combo.emit("용암 다이브", global_position)
			take_damage(9999, Vector2.ZERO)
			return
		"water":
			var w: Dictionary = Units.STATUS.water_slow
			slow(float(w.slow), float(w.time))
			Fx.text(get_parent(), global_position, "풍덩", Color(0.5, 0.8, 1.0))
	if grid.traps.has(c) and grid.traps[c] != "push":
		combo.emit("함정 착지", global_position)
		grid.trigger_trap(c, self)


func _idle_velocity(delta: float) -> Vector2:
	var core := grid.core_position()
	if global_position.distance_to(core) < Units.CORE_ATTACK_RANGE:
		_core_t -= delta
		if _core_t <= 0.0:
			_core_t = Units.CORE_ATTACK_INTERVAL
			scale = base_scale * Vector2(0.8, 1.25)
			create_tween().tween_property(self, "scale", base_scale, 0.15)
			Fx.burst(get_parent(), core.lerp(global_position, 0.4), Color(1, 0.3, 0.3), 8, 90.0)
			attacked_core.emit(self, int(stats.core_damage))
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
	if oil_t > 0.0:
		for i in 3:
			draw_circle(Vector2(-3.0 + i * 3.0, radius - 1.0 + fmod(_anim_t * 6.0 + i, 3.0)), 1.1, Color(0.15, 0.1, 0.05))
	if slow_t > 0.0:
		draw_arc(Vector2(0, radius - 1.0), radius, 0.0, PI, 10, Color(0.75, 0.95, 0.35, 0.9), 2.0)
	if burn_t > 0.0:
		for i in 3:
			var fx := -4.0 + i * 4.0
			var fh := 4.0 + 2.0 * sin(_anim_t * 18.0 + i * 2.0)
			draw_colored_polygon(PackedVector2Array([Vector2(fx - 2, -radius), Vector2(fx, -radius - fh), Vector2(fx + 2, -radius)]),
				Color(2.0, 0.9, 0.2))
	if stats.has("skill_cd"):
		# Skill gauge under the feet; glows when the skill is ready.
		var k := clampf(1.0 - _skill_cd / float(stats.skill_cd), 0.0, 1.0)
		var gy := radius + 3.0
		draw_rect(Rect2(-radius, gy, radius * 2.0, 2.0), Color(0, 0, 0, 0.6))
		var col := Color(2.2, 1.6, 0.4) if k >= 1.0 else Color(0.4, 0.75, 1.0)
		draw_rect(Rect2(-radius, gy, radius * 2.0 * k, 2.0), col)
	if _loot != null and _loot_t > 0.0:
		draw_arc(Vector2(0, -radius - 12.0), 3.0, -PI / 2.0, -PI / 2.0 + TAU * _loot_t / float(_loot.data.loot_time), 12, Color(1, 0.85, 0.2), 1.5)
