## The game's UI look, built in code: dark worn panels with a brass edge, cream text
## with a black outline (readable over the dark, noisy 3D view). main.gd merges it into
## the default theme, so every Control uses it.

const CREAM := Color("#f1e4c8")
const BRASS := Color("#c9a06a")
const INK := Color("#140f12")


static func build() -> Theme:
	var t := Theme.new()
	t.default_font_size = 17
	for type in ["Label", "Button", "LineEdit"]:
		t.set_color("font_color", type, CREAM)
		t.set_color("font_outline_color", type, INK)
		t.set_constant("outline_size", type, 5)
	t.set_color("font_hover_color", "Button", Color.WHITE)
	t.set_color("font_disabled_color", "Button", Color(CREAM, 0.35))
	t.set_color("font_placeholder_color", "LineEdit", Color(CREAM, 0.45))

	t.set_stylebox("panel", "PanelContainer", _box(Color(INK, 0.88), BRASS, 2, 14))
	t.set_stylebox("normal", "Button", _box(Color("#2a1f22"), Color(BRASS, 0.5), 1, 8))
	t.set_stylebox("hover", "Button", _box(Color("#3d2b2b"), BRASS, 2, 8))
	t.set_stylebox("pressed", "Button", _box(Color("#4f3426"), BRASS, 2, 8))
	t.set_stylebox("disabled", "Button", _box(Color("#1d1719"), Color(BRASS, 0.2), 1, 8))
	t.set_stylebox("focus", "Button", _box(Color(0, 0, 0, 0), CREAM, 1, 8))
	t.set_stylebox("normal", "LineEdit", _box(Color("#120d10"), Color(BRASS, 0.5), 1, 8))
	t.set_stylebox("focus", "LineEdit", _box(Color(0, 0, 0, 0), BRASS, 2, 8))
	t.set_constant("separation", "VBoxContainer", 8)
	return t


static func _box(bg: Color, border: Color, width: int, pad: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(width)
	s.set_corner_radius_all(3)
	s.set_content_margin_all(pad)
	s.shadow_color = Color(0, 0, 0, 0.5)
	s.shadow_size = 6
	return s
