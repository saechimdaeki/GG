extends SceneTree
## Unattended demo for screenshots: places a few monsters/traps, then starts wave 1.
##   godot --path . -s res://tests/smoke/raid_demo.gd --write-movie out.png --fixed-fps 60 --quit-after 900


func _initialize() -> void:
	RunSave.save_path = "user://run_check.cfg"  # never touch the player's save
	Story.enabled = false  # dialogue would pause the game waiting for a click
	Tutorial.enabled = false
	if OS.get_environment("BIOME") != "":
		Game.next_biome = OS.get_environment("BIOME")
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	var spots: Array[Vector2i] = []
	for y in DungeonGrid.H:
		for x in DungeonGrid.W:
			if game.grid.is_buildable_floor(Vector2i(x, y)):
				spots.append(Vector2i(x, y))
	game.gold = 400
	var path := game.grid.find_path(game.grid.entrance_position(game.grid.active_entrances[0]), game.grid.core_position())
	for k in [["spike", 4], ["spike", 5], ["slime", 8], ["goblin", 12], ["lure", 16], ["skeleton", 20], ["nest", 24], ["mine", 28]]:
		game.tool = k[0]
		game.try_build(game.grid.cell_at(path[mini(k[1], path.size() - 2)]))
	if OS.get_environment("GATES") != "":
		for i in game.grid.gates.size():
			game.toggle_gate(i)
	if OS.get_environment("WAVE") != "":
		game.wave = int(OS.get_environment("WAVE")) - 1
		game._enter_build()
	await create_timer(1.0).timeout
	game.start_raid()
