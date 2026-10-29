class_name SettingsPanel
extends PanelContainer
## Volume sliders, fullscreen toggle and the controls list. Used by the title screen and the
## pause menu. Emits closed when the player presses 닫기.

signal closed

const CONTROLS := """WASD / 방향키  이동
좌클릭  근접 공격 · 건설
우클릭  마법탄
Space / Shift  대시 (무적)
Q / E  스킬
1-9  건설 도구 · Tab  상점
Enter  침입 시작 · Esc  일시정지"""


func _ready() -> void:
	theme = UiTheme.get_theme()
	if Settings.values.is_empty():
		Settings.load_and_apply()
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	add_child(box)
	box.add_child(_label("설정", 14, Color(1.0, 0.85, 0.4)))
	for row: Array in [["master", "전체 볼륨"], ["music", "음악"], ["sfx", "효과음"]]:
		var h := HBoxContainer.new()
		h.add_child(_label(row[1], 9, Color.WHITE, 70))
		var s := HSlider.new()
		s.min_value = 0.0
		s.max_value = 1.0
		s.step = 0.05
		s.value = float(Settings.values[row[0]])
		s.custom_minimum_size = Vector2(150, 16)
		s.value_changed.connect(func(v: float) -> void: Settings.set_value(row[0], v))
		h.add_child(s)
		box.add_child(h)
	var fs := CheckButton.new()
	fs.text = "전체 화면"
	fs.add_theme_font_size_override("font_size", 9)
	fs.button_pressed = bool(Settings.values.fullscreen)
	fs.focus_mode = Control.FOCUS_NONE
	fs.toggled.connect(func(on: bool) -> void: Settings.set_value("fullscreen", on))
	box.add_child(fs)
	box.add_child(_label("조작법", 10, Color(1.0, 0.85, 0.4)))
	box.add_child(_label(CONTROLS, 8, Color(0.85, 0.82, 0.9)))
	var close := Button.new()
	close.text = "닫기"
	close.focus_mode = Control.FOCUS_NONE
	close.pressed.connect(func() -> void:
		Audio.play("click")
		closed.emit())
	box.add_child(close)


func _label(text: String, size: int, col: Color, min_w: float = 0.0) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	l.custom_minimum_size.x = min_w
	return l
