class_name Game
extends Node2D
## Game controller for one run: build phase -> raid -> reward, ten waves, core HP, run end.

enum Phase { BUILD, RAID, REWARD, OVER }

## Region for the next new run (set by the title screen); a continued run uses its saved one.
static var next_biome: String = "dungeon"

## Build tools selected with keys 1-9 and 0, in this order.
const TOOLS := ["wall", "spike", "fire", "slime", "goblin", "skeleton", "lure", "nest", "mine", "upgrade"]
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
## Permanent upgrades (Meta.load_data()), applied at run start, spent on the end screen.
var meta: Dictionary = {}
var _end_text: String = ""
var _pause: PauseMenu
## This run's region (key of Rooms.BIOMES).
var biome: String = "dungeon"
## Heroes killed this run (tutorial progress, stats).
var kills: int = 0
## True when this run was loaded from a save (skip the intro).
var _continued := false


func _ready() -> void:
	Engine.time_scale = 1.0
	_setup_input()
	var dark := CanvasModulate.new()
	dark.color = Color(0.62, 0.6, 0.7)
	add_child(dark)
	add_child(make_glow_env())
	grid = DungeonGrid.new()
	var saved := RunSave.read() if RunSave.load_next else {}
	RunSave.load_next = false
	biome = String(saved.get("biome", next_biome))
	grid.biome = biome
	dark.color = Rooms.BIOMES[biome].modulate
	if not saved.is_empty():
		grid.seed_value = int(saved.seed)
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
	_apply_meta()
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
	_pause = PauseMenu.new()
	add_child(_pause)
	_hud.title_pressed.connect(_to_title)
	if not saved.is_empty():
		RunSave.restore(self, saved)
		_continued = true
	if Tutorial.pending() and not _continued:
		var tut := Tutorial.new()
		tut.setup(self)
		add_child(tut)
	_enter_build()


## Bloom: anything drawn brighter than GLOW_THRESHOLD (bolts, fire, core, torches) glows.
static func make_glow_env() -> WorldEnvironment:
	var env := Environment.new()
	env.background_mode = Environment.BG_CANVAS
	env.glow_enabled = true
	env.glow_intensity = 0.6
	env.glow_bloom = 0.0
	env.glow_hdr_threshold = 1.0
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_ADDITIVE
	for i in 7:
		env.set_glow_level(i, 1.0 if i in [1, 2, 3, 4] else 0.0)
	var we := WorldEnvironment.new()
	we.environment = env
	return we


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
	Audio.music("music_build")
	_zoom_to(BUILD_ZOOM, _map_center())
	lord.combat_enabled = false
	grid.traps_active = false
	for m: Monster in _monster_cells.values():
		m.reset_for_wave()
	for n in get_tree().get_nodes_in_group("summons"):
		n.queue_free()
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
	RunSave.save(self)  # autosave: 이어하기 resumes from this build phase
	_hud.set_status(_status(), _bottom_line())  # HUD is filled even while dialogue pauses _process
	if Story.enabled:
		_hud.show_dialog(Story.INTRO if wave == 0 and not _continued else Story.WAVE_LINES.get(wave, []))


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
	Audio.play("wave_start")
	Audio.music("music_raid")
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
	hero.setup(Units.HEROES[kind], float(Waves.WAVES[wave].hp_mult) * float(Rooms.BIOMES[biome].hero_hp))
	hero.damage += int(Waves.WAVES[wave].get("dmg_bonus", 0))
	if kind != "boss" and randf() < Units.ELITE_CHANCE_PER_WAVE * maxi(wave - 2, 0) * float(Rooms.BIOMES[biome].elite):
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
	kills += 1
	gold += int(hero.stats.gold)
	if int(hero.stats.gold) > 0:
		Fx.text(self, hero.global_position + Vector2(0, -8), "+%dG" % hero.stats.gold, Color(1, 0.85, 0.3))
		Audio.play("coin", -10.0)
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
	Audio.play("core_hit")
	if core_hp == 0:
		_end_run("패배... 웨이브 %d 에서 코어 파괴" % (wave + 1))


func _wave_cleared() -> void:
	wave += 1
	gold += wave_clear_gold()
	if wave >= Waves.WAVES.size():
		_end_run("승리! 용사 보스 격파")
		return
	phase = Phase.REWARD
	for o: BuildObject in _object_cells.values():
		o.end_raid()
	var mined := _collect_mines()
	_hud.toast("웨이브 클리어! +%dG%s" % [wave_clear_gold(), "  광산 +%dG" % mined if mined > 0 else ""])
	Audio.play("wave_clear")
	Audio.music("music_build")
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
	Audio.play("level_up")
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
	RunSave.clear()
	Audio.music("")
	Audio.play("victory" if wave >= Waves.WAVES.size() else "game_over", 0.0, 0.0)
	lord.combat_enabled = false
	grid.traps_active = false
	for node in get_tree().get_nodes_in_group("heroes"):
		node.set_physics_process(false)
	for o: BuildObject in _object_cells.values():
		o.end_raid()
	var earned := roundi(Meta.souls_for(wave, wave >= Waves.WAVES.size()) * float(Rooms.BIOMES[biome].souls))
	meta.souls += earned
	Meta.save_data(meta)
	_end_text = "%s\n%s\n영혼석 +%d" % [text, Story.VICTORY if wave >= Waves.WAVES.size() else Story.DEFEAT, earned]
	_show_end()


func _apply_meta() -> void:
	meta = Meta.load_data()
	gold += Meta.bonus(meta, "start_gold")
	core_max += Meta.bonus(meta, "core_hp")
	core_hp = core_max
	lord.max_health += Meta.bonus(meta, "lord_hp")
	lord.health = lord.max_health
	lord.melee_bonus += Meta.bonus(meta, "lord_atk")
	if Meta.bonus(meta, "fire_start") > 0:
		unlocked_traps.append("fire")
	if Meta.bonus(meta, "dash_master") > 0:
		lord.dash_cooldown *= 0.8
	if Meta.bonus(meta, "second_skill") > 0:
		var pool := Arsenal.SKILLS.keys().filter(func(k: String) -> bool: return not lord.skills.has(k))
		lord.learn(pool[randi() % pool.size()])
	grid.trap_bonus += Meta.bonus(meta, "trap_master")
	var mhp := 1.0 + Meta.bonus(meta, "monster_hp") / 100.0
	var mdmg := 1.0 + Meta.bonus(meta, "monster_dmg") / 100.0
	for kind: String in Units.MONSTERS:
		monster_mult[kind] = Vector2(mhp, mdmg)


func _show_end() -> void:
	_hud.show_end("%s\n영구 강화는 다음 판부터 적용" % _end_text, meta)


## Buys a permanent upgrade on the end screen.
func buy_meta(id: String) -> bool:
	if phase != Phase.OVER or not Meta.buy(meta, id):
		Audio.play("error", -6.0)
		return false
	Audio.play("coin")
	_show_end()
	return true


func _to_title() -> void:
	get_tree().paused = false
	Engine.time_scale = 1.0
	get_tree().change_scene_to_file("res://src/scenes/title.tscn")


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
		var code := (event as InputEventKey).physical_keycode
		var i: int = 9 if code == KEY_0 else code - KEY_1
		if i >= 0 and i < TOOLS.size():
			_select_tool(TOOLS[i])
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var gi := grid.gate_at(grid.cell_at(_mouse_world))
		if gi != -1:
			toggle_gate(gi)
		elif not try_build(grid.cell_at(_mouse_world)):
			_hud.toast(_build_error(grid.cell_at(_mouse_world)), Color(1, 0.45, 0.4))
			Audio.play("error", -6.0)


func _select_tool(t: String) -> void:
	if not _tool_unlocked(t):
		_hud.toast("아직 잠김 (보상으로 해금)", Color(1, 0.45, 0.4))
		return
	tool = t
	Audio.play("click", -6.0)
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
	Audio.play("build", -4.0)
	match tool:
		"wall":
			grid.fill(c)
		"spike", "fire":
			grid.place_trap(c, tool)
		"upgrade":
			(_monster_cells[c] as Monster).level_up()
		"lure", "nest", "mine":
			place_object(c, tool)
		_:
			place_monster(c, tool)
	grid.refresh_preview()
	return true


## Pays every mine that wasn't robbed this raid, then resets them. Returns the gold earned.
func _collect_mines() -> int:
	var total := 0
	for o: BuildObject in _object_cells.values():
		if o.kind != "mine":
			continue
		if not o.robbed:
			total += int(o.data.income)
			Fx.text(self, o.global_position + Vector2(0, -10), "+%dG" % int(o.data.income), Color(0.8, 0.6, 1.0))
		o.robbed = false
		o.queue_redraw()
	gold += total
	return total


## Opens/closes an iron gate (free, build phase). Closing can't cut heroes off the core.
func toggle_gate(i: int) -> bool:
	if phase != Phase.BUILD:
		return false
	if not grid.toggle_gate(i):
		_hud.toast("철문을 닫으면 용사 길이 완전히 막힘", Color(1, 0.45, 0.4))
		Audio.play("error", -6.0)
		return false
	_hud.toast("철문 닫힘 — 용사가 돌아감" if grid.gates[i].closed else "철문 열림", Color(0.7, 0.8, 1.0))
	Audio.play("gate")
	grid.refresh_preview()
	return true


## Gold for clearing a wave (base + 전리품 금고 meta upgrade).
func wave_clear_gold() -> int:
	return Units.WAVE_CLEAR_GOLD + Meta.bonus(meta, "war_chest")


func _upgrade_cost(c: Vector2i) -> int:
	var cost := Units.UPGRADE_COST * (_monster_cells[c] as Monster).level
	return roundi(cost * 0.7) if Meta.bonus(meta, "upgrade_discount") > 0 else cost


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
		"lure", "nest", "mine":
			return int(Units.OBJECTS[t].cost)
		"upgrade":
			return Units.UPGRADE_COST
		_:
			return int(Units.MONSTERS[t].cost)


func _tool_unlocked(t: String) -> bool:
	return not Units.TRAPS.has(t) or unlocked_traps.has(t)


## Spawns a monster guarding cell c (build tool, or restoring a save).
func place_monster(c: Vector2i, kind: String) -> Monster:
	var m := Monster.new()
	m.kind = kind
	m.refresh(monster_mult.get(kind, Vector2.ONE))
	m.home = grid.cell_center(c)
	m.position = m.home
	add_child(m)
	_monster_cells[c] = m
	m.died.connect(func(_u: Unit) -> void: _monster_cells.erase(c))
	return m


## Places a lure / nest on cell c.
func place_object(c: Vector2i, kind: String) -> BuildObject:
	var o := BuildObject.new()
	o.setup(kind)
	o.position = grid.cell_center(c)
	add_child(o)
	_object_cells[c] = o
	return o


## Vector2i -> Monster, for saving.
func monster_cells() -> Dictionary:
	return _monster_cells


## Vector2i -> BuildObject, for saving.
func object_cells() -> Dictionary:
	return _object_cells


## Shop item id -> times bought this run (prices grow with it).
func bought() -> Dictionary:
	return _bought


func set_bought(b: Dictionary) -> void:
	_bought = b


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
	Audio.play("coin")
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
		grid.hover_ok = grid.gate_at(c) != -1 or _build_error(c) == ""
		grid.hover_visible = true
		var items: Array = []
		for id: String in Units.SHOP:
			var price := shop_price(id)
			items.append({"id": id, "label": Units.SHOP[id].name, "price": price,
				"enabled": gold >= price and (id != "repair" or core_hp < core_max)})
		_hud.set_shop(items)
	_hud.set_status(_status(), _bottom_line())


## Numbers for the StatusPanel HUD.
func _status() -> Dictionary:
	var skills: Array = []
	for i in 2:
		var id := lord.skills[i]
		var sk: Dictionary = Arsenal.SKILLS.get(id, {})
		skills.append({"key": ["Q", "E"][i], "name": String(sk.get("name", "")).split(" ")[-1], "cd": lord.skill_cd[i],
			"cd_ratio": lord.skill_cd[i] / float(sk.cooldown) if id != "" else 0.0,
			"color": sk.get("color", Color.GRAY)})
	var phase_text := ""
	match phase:
		Phase.BUILD: phase_text = "%s · 건설 단계 — [Enter] 침입 시작" % Rooms.BIOMES[biome].name
		Phase.RAID: phase_text = "남은 용사 %d" % (_alive_heroes + _spawn_queue.size())
	return {
		"hp": lord.health, "hp_max": lord.max_health,
		"bolts": lord.bolts, "bolts_max": lord.max_bolts,
		"bolt_charge": lord._recharge / lord.bolt_recharge_time,
		"level": lord_level, "xp": lord_xp, "xp_need": lord_xp_needed(),
		"gold": gold, "core": core_hp, "core_max": core_max,
		"wave": mini(wave + 1, Waves.WAVES.size()), "waves": Waves.WAVES.size(),
		"phase_text": phase_text, "raid": phase == Phase.RAID,
		"weapon": String(lord.weapon.name).split(" ")[-1], "skills": skills,
	}


func _bottom_line() -> String:
	match phase:
		Phase.BUILD:
			var parts: Array[String] = []
			for group: Array in Waves.WAVES[wave].heroes:
				parts.append("%s x%d" % [Units.HEROES[group[0]].name, group[1]])
			return "다음 웨이브: %s · 입구 %d곳(!)   [Tab] 상점 · 철문 클릭 = 열기/닫기 · 점선 = 용사 경로" % [", ".join(parts), grid.active_entrances.size()]
		Phase.RAID:
			return "좌클릭 공격 · 우클릭 마법탄 · Space 대시 · Q/E 스킬 · 무기는 밟으면 장착"
		_:
			return ""
