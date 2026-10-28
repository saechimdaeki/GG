class_name Hud
extends CanvasLayer
## Status line, build toolbar (click or keys 1-7), toast messages, and the end-of-run panel.

signal restart_pressed
signal tool_clicked(id: String)
signal shop_clicked(id: String)
signal meta_clicked(id: String)

var _status: StatusPanel
var _hint: Label
var _toast: Label
var _toolbar: HBoxContainer
var _tool_buttons: Dictionary = {}
var _shop: VBoxContainer
var _shop_buttons: Dictionary = {}
var _end: CenterContainer
var _end_label: Label
var _meta_box: VBoxContainer


func _ready() -> void:
	_status = StatusPanel.new()
	add_child(_status)
	_hint = _label(Vector2(6, 318), 8)
	_hint.add_theme_color_override("font_color", Color(0.78, 0.74, 0.85))
	_toast = _label(Vector2(0, 150), 14)
	_toast.size = Vector2(640, 20)
	_toast.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_toast.modulate.a = 0.0
	_toolbar = HBoxContainer.new()
	_toolbar.position = Vector2(4, 331)
	_toolbar.add_theme_constant_override("separation", 3)
	_toolbar.theme = UiTheme.get_theme()
	add_child(_toolbar)
	var panel := PanelContainer.new()
	panel.position = Vector2(260, 110)
	panel.visible = false
	panel.theme = UiTheme.get_theme()
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
	_end.theme = UiTheme.get_theme()
	add_child(_end)
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.6)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	_end.add_child(dim)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	_end.add_child(box)
	_end_label = Label.new()
	_end_label.add_theme_font_size_override("font_size", 16)
	_end_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(_end_label)
	_meta_box = VBoxContainer.new()
	_meta_box.add_theme_constant_override("separation", 2)
	box.add_child(_meta_box)
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


## Updates the bars/slots (see Game._status() for keys) and the controls hint line.
func set_status(state: Dictionary, hint: String) -> void:
	_status.state = state
	_hint.text = hint
	# Build: hint sits above the toolbar. Raid: bottom-left, left of the skill slots.
	_hint.position.y = 318 if not state.raid else 344


## Short message in the middle of the screen that fades out.
func toast(text: String, color: Color = Color(1, 0.85, 0.4)) -> void:
	_toast.text = text
	_toast.add_theme_color_override("font_color", color)
	_toast.modulate.a = 1.0
	var tw := _toast.create_tween()
	tw.tween_interval(0.8)
	tw.tween_property(_toast, "modulate:a", 0.0, 0.4)


## Shows the run-end panel with permanent-upgrade buttons and a restart button.
## meta_items: [{id, label, enabled}]
func show_end(text: String, meta_items: Array = []) -> void:
	_end_label.text = text
	_toast.modulate.a = 0.0
	show_toolbar(false)
	for child in _meta_box.get_children():
		child.queue_free()
	for it: Dictionary in meta_items:
		var b := Button.new()
		b.text = it.label
		b.disabled = not it.enabled
		b.focus_mode = Control.FOCUS_NONE
		b.add_theme_font_size_override("font_size", 9)
		b.pressed.connect(meta_clicked.emit.bind(it.id))
		_meta_box.add_child(b)
	_end.visible = true
