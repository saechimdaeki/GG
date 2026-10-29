extends SceneTree
## Screenshot of the intro dialogue on a new run (throwaway save):
##   godot --path . -s res://tests/smoke/story_demo.gd --write-movie out.png --fixed-fps 60 --quit-after 40


func _initialize() -> void:
	RunSave.save_path = "user://run_demo.cfg"
	Meta.save_path = "user://meta_demo.cfg"
	Settings.save_path = "user://settings_demo.cfg"
	DirAccess.remove_absolute(Settings.save_path)
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
