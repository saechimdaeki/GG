class_name Title
extends Control
## Title screen: a glowing pixel logo over the shop facade art, and a text menu
## (continue / new game / settings / quit) with a candle cursor on the hovered item.

const SHOP := "res://src/shop/shop_view.tscn"
const ART_BG := "res://assets/sprites/scenes/title_bg.png"
const FALLBACK_BG := "res://assets/sprites/shop_bg.png"
const GOLD := Color(0.95, 0.78, 0.4)
const LOGO := "저주받은 전당포"
const LOGO_SIZE := 45  # Galmuri14 x3
const MENU_X := 320.0
const MENU_Y := 214.0
const MENU_STEP := 22.0
## CC BY 4.0 requires this credit in the game. The other two tracks are CC0 (see README).
const CREDITS := "Music \"Night on the Docks - Sax\" Kevin MacLeod (incompetech.com) · CC BY 4.0"

var _bg: Texture2D
var _t := 0.0
var _items: Array[Button] = []
var _hover := 0


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	theme = ShopView.make_theme()
	PauseMenu.apply_saved()
	for p in [ART_BG, FALLBACK_BG]:
		if ResourceLoader.exists(p):
			_bg = load(p)
			break
	Audio.music("music_title")
	Audio.ambience("ambience")

	var settings := PauseMenu.new()
	settings.in_game = false
	if RunState.has_save():
		var saved := RunState.load_save()
		if saved:
			_item("이어하기  ·  %d일째" % saved.day, func(): _start(true))
	_item("새로 시작", func(): _start(false))
	_item("설정", func(): settings.open())
	_item("종료", func(): get_tree().quit())
	add_child(settings)


## A menu line: plain text button, no box; the candle and color show the hover.
func _item(text: String, action: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.flat = true
	UIFont.style(b, 12)
	for state in ["normal", "hover", "pressed", "focus"]:
		b.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	b.add_theme_color_override("font_color", Color(0.78, 0.72, 0.68))
	b.add_theme_color_override("font_hover_color", GOLD)
	b.add_theme_color_override("font_pressed_color", Color(1, 0.95, 0.75))
	b.size = Vector2(200, 18)
	b.position = Vector2(MENU_X - 100, MENU_Y + _items.size() * MENU_STEP)
	var i := _items.size()
	b.mouse_entered.connect(func():
		if _hover != i:
			Audio.play("click", -12.0)
		_hover = i)
	b.pressed.connect(func():
		Audio.play("bell")
		action.call())
	add_child(b)
	_items.append(b)


func _start(resume: bool) -> void:
	if not resume:
		RunState.delete_save()
	ShopView.resume = resume
	get_tree().change_scene_to_file(SHOP)


func _unhandled_input(event: InputEvent) -> void:
	# keyboard: up / down moves the candle, enter picks
	if event.is_action_pressed("ui_down") or event.is_action_pressed("ui_up"):
		_hover = wrapi(_hover + (1 if event.is_action_pressed("ui_down") else -1), 0, _items.size())
		Audio.play("click", -12.0)
	elif event.is_action_pressed("ui_accept") and not _items.is_empty():
		_items[_hover].pressed.emit()


func _process(delta: float) -> void:
	_t += delta
	queue_redraw()


func _draw() -> void:
	draw_rect(Rect2(0, 0, 640, 360), Color(0.04, 0.02, 0.04))
	if _bg:
		draw_texture_rect(_bg, Rect2(0, 0, 640, 360), false, Color(0.55, 0.5, 0.6) if _bg.resource_path == FALLBACK_BG else Color(0.85, 0.82, 0.9))
	# darken the band behind the logo and the menu so both read over busy art
	for i in 10:
		var a := 0.06 * (1.0 - i / 10.0)
		draw_rect(Rect2(0, 60 + i * 2, 640, 110 - i * 4), Color(0, 0, 0, a))
	draw_rect(Rect2(MENU_X - 110, MENU_Y - 10, 220, _items.size() * MENU_STEP + 14), Color(0.03, 0.01, 0.03, 0.55))
	for i in 10:
		draw_rect(Rect2(i * 5, i * 5, 640 - i * 10, 360 - i * 10), Color(0, 0, 0, 0.09), false, 5)

	_draw_logo()
	var sub := "괴물 손님의 물건을 감정하고, 28일 안에 빚을 갚아라"
	draw_string_outline(UIFont.BODY, Vector2(0, 160), sub, HORIZONTAL_ALIGNMENT_CENTER, 640, 12, 4, Color(0, 0, 0, 0.85))
	draw_string(UIFont.BODY, Vector2(0, 160), sub, HORIZONTAL_ALIGNMENT_CENTER, 640, 12, GOLD)

	# candle cursor beside the hovered line
	if not _items.is_empty():
		var b := _items[_hover]
		var w := UIFont.font(12).get_string_size(b.text, HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x
		var at := Vector2(MENU_X - w / 2.0 - 16, b.position.y + 12)
		_draw_candle(at)
		_draw_candle(Vector2(MENU_X + w / 2.0 + 16, at.y))

	draw_string(UIFont.SMALL, Vector2(8, 352), "v0.7", HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color(0.5, 0.45, 0.45))
	draw_string(UIFont.SMALL, Vector2(0, 352), CREDITS, HORIZONTAL_ALIGNMENT_RIGHT, 632, 10, Color(0.55, 0.5, 0.5))


## Pixel logo: thick dark outline, a violet glow that breathes, pale violet fill.
func _draw_logo() -> void:
	var y := 118.0
	var glow := 0.5 + 0.5 * sin(_t * 1.7) + sin(_t * 9.0) * 0.08
	for r in [9, 6, 3]:
		draw_string_outline(UIFont.BIG, Vector2(0, y), LOGO, HORIZONTAL_ALIGNMENT_CENTER, 640, LOGO_SIZE, r * 2,
			Color(0.55, 0.2, 0.9, 0.07 + glow * 0.05))
	draw_string_outline(UIFont.BIG, Vector2(0, y + 3), LOGO, HORIZONTAL_ALIGNMENT_CENTER, 640, LOGO_SIZE, 6, Color(0, 0, 0, 0.9))
	draw_string_outline(UIFont.BIG, Vector2(0, y), LOGO, HORIZONTAL_ALIGNMENT_CENTER, 640, LOGO_SIZE, 6, Color(0.08, 0.02, 0.1))
	draw_string(UIFont.BIG, Vector2(0, y), LOGO, HORIZONTAL_ALIGNMENT_CENTER, 640, LOGO_SIZE, Color(0.86, 0.7, 1.0))
	# a lighter top half reads as a highlight on the letters
	draw_string(UIFont.BIG, Vector2(0, y - 1), LOGO, HORIZONTAL_ALIGNMENT_CENTER, 640, LOGO_SIZE, Color(1, 0.92, 1, 0.25))


func _draw_candle(at: Vector2) -> void:
	var h := 5.0 + sin(_t * 11.0 + at.x) * 1.0
	draw_rect(Rect2(at.x - 2, at.y - 4, 4, 7), Color(0.92, 0.88, 0.75))
	draw_colored_polygon(PackedVector2Array([at + Vector2(0, -4 - h), at + Vector2(2, -5), at + Vector2(0, -4), at + Vector2(-2, -5)]), Color(1, 0.75, 0.3))
	draw_circle(at + Vector2(0, -6), 5, Color(1, 0.6, 0.2, 0.15))
