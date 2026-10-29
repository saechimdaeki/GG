class_name Title
extends Control
## Title screen: continue (if a morning save exists), new game, quit.
## Uses the title scene art (art/raw/scenes/title_bg.png) when present, else the shop wall.

const SHOP := "res://src/shop/shop_view.tscn"
const ART_BG := "res://assets/sprites/scenes/title_bg.png"

const FALLBACK_BG := "res://assets/sprites/shop_bg.png"
const GOLD := Color(0.95, 0.78, 0.4)

var _bg: Texture2D
var _t := 0.0


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	for p in [ART_BG, FALLBACK_BG]:
		if ResourceLoader.exists(p):
			_bg = load(p)
			break
	Audio.music("ambience")

	var box := VBoxContainer.new()
	box.position = Vector2(240, 214)
	box.size = Vector2(160, 120)
	box.add_theme_constant_override("separation", 6)
	add_child(box)
	if RunState.has_save():
		var saved := RunState.load_save()
		if saved:
			box.add_child(_button("🕯 이어하기 (%d일째)" % saved.day, func(): _start(true)))
	box.add_child(_button("새로 시작", func(): _start(false)))
	box.add_child(_button("종료", func(): get_tree().quit()))


func _button(text: String, action: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(160, 26)
	b.add_theme_font_size_override("font_size", 11)
	b.add_theme_stylebox_override("normal", _box(Color(0.14, 0.08, 0.07, 0.9), Color(0.55, 0.38, 0.22)))
	b.add_theme_stylebox_override("hover", _box(Color(0.3, 0.17, 0.11, 0.95), GOLD))
	b.add_theme_stylebox_override("pressed", _box(Color(0.1, 0.06, 0.05), GOLD))
	b.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
	b.pressed.connect(func():
		Audio.play("click")
		action.call())
	return b


func _box(bg: Color, border: Color) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(1)
	s.set_corner_radius_all(2)
	return s


func _start(resume: bool) -> void:
	if not resume:
		RunState.delete_save()
	ShopView.resume = resume
	get_tree().change_scene_to_file(SHOP)


func _process(delta: float) -> void:
	_t += delta
	queue_redraw()


func _draw() -> void:
	draw_rect(Rect2(0, 0, 640, 360), Color(0.05, 0.03, 0.05))
	if _bg:
		draw_texture_rect(_bg, Rect2(0, 0, 640, 360), false, Color(0.55, 0.5, 0.6) if _bg.resource_path == FALLBACK_BG else Color.WHITE)
	# candle glow and a dark vignette
	var flick := sin(_t * 9.0) * 4.0 + sin(_t * 23.0) * 2.0
	for i in 6:
		draw_circle(Vector2(320, 150), 260.0 - i * 35.0 + flick, Color(1.0, 0.6, 0.25, 0.025))
	for i in 8:
		draw_rect(Rect2(i * 6, i * 6, 640 - i * 12, 360 - i * 12), Color(0, 0, 0, 0.1), false, 6)
	# ponytail: the title text stays code-drawn; image models garble Korean lettering
	var font := ThemeDB.fallback_font
	for o in [Vector2(2, 2), Vector2.ZERO]:
		draw_string(font, Vector2(0, 110) + o, "저주받은 전당포", HORIZONTAL_ALIGNMENT_CENTER, 640, 44,
			Color(0, 0, 0, 0.8) if o != Vector2.ZERO else Color(0.85, 0.6, 1.0))
	draw_string(font, Vector2(0, 140), "괴물 손님의 물건을 감정하고, 28일 안에 빚을 갚아라", HORIZONTAL_ALIGNMENT_CENTER, 640, 11, GOLD)
	draw_string(ThemeDB.fallback_font, Vector2(8, 352), "v0.5 · M1~M5", HORIZONTAL_ALIGNMENT_LEFT, -1, 8, Color(0.5, 0.45, 0.45))
