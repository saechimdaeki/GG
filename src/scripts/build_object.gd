class_name BuildObject
extends Node2D
## Placeable dungeon object (Units.OBJECTS):
## - lure: greedy heroes within `reach` tiles walk over and loot it (loot_time s, once per hero).
## - nest: during raids spawns a weak slime every spawn_every s, up to max_alive at once.

var kind: String
var data: Dictionary
## Nest only: raids tick only while true.
var active: bool = false
## Mine only: a hero looted it this raid, so it pays nothing at wave clear.
var robbed: bool = false

var _clock: float = 0.0
var _spawned: Array[Monster] = []
var _t: float = 0.0


## kind is a key of Units.OBJECTS.
func setup(k: String) -> void:
	kind = k
	data = Units.OBJECTS[k]


func _ready() -> void:
	add_to_group("nests" if kind == "nest" else "lures")  # heroes loot lures and mines
	if kind == "mine":
		add_to_group("mines")
	add_child(Fx.light(data.color, 0.9, 0.6))


## Nest: drops summoned slimes from the last raid.
func end_raid() -> void:
	active = false
	_clock = 0.0
	for m in _spawned:
		if is_instance_valid(m):
			m.queue_free()
	_spawned.clear()


func _physics_process(delta: float) -> void:
	_t += delta
	queue_redraw()
	if kind != "nest" or not active:
		return
	# Untyped param: a freed summon cannot be cast to Monster. assign() keeps the typed array.
	_spawned.assign(_spawned.filter(func(m: Variant) -> bool: return is_instance_valid(m)))
	_clock += delta
	if _clock < float(data.spawn_every) or _spawned.size() >= int(data.max_alive):
		return
	_clock = 0.0
	var m := Monster.new()
	m.setup(Units.MONSTERS.slime, 0.6, 1.0)
	m.kind = "slime"
	m.home = global_position + Vector2(randf_range(-8, 8), randf_range(-8, 8))
	m.position = global_position
	get_parent().add_child(m)
	_spawned.append(m)
	Fx.burst(get_parent(), global_position, data.color, 10, 60.0)


func _draw() -> void:
	var col: Color = data.color
	var tex := Sprites.tex({"lure": "chest", "nest": "nest", "mine": "mine"}[kind])
	if tex != null:
		var bob := sin(_t * 3.0) * 1.0 if kind == "lure" else 0.0
		draw_texture(tex, Vector2(-8, -12 + bob), Color(0.6, 0.6, 0.6) if robbed else Color.WHITE)
		if kind == "mine":
			var imp := Sprites.tex("imp")
			if imp != null:
				# The mining imp hops as it swings.
				var hop := absf(sin(_t * 8.0)) * 2.0
				draw_set_transform(Vector2(9, -4 - hop), 0.0, Vector2(0.6, 0.6))
				draw_texture(imp, Vector2(-8, -8))
				draw_set_transform(Vector2.ZERO)
			if robbed:
				draw_string(ThemeDB.fallback_font, Vector2(-10, -14), "약탈됨", HORIZONTAL_ALIGNMENT_LEFT, -1, 7, Color(1, 0.4, 0.4))
		return
	if kind == "mine":
		# Ore heap + an imp swinging a pick (darker when robbed).
		var ore := col.darkened(0.5) if robbed else col
		draw_colored_polygon(PackedVector2Array([Vector2(-7, 5), Vector2(-4, -3), Vector2(0, -6), Vector2(5, -2), Vector2(7, 5)]), Color(0.3, 0.25, 0.32))
		for p: Vector2 in [Vector2(-3, 0), Vector2(2, -3), Vector2(3, 2)]:
			draw_rect(Rect2(p - Vector2(1.5, 1.5), Vector2(3, 3)), ore * (1.0 if robbed else 1.8))
		var swing := sin(_t * 8.0)
		draw_circle(Vector2(9, 1), 3.0, Color(0.9, 0.35, 0.25))
		draw_line(Vector2(8, 0), Vector2(8, 0) + Vector2.from_angle(-2.2 + swing * 0.6) * 7.0, Color(0.7, 0.7, 0.75), 1.0)
		if robbed:
			draw_string(ThemeDB.fallback_font, Vector2(-8, -9), "약탈됨", HORIZONTAL_ALIGNMENT_LEFT, -1, 7, Color(1, 0.4, 0.4))
		return
	if kind == "lure":
		var bob := sin(_t * 3.0) * 1.5
		draw_rect(Rect2(-6, -4 + bob, 12, 8), Color(0.5, 0.3, 0.1))
		draw_rect(Rect2(-6, -6 + bob, 12, 3), Color(0.65, 0.4, 0.15))
		draw_rect(Rect2(-6, -4 + bob, 12, 8), Color(0.2, 0.1, 0.05), false, 1.0)
		draw_rect(Rect2(-1, -3 + bob, 2, 3), col)
		for i in 3:
			var a := _t * 2.0 + i * TAU / 3.0
			draw_circle(Vector2(cos(a) * 7.0, -8.0 + sin(a) * 2.0 + bob), 1.0, col)
	else:
		draw_circle(Vector2.ZERO, 7.0, Color(0.15, 0.3, 0.15))
		draw_circle(Vector2.ZERO, 5.0 + sin(_t * 2.5), col.darkened(0.3))
		draw_circle(Vector2(-2, -2), 1.5, col.lightened(0.4))
