class_name Telegraph
extends Node2D
## Ground warning circle that fills up over `delay` seconds, then explodes: every node in
## `target_groups` inside `radius` takes `damage` (a dashing demon lord dodges it).

var radius: float = 24.0
var delay: float = 0.65
var damage: int = 3
var color: Color = Color(1.0, 0.35, 0.15)
var target_groups: Array[StringName] = [&"demon_lord", &"monsters"]

var _t: float = 0.0


func _physics_process(delta: float) -> void:
	_t += delta
	queue_redraw()
	if _t < delay:
		return
	for g in target_groups:
		for node in get_tree().get_nodes_in_group(g):
			var n := node as Node2D
			if n.global_position.distance_to(global_position) <= radius + float(n.get("radius")):
				n.call("take_damage", damage, (n.global_position - global_position).normalized())
	Fx.burst(get_parent(), global_position, color, 30, 160.0)
	Fx.ring(get_parent(), global_position, radius, color)
	var lord := get_tree().get_first_node_in_group("demon_lord") as DemonLord
	if lord != null:
		lord.shake(4.0)
	queue_free()


func _draw() -> void:
	var k := clampf(_t / delay, 0.0, 1.0)
	draw_circle(Vector2.ZERO, radius, Color(color, 0.12))
	draw_circle(Vector2.ZERO, radius * k, Color(color, 0.3))
	draw_arc(Vector2.ZERO, radius, 0.0, TAU, 32, Color(color, 0.8), 1.0)
