class_name ShelfPanel
extends Panel
## The stockroom list. Morning: pick what goes on the shelf and set price tags.
## Night: sell anything to the wholesaler. Calls [member on_done] when closed.

var run: RunState
var on_done: Callable
var _night := false
var _title: Label
var _rows: VBoxContainer
var _done: Button


func _init() -> void:
	position = Vector2(40, 22)
	size = Vector2(560, 316)
	_title = Label.new()
	_title.position = Vector2(14, 8)
	_title.size = Vector2(532, 30)
	_title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(_title)
	var scroll := ScrollContainer.new()
	scroll.position = Vector2(10, 44)
	scroll.size = Vector2(540, 232)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	_rows = VBoxContainer.new()
	_rows.custom_minimum_size = Vector2(528, 0)
	_rows.add_theme_constant_override("separation", 3)
	scroll.add_child(_rows)
	_done = Button.new()
	_done.position = Vector2(200, 282)
	_done.size = Vector2(160, 26)
	UIFont.style(_done, 11)
	_done.pressed.connect(func():
		visible = false
		on_done.call())
	add_child(_done)


## Shows the panel. [param night] = wholesale mode instead of shelving.
func open(state: RunState, night: bool, done_text: String, done: Callable) -> void:
	run = state
	_night = night
	on_done = done
	_done.text = done_text
	visible = true
	_rebuild()


func _rebuild() -> void:
	for c in _rows.get_children():
		_rows.remove_child(c)
		c.queue_free()
	if _night:
		_title.text = "🌙 창고 — 도매상은 시세의 %d%%에 바로 사 간다.   소지금 %dG" % [int(MarketData.WHOLESALE * 100), run.money]
	else:
		_title.text = "🏺 진열대 정리 — 진열 %d/%d칸. 가격표가 손님과의 흥정 시작가가 된다.\n%s" % [
			run.shelf().size(), run.shelf_slots(), run.price_line()]
	if run.inventory.is_empty():
		_rows.add_child(_text("창고가 비어 있다. 오늘 손님에게서 물건을 사들이자.", 10, Color(0.7, 0.6, 0.5)))
		return
	for e in run.inventory:
		_rows.add_child(_row(e))


func _row(e: Dictionary) -> Control:
	var item: Dictionary = e["item"]
	var fake: bool = item["truth"] == ItemGenerator.Truth.FAKE
	var cursed: bool = item["truth"] == ItemGenerator.Truth.CURSED
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	var label := ""
	var color := Color(0.93, 0.88, 0.8)
	if fake:
		label = "  (가짜: %s)" % ItemData.MATERIALS[item["true_material"]]["name"]
		color = Color(1, 0.55, 0.5)
	elif cursed:
		label = "  (%s)" % CurseData.CURSES[item["curse"]]["name"]
		color = Color(0.78, 0.55, 1.0)
	var name := _text(RunState.item_name(item, true) + label, 9, color)
	name.custom_minimum_size = Vector2(176, 0)
	name.clip_text = true
	row.add_child(name)
	var info := _text("산 값 %d · 시세 %dG" % [e["paid"], run.market_value(item)], 9, Color(0.75, 0.68, 0.6))
	info.custom_minimum_size = Vector2(108, 0)
	row.add_child(info)
	if cursed:
		row.add_child(_button("정화 -%dG" % CurseData.PURIFY_COST, func():
			run.purify(e)
			_rebuild()))
	if _night:
		var gold := int(round(run.market_value(item) * MarketData.WHOLESALE))
		row.add_child(_button("도매로 팔기 +%dG" % gold, func():
			run.wholesale(e)
			_rebuild()))
		return row
	row.add_child(_button("-10", func():
		e["tag"] = maxi(5, e["tag"] - 10)
		_rebuild()))
	var tag := _text("%dG" % e["tag"], 10, Color(0.95, 0.78, 0.4))
	tag.custom_minimum_size = Vector2(40, 0)
	tag.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(tag)
	row.add_child(_button("+10", func():
		e["tag"] += 10
		_rebuild()))
	row.add_child(_button("✔ 진열" if e["shelved"] else "진열", func():
		run.set_shelved(e, not e["shelved"])
		_rebuild()))
	return row


func _text(t: String, font_size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = t
	UIFont.style(l, font_size)
	l.add_theme_color_override("font_color", color)
	return l


func _button(t: String, action: Callable) -> Button:
	var b := Button.new()
	b.text = t
	b.custom_minimum_size = Vector2(0, 20)
	b.pressed.connect(action)
	return b
