class_name UpgradePanel
extends Panel
## Night shop: spend gold on shelf space, appraisal tools and protection.
## Calls [member on_done] when closed.

var run: RunState
var on_done: Callable
var _title: Label
var _rows: VBoxContainer
var _done: Button


func _init() -> void:
	position = Vector2(40, 22)
	size = Vector2(560, 316)
	_title = Label.new()
	_title.position = Vector2(14, 8)
	_title.size = Vector2(532, 30)
	add_child(_title)
	_rows = VBoxContainer.new()
	_rows.position = Vector2(12, 44)
	_rows.size = Vector2(536, 230)
	_rows.add_theme_constant_override("separation", 5)
	add_child(_rows)
	_done = Button.new()
	_done.position = Vector2(200, 282)
	_done.size = Vector2(160, 26)
	_done.add_theme_font_size_override("font_size", 11)
	_done.pressed.connect(func():
		visible = false
		on_done.call())
	add_child(_done)


func open(state: RunState, done_text: String, done: Callable) -> void:
	run = state
	on_done = done
	_done.text = done_text
	visible = true
	_rebuild()


func _rebuild() -> void:
	for c in _rows.get_children():
		_rows.remove_child(c)
		c.queue_free()
	_title.text = "🛠 밤의 상점 — 소지금 %dG   (%s)" % [run.money, run.debt_line()]
	for id in ProgressionData.UPGRADES:
		var u: Dictionary = ProgressionData.UPGRADES[id]
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 6)
		var lv := run.level(id)
		var max_lv: int = u["prices"].size()
		var name := _text("%s %s" % [u["name"], ("Lv%d" % lv) if max_lv > 1 else ("✔" if lv > 0 else "")], 10, Color(0.95, 0.85, 0.6))
		name.custom_minimum_size = Vector2(130, 0)
		row.add_child(name)
		var desc := _text(u["desc"], 9, Color(0.78, 0.72, 0.64))
		desc.custom_minimum_size = Vector2(270, 0)
		row.add_child(desc)
		var price := run.upgrade_price(id)
		var b := Button.new()
		b.custom_minimum_size = Vector2(110, 22)
		if price < 0:
			b.text = "완료"
			b.disabled = true
		else:
			b.text = "사기 %dG" % price
			b.disabled = run.money < price
			b.pressed.connect(func():
				run.buy_upgrade(id)
				_rebuild())
		row.add_child(b)
		_rows.add_child(row)


func _text(t: String, font_size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = t
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", color)
	return l
