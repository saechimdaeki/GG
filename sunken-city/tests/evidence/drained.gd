extends "res://src/game/game.gd"
## Evidence scene: starts at the pump with full materials and repairs it, so a
## --write-movie capture shows the drain. Not part of the game build.


func _ready() -> void:
	super()
	diver.inventory = Tuning.PUMP_COST.duplicate()
	diver.pos = world.pump + Vector2(-20, 0)
	_try_repair()
