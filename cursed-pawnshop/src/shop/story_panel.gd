class_name StoryPanel
extends Panel
## Plays a list of story scenes (StoryData.BEATS entries): portrait, text, choices,
## then the NPC's reply and what changed. Calls the done callback after the last one.

const PORTRAIT_PATHS := ["res://assets/sprites/portraits/%s_neutral.png", "res://assets/sprites/portraits/%s.png"]

var run: RunState
var _beats: Array = []
var _done: Callable
var _portrait: TextureRect
var _fallback: ColorRect
var _initial: Label
var _name: Label
var _text: Label
var _choices: VBoxContainer


func _init() -> void:
	position = Vector2(30, 24)
	size = Vector2(580, 312)
	_fallback = ColorRect.new()
	_fallback.position = Vector2(14, 40)
	_fallback.size = Vector2(150, 150)
	add_child(_fallback)
	_initial = Label.new()
	_initial.position = _fallback.position
	_initial.size = _fallback.size
	_initial.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_initial.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_initial.add_theme_font_size_override("font_size", 48)
	add_child(_initial)
	_portrait = TextureRect.new()
	_portrait.position = Vector2(14, 40)
	_portrait.size = Vector2(150, 150)
	_portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	add_child(_portrait)
	_name = Label.new()
	_name.position = Vector2(14, 10)
	_name.add_theme_font_size_override("font_size", 12)
	_name.add_theme_color_override("font_color", Color(0.95, 0.78, 0.4))
	add_child(_name)
	_text = Label.new()
	_text.position = Vector2(178, 40)
	_text.size = Vector2(388, 150)
	_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(_text)
	_choices = VBoxContainer.new()
	_choices.position = Vector2(178, 200)
	_choices.size = Vector2(388, 100)
	_choices.add_theme_constant_override("separation", 4)
	add_child(_choices)


## Plays [param beats] in order, then calls [param done].
func play(state: RunState, beats: Array, done: Callable) -> void:
	run = state
	_beats = beats.duplicate()
	_done = done
	visible = true
	_next()


func _next() -> void:
	if _beats.is_empty():
		visible = false
		_done.call()
		return
	var b: Dictionary = _beats.pop_front()
	Audio.play("page")
	_show_speaker(b)
	_text.text = b["text"]
	_text.visible_ratio = 0.0
	create_tween().tween_property(_text, "visible_ratio", 1.0, clampf(b["text"].length() * 0.012, 0.3, 2.0))
	_clear()
	for c in b["choices"]:
		var ok := run.check(c.get("if", {}))
		var btn := _button(c["label"] + ("" if ok else "  (불가)"))
		btn.disabled = not ok
		btn.pressed.connect(_choose.bind(c))


func _choose(c: Dictionary) -> void:
	if not c.has("reply") and not c.has("effects"):
		_next()
		return
	var summary := run.apply_effects(c.get("effects", {}))
	_text.text = c.get("reply", "...") + ("\n\n" + summary if summary != "" else "")
	_text.visible_ratio = 1.0
	_clear()
	_button("계속").pressed.connect(_next)


func _show_speaker(b: Dictionary) -> void:
	var id: String = b["npc"]
	var npc: Dictionary = {}
	if b.get("species", false):
		npc = {"name": CustomerData.SPECIES[id]["name"], "title": "손님", "color": CustomerData.SPECIES[id]["skin"]}
	else:
		npc = StoryData.NPCS[id]
	_name.text = "%s  ·  %s" % [npc["name"], npc["title"]]
	_portrait.texture = null
	for p in PORTRAIT_PATHS:
		if ResourceLoader.exists(p % id):
			_portrait.texture = load(p % id)
			break
	_fallback.visible = _portrait.texture == null
	_initial.visible = _fallback.visible
	# no portrait yet (art batch 2): a tinted silhouette with "?" instead of a letter
	_fallback.color = (npc["color"] as Color).darkened(0.7)
	_initial.text = "?"


func _clear() -> void:
	for c in _choices.get_children():
		_choices.remove_child(c)
		c.queue_free()


func _button(t: String) -> Button:
	var b := Button.new()
	b.text = t
	b.custom_minimum_size = Vector2(0, 22)
	_choices.add_child(b)
	return b
