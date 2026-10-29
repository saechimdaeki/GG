class_name DialogBox
extends Control
## Visual-novel style dialogue: dimmed screen, speaker portrait (enemies on the right),
## name tab, typewriter text with a blip, ▼ when the line is done. Click / Space / Enter
## finishes the line, then advances. Pauses the game until the last line is read.

signal finished

const CPS := 40.0  # typewriter characters per second
## Speaker -> sprite used as the portrait (Sprites.tex). Right-side speakers face left.
const PORTRAITS := {"마왕": "demon_lord", "집사 임프": "imp", "용사 보스": "boss_hero"}
const RIGHT_SIDE := ["용사 보스"]

var _lines: Array = []
var _panel: PanelContainer
var _name: Label
var _text: Label
var _more: Label
var _portrait: TextureRect
var _frame: PanelContainer
var _t: float = 0.0
var _shown: float = 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	process_mode = Node.PROCESS_MODE_ALWAYS
	visible = false
	theme = UiTheme.get_theme()
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.4)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	_frame = PanelContainer.new()
	_frame.custom_minimum_size = Vector2(84, 84)
	add_child(_frame)
	_portrait = TextureRect.new()
	_portrait.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_portrait.custom_minimum_size = Vector2(76, 76)
	_frame.add_child(_portrait)
	_panel = PanelContainer.new()
	_panel.custom_minimum_size = Vector2(470, 84)
	add_child(_panel)
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 4)
	_panel.add_child(v)
	_name = Label.new()
	_name.add_theme_font_size_override("font_size", 12)
	_name.add_theme_constant_override("outline_size", 4)
	v.add_child(_name)
	_text = Label.new()
	_text.add_theme_font_size_override("font_size", 11)
	_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_text.custom_minimum_size = Vector2(450, 40)
	v.add_child(_text)
	_more = Label.new()
	_more.text = "▼"
	_more.add_theme_font_size_override("font_size", 9)
	_more.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	v.add_child(_more)


## Plays lines ([speaker, text]) one by one.
func play(lines: Array) -> void:
	if lines.is_empty():
		return
	_lines = lines.duplicate()
	get_tree().paused = true
	visible = true
	_next()


## True while a line is on screen.
func is_open() -> bool:
	return visible


func _next() -> void:
	if _lines.is_empty():
		visible = false
		get_tree().paused = false
		finished.emit()
		return
	var line: Array = _lines.pop_front()
	var who: String = line[0]
	var col: Color = Story.SPEAKERS.get(who, Color.WHITE)
	var right := RIGHT_SIDE.has(who)
	_name.text = who
	_name.add_theme_color_override("font_color", col)
	_text.text = line[1]
	_text.visible_characters = 0
	_shown = 0.0
	_portrait.texture = Sprites.tex(String(PORTRAITS.get(who, "")))
	_portrait.flip_h = right
	_frame.visible = _portrait.texture != null
	var vp := get_viewport_rect().size
	var y := vp.y - 100.0
	_frame.position = Vector2(vp.x - 104.0 if right else 20.0, y)
	_panel.position = Vector2(20.0 if right else 112.0, y)
	var fs := StyleBoxFlat.new()
	fs.bg_color = col.darkened(0.8)
	fs.border_color = col
	fs.set_border_width_all(2)
	fs.set_corner_radius_all(4)
	_frame.add_theme_stylebox_override("panel", fs)
	# Speaker pops in.
	_frame.pivot_offset = _frame.custom_minimum_size / 2.0
	_frame.scale = Vector2(0.85, 1.15)
	_frame.create_tween().tween_property(_frame, "scale", Vector2.ONE, 0.15).set_trans(Tween.TRANS_BACK)


func _process(delta: float) -> void:
	if not visible:
		return
	_t += delta
	var total := _text.get_total_character_count()
	if _text.visible_characters < total:
		var before := _text.visible_characters
		_shown += delta * CPS
		_text.visible_characters = mini(int(_shown), total)
		if _text.visible_characters / 2 != before / 2:
			Audio.play("click", -20.0, 0.25)
	_more.modulate.a = 0.0 if _text.visible_characters < total else 0.5 + 0.5 * sin(_t * 8.0)
	# Idle bob on the portrait.
	_portrait.position.y = 2.0 * sin(_t * 3.0)


func _input(event: InputEvent) -> void:
	if not visible:
		return
	var press: bool = (event is InputEventMouseButton and event.pressed) \
		or (event is InputEventKey and event.pressed and not event.echo
			and (event as InputEventKey).physical_keycode in [KEY_SPACE, KEY_ENTER, KEY_ESCAPE])
	if not press:
		return
	get_viewport().set_input_as_handled()
	if _text.visible_characters < _text.get_total_character_count():
		_text.visible_characters = _text.get_total_character_count()
		_shown = 9999.0
	else:
		_next()
