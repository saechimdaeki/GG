extends Node2D
## Title screen: a slowly drifting random dungeon behind the menu
## (새 게임 / 이어하기 / 영구 강화 / 설정 / 종료).

const GAME_SCENE := "res://src/scenes/main.tscn"

var _menu: VBoxContainer
var _meta_panel: PanelContainer
var _meta_box: VBoxContainer
var _settings: SettingsPanel
var _camera: Camera2D
var _t: float = 0.0


func _ready() -> void:
	Settings.load_and_apply()
	get_tree().paused = false
	var dark := CanvasModulate.new()
	dark.color = Color(0.45, 0.42, 0.55)
	add_child(dark)
	add_child(Game.make_glow_env())
	var grid := DungeonGrid.new()
	add_child(grid)
	_camera = Camera2D.new()
	_camera.zoom = Vector2.ONE * 0.9
	_camera.position = Vector2(DungeonGrid.W, DungeonGrid.H) * DungeonGrid.TILE / 2.0
	add_child(_camera)

	var ui := CanvasLayer.new()
	add_child(ui)
	var root := Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.theme = UiTheme.get_theme()
	ui.add_child(root)
	var shade := ColorRect.new()
	shade.color = Color(0, 0, 0, 0.35)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(shade)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(center)
	var col := VBoxContainer.new()
	col.alignment = BoxContainer.ALIGNMENT_CENTER
	col.add_theme_constant_override("separation", 6)
	center.add_child(col)
	col.add_child(_label("마왕님은 바쁘다", 30, Color(1.0, 0.45, 0.55)))
	col.add_child(_label("던전을 짓고, 몬스터를 키우고, 직접 용사를 베어라", 9, Color(0.85, 0.8, 0.95)))
	var gap := Control.new()
	gap.custom_minimum_size.y = 10
	col.add_child(gap)
	_menu = VBoxContainer.new()
	_menu.add_theme_constant_override("separation", 4)
	col.add_child(_menu)
	if RunSave.exists():
		_button("이어하기", _continue)
	_button("새 게임", _new_game)
	_button("영구 강화", _open_meta)
	_button("설정", _open_settings)
	_button("종료", func() -> void: get_tree().quit())
	var souls := _label("영혼석 %d" % int(Meta.load_data().souls), 9, Color(0.75, 0.6, 1.0))
	col.add_child(souls)

	_meta_panel = PanelContainer.new()
	_meta_panel.visible = false
	center.add_child(_meta_panel)
	_meta_box = VBoxContainer.new()
	_meta_box.add_theme_constant_override("separation", 3)
	_meta_panel.add_child(_meta_box)
	_settings = SettingsPanel.new()
	_settings.visible = false
	_settings.closed.connect(_back)
	center.add_child(_settings)
	Audio.music("music_build")


func _process(delta: float) -> void:
	_t += delta
	_camera.position = Vector2(DungeonGrid.W, DungeonGrid.H) * DungeonGrid.TILE / 2.0 \
		+ Vector2(sin(_t * 0.07), cos(_t * 0.05)) * Vector2(200, 90)


func _label(text: String, size: int, col: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", col)
	l.add_theme_constant_override("outline_size", 6)
	return l


func _button(text: String, action: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	b.custom_minimum_size = Vector2(160, 24)
	b.add_theme_font_size_override("font_size", 11)
	b.pressed.connect(func() -> void:
		Audio.play("click")
		action.call())
	_menu.add_child(b)


func _new_game() -> void:
	RunSave.clear()
	get_tree().change_scene_to_file(GAME_SCENE)


func _continue() -> void:
	RunSave.load_next = true
	get_tree().change_scene_to_file(GAME_SCENE)


func _show(panel: Control) -> void:
	_menu.get_parent().visible = panel == null
	_meta_panel.visible = panel == _meta_panel
	_settings.visible = panel == _settings


func _back() -> void:
	_show(null)


func _open_settings() -> void:
	_show(_settings)


func _open_meta() -> void:
	_show(_meta_panel)
	_refresh_meta()


func _refresh_meta() -> void:
	for c in _meta_box.get_children():
		c.queue_free()
	var data := Meta.load_data()
	_meta_box.add_child(_label("영구 강화 — 보유 영혼석 %d" % int(data.souls), 12, Color(0.8, 0.65, 1.0)))
	for id: String in Meta.UPGRADES:
		var up: Dictionary = Meta.UPGRADES[id]
		var p := Meta.price(data, id)
		var desc: String = up.desc % Meta.bonus(data, id) if "%d" in up.desc else up.desc
		var b := Button.new()
		b.text = "%s Lv%d/%d  (%s)  %s" % [up.name, data.levels[id], up.max, desc, "MAX" if p < 0 else "%d영혼" % p]
		b.disabled = p < 0 or int(data.souls) < p
		b.focus_mode = Control.FOCUS_NONE
		b.add_theme_font_size_override("font_size", 9)
		b.pressed.connect(func() -> void:
			Audio.play("coin" if Meta.buy(data, id) else "error")
			_refresh_meta())
		_meta_box.add_child(b)
	var back := Button.new()
	back.text = "돌아가기"
	back.focus_mode = Control.FOCUS_NONE
	back.pressed.connect(func() -> void:
		Audio.play("click")
		_back())
	_meta_box.add_child(back)
