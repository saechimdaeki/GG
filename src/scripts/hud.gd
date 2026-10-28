class_name Hud
extends CanvasLayer
## Status text (top), phase/build help (bottom), and the end-of-run panel.

signal restart_pressed

var _top: Label
var _bottom: Label
var _end: CenterContainer
var _end_label: Label


func _ready() -> void:
	_top = _label(Vector2(6, 2))
	_bottom = _label(Vector2(6, 344))
	_end = CenterContainer.new()
	_end.set_anchors_preset(Control.PRESET_FULL_RECT)
	_end.visible = false
	add_child(_end)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	_end.add_child(box)
	_end_label = Label.new()
	_end_label.add_theme_font_size_override("font_size", 20)
	_end_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(_end_label)
	var button := Button.new()
	button.text = "다시 시작"
	button.pressed.connect(restart_pressed.emit)
	box.add_child(button)


func _label(pos: Vector2) -> Label:
	var l := Label.new()
	l.position = pos
	l.add_theme_font_size_override("font_size", 10)
	l.add_theme_color_override("font_outline_color", Color.BLACK)
	l.add_theme_constant_override("outline_size", 3)
	add_child(l)
	return l


## Sets the two status lines.
func set_lines(top: String, bottom: String) -> void:
	_top.text = top
	_bottom.text = bottom


## Shows the run-end panel with a restart button.
func show_end(text: String) -> void:
	_end_label.text = text
	_end.visible = true
