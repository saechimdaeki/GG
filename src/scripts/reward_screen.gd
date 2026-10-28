class_name RewardScreen
extends CanvasLayer
## Wave-clear reward picker: three cards, click one.

signal picked(id: String)

var _root: Control
var _row: HBoxContainer
var _title: Label


func _ready() -> void:
	layer = 5
	_root = ColorRect.new()
	(_root as ColorRect).color = Color(0, 0, 0, 0.6)
	_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	_root.visible = false
	_root.theme = UiTheme.get_theme()
	add_child(_root)
	var center := CenterContainer.new()
	center.set_anchors_preset(Control.PRESET_FULL_RECT)
	_root.add_child(center)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	center.add_child(box)
	_title = Label.new()
	_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_title.add_theme_font_size_override("font_size", 14)
	_title.add_theme_color_override("font_color", Color(1.0, 0.85, 0.4))
	box.add_child(_title)
	_row = HBoxContainer.new()
	_row.add_theme_constant_override("separation", 12)
	box.add_child(_row)


## Shows one card per reward dictionary (id, title, desc).
func open(choices: Array, title: String = "웨이브 클리어! 보상을 하나 고르세요") -> void:
	_title.text = title
	for child in _row.get_children():
		child.queue_free()
	for r: Dictionary in choices:
		var card := Button.new()
		card.focus_mode = Control.FOCUS_NONE
		card.text = "%s\n\n%s" % [r.title, r.desc]
		card.custom_minimum_size = Vector2(140, 90)
		card.pressed.connect(_choose.bind(r.id))
		_row.add_child(card)
	_root.visible = true


func _choose(id: String) -> void:
	_root.visible = false
	picked.emit(id)
