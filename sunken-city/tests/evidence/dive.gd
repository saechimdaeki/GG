extends "res://src/game/game.gd"
## Evidence scene: the diver placed at EVIDENCE_AT="x,y" (tile coords) in the flooded
## zone, to capture any spot of the sea. Not part of the game build.


func _ready() -> void:
	super()
	var at := OS.get_environment("EVIDENCE_AT").split(",")
	if at.size() == 2:
		diver.pos = Vector2(float(at[0]) + 0.5, float(at[1]) + 0.5) * Tuning.TILE
	_msg_time = 0.0
