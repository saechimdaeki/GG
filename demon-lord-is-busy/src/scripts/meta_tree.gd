class_name MetaTree
extends VBoxContainer
## Permanent-upgrade skill tree: one column per Meta.BRANCHES, nodes top to bottom in
## prerequisite order. Buying spends souls from `data` (Meta.load_data()) and saves.
## Used on the title screen and the run-end screen.

signal changed

var data: Dictionary = {}


## Rebuilds the buttons from data.
func refresh() -> void:
	for c in get_children():
		c.queue_free()
	add_theme_constant_override("separation", 4)
	var head := Label.new()
	head.text = "영구 강화 — 보유 영혼석 %d" % int(data.souls)
	head.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	head.add_theme_font_size_override("font_size", 11)
	head.add_theme_color_override("font_color", Color(0.8, 0.65, 1.0))
	add_child(head)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	add_child(row)
	for branch: String in Meta.BRANCHES:
		var col := VBoxContainer.new()
		col.add_theme_constant_override("separation", 3)
		row.add_child(col)
		var title := Label.new()
		title.text = Meta.BRANCHES[branch]
		title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		title.add_theme_font_size_override("font_size", 10)
		title.add_theme_color_override("font_color", Color(1.0, 0.85, 0.4))
		col.add_child(title)
		for id: String in Meta.UPGRADES:
			if Meta.UPGRADES[id].branch == branch:
				col.add_child(_node(id))


func _node(id: String) -> Button:
	var up: Dictionary = Meta.UPGRADES[id]
	var lv := int(data.levels.get(id, 0))
	var p := Meta.price(data, id)
	var open := Meta.unlocked(data, id)
	var desc: String = up.desc % maxi(Meta.bonus(data, id), int(up.value)) if "%d" in up.desc else up.desc
	var cost := "MAX" if p < 0 else ("%d영혼" % p if open else "잠김")
	var b := Button.new()
	b.text = "%s  %d/%d\n%s · %s" % [up.name, lv, up.max, desc, cost]
	b.custom_minimum_size = Vector2(150, 34)
	b.add_theme_font_size_override("font_size", 8)
	b.focus_mode = Control.FOCUS_NONE
	b.disabled = p < 0 or not open or int(data.souls) < p
	if lv > 0:
		b.add_theme_color_override("font_color", Color(0.75, 1.0, 0.75))
		b.add_theme_color_override("font_disabled_color", Color(0.6, 0.85, 0.6))
	b.pressed.connect(func() -> void:
		Audio.play("coin" if Meta.buy(data, id) else "error")
		refresh()
		changed.emit())
	return b
