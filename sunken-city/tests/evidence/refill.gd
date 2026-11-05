extends "res://tests/evidence/dive.gd"
## Evidence scene: zone drained, village built, then the valve refills it; the diver
## floats by the sea pillar. Not part of the game build.


func _ready() -> void:
	super()
	diver.inventory = {"scrap": 30, "pipe": 3, "gear": 3}
	world.repair_pump(diver.inventory)
	world.step(Tuning.DRAIN_TIME + 0.1)
	var kinds := ["workshop", "lodge", "tower"]
	for i in 3:
		world.village.build(i, kinds[i], diver.inventory)
	world.toggle_valve()
	world.step(Tuning.DRAIN_TIME + 0.1)
	diver.pos = Vector2(46.5, 28.5) * Tuning.TILE
