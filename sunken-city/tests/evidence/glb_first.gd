extends "res://tests/evidence/dive.gd"
## Capture either rock at the same position, width and depth under game lighting.
func _ready() -> void:
	super()
	diver.pos = Vector2(40.5, 13.5) * Tuning.TILE
	var base := Vector3(46, -18.5, -1.5)
	var use_cutout := OS.get_environment("EVIDENCE_CUTOUT") == "1"
	if use_cutout:
		view._stage_cutout("rock_big", 9.0, base, Color.WHITE, false)
	else:
		var rock := view._stage_piece("rock_big", 9.0, base)
		assert(not rock is Sprite3D, "rock_big GLB failed to load")
	_msg_time = 0.0
	var caption := Label.new()
	caption.text = "기존 컷아웃 · 9타일" if use_cutout else "TRELLIS GLB · 9타일"
	caption.position = Vector2(12, 330)
	add_child(caption)
