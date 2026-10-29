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


## Shows one card per reward dictionary (id, title, desc, rarity). Border and label follow
## Rewards.RARITY; cards pop in one after another.
func open(choices: Array, title: String = "웨이브 클리어! 보상을 하나 고르세요") -> void:
	_title.text = title
	for child in _row.get_children():
		child.queue_free()
	var best := "common"
	for i in choices.size():
		var r: Dictionary = choices[i]
		var rarity: String = r.get("rarity", "common")
		if rarity == "legendary" or (rarity == "rare" and best == "common"):
			best = rarity
		var col: Color = Rewards.RARITY[rarity].color
		var card := Button.new()
		card.focus_mode = Control.FOCUS_NONE
		card.text = "[%s]\n%s\n\n%s" % [Rewards.RARITY[rarity].label, r.title, r.desc]
		card.custom_minimum_size = Vector2(150, 110)
		card.add_theme_font_size_override("font_size", 9)
		card.add_theme_color_override("font_color", col.lightened(0.45))
		card.add_theme_color_override("font_hover_color", Color.WHITE)
		for state: String in ["normal", "hover", "pressed"]:
			var sb := StyleBoxFlat.new()
			sb.bg_color = col.darkened(0.82 if state == "normal" else 0.65)
			sb.border_color = col if state == "normal" else col.lightened(0.4)
			sb.set_border_width_all(2 if rarity == "common" else 3)
			sb.set_corner_radius_all(4)
			sb.shadow_color = Color(col, 0.5 if rarity != "common" else 0.0)
			sb.shadow_size = 6
			card.add_theme_stylebox_override(state, sb)
		card.pressed.connect(_choose.bind(r.id))
		card.pivot_offset = card.custom_minimum_size / 2.0
		card.scale = Vector2(0.6, 0.6)
		card.modulate.a = 0.0
		_row.add_child(card)
		var tw := card.create_tween()
		tw.tween_interval(0.08 * i)
		tw.tween_property(card, "scale", Vector2.ONE, 0.18).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		tw.parallel().tween_property(card, "modulate:a", 1.0, 0.12)
	_root.visible = true
	if best != "common":
		Audio.play("card_rare", -4.0, 0.0)


func _choose(id: String) -> void:
	Audio.play("click")
	_root.visible = false
	picked.emit(id)
