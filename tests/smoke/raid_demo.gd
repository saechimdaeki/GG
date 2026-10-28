extends SceneTree
## Unattended demo for screenshots: places a few monsters/traps, then starts wave 1.
##   godot --path . -s res://tests/smoke/raid_demo.gd --write-movie out.png --fixed-fps 60 --quit-after 900


func _initialize() -> void:
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame
	for build: Array in [["spike", Vector2i(8, 10)], ["spike", Vector2i(9, 11)], ["slime", Vector2i(12, 8)],
			["goblin", Vector2i(18, 5)], ["skeleton", Vector2i(24, 10)]]:
		game.tool = build[0]
		game.try_build(build[1])
	await create_timer(1.0).timeout
	game.start_raid()
