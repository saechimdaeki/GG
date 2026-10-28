class_name Game
extends Node2D
## Game controller for one run: build phase -> raid -> reward, ten waves, core HP, run end.

enum Phase { BUILD, RAID, REWARD, OVER }

## Build tools selected with keys 1-9, in this order.
const TOOLS := ["wall", "spike", "fire", "slime", "goblin", "skeleton", "lure", "nest", "upgrade"]
## Camera zoom while building (whole map visible) and while raiding (follow the lord).
const BUILD_ZOOM := 0.62
const RAID_ZOOM := 1.0

var phase: Phase = Phase.BUILD
var gold: int = Units.START_GOLD
var core_hp: int = Units.CORE_HP
## Index of the next (or current) wave in Waves.WAVES.
var wave: int = 0
var tool: String = "spike"
var unlocked_traps: Array[String] = ["spike"]
## Monster kind -> Vector2(hp_mult, dmg_mult), grown by rewards.
var monster_mult: Dictionary = {}
var grid: DungeonGrid
var lord: DemonLord

var _hud: Hud
var _rewards: RewardScreen
var _camera: Camera2D
var _spawn_queue: Array[String] = []
var _spawn_timer: float = 0.0
var _alive_heroes: int = 0
## Vector2i -> Monster standing guard on that cell.
var _monster_cells: Dictionary = {}
var _mouse_world: Vector2 = Vector2(-100, -100)
var _mouse_screen: Vector2 = Vector2(-100, -100)
## Demon lord level (kills fill XP; each level = one reward card mid-raid).
var lord_level: int = 1
var lord_xp: int = 0
var _leveling: bool = false
## Vector2i -> BuildObject (lure / nest) on that cell.
var _object_cells: Dictionary = {}
## Shop item id -> times bought this run.
var _bought: Dictionary = {}
var core_max: int = Units.CORE_HP


func _ready() -> void:
	Engine.time_scale = 1.0
	_setup_input()
	var dark := CanvasModulate.new()
	dark.color = Color(0.5, 0.47, 0.58)
	add_child(dark)
	grid = DungeonGrid.new()
	grid.position = Vector2(0, 4)
	add_child(grid)
	_camera = Camera2D.new()
	_camera.position = _map_center()
	_camera.zoom = Vector2.ONE * BUILD_ZOOM
	add_child(_camera)
	lord = DemonLord.new()
	lord.camera = _camera
	lord.bounds = Rect2(grid.position, Vector2(DungeonGrid.W, DungeonGrid.H) * DungeonGrid.TILE)
	lord.position = grid.cell_center(grid.core + Vector2i(-2, 0))
	add_child(lord)
	lord.died.connect(_on_lord_died)
	_hud = Hud.new()
	add_child(_hud)
	_hud.restart_pressed.connect(_restart)
	_hud.tool_clicked.connect(_select_tool)
	_hud.shop_clicked.connect(buy)
	_hud.process_mode = Node.PROCESS_MODE_ALWAYS
	_rewards = RewardScreen.new()
	_rewards.process_mode = Node.PROCESS_MODE_ALWAYS
	add_child(_rewards)
	_rewards.picked.connect(_on_reward_picked)
	_enter_build()


# ponytail: input map built in code, move to project.godot [input] if rebinding UI is ever needed
func _setup_input() -> void:
	var keys := {
		"move_left": [KEY_A, KEY_LEFT],
		"move_right": [KEY_D, KEY_RIGHT],
		"move_up": [KEY_W, KEY_UP],
		"move_down": [KEY_S, KEY_DOWN],
		"dash": [KEY_SPACE, KEY_SHIFT],
		"start_raid": [KEY_ENTER, KEY_KP_ENTER],
		"skill_q": [KEY_Q],
		"skill_e": [KEY_E],
	}
	for action: String in keys:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		for code: Key in keys[action]:
			var ev := InputEventKey.new()
			ev.physical_keycode = code
			InputMap.action_add_event(action, ev)
	var buttons := {"melee": MOUSE_BUTTON_LEFT, "bolt": MOUSE_BUTTON_RIGHT}
	for action: String in buttons:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		var mb := InputEventMouseButton.new()
		mb.button_index = buttons[action]
		InputMap.action_add_event(action, mb)


# --- phases ---------------------------------------------------------------

func _map_center() -> Vector2:
	return grid.position + Vector2(DungeonGrid.W, DungeonGrid.H) * DungeonGrid.TILE / 2.0


func _zoom_to(z: float, pos: Vector2) -> void:
	# Clamp the view to the map only when zoomed in; zoomed out the whole map must fit.
	var big := 10000000
	var map := Rect2(grid.position, Vector2(DungeonGrid.W, DungeonGrid.H) * DungeonGrid.TILE)
	_camera.limit_left = int(map.position.x) if z >= 1.0 else -big
	_camera.limit_top = int(map.position.y) - 20 if z >= 1.0 else -big
	_camera.limit_right = int(map.end.x) if z >= 1.0 else big
	_camera.limit_bottom = int(map.end.y) + 30 if z >= 1.0 else big
	var tw := create_tween().set_parallel()
	tw.tween_property(_camera, "zoom", Vector2.ONE * z, 0.4).set_ease(Tween.EASE_OUT)
	tw.tween_property(_camera, "position", pos, 0.4).set_ease(Tween.EASE_OUT)


func _enter_build() -> void:
	phase = Phase.BUILD
	_zoom_to(BUILD_ZOOM, _map_center())
	lord.combat_enabled = false
	grid.traps_active = false
	for m: Monster in _monster_cells.values():
		m.reset_for_wave()
	for o: BuildObject in _object_cells.values():
		o.end_raid()
	# Waves 1-3 use one entrance, 4-7 two, 8-10 all three; which ones is random each wave.
	var count := 1 if wave < 3 else (2 if wave < 7 else grid.entrances.size())
	var order: Array[int] = []
	for i in grid.entrances.size():
		order.append(i)
	order.shuffle()
	grid.active_entrances = order.slice(0, count)
	_refresh_toolbar()
	_hud.show_toolbar(true)
	grid.refresh_preview()


## Starts the current wave's raid.
func start_raid() -> void:
	if phase != Phase.BUILD:
		return
	phase = Phase.RAID
	_zoom_to(RAID_ZOOM, lord.global_position)
	lord.combat_enabled = true
	grid.traps_active = true
	grid.hover_visible = false
	for o: BuildObject in _object_cells.values():
		o.active = true
	_hud.show_toolbar(false)
	_hud.toast("웨이브 %d 침입!" % (wave + 1), Color(1, 0.4, 0.3))
	_spawn_queue.clear()
	for group: Array in Waves.WAVES[wave].heroes:
		for i in int(group[1]):
			_spawn_queue.append(group[0])
	_spawn_timer = 0.0


func _physics_process(delta: float) -> void:
	if phase != Phase.RAID:
		return
	_spawn_timer -= delta
	if _spawn_timer <= 0.0 and not _spawn_queue.is_empty():
		_spawn_hero(_spawn_queue.pop_front())
		_spawn_timer = float(Waves.WAVES[wave].interval)
	if _spawn_queue.is_empty() and _alive_heroes == 0:
		_wave_cleared()


func _spawn_hero(kind: String, at: Vector2 = Vector2.INF) -> void:
	var hero := Hero.new()
	hero.setup(Units.HEROES[kind], float(Waves.WAVES[wave].hp_mult))
	hero.damage += int(Waves.WAVES[wave].get("dmg_bonus", 0))
	if kind != "boss" and randf() < Units.ELITE_CHANCE_PER_WAVE * maxi(wave - 2, 0):
		hero.make_elite()
	hero.grid = grid
	if at == Vector2.INF:
		at = grid.entrance_position(grid.active_entrances.pick_random()) + Vector2(randf_range(-4, 4), randf_range(-4, 4))
	hero.position = at
	add_child(hero)
	hero.summon_requested.connect(func(k: String, p: Vector2) -> void: _spawn_hero.call_deferred(k, p))
	_alive_heroes += 1
	hero.died.connect(_on_hero_died)
	hero.reached_core.connect(_on_hero_reached_core)


func _on_hero_died(hero: Unit) -> void:
	_alive_heroes -= 1
	gold += int(hero.stats.gold)
	if int(hero.stats.gold) > 0:
		Fx.text(self, hero.global_position + Vector2(0, -8), "+%dG" % hero.stats.gold, Color(1, 0.85, 0.3))
	lord_xp += 5 if hero.stats.sprite == "boss_hero" else (3 if (hero as Hero).elite else 1)
	if not _leveling and lord_xp >= lord_xp_needed() and phase == Phase.RAID:
		_level_up.call_deferred()
	for m: Monster in _monster_cells.values():
		if m.global_position.distance_to(hero.global_position) < 80.0:
			m.add_xp(1)
	if hero.stats.sprite == "boss_hero" or randf() < Arsenal.DROP_CHANCE:
		var choices := Arsenal.WEAPONS.keys().filter(func(k: String) -> bool: return k != "claws" and k != lord.weapon_id)
		var drop := WeaponPickup.new()
		drop.weapon_id = choices.pick_random()
		drop.position = hero.global_position
		drop.picked.connect(func(id: String) -> void: _hud.toast("%s 장착!" % Arsenal.WEAPONS[id].name, Arsenal.WEAPONS[id].color))
		add_child.call_deferred(drop)


func _on_hero_reached_core(hero: Hero) -> void:
	_alive_heroes -= 1
	damage_core(int(hero.stats.core_damage))


## Damages the dungeon core; at 0 the run is lost.
func damage_core(amount: int) -> void:
	if phase == Phase.OVER:
		return
	core_hp = maxi(core_hp - amount, 0)
	lord.shake(6.0)
	_hud.toast("코어가 공격받는 중! (-%d)" % amount, Color(1, 0.3, 0.3))
	if core_hp == 0:
		_end_run("패배... 웨이브 %d 에서 코어 파괴" % (wave + 1))


func _wave_cleared() -> void:
	wave += 1
	gold += Units.WAVE_CLEAR_GOLD
	if wave >= Waves.WAVES.size():
		_end_run("승리! 용사 보스 격파")
		return
	phase = Phase.REWARD
	for o: BuildObject in _object_cells.values():
		o.end_raid()
	_hud.toast("웨이브 클리어! +%dG" % Units.WAVE_CLEAR_GOLD)
	lord.combat_enabled = false
	grid.traps_active = false
	_rewards.open(Rewards.roll(3, self))


## XP the lord needs for the next level.
func lord_xp_needed() -> int:
	return 4 + 3 * lord_level


func _level_up() -> void:
	if _leveling or lord_xp < lord_xp_needed() or phase != Phase.RAID:
		return
	lord_xp -= lord_xp_needed()
	lord_level += 1
	_leveling = true
	lord.health = mini(lord.health + 3, lord.max_health)
	Fx.ring(self, lord.global_position, 60.0, Color(1, 0.85, 0.3))
	get_tree().paused = true
	_rewards.open(Rewards.roll(3, self), "마왕 레벨 %d! 능력을 하나 고르세요" % lord_level)


func _on_reward_picked(id: String) -> void:
	Rewards.apply(id, self)
	if _leveling:
		_leveling = false
		get_tree().paused = false
		_level_up.call_deferred()
		return
	_enter_build()


func _end_run(text: String) -> void:
	phase = Phase.OVER
	lord.combat_enabled = false
	grid.traps_active = false
	for node in get_tree().get_nodes_in_group("heroes"):
		node.set_physics_process(false)
	for o: BuildObject in _object_cells.values():
		o.end_raid()
	_hud.show_end(text)


func _restart() -> void:
	Engine.time_scale = 1.0
	get_tree().reload_current_scene()


func _on_lord_died() -> void:
	lord.remove_from_group("demon_lord")
	lord.visible = false
	lord.set_physics_process(false)
	await get_tree().create_timer(Units.LORD_RESPAWN_TIME).timeout
	lord.position = grid.cell_center(grid.core + Vector2i(-2, 0))
	lord.reset_stats()
	lord.visible = true
	lord.set_physics_process(true)
	lord.add_to_group("demon_lord")


# --- building -------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouse:
		# ponytail: click/hover use the event's own position, not get_global_mouse_position()
		_mouse_screen = (event as InputEventMouse).position
		_mouse_world = get_canvas_transform().affine_inverse() * _mouse_screen
	if phase != Phase.BUILD:
		return
	if event.is_action_pressed("start_raid"):
		start_raid()
	elif event is InputEventKey and event.pressed and (event as InputEventKey).physical_keycode == KEY_TAB:
		_hud.toggle_shop()
	elif event is InputEventKey and event.pressed and not event.echo:
		var i: int = (event as InputEventKey).physical_keycode - KEY_1
		if i >= 0 and i < TOOLS.size():
			_select_tool(TOOLS[i])
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if not try_build(grid.cell_at(_mouse_world)):
			_hud.toast(_build_error(grid.cell_at(_mouse_world)), Color(1, 0.45, 0.4))


func _select_tool(t: String) -> void:
	if not _tool_unlocked(t):
		_hud.toast("아직 잠김 (보상으로 해금)", Color(1, 0.45, 0.4))
		return
	tool = t
	_hud.select_tool(t)


func _refresh_toolbar() -> void:
	var tools: Array = []
	for i in TOOLS.size():
		var t: String = TOOLS[i]
		tools.append({"id": t, "key": i + 1, "label": _tool_label(t), "cost": _tool_cost(t),
			"color": _tool_color(t), "locked": not _tool_unlocked(t)})
	_hud.set_tools(tools)
	_hud.select_tool(tool)


func _tool_label(t: String) -> String:
	match t:
		"wall":
			return "벽"
		"upgrade":
			return "강화"
	for table: Dictionary in [Units.TRAPS, Units.OBJECTS, Units.MONSTERS]:
		if table.has(t):
			return table[t].name
	return t


func _tool_color(t: String) -> Color:
	match t:
		"wall":
			return Color(0.6, 0.6, 0.7)
		"upgrade":
			return Color(1.0, 0.85, 0.3)
	for table: Dictionary in [Units.TRAPS, Units.OBJECTS, Units.MONSTERS]:
		if table.has(t):
			return table[t].color
	return Color.WHITE


## Applies the selected tool at a cell if allowed and affordable. Returns true on success.
func try_build(c: Vector2i) -> bool:
	if _build_error(c) != "":
		return false
	gold -= _upgrade_cost(c) if tool == "upgrade" else _tool_cost(tool)
	Fx.burst(self, grid.cell_center(c), _tool_color(tool), 10, 70.0)
	match tool:
		"wall":
			grid.fill(c)
		"spike", "fire":
			grid.place_trap(c, tool)
		"upgrade":
			(_monster_cells[c] as Monster).level_up()
		"lure", "nest":
			var o := BuildObject.new()
			o.setup(tool)
			o.position = grid.cell_center(c)
			add_child(o)
			_object_cells[c] = o
		_:
			_place_monster(c, tool)
	grid.refresh_preview()
	return true


func _upgrade_cost(c: Vector2i) -> int:
	return Units.UPGRADE_COST * (_monster_cells[c] as Monster).level


## Why the selected tool can't be used on cell c, or "" if it can.
func _build_error(c: Vector2i) -> String:
	if not _tool_unlocked(tool):
		return "아직 잠긴 도구"
	if tool == "upgrade":
		if not _monster_cells.has(c):
			return "강화는 몬스터를 클릭"
		if (_monster_cells[c] as Monster).level >= Units.MONSTER_MAX_LEVEL:
			return "이미 최대 레벨"
		return "" if gold >= _upgrade_cost(c) else "골드 부족 (%dG 필요)" % _upgrade_cost(c)
	if gold < _tool_cost(tool):
		return "골드 부족 (%dG 필요)" % _tool_cost(tool)
	match tool:
		"wall":
			if grid.is_wall(c):
				return "벽은 바닥(밝은 칸)에만 세움"
			if _monster_cells.has(c) or _object_cells.has(c) or grid.traps.has(c) or grid.cell_at(lord.global_position) == c:
				return "이미 뭔가 있음"
			return "" if grid.can_fill(c) else "용사 길을 완전히 막을 순 없음"
		"spike", "fire":
			if not grid.is_buildable_floor(c):
				return "함정은 바닥에만"
			return "" if grid.can_place_trap(c) else "이미 함정 있음"
		_:
			if not grid.is_buildable_floor(c):
				return "바닥에만 놓을 수 있음"
			return "이미 뭔가 있음" if _monster_cells.has(c) or _object_cells.has(c) else ""


func _tool_cost(t: String) -> int:
	match t:
		"wall":
			return Units.WALL_COST
		"spike", "fire":
			return int(Units.TRAPS[t].cost)
		"lure", "nest":
			return int(Units.OBJECTS[t].cost)
		"upgrade":
			return Units.UPGRADE_COST
		_:
			return int(Units.MONSTERS[t].cost)


func _tool_unlocked(t: String) -> bool:
	return not Units.TRAPS.has(t) or unlocked_traps.has(t)


func _place_monster(c: Vector2i, kind: String) -> void:
	var m := Monster.new()
	m.kind = kind
	m.refresh(monster_mult.get(kind, Vector2.ONE))
	m.home = grid.cell_center(c)
	m.position = m.home
	add_child(m)
	_monster_cells[c] = m
	m.died.connect(func(_u: Unit) -> void: _monster_cells.erase(c))


## Permanently boosts one monster kind for the rest of the run (living and future ones).
func buff_monster(kind: String, add: Vector2) -> void:
	var mult: Vector2 = monster_mult.get(kind, Vector2.ONE) + add
	monster_mult[kind] = mult
	for m: Monster in _monster_cells.values():
		if m.kind == kind:
			m.refresh(mult)


# --- shop -----------------------------------------------------------------

func shop_price(id: String) -> int:
	var item: Dictionary = Units.SHOP[id]
	return int(item.price) + int(item.step) * int(_bought.get(id, 0))


## Buys a shop item during the build phase. Returns true on success.
func buy(id: String) -> bool:
	var price := shop_price(id)
	if phase != Phase.BUILD or gold < price or (id == "repair" and core_hp >= core_max):
		_hud.toast("살 수 없음", Color(1, 0.45, 0.4))
		return false
	gold -= price
	_bought[id] = int(_bought.get(id, 0)) + 1
	match id:
		"repair":
			core_hp = mini(core_hp + 5, core_max)
		"lord_atk":
			lord.melee_bonus += 1
		"lord_hp":
			lord.max_health += 3
			lord.health = lord.max_health
		"bolts":
			lord.max_bolts += 1
			lord.bolts = lord.max_bolts
		"traps":
			grid.trap_bonus += 1
	_hud.toast("%s 구매!" % Units.SHOP[id].name, Color(0.5, 1, 0.5))
	Fx.burst(self, lord.global_position, Color(1, 0.85, 0.3), 14, 80.0)
	return true


# --- HUD ------------------------------------------------------------------

func _process(delta: float) -> void:
	_mouse_world = get_canvas_transform().affine_inverse() * _mouse_screen
	grid.core_ratio = float(core_hp) / core_max
	if phase == Phase.RAID and _camera.zoom.x > 0.95:
		_camera.position = _camera.position.lerp(lord.global_position, minf(1.0, 8.0 * delta))
	if phase == Phase.BUILD:
		var c := grid.cell_at(_mouse_world)
		grid.hover_cell = c
		grid.hover_ok = _build_error(c) == ""
		grid.hover_visible = true
		var items: Array = []
		for id: String in Units.SHOP:
			var price := shop_price(id)
			items.append({"id": id, "label": Units.SHOP[id].name, "price": price,
				"enabled": gold >= price and (id != "repair" or core_hp < core_max)})
		_hud.set_shop(items)
	_hud.set_lines(_top_line(), _bottom_line())


func _top_line() -> String:
	var sk: Array[String] = []
	for i in 2:
		var id := lord.skills[i]
		var key: String = ["Q", "E"][i]
		if id == "":
			sk.append("%s -" % key)
		elif lord.skill_cd[i] > 0.0:
			sk.append("%s %s %.0fs" % [key, Arsenal.SKILLS[id].name, ceilf(lord.skill_cd[i])])
		else:
			sk.append("%s %s OK" % [key, Arsenal.SKILLS[id].name])
	return "HP %d/%d  마법탄 %s  골드 %d  웨이브 %d/%d  코어 %d/%d  마왕 Lv%d (%d/%d)\n무기: %s   스킬: %s" % [
		lord.health, lord.max_health,
		"●".repeat(lord.bolts) + "○".repeat(maxi(lord.max_bolts - lord.bolts, 0)),
		gold, mini(wave + 1, Waves.WAVES.size()), Waves.WAVES.size(), core_hp, core_max,
		lord_level, lord_xp, lord_xp_needed(),
		lord.weapon.name, "  ".join(sk),
	]


func _bottom_line() -> String:
	match phase:
		Phase.BUILD:
			var parts: Array[String] = []
			for group: Array in Waves.WAVES[wave].heroes:
				parts.append("%s x%d" % [Units.HEROES[group[0]].name, group[1]])
			return "다음: %s  입구 %d곳(!)  |  [Tab] 상점 · 점선=용사 경로 · [Enter] 침입" % [", ".join(parts), grid.active_entrances.size()]
		Phase.RAID:
			return "남은 용사 %d   좌클릭 공격 · 우클릭 마법탄 · Space 대시 · Q/E 스킬 · 떨어진 무기 밟으면 장착" % (_alive_heroes + _spawn_queue.size())
		_:
			return ""
