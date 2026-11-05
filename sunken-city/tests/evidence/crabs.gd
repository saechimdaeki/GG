extends "res://tests/evidence/dive.gd"
## Evidence scene: zone drained, diver standing on a tower floor next to a crab. Not part of the game build.


func _ready() -> void:
	super()
	world.repair_pump({"scrap": 3, "pipe": 1, "gear": 1})
	world.step(Tuning.DRAIN_TIME + 0.1)
	var c: Crab = world.crabs[0]
	diver.pos = c.pos + Vector2(-40, -4)
