extends SceneTree
## Screenshot of the run-end screen with permanent upgrades (uses a throwaway save):
##   godot --path . -s res://tests/smoke/meta_demo.gd --write-movie out.png --fixed-fps 60 --quit-after 30


func _initialize() -> void:
	Meta.save_path = "user://meta_demo.cfg"
	Meta.save_data({"souls": 40, "levels": {"start_gold": 2, "core_hp": 1}})
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	game.wave = 5
	game.damage_core(999)
