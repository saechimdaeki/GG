class_name CutscenePlayer
extends Control
## Plays a CutsceneData sequence: full-screen picture with a slow push-in,
## letterbox bars, a typed caption, click / space to advance, ESC to skip.
## Calls the done callback at the end (or on skip).

var _frames: Array = []
var _done: Callable
var _image: TextureRect
var _caption: Label
var _speaker: Label
var _hint: Label
var _fade: ColorRect
var _t := 0.0
var _busy := false
var _typing: Tween


func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	size = Vector2(640, 360)
	mouse_filter = Control.MOUSE_FILTER_STOP
	visible = false
	var black := ColorRect.new()
	black.color = Color.BLACK
	black.size = size
	add_child(black)
	_image = TextureRect.new()
	_image.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_image.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	_image.size = size
	_image.pivot_offset = size / 2
	add_child(_image)
	for y in [0.0, 318.0]:  # letterbox bars
		var bar := ColorRect.new()
		bar.color = Color(0, 0, 0, 0.92)
		bar.position = Vector2(0, y)
		bar.size = Vector2(640, 42 if y == 0.0 else 42)
		add_child(bar)
	var shade := ColorRect.new()  # darkens the lower third under the caption
	shade.color = Color(0, 0, 0, 0.55)
	shade.position = Vector2(0, 250)
	shade.size = Vector2(640, 68)
	add_child(shade)
	_speaker = Label.new()
	_speaker.position = Vector2(40, 254)
	UIFont.style(_speaker, 12)
	_speaker.add_theme_color_override("font_color", Color(0.95, 0.78, 0.4))
	add_child(_speaker)
	_caption = Label.new()
	_caption.position = Vector2(40, 268)
	_caption.size = Vector2(560, 60)
	_caption.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	UIFont.style(_caption, 12)
	add_child(_caption)
	_hint = Label.new()
	_hint.position = Vector2(470, 336)
	_hint.text = "클릭: 다음 · ESC: 건너뛰기"
	UIFont.style(_hint, 9)
	_hint.add_theme_color_override("font_color", Color(0.6, 0.55, 0.55))
	add_child(_hint)
	_fade = ColorRect.new()
	_fade.color = Color.BLACK
	_fade.size = size
	_fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_fade)


## Plays CutsceneData.SCENES[id], then calls [param done].
func play(id: String, done: Callable) -> void:
	_frames = CutsceneData.SCENES.get(id, []).duplicate()
	_done = done
	if _frames.is_empty():
		done.call()
		return
	visible = true
	_fade.modulate.a = 1.0
	_show_next()


func _show_next() -> void:
	if _frames.is_empty():
		_finish()
		return
	var f: Dictionary = _frames.pop_front()
	_busy = true
	var tw := create_tween()
	tw.tween_property(_fade, "modulate:a", 1.0, 0.35)
	tw.tween_callback(func():
		_image.texture = _picture(f)
		_image.scale = Vector2.ONE
		_speaker.text = f.get("speaker", "")
		_caption.text = f["text"]
		_caption.visible_ratio = 0.0
		Audio.play("page", -6.0))
	tw.tween_property(_fade, "modulate:a", 0.0, 0.5)
	tw.tween_callback(func():
		_busy = false
		_typing = create_tween()
		_typing.tween_property(_caption, "visible_ratio", 1.0, clampf(f["text"].length() * 0.03, 0.5, 3.0)))
	# slow push-in for the whole time the frame is up
	create_tween().tween_property(_image, "scale", Vector2.ONE * 1.08, 9.0)


func _picture(f: Dictionary) -> Texture2D:
	for name in [f["image"], f.get("fallback", "")]:
		var path := "res://assets/sprites/scenes/%s.png" % name
		if name != "" and ResourceLoader.exists(path):
			return load(path)
	return null


func _finish() -> void:
	var tw := create_tween()
	tw.tween_property(_fade, "modulate:a", 1.0, 0.4)
	tw.tween_callback(func():
		visible = false
		_done.call())


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		_advance()


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("ui_cancel"):
		_frames.clear()
		_finish()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("ui_accept"):
		_advance()
		get_viewport().set_input_as_handled()


## First click finishes typing the caption, the next one moves on.
func _advance() -> void:
	if _busy:
		return
	if _caption.visible_ratio < 1.0:
		if _typing:
			_typing.kill()
		_caption.visible_ratio = 1.0
		return
	_show_next()


func _process(delta: float) -> void:
	_t += delta
	_hint.modulate.a = 0.6 + 0.4 * sin(_t * 3.0)
