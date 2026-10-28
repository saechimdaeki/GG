class_name UiTheme
## Shared dark-purple theme for buttons and panels (toolbar, shop, rewards, end screen).

static var _theme: Theme


static func get_theme() -> Theme:
	if _theme != null:
		return _theme
	_theme = Theme.new()
	_theme.set_stylebox("normal", "Button", _box(Color(0.1, 0.06, 0.15, 0.92), Color(0.45, 0.3, 0.65)))
	_theme.set_stylebox("hover", "Button", _box(Color(0.18, 0.1, 0.26, 0.95), Color(0.85, 0.6, 1.0)))
	_theme.set_stylebox("pressed", "Button", _box(Color(0.3, 0.14, 0.38, 0.95), Color(1.0, 0.8, 0.35), 2))
	_theme.set_stylebox("hover_pressed", "Button", _box(Color(0.3, 0.14, 0.38, 0.95), Color(1.0, 0.8, 0.35), 2))
	_theme.set_stylebox("disabled", "Button", _box(Color(0.06, 0.05, 0.08, 0.85), Color(0.22, 0.2, 0.26)))
	_theme.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	_theme.set_stylebox("panel", "PanelContainer", _box(Color(0.07, 0.04, 0.11, 0.94), Color(0.55, 0.35, 0.8), 1, 8))
	_theme.set_color("font_color", "Button", Color(0.95, 0.9, 1.0))
	_theme.set_color("font_hover_color", "Button", Color(1, 1, 1))
	_theme.set_color("font_pressed_color", "Button", Color(1.0, 0.9, 0.5))
	_theme.set_color("font_disabled_color", "Button", Color(0.45, 0.42, 0.5))
	_theme.set_color("font_outline_color", "Label", Color.BLACK)
	_theme.set_constant("outline_size", "Label", 3)
	return _theme


static func _box(bg: Color, border: Color, bw: int = 1, pad: int = 4) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(bw)
	s.set_corner_radius_all(3)
	s.set_content_margin_all(pad)
	return s
