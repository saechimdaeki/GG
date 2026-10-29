class_name WeaponPickup
extends Node2D
## Weapon dropped by a fallen hero. The demon lord equips it by walking over it.

signal picked(id: String)

var weapon_id: String

var _t: float = 0.0


func _ready() -> void:
	add_to_group("pickups")
	add_child(Fx.light(Arsenal.WEAPONS[weapon_id].color, 1.0, 0.6))


func _physics_process(delta: float) -> void:
	_t += delta
	queue_redraw()
	var lord := get_tree().get_first_node_in_group("demon_lord") as DemonLord
	if lord != null and lord.global_position.distance_to(global_position) < 12.0:
		lord.equip(weapon_id)
		Fx.burst(get_parent(), global_position, Arsenal.WEAPONS[weapon_id].color, 16, 90.0)
		picked.emit(weapon_id)
		queue_free()


func _draw() -> void:
	var w: Dictionary = Arsenal.WEAPONS[weapon_id]
	var bob := sin(_t * 3.0) * 2.0
	var col: Color = w.color
	draw_circle(Vector2(0, 5), 5.0, Color(0, 0, 0, 0.35))
	draw_circle(Vector2(0, bob), 7.0 + sin(_t * 5.0), Color(col, 0.18))
	var a := -PI / 4.0
	var dir := Vector2.from_angle(a)
	var base := Vector2(0, bob) - dir * float(w.len) / 2.0
	var tip := base + dir * float(w.len)
	draw_line(base, tip, Color(0.1, 0.08, 0.12), 4.0)
	draw_line(base, tip, col, 2.0)
	if w.shape == "hammer":
		draw_rect(Rect2(tip - Vector2(4, 4), Vector2(8, 8)), col)
	draw_string(ThemeDB.fallback_font, Vector2(-20, bob - 12), String(w.name), HORIZONTAL_ALIGNMENT_CENTER, 40, 7, Color(1, 1, 1, 0.8))
