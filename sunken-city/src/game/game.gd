extends Node2D
## M1 dive scene: draws the World with code art (until the Gemini sprites land),
## reads input, follows the diver with the camera and shows the HUD.

const COLORS := {
	"wall": Color("1d2b3a"), "wall_edge": Color("2f4558"),
	"cracked": Color("3a4a52"), "crack": Color("0e161d"),
	"rubble": Color("54504a"), "open": Color("0b1620"),
	"open_dry": Color("4a4436"), "wall_dry": Color("6b6152"), "moss": Color("6f9a4a"),
	"water": Color(0.1, 0.45, 0.55, 0.35), "surface": Color(0.55, 0.9, 1.0, 0.8),
	"scrap": Color("c9a24a"), "pipe": Color("b8743c"), "gear": Color("7f9a6a"),
	"plant": Color("5fd6c0"), "eel": Color("3f6e5a"), "lure": Color("ffe9a0"),
	"diver": Color("c9a24a"), "suit": Color("8a7a5c"), "glass": Color("9fe6ff"),
	"pump_broken": Color("6b4a32"), "pump_fixed": Color("d9b45a"),
}
const DEEP_TINT := Color(0.35, 0.45, 0.6)
const DRY_TINT := Color(1.0, 0.95, 0.85)
const LABELS := {"scrap": "황동 조각", "pipe": "파이프", "gear": "톱니"}

var world := World.new()
var diver := Diver.new(world.start)
var _cam := Camera2D.new()
var _light := PointLight2D.new()
var _tint := CanvasModulate.new()
var _oxygen_bar := ProgressBar.new()
var _inv_label := Label.new()
var _msg_label := Label.new()
var _msg_time := 0.0
var _time := 0.0
var _dry := 0.0  # 0 flooded .. 1 fully drained, warms the palette


func _ready() -> void:
	_bind_keys()
	_tint.color = DEEP_TINT
	add_child(_tint)
	_light.texture = _radial_texture()
	_light.texture_scale = 2.5
	_light.energy = 1.4
	add_child(_light)
	_cam.limit_right = world.width * Tuning.TILE
	_cam.limit_bottom = world.height * Tuning.TILE
	_cam.limit_left = 0
	_cam.limit_top = 0
	_cam.position_smoothing_enabled = true
	add_child(_cam)
	_build_hud()
	diver.fainted.connect(func(): _say("산소가 바닥났다… 출발점에서 깨어났다. 재료 절반을 잃었다."))
	diver.picked.connect(func(k): _say("%s 주웠다" % LABELS[k], 1.2))
	diver.hurt.connect(func(): _say("장어에게 물렸다! 산소 -%d" % int(Tuning.EEL_BITE), 1.2))
	_say("방향키/WASD 이동 · Space 점프 · J 부수기 · E 펌프 수리\n펌프를 고칠 재료: 황동 조각 3 · 파이프 1 · 톱니 1", 6.0)


func _physics_process(dt: float) -> void:
	_time += dt
	var move := Input.get_vector("left", "right", "up", "down")
	diver.step(world, move, Input.is_action_just_pressed("jump"), Input.is_action_pressed("tool"), dt)
	if Input.is_action_just_pressed("interact") and diver.near_pump(world):
		_try_repair()
	world.step(dt)
	_dry = inverse_lerp(Zone1.SURFACE_ROW * Tuning.TILE, world.drained_y, world.surface_y)
	_tint.color = DEEP_TINT.lerp(DRY_TINT, _dry)
	_cam.position = diver.pos
	_light.position = diver.pos
	_msg_time -= dt
	_msg_label.visible = _msg_time > 0
	_update_hud()
	queue_redraw()


func _try_repair() -> void:
	if world.pump_fixed:
		return
	var need := World.missing(diver.inventory)
	if world.repair_pump(diver.inventory):
		_say("펌프가 돌아간다! 물이 빠진다…", 4.0)
		await get_tree().create_timer(Tuning.DRAIN_TIME).timeout
		_say("구역이 말랐다! 이 땅에 마을을 지을 수 있다. (다음: M2 마을)", 8.0)
	else:
		var parts := []
		for k in need:
			parts.append("%s %d" % [LABELS[k], need[k]])
		_say("재료가 모자란다: " + ", ".join(parts), 2.5)


func _say(text: String, seconds := 3.0) -> void:
	_msg_label.text = text
	_msg_time = seconds


# --- drawing -------------------------------------------------------------

func _draw() -> void:
	var t := Tuning.TILE
	draw_rect(Rect2(0, 0, world.width * t, world.height * t), COLORS["open"].lerp(COLORS["open_dry"], _dry))
	for y in world.height:
		for x in world.width:
			var r := Rect2(x * t, y * t, t, t)
			match world.cell(Vector2i(x, y)):
				World.Cell.WALL:
					draw_rect(r, COLORS["wall"].lerp(COLORS["wall_dry"], _dry))
					if not world.is_solid(Vector2i(x, y - 1)):
						draw_rect(Rect2(r.position, Vector2(t, 3)), COLORS["wall_edge"].lerp(COLORS["moss"], _dry))
				World.Cell.CRACKED:
					draw_rect(r, COLORS["cracked"])
					var hurt: int = world.damage.get(Vector2i(x, y), 0)
					draw_line(r.position + Vector2(3, 2), r.position + Vector2(9, 9), COLORS["crack"], 1.0)
					draw_line(r.position + Vector2(9, 9), r.position + Vector2(6, 14), COLORS["crack"], 1.0)
					if hurt > 0:
						draw_line(r.position + Vector2(13, 3), r.position + Vector2(8, 10), COLORS["crack"], 2.0)
				World.Cell.RUBBLE:
					draw_circle(r.get_center() + Vector2(-3, 3), 5, COLORS["rubble"])
					draw_circle(r.get_center() + Vector2(4, 4), 4, COLORS["rubble"].darkened(0.2))
					draw_circle(r.get_center() + Vector2(0, -2), 4, COLORS["rubble"].lightened(0.1))
	for a in world.air_plants:
		draw_line(a + Vector2(0, 8), a + Vector2(0, -2), COLORS["plant"].darkened(0.4), 2.0)
		for i in 3:
			var bob := sin(_time * 2.0 + i) * 1.5
			draw_circle(a + Vector2(-4 + i * 4, -3 + bob), 2.5, COLORS["plant"])
	_draw_pump()
	for p in world.pickups:
		_draw_pickup(p["pos"], p["kind"])
	for e in world.eels:
		if e.alive:
			_draw_eel(e)
	_draw_diver()
	# water on top of everything below the surface
	var sy := world.surface_y
	draw_rect(Rect2(0, sy, world.width * t, world.height * t - sy), COLORS["water"])
	var pts := PackedVector2Array()
	for i in range(0, world.width * t + 1, 4):
		pts.append(Vector2(i, sy + sin(i * 0.08 + _time * 3.0)))
	draw_polyline(pts, COLORS["surface"], 1.0)


func _draw_pump() -> void:
	var c := world.pump
	var col: Color = COLORS["pump_fixed"] if world.pump_fixed else COLORS["pump_broken"]
	draw_rect(Rect2(c + Vector2(-10, -6), Vector2(20, 14)), col)
	draw_rect(Rect2(c + Vector2(10, -2), Vector2(10, 4)), col.darkened(0.3))
	var spin := _time * 4.0 if world.pump_fixed else 0.6
	draw_arc(c + Vector2(-2, -10), 6, spin, spin + TAU * 0.8, 10, col.lightened(0.2), 2.0)
	if world.draining():
		for i in 4:
			draw_circle(c + Vector2(22 + fmod(_time * 30.0 + i * 6, 20), 0), 2, COLORS["surface"])


func _draw_pickup(p: Vector2, kind: String) -> void:
	var bob := Vector2(0, sin(_time * 3.0 + p.x) * 1.5)
	match kind:
		"scrap": draw_colored_polygon(PackedVector2Array([p + bob + Vector2(-4, 2), p + bob + Vector2(0, -4), p + bob + Vector2(4, 1), p + bob + Vector2(1, 4)]), COLORS["scrap"])
		"pipe": draw_line(p + bob + Vector2(-5, 2), p + bob + Vector2(4, -3), COLORS["pipe"], 3.0)
		"gear":
			draw_circle(p + bob, 4.5, COLORS["gear"])
			draw_circle(p + bob, 1.5, COLORS["open"])


func _draw_eel(e: Eel) -> void:
	var pts := PackedVector2Array()
	for i in 6:
		var x := -e.dir * (i * 4 - 10)
		pts.append(e.pos + Vector2(x, sin(_time * 6.0 + i) * 2.0))
	draw_polyline(pts, COLORS["eel"], 4.0)
	var head := e.pos + Vector2(e.dir * 10, 0)
	draw_line(head, head + Vector2(e.dir * 4, -6), COLORS["eel"], 1.0)
	draw_circle(head + Vector2(e.dir * 4, -7), 2.0, COLORS["lure"])


func _draw_diver() -> void:
	if diver.invulnerable() and int(_time * 12) % 2 == 0:
		return
	var p := diver.pos
	var f := float(diver.facing.x if diver.facing.x != 0 else 1)
	draw_rect(Rect2(p + Vector2(-5, -1), Vector2(10, 8)), COLORS["suit"])        # body
	draw_rect(Rect2(p + Vector2(-f * 7 - 2, -1), Vector2(4, 6)), COLORS["suit"].darkened(0.3))  # tank
	draw_circle(p + Vector2(0, -3), 5, COLORS["diver"])                          # helmet
	draw_circle(p + Vector2(f * 1.5, -3), 2.5, COLORS["glass"])
	draw_circle(p + Vector2(0, -8.5), 1.2, COLORS["lure"])                      # lamp
	if world.in_water(p) and int(_time * 2) % 2 == 0:
		draw_circle(p + Vector2(f * 3, -12 - fmod(_time * 10, 6)), 1.2, COLORS["surface"])


# --- setup ---------------------------------------------------------------

func _build_hud() -> void:
	var hud := CanvasLayer.new()
	add_child(hud)
	var box := VBoxContainer.new()
	box.position = Vector2(8, 6)
	hud.add_child(box)
	var o2 := Label.new()
	o2.text = "산소"
	box.add_child(o2)
	_oxygen_bar.max_value = Tuning.OXYGEN_MAX
	_oxygen_bar.custom_minimum_size = Vector2(120, 8)
	_oxygen_bar.show_percentage = false
	box.add_child(_oxygen_bar)
	box.add_child(_inv_label)
	_msg_label.position = Vector2(8, 300)
	_msg_label.size = Vector2(624, 52)
	_msg_label.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	_msg_label.add_theme_color_override("font_outline_color", Color.BLACK)
	_msg_label.add_theme_constant_override("outline_size", 4)
	hud.add_child(_msg_label)


func _update_hud() -> void:
	_oxygen_bar.value = diver.oxygen
	var parts := []
	for k in World.MATERIALS:
		parts.append("%s %d/%d" % [LABELS[k], diver.inventory[k], Tuning.PUMP_COST[k]])
	_inv_label.text = "  ".join(parts)
	if diver.near_pump(world) and not world.pump_fixed and _msg_time <= 0:
		_say("E: 펌프 수리", 0.2)


func _radial_texture() -> GradientTexture2D:
	var g := Gradient.new()
	g.set_color(0, Color.WHITE)
	g.set_color(1, Color(1, 1, 1, 0))
	var tex := GradientTexture2D.new()
	tex.gradient = g
	tex.fill = GradientTexture2D.FILL_RADIAL
	tex.fill_from = Vector2(0.5, 0.5)
	tex.fill_to = Vector2(1.0, 0.5)
	tex.width = 128
	tex.height = 128
	return tex


## ponytail: actions registered in code instead of project.godot's verbose InputEvent blobs.
func _bind_keys() -> void:
	var keys := {
		"left": [KEY_LEFT, KEY_A], "right": [KEY_RIGHT, KEY_D],
		"up": [KEY_UP, KEY_W], "down": [KEY_DOWN, KEY_S],
		"jump": [KEY_SPACE], "tool": [KEY_J, KEY_X], "interact": [KEY_E],
	}
	for action in keys:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		for k in keys[action]:
			var ev := InputEventKey.new()
			ev.physical_keycode = k
			InputMap.action_add_event(action, ev)
