extends SceneTree
## Screenshot helper for story 006: opens the reward screen right away.
##   godot --path . -s res://tests/smoke/reward_demo.gd --write-movie out.png --fixed-fps 60 --quit-after 30


func _initialize() -> void:
	RunSave.save_path = "user://run_check.cfg"  # never touch the player's save
	Story.enabled = false  # dialogue would pause the game waiting for a click
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	game._wave_cleared()
