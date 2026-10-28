extends Node2D
## Projectile used by the demon lord and ranged units: flies straight, stops at walls,
## hits the first node in target_groups whose take_damage accepts it (a dashing lord does not).

var speed: float = 320.0
var lifetime: float = 1.6
var radius: float = 3.0
var direction: Vector2 = Vector2.RIGHT
var damage: int = 2
var target_groups: Array[StringName] = [&"heroes"]
var color: Color = Color(0.85, 0.6, 1.0)
## Player bolts trigger hit-stop on impact.
var from_player: bool = false

var _grid: DungeonGrid


func _ready() -> void:
	_grid = get_tree().get_first_node_in_group("dungeon") as DungeonGrid


func _physics_process(delta: float) -> void:
	global_position += direction * speed * delta
	lifetime -= delta
	if lifetime <= 0.0 or (_grid != null and _grid.is_wall_at(global_position)):
		queue_free()
		return
	for g in target_groups:
		for node in get_tree().get_nodes_in_group(g):
			var n := node as Node2D
			if global_position.distance_to(n.global_position) > radius + float(n.get("radius")):
				continue
			if n.call("take_damage", damage, direction):
				if from_player:
					HitFeedback.hitstop(get_tree(), 0.03)
				queue_free()
				return


func _draw() -> void:
	draw_circle(Vector2.ZERO, radius * 2.2, Color(color, 0.25))
	draw_circle(Vector2.ZERO, radius, color.lightened(0.3))
