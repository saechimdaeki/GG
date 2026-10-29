extends SceneTree
## Close-up of lava (first second) and water (second second) for screenshots:
##   godot --path . -s res://tests/smoke/terrain_demo.gd --write-movie out.png --fixed-fps 60 --quit-after 120


func _initialize() -> void:
	var grid: DungeonGrid
	for s in range(1, 60):
		grid = DungeonGrid.new()
		grid.seed_value = s
		root.add_child(grid)
		await process_frame
		if grid.terrain.values().has("lava") and grid.terrain.values().has("water"):
			break
		grid.free()
	var cam := Camera2D.new()
	cam.zoom = Vector2.ONE * 2.0
	root.add_child(cam)
	for kind: String in ["lava", "water"]:
		for c: Vector2i in grid.terrain:
			if grid.terrain[c] == kind:
				cam.position = grid.cell_center(c)
				break
		await create_timer(1.0).timeout
