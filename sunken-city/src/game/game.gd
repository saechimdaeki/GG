extends Node2D
## Dive scene: game rules (World, Diver, Village) shown through the 2.5D SeaView — 3D
## sea, pixel-sprite characters. Reads input, runs the village menus and the HUD.

const LABELS := {"scrap": "황동 조각", "pipe": "파이프", "gear": "톱니"}
const NEAR := 28.0
const MENU_KEYS := [KEY_1, KEY_2, KEY_3]
const RESIDENT_ART := ["moru", "mulbangul"]
const MENU_COLOR := Color("ffd27a")
## Every image in assets/ the game uses (made by tools/art_build.py), keyed "folder/name".
const ART := [
	"sprites/diver", "sprites/eel_lantern", "sprites/moru", "sprites/mulbangul",
	"props/air_bubble_plant", "props/pump_broken", "props/pump_fixed", "props/floodgate",
	"props/scrap_brass", "props/pipe_part", "props/gear",
	"props/building_workshop", "props/building_lodge", "props/building_tower",
	"portraits/moru", "portraits/mulbangul",
]
const OPTIONAL_ART := ["sprites/diver_stand"]  # used when present, else the swim pose
const PICKUP_ART := {"scrap": "props/scrap_brass", "pipe": "props/pipe_part", "gear": "props/gear"}

var world := World.new()
var diver := Diver.new(world.start)
var view := SeaView.new()
var _oxygen_bar := ProgressBar.new()
var _inv_label := Label.new()
var _msg_label := Label.new()
var _menu_label := Label.new()
var _menu: Array = []          # {text: String, run: Callable}
var _menu_at := Vector2.ZERO
var _msg_time := 0.0
var _time := 0.0
var _tex := {}
var _portraits := HBoxContainer.new()
var _menu_lodge := false  # the open menu is the lodge: show the residents' faces
var _dry := 0.0  # 0 flooded .. 1 fully drained


func _ready() -> void:
	_bind_keys()
	for name in ART + OPTIONAL_ART:
		if ResourceLoader.exists("res://assets/%s.png" % name):
			_tex[name] = load("res://assets/%s.png" % name)
	add_child(view)
	view.setup(world, _tex)
	_build_hud()
	diver.fainted.connect(func(): _say("산소가 바닥났다… 정신을 차려보니 %s. 황동 조각 절반을 잃었다." % ("마을이다" if world.pump_fixed else "출발점이다")))
	diver.picked.connect(func(k): _say("%s 주웠다" % LABELS[k], 1.2))
	diver.hurt.connect(func(): _say("장어에게 물렸다! 산소 -%d" % int(Tuning.EEL_BITE), 1.2))
	_say("방향키/WASD 이동 · Space 점프 · J 부수기 · E 상호작용 · R 귀환\n펌프를 고칠 재료: 황동 조각 3 · 파이프 1 · 톱니 1", 6.0)


func _physics_process(dt: float) -> void:
	_time += dt
	var move := Input.get_vector("left", "right", "up", "down")
	diver.step(world, move, Input.is_action_just_pressed("jump"), Input.is_action_pressed("tool"), dt)
	if Input.is_action_just_pressed("interact"):
		_interact()
	if Input.is_action_just_pressed("return"):
		diver.pos = world.respawn()
		diver.vel = Vector2.ZERO
		_say("밧줄을 당겨 %s(으)로 돌아왔다" % ("마을" if world.pump_fixed else "출발점"), 1.5)
	if not _menu.is_empty() and diver.pos.distance_to(_menu_at) > NEAR * 1.5:
		_menu = []
	var was_found := world.gate_found
	world.step(dt, diver.pos)
	if world.gate_found and not was_found:
		_say("봉인된 고대 수문을 찾았다… 이 너머에 더 깊은 도시가 있다. (다음: M3 수문)", 8.0)
	_dry = inverse_lerp(Zone1.SURFACE_ROW * Tuning.TILE, world.drained_y, world.surface_y)
	view.sync(diver, _dry, dt)
	_show_sprites()
	_msg_time -= dt
	_msg_label.visible = _msg_time > 0
	_update_hud()


func _try_repair() -> void:
	if world.pump_fixed:
		return
	var need := World.missing(diver.inventory)
	if world.repair_pump(diver.inventory):
		_say("펌프가 돌아간다! 물이 빠진다…", 4.0)
		await get_tree().create_timer(Tuning.DRAIN_TIME).timeout
		_say("구역이 말랐다! 금빛 표시가 된 빈터에서 E를 눌러 건물을 지어보자.", 8.0)
	else:
		var parts := []
		for k in need:
			parts.append("%s %d" % [LABELS[k], need[k]])
		_say("재료가 모자란다: " + ", ".join(parts), 2.5)


## E picks the closest thing in reach: the broken pump, an empty plot, the workshop or the lodge.
func _interact() -> void:
	if not _menu.is_empty():
		_menu = []
		return
	if not world.pump_fixed:
		if diver.near_pump(world):
			_try_repair()
		return
	var v := world.village
	for i in v.plots.size():
		var plot: Dictionary = v.plots[i]
		if diver.pos.distance_to(plot["pos"]) > NEAR:
			continue
		match plot["building"]:
			"": _open_build_menu(i)
			"workshop": _open_workshop_menu()
			"lodge": _open_lodge_menu()
			"tower": _say("조명탑이 깊은 곳까지 빛을 보낸다.", 2.0)
		_menu_at = plot["pos"]
		return


func _open_build_menu(plot: int) -> void:
	_menu = []
	_menu_lodge = false
	for kind in world.village.unbuilt():
		var b: Dictionary = VillageData.BUILDINGS[kind]
		_menu.append({"text": "%s — %s (%s)" % [b["name"], b["desc"], Village.cost_text(b["cost"])], "run": func():
			if world.village.build(plot, kind, diver.inventory):
				_say("%s을(를) 지었다!" % b["name"], 2.5)
				if kind == "lodge":
					_say("숙소에 주민들이 찾아왔다. 숙소에서 E로 말을 걸어보자.", 4.0)
			else:
				_say("재료가 모자란다 (%s)" % Village.cost_text(b["cost"]), 2.0)})


func _open_workshop_menu() -> void:
	_menu = []
	_menu_lodge = false
	var v := world.village
	for kind in VillageData.UPGRADES:
		var u: Dictionary = VillageData.UPGRADES[kind]
		var cost := v.upgrade_cost(kind)
		var lv: int = v.levels[kind]
		if cost.is_empty():
			_menu.append({"text": "%s Lv%d — 최고 단계" % [u["name"], lv], "run": func(): pass})
			continue
		_menu.append({"text": "%s Lv%d→%d — %s (%s)" % [u["name"], lv, lv + 1, u["desc"], Village.cost_text(cost)], "run": func():
			if v.upgrade(kind, diver.inventory):
				_say("%s 업그레이드 완료! (Lv%d)" % [u["name"], v.levels[kind]], 2.5)
			else:
				_say("재료가 모자란다 (%s)" % Village.cost_text(cost), 2.0)})


func _open_lodge_menu() -> void:
	_menu = []
	_menu_lodge = true
	var v := world.village
	for i in v.residents().size():
		var r: Dictionary = v.residents()[i]
		if v.requests_done.has(i):
			_menu.append({"text": "%s: \"고마워, 덕분에 살 만해.\"" % r["name"], "run": func(): pass})
			continue
		_menu.append({"text": "%s(%s): \"%s\" (주기: %s)" % [r["name"], r["look"], r["ask"], Village.cost_text(r["need"])], "run": func():
			if v.fulfil(i, diver.inventory):
				_say("%s: \"%s\" (+%s)" % [r["name"], r["thanks"], Village.cost_text(r["reward"])], 4.0)
			else:
				_say("%s: \"%s\"" % [r["name"], r["hello"]], 3.0)})


func _unhandled_input(event: InputEvent) -> void:
	if _menu.is_empty() or not (event is InputEventKey and event.pressed and not event.echo):
		return
	var i := MENU_KEYS.find(event.physical_keycode)
	if i >= 0 and i < _menu.size():
		var run: Callable = _menu[i]["run"]
		_menu = []
		run.call()


func _say(text: String, seconds := 3.0) -> void:
	_msg_label.text = text
	_msg_time = seconds


# --- what the 3D view shows ------------------------------------------------

## Everything that moves or changes, as pixel sprites in the 3D sea. Sprites a little
## behind the play plane (negative z) sit "in" the scene; the diver is frontmost.
func _show_sprites() -> void:
	view.begin_frame()
	for i in world.air_plants.size():
		view.show_sprite("plant%d" % i, "props/air_bubble_plant", world.air_plants[i] + Vector2(0, sin(_time * 2.0 + i) * 0.5), false, false, -0.2)
	view.show_sprite("pump", "props/pump_fixed" if world.pump_fixed else "props/pump_broken", world.pump + Vector2(0, 8), false, true, -0.3)
	view.show_sprite("gate", "props/floodgate", world.gate + Vector2(0, 8), false, true, -0.3,
		Color(1.4, 1.4, 1.2) if world.gate_found else Color.WHITE)
	if world.pump_fixed:
		var v := world.village
		for i in v.plots.size():
			var kind: String = v.plots[i]["building"]
			if kind != "":
				view.show_sprite("building%d" % i, "props/building_" + kind, v.plots[i]["pos"] + Vector2(0, 9), false, true, -0.5)
		if v.has("lodge"):
			var lodge: Vector2 = v.plots.filter(func(p): return p["building"] == "lodge")[0]["pos"]
			for i in RESIDENT_ART.size():
				var tint := Color.WHITE if v.requests_done.has(i) else Color(1.15, 1.1, 0.9)  # waiting: a little glow
				view.show_sprite("resident%d" % i, "sprites/" + RESIDENT_ART[i], lodge + Vector2(-34 + i * 68, 9), i == 1, true, 0.1, tint)
	for i in world.pickups.size():
		var p: Dictionary = world.pickups[i]
		view.show_sprite("pickup%d" % i, PICKUP_ART[p["kind"]], p["pos"] + Vector2(0, sin(_time * 3.0 + p["pos"].x) * 1.5), false, false, 0.1)
	for i in world.eels.size():
		var e: Eel = world.eels[i]
		if e.alive:
			view.show_sprite("eel%d" % i, "sprites/eel_lantern", e.pos + Vector2(0, sin(_time * 6.0 + i)), e.dir < 0, false, 0.2)
	if not (diver.invulnerable() and int(_time * 12) % 2 == 0):
		var left := diver.facing.x < 0
		if world.in_water(diver.pos) or not _tex.has("sprites/diver_stand"):
			view.show_sprite("diver", "sprites/diver", diver.pos + Vector2(0, sin(_time * 3.0) * 0.8), left, false, 0.4)
		else:
			view.show_sprite("diver", "sprites/diver_stand", diver.pos + Vector2(0, Tuning.DIVER_SIZE.y / 2 + 1), left, true, 0.4)
	view.end_frame()


# --- setup ---------------------------------------------------------------

func _build_hud() -> void:
	var hud := CanvasLayer.new()
	add_child(hud)
	var panel := PanelContainer.new()
	panel.position.y = 4
	var back := StyleBoxFlat.new()
	back.bg_color = Color(0.02, 0.04, 0.06, 0.6)
	back.set_content_margin_all(4)
	panel.add_theme_stylebox_override("panel", back)
	hud.add_child(panel)
	# top-right: the start pocket is the map's top-left corner, where the camera pins the diver
	panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	panel.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	panel.position.x -= 4
	var box := VBoxContainer.new()
	panel.add_child(box)
	var o2 := Label.new()
	o2.text = "산소"
	box.add_child(o2)
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
	_menu_label.position = Vector2(8, 64)
	_menu_label.size = Vector2(624, 0)
	_menu_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0.03, 0.05, 0.08, 0.85)
	bg.set_content_margin_all(6)
	_menu_label.add_theme_stylebox_override("normal", bg)
	_menu_label.add_theme_color_override("font_outline_color", Color.BLACK)
	_menu_label.add_theme_constant_override("outline_size", 4)
	_menu_label.add_theme_color_override("font_color", MENU_COLOR)
	hud.add_child(_menu_label)
	_portraits.position = Vector2(500, 200)
	for name in RESIDENT_ART:
		var face := TextureRect.new()
		face.texture = _tex["portraits/" + name]
		_portraits.add_child(face)
	hud.add_child(_portraits)


func _update_hud() -> void:
	_oxygen_bar.max_value = diver.oxygen_max(world)
	_oxygen_bar.value = diver.oxygen
	_oxygen_bar.modulate = Color(1, 0.4, 0.4) if world.crushing(diver.pos) else Color.WHITE
	var parts := []
	for k in World.MATERIALS:
		parts.append("%s %d" % [LABELS[k], diver.inventory[k]] if world.pump_fixed else "%s %d/%d" % [LABELS[k], diver.inventory[k], Tuning.PUMP_COST[k]])
	_inv_label.text = "  ".join(parts)
	if world.crushing(diver.pos) and _msg_time <= 0:
		_say("수압이 짓누른다! 산소가 빨리 닳는다 — 공방에서 잠수복을 업그레이드하자", 0.3)
	elif diver.near_pump(world) and not world.pump_fixed and _msg_time <= 0:
		_say("E: 펌프 수리", 0.2)
	var lines := []
	for i in _menu.size():
		lines.append("%d. %s" % [i + 1, _menu[i]["text"]])
	if not lines.is_empty():
		lines.append("(숫자로 고르기 · E 닫기)")
	_menu_label.text = "\n".join(lines)
	_menu_label.visible = not lines.is_empty()
	_portraits.visible = _menu_lodge and not lines.is_empty()
	_menu_label.size.y = 0  # shrink back to the text after a longer menu


## ponytail: actions registered in code instead of project.godot's verbose InputEvent blobs.
func _bind_keys() -> void:
	var keys := {
		"left": [KEY_LEFT, KEY_A], "right": [KEY_RIGHT, KEY_D],
		"up": [KEY_UP, KEY_W], "down": [KEY_DOWN, KEY_S],
		"jump": [KEY_SPACE], "tool": [KEY_J, KEY_X], "interact": [KEY_E], "return": [KEY_R],
	}
	for action in keys:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		for k in keys[action]:
			var ev := InputEventKey.new()
			ev.physical_keycode = k
			InputMap.action_add_event(action, ev)
