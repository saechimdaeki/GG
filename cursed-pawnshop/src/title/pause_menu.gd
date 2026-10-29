class_name PauseMenu
extends Panel
## ESC menu and settings: music / effects volume and fullscreen (saved to
## user://settings.cfg), plus resume / back to title / quit. The title screen uses it
## as a plain settings panel ([member in_game] = false).

const SETTINGS_PATH := "user://settings.cfg"
const TITLE := "res://src/title/title.tscn"

## True in the shop (pauses the game, shows resume / back to title).
var in_game := true
var _music: HSlider
var _sfx: HSlider
var _full: CheckButton


## Applies saved settings; call once at startup.
static func apply_saved() -> void:
	var cfg := ConfigFile.new()
	cfg.load(SETTINGS_PATH)
	Audio.set_volume("Music", cfg.get_value("audio", "music", 0.8))
	Audio.set_volume("SFX", cfg.get_value("audio", "sfx", 0.8))
	var full: bool = cfg.get_value("video", "fullscreen", false)
	if DisplayServer.get_name() != "headless":
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN if full else DisplayServer.WINDOW_MODE_WINDOWED)


func _init() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	position = Vector2(170, 60)
	size = Vector2(300, 240)
	visible = false


func _ready() -> void:
	var cfg := ConfigFile.new()
	cfg.load(SETTINGS_PATH)
	var box := VBoxContainer.new()
	box.position = Vector2(20, 14)
	box.size = Vector2(260, 212)
	box.add_theme_constant_override("separation", 6)
	add_child(box)
	box.add_child(_label("⚙ 설정" if not in_game else "⏸ 일시정지", 13))
	_music = _slider(box, "음악", cfg.get_value("audio", "music", 0.8))
	_sfx = _slider(box, "효과음", cfg.get_value("audio", "sfx", 0.8))
	_full = CheckButton.new()
	_full.text = "전체화면"
	_full.button_pressed = cfg.get_value("video", "fullscreen", false)
	_full.toggled.connect(func(_on): _save())
	box.add_child(_full)
	if in_game:
		box.add_child(_button("▶ 계속하기", close))
		box.add_child(_button("🏠 타이틀로 (오늘 아침부터 다시)", func():
			get_tree().paused = false
			get_tree().change_scene_to_file(TITLE)))
		box.add_child(_button("종료", func(): get_tree().quit()))
	else:
		box.add_child(_button("닫기", close))


func open() -> void:
	visible = true
	if in_game:
		get_tree().paused = true


func close() -> void:
	visible = false
	if in_game:
		get_tree().paused = false


func _unhandled_input(event: InputEvent) -> void:
	if in_game and event.is_action_pressed("ui_cancel"):
		close() if visible else open()
		get_viewport().set_input_as_handled()


func _save() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("audio", "music", _music.value)
	cfg.set_value("audio", "sfx", _sfx.value)
	cfg.set_value("video", "fullscreen", _full.button_pressed)
	cfg.save(SETTINGS_PATH)
	apply_saved()


func _slider(parent: Node, text: String, value: float) -> HSlider:
	var row := HBoxContainer.new()
	var l := _label(text, 10)
	l.custom_minimum_size = Vector2(60, 0)
	row.add_child(l)
	var s := HSlider.new()
	s.min_value = 0.0
	s.max_value = 1.0
	s.step = 0.05
	s.value = value
	s.custom_minimum_size = Vector2(180, 18)
	s.value_changed.connect(func(_v): _save())
	row.add_child(s)
	parent.add_child(row)
	return s


func _label(text: String, font_size: int) -> Label:
	var l := Label.new()
	l.text = text
	UIFont.style(l, font_size)
	return l


func _button(text: String, action: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(0, 22)
	b.pressed.connect(func():
		Audio.play("click")
		action.call())
	return b
