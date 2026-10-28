class_name Game
extends Node2D
## Game controller for one run: build phase -> raid -> reward, ten waves, core HP, run end.

enum Phase { BUILD, RAID, REWARD, OVER }

## Build tools selected with keys 1-7, in this order.
const TOOLS := ["dig", "wall", "spike", "fire", "slime", "goblin", "skeleton"]

var phase: Phase = Phase.BUILD
var gold: int = Units.START_GOLD
var core_hp: int = Units.CORE_HP
## Index of the next (or current) wave in Waves.WAVES.
var wave: int = 0
var tool: String = "dig"
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


func _ready() -> void:
	Engine.time_scale = 1.0
	_setup_input()
	grid = DungeonGrid.new()
	grid.position = Vector2(0, 4)
	add_child(grid)
	_camera = Camera2D.new()
	_camera.position = Vector2(320, 180)
	add_child(_camera)
	lord = DemonLord.new()
	lord.camera = _camera
	lord.bounds = Rect2(0, 4, 640, 352)
	lord.position = grid.cell_center(grid.core + Vector2i(-2, 0))
	add_child(lord)
	lord.died.connect(_on_lord_died)
	_hud = Hud.new()
	add_child(_hud)
	_hud.restart_pressed.connect(_restart)
	_rewards = RewardScreen.new()
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

func _enter_build() -> void:
	phase = Phase.BUILD
	lord.combat_enabled = false
	grid.traps_active = false
	for m: Monster in _monster_cells.values():
		m.reset_for_wave()


## Starts the current wave's raid.
func start_raid() -> void:
	if phase != Phase.BUILD:
		return
	phase = Phase.RAID
	lord.combat_enabled = true
	grid.traps_active = true
	grid.hover_visible = false
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


func _spawn_hero(kind: String) -> void:
	var hero := Hero.new()
	hero.setup(Units.HEROES[kind], float(Waves.WAVES[wave].hp_mult))
	hero.grid = grid
	hero.position = grid.entrance_position() + Vector2(0, randf_range(-6, 6))
	add_child(hero)
	_alive_heroes += 1
	hero.died.connect(_on_hero_died)
	hero.reached_core.connect(_on_hero_reached_core)


func _on_hero_died(hero: Unit) -> void:
	_alive_heroes -= 1
	gold += int(hero.stats.gold)


func _on_hero_reached_core(hero: Hero) -> void:
	_alive_heroes -= 1
	damage_core(int(hero.stats.core_damage))


## Damages the dungeon core; at 0 the run is lost.
func damage_core(amount: int) -> void:
	if phase == Phase.OVER:
		return
	core_hp = maxi(core_hp - amount, 0)
	lord.shake(6.0)
	if core_hp == 0:
		_end_run("패배... 웨이브 %d 에서 코어 파괴" % (wave + 1))


func _wave_cleared() -> void:
	wave += 1
	gold += Units.WAVE_CLEAR_GOLD
	if wave >= Waves.WAVES.size():
		_end_run("승리! 용사 보스 격파")
		return
	phase = Phase.REWARD
	lord.combat_enabled = false
	grid.traps_active = false
	_rewards.open(Rewards.roll(3, self))


func _on_reward_picked(id: String) -> void:
	Rewards.apply(id, self)
	_enter_build()


func _end_run(text: String) -> void:
	phase = Phase.OVER
	lord.combat_enabled = false
	grid.traps_active = false
	for node in get_tree().get_nodes_in_group("heroes"):
		node.set_physics_process(false)
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
	if phase != Phase.BUILD:
		return
	if event.is_action_pressed("start_raid"):
		start_raid()
	elif event is InputEventKey and event.pressed:
		var i: int = (event as InputEventKey).physical_keycode - KEY_1
		if i >= 0 and i < TOOLS.size() and _tool_unlocked(TOOLS[i]):
			tool = TOOLS[i]
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		try_build(grid.cell_at(get_global_mouse_position()))


## Applies the selected tool at a cell if allowed and affordable. Returns true on success.
func try_build(c: Vector2i) -> bool:
	if not _can_build(c):
		return false
	gold -= _tool_cost(tool)
	match tool:
		"dig":
			grid.dig(c)
		"wall":
			grid.fill(c)
		"spike", "fire":
			grid.place_trap(c, tool)
		_:
			_place_monster(c, tool)
	return true


func _can_build(c: Vector2i) -> bool:
	if not _tool_unlocked(tool) or gold < _tool_cost(tool):
		return false
	match tool:
		"dig":
			return grid.can_dig(c)
		"wall":
			return grid.can_fill(c) and not _monster_cells.has(c) and grid.cell_at(lord.global_position) != c
		"spike", "fire":
			return grid.can_place_trap(c)
		_:
			return grid.is_buildable_floor(c) and not _monster_cells.has(c)


func _tool_cost(t: String) -> int:
	match t:
		"dig":
			return Units.DIG_COST
		"wall":
			return Units.WALL_COST
		"spike", "fire":
			return int(Units.TRAPS[t].cost)
		_:
			return int(Units.MONSTERS[t].cost)


func _tool_unlocked(t: String) -> bool:
	return not Units.TRAPS.has(t) or unlocked_traps.has(t)


func _place_monster(c: Vector2i, kind: String) -> void:
	var m := Monster.new()
	var mult: Vector2 = monster_mult.get(kind, Vector2.ONE)
	m.setup(Units.MONSTERS[kind], mult.x, mult.y)
	m.kind = kind
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
			m.setup(Units.MONSTERS[kind], mult.x, mult.y)


# --- HUD ------------------------------------------------------------------

func _process(_delta: float) -> void:
	if phase == Phase.BUILD:
		var c := grid.cell_at(get_global_mouse_position())
		grid.hover_cell = c
		grid.hover_ok = _can_build(c)
		grid.hover_visible = true
	_hud.set_lines(_top_line(), _bottom_line())


func _top_line() -> String:
	return "HP %d/%d   마법탄 %s   골드 %d   웨이브 %d/%d   코어 %d/%d" % [
		lord.health, lord.max_health,
		"●".repeat(lord.bolts) + "○".repeat(maxi(lord.max_bolts - lord.bolts, 0)),
		gold, mini(wave + 1, Waves.WAVES.size()), Waves.WAVES.size(), core_hp, Units.CORE_HP,
	]


func _bottom_line() -> String:
	match phase:
		Phase.BUILD:
			var parts: Array[String] = []
			for i in TOOLS.size():
				var t: String = TOOLS[i]
				if not _tool_unlocked(t):
					continue
				var label: String = {"dig": "굴착", "wall": "벽"}.get(t, "")
				if label == "":
					label = Units.TRAPS[t].name if Units.TRAPS.has(t) else Units.MONSTERS[t].name
				var mark := ">" if t == tool else " "
				parts.append("%s[%d]%s %d" % [mark, i + 1, label, _tool_cost(t)])
			return "건설: " + "  ".join(parts) + "   |  Enter: 침입 시작"
		Phase.RAID:
			return "침입 중! 남은 용사 %d   좌클릭 근접 / 우클릭 마법탄 / Space 대시" % (_alive_heroes + _spawn_queue.size())
		_:
			return ""
