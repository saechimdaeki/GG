class_name StatusPanel
extends Control
## In-game HUD drawn as bars and slots: HP / magic bolts / XP (top-left), wave (top-center),
## gold / core (top-right), weapon + Q/E skills (bottom-right, raid only). Fed by Hud.set_status().

const BG := Color(0.07, 0.04, 0.11, 0.82)
const BORDER := Color(0.55, 0.35, 0.8, 0.9)
const HP_COL := Color(0.9, 0.2, 0.25)
const BOLT_COL := Color(0.75, 0.5, 1.0)
const XP_COL := Color(1.0, 0.8, 0.3)
const CORE_COL := Color(1.0, 0.35, 0.55)
const GOLD_COL := Color(1.0, 0.82, 0.25)

## See Game._status() for the keys.
var state: Dictionary = {}


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## Draws text with a black outline. align_w > 0 centers it in [pos.x, pos.x + align_w].
func _text(pos: Vector2, s: String, size: int, col: Color, align_w: float = -1.0) -> void:
	var font := ThemeDB.fallback_font
	var al := HORIZONTAL_ALIGNMENT_CENTER if align_w > 0.0 else HORIZONTAL_ALIGNMENT_LEFT
	draw_string_outline(font, pos, s, al, align_w, size, 3, Color.BLACK)
	draw_string(font, pos, s, al, align_w, size, col)


func _box(r: Rect2, border: Color = BORDER) -> void:
	draw_rect(r, BG)
	draw_rect(r, border, false, 1.0)


func _bar(r: Rect2, ratio: float, col: Color, label: String = "") -> void:
	draw_rect(r, Color(0, 0, 0, 0.6))
	draw_rect(Rect2(r.position, Vector2(r.size.x * clampf(ratio, 0.0, 1.0), r.size.y)), col)
	draw_rect(Rect2(r.position, Vector2(r.size.x * clampf(ratio, 0.0, 1.0), 2)), col.lightened(0.35))
	draw_rect(r, Color(0, 0, 0, 0.9), false, 1.0)
	if label != "":
		_text(r.position + Vector2(0, r.size.y - 1), label, 8, Color.WHITE, r.size.x)


func _process(_delta: float) -> void:
	queue_redraw()


func _draw() -> void:
	if state.is_empty():
		return
	var vp := get_viewport_rect().size
	var w := vp.x
	# Top-left: lord.
	_box(Rect2(6, 6, 150, 44))
	_text(Vector2(11, 17), "HP", 8, HP_COL.lightened(0.3))
	_bar(Rect2(28, 9, 122, 10), float(state.hp) / state.hp_max, HP_COL, "%d / %d" % [state.hp, state.hp_max])
	_text(Vector2(11, 30), "탄", 8, BOLT_COL)
	for i in int(state.bolts_max):
		var c := Vector2(33 + i * 10, 26)
		if i < state.bolts:
			draw_circle(c, 3.5, BOLT_COL * 1.6)
		elif i == state.bolts:
			draw_circle(c, 3.5 * float(state.bolt_charge), BOLT_COL.darkened(0.3))
		draw_arc(c, 3.5, 0.0, TAU, 12, BOLT_COL, 1.0)
	_text(Vector2(11, 45), "Lv%d" % state.level, 8, XP_COL)
	_bar(Rect2(34, 39, 116, 6), float(state.xp) / state.xp_need, XP_COL)
	# Top-center: wave.
	_text(Vector2(w / 2.0 - 80, 18), "WAVE  %d / %d" % [state.wave, state.waves], 13, Color(1, 0.92, 0.8), 160)
	_text(Vector2(w / 2.0 - 80, 30), state.phase_text, 8, Color(0.8, 0.75, 0.9), 160)
	# Top-right: gold + core.
	_box(Rect2(w - 136, 6, 130, 32))
	draw_circle(Vector2(w - 126, 15), 4.0, GOLD_COL * 1.3)
	draw_circle(Vector2(w - 126, 15), 2.0, GOLD_COL.darkened(0.4))
	_text(Vector2(w - 118, 19), str(state.gold), 10, GOLD_COL)
	_text(Vector2(w - 84, 18), "골드", 7, Color(0.7, 0.65, 0.75))
	_text(Vector2(w - 131, 34), "코어", 7, CORE_COL.lightened(0.3))
	_bar(Rect2(w - 110, 26, 98, 9), float(state.core) / state.core_max, CORE_COL, "%d / %d" % [state.core, state.core_max])
	# Bottom-right: weapon + skills (raid only; the build toolbar owns the bottom row then).
	if not state.raid:
		return
	var y := vp.y - 40
	var x: float = w - 6 - (1 + (state.skills as Array).size()) * 50
	_slot(Rect2(x, y, 46, 34), "무기", state.weapon, 0.0, Color(0.85, 0.85, 0.9))
	for i in state.skills.size():
		var s: Dictionary = state.skills[i]
		_slot(Rect2(x + 50 * (i + 1), y, 46, 34), s.key, s.name, s.cd_ratio, s.color, s.cd)


func _slot(r: Rect2, key: String, label: String, cd_ratio: float, col: Color, cd: float = 0.0) -> void:
	var empty := label == ""
	_box(r, col.darkened(0.2) if not empty else Color(0.3, 0.3, 0.35))
	_text(r.position + Vector2(3, 9), key, 8, col if not empty else Color(0.5, 0.5, 0.55))
	_text(r.position + Vector2(0, 26), label if not empty else "-", 8, Color.WHITE, r.size.x)
	if cd_ratio > 0.0:
		draw_rect(Rect2(r.position, Vector2(r.size.x, r.size.y * cd_ratio)), Color(0, 0, 0, 0.65))
		_text(r.position + Vector2(0, 22), "%.0f" % ceilf(cd), 12, Color(1, 1, 1, 0.9), r.size.x)
