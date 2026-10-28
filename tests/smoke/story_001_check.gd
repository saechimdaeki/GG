extends SceneTree
## Headless self-check for the whole game loop (stories 001-007 + random dungeon, entrances,
## lure/nest, weapons, skills). Run:
##   godot --headless --path . -s res://tests/smoke/story_001_check.gd
## Prints PASS/FAIL per check and exits 1 on any failure.

var _fails := 0


func _initialize() -> void:
	# Random dungeon: 20 seeds all valid.
	var bad_maps := 0
	var has_water := false
	var has_lava := false
	for s in range(1, 21):
		var g := DungeonGrid.new()
		g.seed_value = s
		g.position = Vector2(0, 4)
		root.add_child(g)
		await process_frame
		if g.entrances.size() != 3 or not g.all_connected() or g.rooms.size() < 5:
			bad_maps += 1
		has_water = has_water or g.terrain.values().has("water")
		has_lava = has_lava or g.terrain.values().has("lava")
		g.free()
	_check("map: 20 seeds all have 3 entrances reaching the core", bad_maps == 0)
	_check("map: water and lava both appear across seeds", has_water and has_lava)

	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	var grid := game.grid
	var lord := game.lord
	game.gold = 9999

	# 002: build phase, dig / wall / trap rules
	_check("002 starts in build phase", game.phase == Game.Phase.BUILD)
	_check("B wave 1 uses one entrance", grid.active_entrances.size() == 1)
	_check("map is the big 60x36 grid", DungeonGrid.W == 60 and DungeonGrid.H == 36)
	game.tool = "wall"
	var placed := 0
	for c in _cells(grid, func(c: Vector2i) -> bool: return grid.is_buildable_floor(c)):
		if game.try_build(c):
			placed += 1
	_check("002 walls never cut an entrance off the core (%d placed)" % placed, placed > 0 and grid.all_connected())
	game.tool = "spike"
	var floor_cell := _find(grid, func(c: Vector2i) -> bool: return grid.can_place_trap(c))
	_check("002 spike trap placed on floor", game.try_build(floor_cell) and grid.traps.has(floor_cell))
	game.tool = "fire"
	_check("002 fire trap locked at start", not game.try_build(_find(grid, func(c: Vector2i) -> bool: return grid.can_place_trap(c))))

	# 003: monsters
	game.tool = "goblin"
	var mcell := _find(grid, func(c: Vector2i) -> bool: return grid.is_buildable_floor(c) and not grid.traps.has(c))
	_check("003 goblin placed", game.try_build(mcell))
	_check("003 one thing per cell", not game.try_build(mcell))
	var goblin := get_first_node_in_group("monsters") as Monster
	goblin.take_damage(999, Vector2.ZERO)
	await process_frame
	_check("003 dead monster is gone for the run", get_nodes_in_group("monsters").is_empty())

	# B: lure + nest
	game.tool = "lure"
	var lure_cell := _find(grid, func(c: Vector2i) -> bool: return game._build_error(c) == "")
	_check("B lure placed", game.try_build(lure_cell))
	var lure := get_first_node_in_group("lures") as BuildObject
	game.tool = "nest"
	_check("B nest placed", game.try_build(_find(grid, func(c: Vector2i) -> bool: return game._build_error(c) == "")))

	# raid
	game.start_raid()
	_check("004 raid starts", game.phase == Game.Phase.RAID)
	for i in 3:
		await physics_frame
	var hero := get_first_node_in_group("heroes") as Hero
	_check("005 wave 1 spawns a hero", hero != null)
	hero.set_physics_process(false)
	hero.global_position = lure.global_position + Vector2(16, 0)
	_check("B greedy hero targets a lure in reach", hero._pick_lure() == lure)

	# 001: lord combat
	var aim := (lord.get_global_mouse_position() - lord.global_position).normalized()
	hero.global_position = lord.global_position + aim * 15.0
	var hp_before := hero.health
	await _press("melee")
	_check("001 melee damages hero in arc", hero.health < hp_before)
	await _press("bolt")
	_check("001 bolt spends one charge", lord.bolts == lord.max_bolts - 1)
	await _press("dash")
	var lord_hp := lord.health
	lord.take_damage(1)
	_check("001 dash makes lord invulnerable", lord.health == lord_hp)

	# skills + weapons
	hero.health = 999
	hero.max_health = 999
	hero.global_position = lord.global_position + Vector2(20, 0)
	hp_before = hero.health
	_check("skill Q hellfire casts", lord.try_skill(0))
	_check("skill hellfire damages nearby hero", hero.health < hp_before)
	_check("skill on cooldown can't recast", not lord.try_skill(0))
	lord.learn("fear")
	_check("learned skill goes to empty E slot", lord.skills[1] == "fear")
	lord.try_skill(1)
	_check("skill fear stuns hero", hero._stun > 0.0)
	var drop := WeaponPickup.new()
	drop.weapon_id = "greatsword"
	drop.position = lord.global_position
	game.add_child(drop)
	for i in 2:
		await physics_frame
	_check("weapon pickup equips greatsword", lord.weapon_id == "greatsword")

	# 004: path from every entrance to the core
	var path := grid.find_path(grid.entrance_position(0), grid.core_position())
	_check("004 hero path ends at core", path.size() > 0 and path[path.size() - 1].distance_to(grid.core_position()) < 1.0)

	# 007: core damage
	var core_before := game.core_hp
	game.damage_core(3)
	_check("007 core takes damage", game.core_hp == core_before - 3)

	# 005/006: clearing the wave opens the reward screen
	for node in get_nodes_in_group("heroes"):
		(node as Hero).take_damage(99999, Vector2.ZERO)
	game._spawn_queue.clear()
	for i in 3:
		await physics_frame
	_check("005 wave cleared advances wave", game.wave == 1)
	_check("006 reward phase after clear", game.phase == Game.Phase.REWARD)
	var choices := Rewards.roll(3, game)
	var ids := choices.map(func(r: Dictionary) -> String: return r.id)
	_check("006 three distinct rewards", ids.size() == 3 and ids[0] != ids[1] and ids[1] != ids[2] and ids[0] != ids[2])
	_check("006 owned skills are not offered", not Rewards.roll(99, game).any(func(r: Dictionary) -> bool: return r.id == "skill_fear"))
	Rewards.apply("fire_trap", game)
	_check("006 fire trap reward unlocks fire", game.unlocked_traps.has("fire"))
	game._on_reward_picked("gold")
	_check("006 picking returns to build", game.phase == Game.Phase.BUILD)
	game.wave = 8
	game._enter_build()
	_check("B late waves use all three entrances", grid.active_entrances.size() == 3)
	_check("005 ten waves, last has boss", Waves.WAVES.size() == 10 and Waves.WAVES[9].heroes[0][0] == "boss")

	# enemies: elites, armor, dodge, heal, fireball, boss
	var g2 := Hero.new()
	g2.setup(Units.HEROES.guardian)
	g2.grid = grid
	g2.position = grid.core_position() + Vector2(0, 200)
	game.add_child(g2)
	await process_frame
	g2.set_physics_process(false)
	var ghp := g2.health
	g2.take_damage(4, Vector2.ZERO)
	_check("enemy guardian armor halves damage", ghp - g2.health == 2)
	var e := Hero.new()
	e.setup(Units.HEROES.warrior)
	var base_hp := e.max_health
	e.make_elite()
	_check("enemy elite has more HP and is bigger", e.max_health > base_hp and e.base_scale.x > 1.0)
	e.free()
	var rogue_misses := 0
	var rg := Hero.new()
	rg.setup(Units.HEROES.rogue)
	for i in 200:
		if rg._modify_damage(3) == 0:
			rogue_misses += 1
	_check("enemy rogue dodges some hits (%d/200)" % rogue_misses, rogue_misses > 30 and rogue_misses < 110)
	rg.free()
	var cl := Hero.new()
	cl.setup(Units.HEROES.cleric)
	cl.grid = grid
	cl.position = g2.position + Vector2(10, 0)
	game.add_child(cl)
	await process_frame
	cl.set_physics_process(false)
	g2.health = 5
	cl._skill_cd = 0.0
	cl._tick_skill(0.016)
	_check("enemy cleric heals a hurt ally", g2.health > 5)
	cl.queue_free()
	g2.queue_free()
	var tg := Telegraph.new()
	tg.position = lord.global_position
	tg.delay = 0.05
	game.add_child(tg)
	Engine.time_scale = 1.0
	lord._dash_left = 0.0
	var lhp := lord.health
	for i in 12:
		await physics_frame
	_check("enemy fireball telegraph hits the lord", lord.health < lhp)
	var boss := Hero.new()
	boss.setup(Units.HEROES.boss)
	boss.grid = grid
	boss.position = grid.core_position() + Vector2(0, 200)
	game.add_child(boss)
	await process_frame
	boss.set_physics_process(false)
	var summons := [0]
	boss.summon_requested.connect(func(_k: String, _p: Vector2) -> void: summons[0] += 1)
	for i in 3:
		boss._skill_cd = 0.0
		boss._target = lord
		boss._tick_skill(0.016)
	_check("boss cycles patterns incl. summoning 2", summons[0] == 2)
	boss.queue_free()

	# monster growth + upgrade tool + shop
	game.phase = Game.Phase.BUILD
	game.tool = "slime"
	var scell := _find(grid, func(c: Vector2i) -> bool: return game._build_error(c) == "")
	game.try_build(scell)
	var sl: Monster = game._monster_cells[scell]
	var hp1 := sl.max_health
	for i in Units.MONSTER_XP[0]:
		sl.add_xp(1)
	_check("monster levels up from XP", sl.level == 2 and sl.max_health > hp1)
	game.tool = "upgrade"
	var g_before := game.gold
	_check("upgrade tool levels a monster for gold", game.try_build(scell) and sl.level == 3 and game.gold == g_before - Units.UPGRADE_COST * 2)
	_check("upgrade refused at max level", not game.try_build(scell))
	var atk := lord.melee_bonus
	var p1 := game.shop_price("lord_atk")
	_check("shop buys lord attack", game.buy("lord_atk") and lord.melee_bonus == atk + 1)
	_check("shop price rises after buying", game.shop_price("lord_atk") > p1)
	game.buy("traps")
	_check("shop trap upgrade raises trap damage", grid.trap_bonus == 1)

	# lord level-up mid-raid
	game.phase = Game.Phase.RAID
	var lv := game.lord_level
	game.lord_xp = game.lord_xp_needed()
	game._level_up()
	_check("lord levels up and pauses for a card", game.lord_level == lv + 1 and paused and game._leveling)
	game._on_reward_picked("lord_hp")
	_check("picking a level card resumes the raid", not paused and game.phase == Game.Phase.RAID)

	# 007: core 0 ends the run
	game.damage_core(999)
	_check("007 core 0 ends run", game.phase == Game.Phase.OVER)

	quit(1 if _fails > 0 else 0)


func _cells(grid: DungeonGrid, pred: Callable) -> Array[Vector2i]:
	var out: Array[Vector2i] = []
	for y in DungeonGrid.H:
		for x in DungeonGrid.W:
			if pred.call(Vector2i(x, y)):
				out.append(Vector2i(x, y))
	return out


func _find(grid: DungeonGrid, pred: Callable) -> Vector2i:
	var all := _cells(grid, pred)
	return all[0] if not all.is_empty() else Vector2i(-1, -1)


func _press(action: String) -> void:
	Input.action_press(action)
	await physics_frame
	await physics_frame
	Input.action_release(action)


func _check(name: String, ok: bool) -> void:
	print(("PASS " if ok else "FAIL ") + name)
	if not ok:
		_fails += 1
