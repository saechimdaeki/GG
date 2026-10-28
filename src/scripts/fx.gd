class_name Fx
## One-shot visual effects: particle bursts, floating damage numbers, afterimages, lights.

static var _light_tex: GradientTexture2D


## Spray of square particles at pos (hits, deaths).
static func burst(parent: Node, pos: Vector2, color: Color, amount: int = 8, speed: float = 90.0) -> void:
	var p := CPUParticles2D.new()
	p.one_shot = true
	p.emitting = true
	p.amount = amount
	p.lifetime = 0.35
	p.explosiveness = 1.0
	p.spread = 180.0
	p.initial_velocity_min = speed * 0.4
	p.initial_velocity_max = speed
	p.damping_min = 120.0
	p.damping_max = 200.0
	p.scale_amount_min = 1.5
	p.scale_amount_max = 3.0
	p.color = color
	p.global_position = pos
	p.finished.connect(p.queue_free)
	parent.add_child(p)


## Number that pops up and fades.
static func damage_number(parent: Node, pos: Vector2, amount: int, color: Color) -> void:
	text(parent, pos, str(amount), color)


## Short word that pops up and fades (MISS, LV UP, ...).
static func text(parent: Node, pos: Vector2, content: String, color: Color) -> void:
	var label := Label.new()
	label.text = content
	label.add_theme_font_size_override("font_size", 9)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", Color.BLACK)
	label.add_theme_constant_override("outline_size", 3)
	label.position = pos + Vector2(randf_range(-6, 2), -16)
	label.z_index = 20
	parent.add_child(label)
	var tw := label.create_tween()
	tw.tween_property(label, "position:y", label.position.y - 12.0, 0.45).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(label, "modulate:a", 0.0, 0.45).set_delay(0.15)
	tw.tween_callback(label.queue_free)


## Fading copy of a circle body (dash trail).
static func afterimage(parent: Node, pos: Vector2, radius: float, color: Color) -> void:
	var ghost := Polygon2D.new()
	var pts := PackedVector2Array()
	for i in 12:
		pts.append(Vector2.from_angle(TAU * i / 12.0) * radius)
	ghost.polygon = pts
	ghost.color = color
	ghost.global_position = pos
	parent.add_child(ghost)
	var tw := ghost.create_tween()
	tw.tween_property(ghost, "modulate:a", 0.0, 0.25)
	tw.tween_callback(ghost.queue_free)


## Expanding ring that fades (skill area indicator).
static func ring(parent: Node, pos: Vector2, radius: float, color: Color) -> void:
	var line := Line2D.new()
	for i in 33:
		line.add_point(Vector2.from_angle(TAU * i / 32.0))
	line.width = 0.08
	line.default_color = color
	line.global_position = pos
	line.scale = Vector2.ONE * radius * 0.3
	line.material = additive()
	parent.add_child(line)
	var tw := line.create_tween()
	tw.tween_property(line, "scale", Vector2.ONE * radius, 0.25).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(line, "modulate:a", 0.0, 0.35)
	tw.tween_callback(line.queue_free)


## Soft round light; lights only show against the scene's CanvasModulate darkness.
static func light(color: Color, energy: float = 1.0, size: float = 1.0) -> PointLight2D:
	if _light_tex == null:
		_light_tex = GradientTexture2D.new()
		_light_tex.width = 128
		_light_tex.height = 128
		_light_tex.fill = GradientTexture2D.FILL_RADIAL
		_light_tex.fill_from = Vector2(0.5, 0.5)
		_light_tex.fill_to = Vector2(0.5, 0.0)
		var g := Gradient.new()
		g.set_color(0, Color.WHITE)
		g.set_color(1, Color(1, 1, 1, 0))
		_light_tex.gradient = g
	var l := PointLight2D.new()
	l.texture = _light_tex
	l.color = color
	l.energy = energy
	l.texture_scale = size
	return l


## Additive blend material so overlapping glows brighten.
static func additive() -> CanvasItemMaterial:
	var m := CanvasItemMaterial.new()
	m.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	return m
