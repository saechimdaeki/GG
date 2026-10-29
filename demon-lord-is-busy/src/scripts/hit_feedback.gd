class_name HitFeedback
## Hit-stop helper: freezes the game for a few frames on a solid hit.

## Slows global time briefly. Overlapping calls just extend the freeze.
static func hitstop(tree: SceneTree, duration: float = 0.05) -> void:
	Engine.time_scale = 0.05
	await tree.create_timer(duration, true, false, true).timeout
	Engine.time_scale = 1.0
