extends SceneTree
## Headless self-check for stories 001-007. Run:
##   godot --headless --path . -s res://tests/smoke/story_001_check.gd
## Prints PASS/FAIL per check and exits 1 on any failure.

var _fails := 0


func _initialize() -> void:
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	var grid := game.grid
	var lord := game.lord

	# 002: build phase, dig / wall / trap rules
	_check("002 starts in build phase", game.phase == Game.Phase.BUILD)
	_check("002 entrance reaches core", not grid.find_path(grid.entrance_position(), grid.core_position()).is_empty())
	game.tool = "wall"
	_check("002 wall on one lane of a 2-wide corridor is allowed", game.try_build(Vector2i(11, 7)))
	_check("002 wall that cuts the last lane is refused", not game.try_build(Vector2i(12, 7)))
	game.tool = "dig"
	var gold_before := game.gold
	_check("002 dig wall costs gold", game.try_build(Vector2i(6, 9)) and game.gold == gold_before - Units.DIG_COST)
	game.tool = "spike"
	_check("002 spike trap placed on floor", game.try_build(Vector2i(8, 10)) and grid.traps.has(Vector2i(8, 10)))
	game.tool = "fire"
	_check("002 fire trap locked at start", not game.try_build(Vector2i(8, 11)))

	# 003: monsters
	game.tool = "goblin"
	_check("003 goblin placed", game.try_build(Vector2i(30, 16)))
	_check("003 one monster per cell", not game.try_build(Vector2i(30, 16)))
	var goblin := get_first_node_in_group("monsters") as Monster
	goblin.take_damage(999, Vector2.ZERO)
	await process_frame
	_check("003 dead monster is gone for the run", get_nodes_in_group("monsters").is_empty())

	# 001: lord combat (needs raid phase + a hero)
	game.start_raid()
	_check("004 raid starts", game.phase == Game.Phase.RAID)
	for i in 3:
		await physics_frame
	var hero := get_first_node_in_group("heroes") as Hero
	_check("005 wave 1 spawns a hero at the entrance", hero != null)
	hero.set_physics_process(false)
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

	# 004: path from entrance to core is walkable
	var path := grid.find_path(grid.entrance_position(), grid.core_position())
	_check("004 hero path ends at core", path.size() > 0 and path[path.size() - 1].distance_to(grid.core_position()) < 1.0)

	# 007: core damage
	var core_before := game.core_hp
	game.damage_core(3)
	_check("007 core takes damage", game.core_hp == core_before - 3)

	# 005/006: clearing the wave opens the reward screen
	for node in get_nodes_in_group("heroes"):
		(node as Hero).take_damage(999, Vector2.ZERO)
	game._spawn_queue.clear()
	for i in 3:
		await physics_frame
	_check("005 wave cleared advances wave", game.wave == 1)
	_check("006 reward phase after clear", game.phase == Game.Phase.REWARD)
	var choices := Rewards.roll(3, game)
	var ids := choices.map(func(r: Dictionary) -> String: return r.id)
	_check("006 three distinct rewards", ids.size() == 3 and ids[0] != ids[1] and ids[1] != ids[2] and ids[0] != ids[2])
	Rewards.apply("fire_trap", game)
	_check("006 fire trap reward unlocks fire", game.unlocked_traps.has("fire"))
	game._on_reward_picked("gold")
	_check("006 picking returns to build", game.phase == Game.Phase.BUILD)
	_check("005 ten waves, last has boss", Waves.WAVES.size() == 10 and Waves.WAVES[9].heroes[0][0] == "boss")

	# 007: core 0 ends the run
	game.damage_core(999)
	_check("007 core 0 ends run", game.phase == Game.Phase.OVER)

	quit(1 if _fails > 0 else 0)


func _press(action: String) -> void:
	Input.action_press(action)
	await physics_frame
	await physics_frame
	Input.action_release(action)


func _check(name: String, ok: bool) -> void:
	print(("PASS " if ok else "FAIL ") + name)
	if not ok:
		_fails += 1
