extends SceneTree
## Drives the build phase through the real input pipeline (key + mouse events), like a player.
##   godot --headless --path . -s res://tests/smoke/input_check.gd

var _fails := 0


func _initialize() -> void:
	RunSave.save_path = "user://run_check.cfg"  # never touch the player's save
	Story.enabled = false  # dialogue would pause the game waiting for a click
	Tutorial.enabled = false
	var game: Game = load("res://src/scenes/main.tscn").instantiate()
	root.add_child(game)
	await process_frame

	await create_timer(0.6).timeout  # build-phase camera zoom settles
	_key(KEY_5)
	await process_frame
	_check("key 5 selects goblin", game.tool == "goblin")

	var cell := _floor_cell(game, Vector2i(-1, -1))
	var screen := (game.get_canvas_transform() * game.grid.cell_center(cell)) * 2.0
	var gold := game.gold
	_click(screen)
	await process_frame
	await process_frame
	print("tool=%s gold %d -> %d, mouse world=%s target=%s" % [game.tool, gold, game.gold, game.get_global_mouse_position(), game.grid.cell_center(cell)])
	_check("left click builds goblin", game.gold == gold - 35)

	_key(KEY_1)
	await process_frame
	var wall_cell := _floor_cell(game, cell)
	_click((game.get_canvas_transform() * game.grid.cell_center(wall_cell)) * 2.0)
	await process_frame
	_check("key 1 + click builds wall", game.grid.is_wall(wall_cell))
	quit(1 if _fails > 0 else 0)


func _floor_cell(game: Game, skip: Vector2i) -> Vector2i:
	for y in DungeonGrid.H:
		for x in DungeonGrid.W:
			var c := Vector2i(x, y)
			if c != skip and game.grid.is_buildable_floor(c) and game.grid.can_fill(c):
				return c
	return Vector2i(-1, -1)


func _key(code: Key) -> void:
	for pressed in [true, false]:
		var ev := InputEventKey.new()
		ev.physical_keycode = code
		ev.keycode = code
		ev.pressed = pressed
		Input.parse_input_event(ev)


func _click(pos: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = pos
	Input.parse_input_event(motion)
	for pressed in [true, false]:
		var ev := InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_LEFT
		ev.position = pos
		ev.pressed = pressed
		Input.parse_input_event(ev)


func _check(name: String, ok: bool) -> void:
	print(("PASS " if ok else "FAIL ") + name)
	if not ok:
		_fails += 1
