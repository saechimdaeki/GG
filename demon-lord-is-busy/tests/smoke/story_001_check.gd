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
	var themes := {}
	var altar_seen := false
	var gate_maps := 0
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
		for t in g.room_themes:
			themes[t] = true
		altar_seen = altar_seen or not g.altars.is_empty()
		gate_maps += 1 if not g.gates.is_empty() else 0
		g.free()
	_check("map: 20 seeds all have 3 entrances reaching the core", bad_maps == 0)
	_check("map: water and lava both appear across seeds", has_water and has_lava)
	var forge_rooms := {"dungeon": 0, "volcano": 0}
	for b: String in forge_rooms:
		for sd in range(1, 6):
			var bg := DungeonGrid.new()
			bg.seed_value = sd
			bg.biome = b
			root.add_child(bg)
			await process_frame
			forge_rooms[b] += bg.room_themes.count("forge")
			bg.free()
	_check("biome: volcano maps have more forge rooms (%s)" % forge_rooms, forge_rooms.volcano > forge_rooms.dungeon * 2)
	_check("map: most maps have iron gates (%d/20)" % gate_maps, gate_maps >= 15)
	_check("map: rooms get several themes (%d) and altars appear" % themes.size(), themes.size() >= 5 and altar_seen)

	# Meta save goes to a throwaway file so the check never touches the player's save.
	Meta.save_path = "user://meta_check.cfg"
	DirAccess.remove_absolute(Meta.save_path)
	RunSave.save_path = "user://run_check.cfg"  # never touch the player's save
	Story.enabled = false  # dialogue would pause the game waiting for a click
	Tutorial.enabled = false
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	var grid := game.grid
	var lord := game.lord
	game.gold = 9999

	# 002: build phase, dig / wall / trap rules
	_check("002 starts in build phase", game.phase == Game.Phase.BUILD)
	_check("B wave 1 uses one entrance", grid.active_entrances.size() == 1)
	if not grid.gates.is_empty():
		var before := grid.find_path(grid.entrance_position(0), grid.core_position()).size()
		var closed_ok := game.toggle_gate(0)
		_check("gate: closing keeps entrances connected", grid.all_connected())
		_check("gate: closed gate is not buildable", not grid.is_buildable_floor(grid.gates[0].cells[0]))
		if closed_ok:
			game.toggle_gate(0)
		_check("gate: reopening restores the path", grid.find_path(grid.entrance_position(0), grid.core_position()).size() == before and not grid.gates[0].closed)
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
	_check("003 dead monster is down, not gone", is_instance_valid(goblin) and goblin.down_t > 0.0
		and get_nodes_in_group("monsters").is_empty() and game.monster_cells().has(mcell))
	goblin.down_t = 0.01
	for i in 3:
		await physics_frame
	_check("003 monster respawns at home with full HP", goblin.is_in_group("monsters")
		and goblin.health == goblin.max_health and goblin.global_position == goblin.home)

	# B: lure + nest
	game.tool = "lure"
	var lure_cell := _find(grid, func(c: Vector2i) -> bool: return game._build_error(c) == "")
	_check("B lure placed", game.try_build(lure_cell))
	var lure := get_first_node_in_group("lures") as BuildObject
	game.tool = "nest"
	_check("B nest placed", game.try_build(_find(grid, func(c: Vector2i) -> bool: return game._build_error(c) == "")))
	game.tool = "mine"
	_check("mine placed", game.try_build(_find(grid, func(c: Vector2i) -> bool: return game._build_error(c) == "")))
	var mine := get_first_node_in_group("mines") as BuildObject
	var g0 := game.gold
	_check("mine pays income at wave clear", game._collect_mines() == int(Units.OBJECTS.mine.income) and game.gold == g0 + int(Units.OBJECTS.mine.income))
	mine.robbed = true
	_check("robbed mine pays nothing and resets", game._collect_mines() == 0 and not mine.robbed)

	# raid
	game.start_raid()
	_check("004 raid starts", game.phase == Game.Phase.RAID)
	var nest: BuildObject = game.object_cells().values().filter(func(o: BuildObject) -> bool: return o.kind == "nest")[0]
	nest._clock = 999.0
	for i in 3:
		await physics_frame
	var spawned_one := not nest._spawned.is_empty()
	if spawned_one:
		nest._spawned[0].free()
	for i in 2:
		await physics_frame
	_check("nest spawns and forgets dead summons", spawned_one and nest._spawned.is_empty())
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
	# Heroes at the core stay and hit it instead of vanishing into it.
	var raider := Hero.new()
	raider.setup(Units.HEROES.warrior)
	raider.grid = grid
	raider.position = grid.core_position()
	game.add_child(raider)
	await process_frame
	raider.enemy_groups = []  # ignore the lord and monsters: only the core
	var hits := [0]
	raider.attacked_core.connect(func(_h: Hero, _a: int) -> void: hits[0] += 1)
	raider._core_t = 0.0
	for i in 3:
		await physics_frame
	raider._core_t = 0.0
	for i in 3:
		await physics_frame
	_check("hero at the core keeps hitting it and stays", hits[0] >= 2 and is_instance_valid(raider))
	raider.free()

	# A/C: trap combos and the kick.
	var dummy := Hero.new()
	dummy.setup(Units.HEROES.guardian, 20.0)
	dummy.grid = grid
	dummy.position = lord.global_position + Vector2(200, 0)
	game.add_child(dummy)
	await process_frame
	dummy.enemy_groups = []
	var combos: Array[String] = []
	dummy.combo.connect(func(n: String, _p: Vector2) -> void: combos.append(n))
	dummy.on_trap("oil", Units.TRAPS.oil, 0, Vector2.ZERO)
	dummy.on_trap("fire", Units.TRAPS.fire, 0, Vector2.ZERO)
	_check("oil + fire explodes (combo)", combos.has("기름 폭발") and dummy.oil_t <= 0.0)
	dummy.on_trap("glue", Units.TRAPS.glue, 0, Vector2.ZERO)
	_check("glue slows the hero", dummy._speed_mult() < 1.0)
	_check("push aims somewhere", absf(grid.push_dir(dummy).length() - 1.0) < 0.01)
	var to_mouse := (lord.get_global_mouse_position() - lord.global_position).normalized()
	if to_mouse == Vector2.ZERO:
		to_mouse = Vector2.RIGHT
	dummy.global_position = lord.global_position + to_mouse * 14.0
	lord.kick_cd = 0.0
	_check("F kick launches a hero in front", lord.kick() == 1 and dummy.airborne())
	var g0c := game.gold
	game.on_combo("테스트", Vector2.ZERO)
	game.on_combo("테스트", Vector2.ZERO)
	_check("combo chain pays COMBO_GOLD x chain", game.gold == g0c + Units.COMBO_GOLD * 3)
	# Lava dive: a launched hero landing on lava dies outright.
	dummy._air_t = 0.0  # settle the hero the kick above sent flying
	dummy._knockback = Vector2.ZERO
	dummy.rotation = 0.0
	# Lava on the 3x3 around the hero: after the kick it can sit against a wall, and
	# move_and_slide may nudge it into a neighbouring cell as it lands (CI flake).
	var lava_cell := dummy.grid.cell_at(dummy.global_position)
	var had := {}
	for dx in [-1, 0, 1]:
		for dy in [-1, 0, 1]:
			var c: Vector2i = lava_cell + Vector2i(dx, dy)
			had[c] = grid.terrain.get(c)
			grid.terrain[c] = "lava"
	dummy.launch(Vector2.ZERO, 0.02)
	dummy.health = 999
	for i in 60:  # hitstop from the combos above slows time; wait for the landing
		if not is_instance_valid(dummy) or not dummy.airborne():
			break
		await physics_frame
	var landed := "gone" if not is_instance_valid(dummy) else "%s hp %d air %s" % [
		dummy.grid.cell_at(dummy.global_position), dummy.health, dummy.airborne()]
	_check("launched hero landing in lava dies (lava dive) [lava around %s, hero %s]" % [lava_cell, landed],
		not is_instance_valid(dummy) or dummy.health <= 0)
	for c in had:
		if had[c] == null:
			grid.terrain.erase(c)
		else:
			grid.terrain[c] = had[c]
	# B: perk cards are rule changes, offered once.
	Rewards.apply("bowling", game)
	_check("perk card is recorded and not offered again", game.perks.has("bowling")
		and not Rewards.roll(99, game).any(func(r: Dictionary) -> bool: return r.id == "bowling"))
	game.perks.erase("bowling")

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
	# Hero skills fire when the lord is close (skill cooldown restarts).
	var fired: Array[String] = []
	# A floor cell with floor on both sides (room for the rogue to blink behind), nearest the core.
	var opens := _cells(grid, func(c: Vector2i) -> bool:
		return not grid.is_wall(c) and not grid.is_wall(c + Vector2i.LEFT) and not grid.is_wall(c + Vector2i.RIGHT))
	opens.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return a.distance_to(grid.core) < b.distance_to(grid.core))
	var open_cell: Vector2i = opens[0] if not opens.is_empty() else grid.core + Vector2i(-2, 0)
	for kind: String in ["warrior", "archer", "rogue", "guardian"]:
		lord.position = grid.cell_center(open_cell)
		lord.health = lord.max_health  # earlier volleys must not kill the target
		var sh := Hero.new()
		sh.setup(Units.HEROES[kind])
		sh.grid = grid
		sh.position = lord.position + Vector2(16, 0)
		game.add_child(sh)
		await process_frame
		sh.enemy_groups = [&"demon_lord"]  # ignore monsters left over from earlier checks
		if kind == "warrior":
			_check("enemy skill gauge starts empty", sh._skill_cd > float(Units.HEROES.warrior.skill_cd) - 0.2)  # a physics tick may already run
		sh._skill_cd = 0.0
		await physics_frame
		await physics_frame
		if sh._skill_cd > 1.0:
			fired.append(kind)
		sh.free()
		for n in game.get_children():
			if n is Telegraph or n.get_script() == load("res://src/scripts/magic_bolt.gd"):
				n.free()
	_check("enemy skills: charge / volley / blink / slam all fire (%s)" % ", ".join(fired), fired.size() == 4)
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
	var sk := Monster.new()
	sk.kind = "skeleton"
	sk.level = Units.MONSTER_MAX_LEVEL
	sk.refresh(Vector2.ONE)
	_check("evolved skeleton (lich) fires a 3-shot spread", int(sk.stats.get("spread", 1)) == 3)
	sk.free()
	game.tool = "upgrade"
	var g_before := game.gold
	_check("upgrade tool levels a monster for gold", game.try_build(scell) and sl.level == 3 and game.gold == g_before - Units.UPGRADE_COST * 2)
	_check("monster evolves at max level (%s)" % sl.stats.name, sl.is_evolved() and sl.stats.name == Units.EVOLUTIONS.slime.name)
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

	# Save / continue: the rebuilt run matches (map, walls, monsters, gold, wave).
	game.phase = Game.Phase.BUILD
	game.kills = 7
	RunSave.save(game)
	_check("save: file written", RunSave.exists())
	RunSave.load_next = true
	var g4: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(g4)
	await process_frame
	var same_walls := true
	for y in DungeonGrid.H:
		for x in DungeonGrid.W:
			if g4.grid.is_wall(Vector2i(x, y)) != grid.is_wall(Vector2i(x, y)):
				same_walls = false
	_check("save: map and built walls restored", same_walls)
	_check("save: gold / wave / monsters / traps restored",
		g4.wave == game.wave and g4.monster_cells().size() == game.monster_cells().size()
		and g4.grid.traps.size() == grid.traps.size() and g4.lord.weapon_id == lord.weapon_id and g4.kills == 7)
	g4.free()
	RunSave.clear()
	game.phase = Game.Phase.RAID

	# Story dialogue pauses the game until the last line is read.
	game._hud.show_dialog([["마왕", "a"], ["집사 임프", "b"]])
	var paused_in := paused
	game._hud._dialog._next()
	var still := paused and game._hud.dialog_open()
	game._hud._dialog._next()
	_check("story: dialogue pauses, advances, then resumes", paused_in and still and not paused and not game._hud.dialog_open())

	# Tutorial advances on game state (traps/monsters already placed) and remembers it's done.
	Settings.save_path = "user://settings_check.cfg"
	DirAccess.remove_absolute(Settings.save_path)
	Settings.load_and_apply()
	var tut := Tutorial.new()
	tut.setup(game)
	game.add_child(tut)
	for k in 4:
		await process_frame
	_check("tutorial: skips steps already done (step %d)" % tut.step(), tut.step() >= 1)
	tut.finish()
	Tutorial.enabled = true
	_check("tutorial: finishing saves it, never shown again", not Tutorial.pending())
	Tutorial.enabled = false
	DirAccess.remove_absolute(Settings.save_path)

	# Altar heals the lord standing next to it.
	lord.add_to_group("demon_lord")  # earlier fights may have killed it (out of the group while respawning)
	var acell := grid.cell_at(lord.global_position)
	grid.altars.append(acell)
	lord.health = lord.max_health - 2
	grid._altar_heal()
	_check("altar heals the lord nearby", lord.health == lord.max_health - 2 + int(Rooms.ALTAR.heal))
	grid.altars.erase(acell)

	# 007: core 0 ends the run
	game.damage_core(999)
	_check("007 core 0 ends run", game.phase == Game.Phase.OVER)

	# Meta upgrades: run end pays souls, buying saves, next run starts stronger.
	var m: Dictionary = Meta.load_data()
	_check("meta: run end saved souls", m.souls == Meta.souls_for(game.wave, false) and m.souls > 0)
	game.meta.souls = 100
	_check("meta tree: node is locked until its prerequisite has a level", not game.buy_meta("lord_atk"))
	_check("meta: buy start_gold", game.buy_meta("start_gold"))
	_check("meta: buy fire_start", game.buy_meta("fire_start"))
	_check("meta: fire_start maxes at 1", Meta.price(game.meta, "fire_start") == -1 and not game.buy_meta("fire_start"))
	_check("meta: purchase persisted", Meta.load_data().levels.start_gold == 1)
	var g3: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(g3)
	await process_frame
	_check("meta: next run gets bonus gold + fire trap",
		g3.gold == Units.START_GOLD + 30 and g3.unlocked_traps.has("fire"))
	g3.free()
	DirAccess.remove_absolute(Meta.save_path)

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
