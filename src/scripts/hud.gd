class_name Hud
extends CanvasLayer
## Status line, build toolbar (click or keys 1-7), toast messages, and the end-of-run panel.

signal restart_pressed
signal tool_clicked(id: String)
signal shop_clicked(id: String)

var _top: Label
var _hint: Label
var _toast: Label
var _toolbar: HBoxContainer
var _tool_buttons: Dictionary = {}
var _shop: VBoxContainer
var _shop_buttons: Dictionary = {}
var _end: CenterContainer
var _end_label: Label


func _ready() -> void:
	_top = _label(Vector2(6, 2), 9)
	_hint = _label(Vector2(6, 318), 9)
	_toast = _label(Vector2(0, 150), 14)
	_toast.size = Vector2(640, 20)
	_toast.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_toast.modulate.a = 0.0
	_toolbar = HBoxContainer.new()
	_toolbar.position = Vector2(4, 331)
	_toolbar.add_theme_constant_override("separation", 3)
	add_child(_toolbar)
	var panel := PanelContainer.new()
	panel.position = Vector2(260, 110)
	panel.visible = false
	add_child(panel)
	_shop = VBoxContainer.new()
	_shop.add_theme_constant_override("separation", 3)
	panel.add_child(_shop)
	var title := Label.new()
	title.text = "상점  [Tab] 닫기"
	title.add_theme_font_size_override("font_size", 9)
	_shop.add_child(title)
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
	button.focus_mode = Control.FOCUS_NONE
	button.pressed.connect(restart_pressed.emit)
	box.add_child(button)


func _label(pos: Vector2, size: int) -> Label:
	var l := Label.new()
	l.position = pos
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_outline_color", Color.BLACK)
	l.add_theme_constant_override("outline_size", 4)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(l)
	return l


## Rebuilds the toolbar. tools: [{id, key, label, cost, color, locked}]
func set_tools(tools: Array) -> void:
	for child in _toolbar.get_children():
		child.queue_free()
	_tool_buttons.clear()
	for t: Dictionary in tools:
		var b := Button.new()
		b.text = "%d %s %dG" % [t.key % 10, t.label, t.cost] if not t.locked else "%d 잠김" % (t.key % 10)
		b.disabled = t.locked
		b.focus_mode = Control.FOCUS_NONE
		b.toggle_mode = true
		b.add_theme_font_size_override("font_size", 8)
		b.add_theme_color_override("font_color", (t.color as Color).lightened(0.3))
		b.custom_minimum_size = Vector2(60, 24)
		b.pressed.connect(tool_clicked.emit.bind(t.id))
		_toolbar.add_child(b)
		_tool_buttons[t.id] = b


## Highlights the selected tool button.
func select_tool(id: String) -> void:
	for key: String in _tool_buttons:
		(_tool_buttons[key] as Button).set_pressed_no_signal(key == id)


func show_toolbar(on: bool) -> void:
	_toolbar.visible = on
	if not on:
		_shop.get_parent().visible = false


## Opens / closes the shop panel (build phase).
func toggle_shop() -> void:
	var panel := _shop.get_parent() as Control
	panel.visible = not panel.visible


## Creates or updates shop buttons. items: [{id, label, price, enabled}]
func set_shop(items: Array) -> void:
	for it: Dictionary in items:
		var b: Button = _shop_buttons.get(it.id)
		if b == null:
			b = Button.new()
			b.focus_mode = Control.FOCUS_NONE
			b.add_theme_font_size_override("font_size", 9)
			b.custom_minimum_size = Vector2(120, 20)
			b.pressed.connect(shop_clicked.emit.bind(it.id))
			_shop.add_child(b)
			_shop_buttons[it.id] = b
		b.text = "%s %dG" % [it.label, it.price]
		b.disabled = not it.enabled


## Sets the status line and the hint line above the toolbar.
func set_lines(top: String, hint: String) -> void:
	_top.text = top
	_hint.text = hint


## Short message in the middle of the screen that fades out.
func toast(text: String, color: Color = Color(1, 0.85, 0.4)) -> void:
	_toast.text = text
	_toast.add_theme_color_override("font_color", color)
	_toast.modulate.a = 1.0
	var tw := _toast.create_tween()
	tw.tween_interval(0.8)
	tw.tween_property(_toast, "modulate:a", 0.0, 0.4)


## Shows the run-end panel with a restart button.
func show_end(text: String) -> void:
	_end_label.text = text
	_end.visible = true
