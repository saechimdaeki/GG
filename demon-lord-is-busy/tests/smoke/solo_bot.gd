extends SceneTree
## Balance probe: the demon lord alone (no traps, no monsters) holding attack on the nearest
## hero, like a player holding the mouse. Prints core HP and lord deaths per wave. If one
## mouse button clears the run, the dungeon has no reason to exist.
##   WAVES=3 godot --headless --path . -s res://tests/smoke/solo_bot.gd --fixed-fps 60

var _deaths := 0


func _initialize() -> void:
	RunSave.save_path = "user://run_check.cfg"
	Story.enabled = false
	Tutorial.enabled = false
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	game.lord.died.connect(func() -> void: _deaths += 1)
	var waves := int(OS.get_environment("WAVES")) if OS.get_environment("WAVES") != "" else 3
	for w in waves:
		var core0 := game.core_hp
		var d0 := _deaths
		game.start_raid()
		var frames := 0
		while game.phase == Game.Phase.RAID and frames < 60 * 120:
			await physics_frame
			frames += 1
			_bot(game)
		print("wave %d: core %d -> %d, lord deaths %d, %.0fs, heroes left %d, kills %d, lord hp %d" % [w + 1, core0, game.core_hp,
			_deaths - d0, frames / 60.0, game.get_tree().get_nodes_in_group("heroes").size(), game.kills, game.lord.health])
		for h in game.get_tree().get_nodes_in_group("heroes"):
			var hh := h as Hero
			var c := game.grid.cell_at(hh.global_position)
			print("  stuck? %s at %s cell %s wall=%s target=%s path=%d lord=%s air=%s stun=%.1f" % [hh.stats.name, hh.global_position, c,
				game.grid.is_wall(c), hh._target, game.grid.find_path(hh.global_position, game.grid.core_position()).size(),
				game.lord.global_position, hh.airborne(), hh._stun])
		if game.phase == Game.Phase.OVER:
			print("RUN LOST at wave %d" % (w + 1))
			break
		# Skip the reward pick and go straight to the next build phase.
		if game.phase == Game.Phase.REWARD:
			game._on_reward_picked("gold")
		await process_frame
	quit()


func _bot(game: Game) -> void:
	var lord := game.lord
	if not lord.is_physics_processing():
		return
	var best: Node2D = null
	for h in game.get_tree().get_nodes_in_group("heroes"):
		if best == null or (h as Node2D).global_position.distance_to(lord.global_position) < best.global_position.distance_to(lord.global_position):
			best = h
	if best == null:
		return
	var to := best.global_position - lord.global_position
	if to.length() > 20.0:
		# Walk the grid path (a straight line gets stuck on walls).
		var path := game.grid.find_path(lord.global_position, best.global_position)
		var goal := best.global_position if path.size() < 2 else path[1]
		lord.velocity = (goal - lord.global_position).normalized() * lord.move_speed
		lord.move_and_slide()
	if lord._melee_cd <= 0.0:
		lord._swing(to.normalized())
