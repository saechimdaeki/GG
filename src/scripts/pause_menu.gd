class_name PauseMenu
extends CanvasLayer
## Esc during a run: pauses the game with 계속 / 설정 / 타이틀로.

var _root: Control
var _menu: VBoxContainer
var _settings: SettingsPanel


func _ready() -> void:
	layer = 20
	process_mode = Node.PROCESS_MODE_ALWAYS
	_root = ColorRect.new()
	(_root as ColorRect).color = Color(0, 0, 0, 0.6)
	_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.theme = UiTheme.get_theme()
	_root.visible = false
	add_child(_root)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.add_child(center)
	_menu = VBoxContainer.new()
	_menu.add_theme_constant_override("separation", 4)
	center.add_child(_menu)
	var title := Label.new()
	title.text = "일시정지"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 16)
	title.add_theme_color_override("font_color", Color(1.0, 0.85, 0.4))
	_menu.add_child(title)
	for item: Array in [["계속", close], ["설정", _open_settings], ["타이틀로 (건설 단계부터 이어하기 가능)", _to_title]]:
		var b := Button.new()
		b.text = item[0]
		b.focus_mode = Control.FOCUS_NONE
		b.custom_minimum_size = Vector2(200, 22)
		var action: Callable = item[1]
		b.pressed.connect(func() -> void:
			Audio.play("click")
			action.call())
		_menu.add_child(b)
	_settings = SettingsPanel.new()
	_settings.visible = false
	_settings.closed.connect(func() -> void:
		_settings.visible = false
		_menu.visible = true)
	center.add_child(_settings)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and (event as InputEventKey).physical_keycode == KEY_ESCAPE:
		if _root.visible:
			close()
		elif not get_tree().paused:  # level-up cards also pause; don't stack on them
			open()
		get_viewport().set_input_as_handled()


func open() -> void:
	_root.visible = true
	_menu.visible = true
	_settings.visible = false
	get_tree().paused = true


func close() -> void:
	_root.visible = false
	get_tree().paused = false


func _open_settings() -> void:
	_menu.visible = false
	_settings.visible = true


func _to_title() -> void:
	get_tree().paused = false
	get_tree().change_scene_to_file("res://src/scenes/title.tscn")
