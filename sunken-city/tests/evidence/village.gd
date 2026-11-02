extends "res://src/game/game.gd"
## Evidence scene: zone already drained, all three buildings up, diver at the lodge
## with its resident menu open (EVIDENCE_NO_MENU=1 skips the menu, EVIDENCE_DEEP=1
## drops the diver into the crushing deep instead). Not part of the game build.


func _ready() -> void:
	super()
	diver.inventory = {"scrap": 30, "pipe": 3, "gear": 3}
	world.repair_pump(diver.inventory)
	world.step(Tuning.DRAIN_TIME + 0.1)
	var kinds := ["workshop", "lodge", "tower"]
	for i in 3:
		world.village.build(i, kinds[i], diver.inventory)
	diver.pos = world.village.plots[1]["pos"]
	if OS.get_environment("EVIDENCE_DEEP") == "1":
		diver.pos = Vector2(60.5, 52.5) * Tuning.TILE
	elif OS.get_environment("EVIDENCE_NO_MENU") != "1":
		_interact()
