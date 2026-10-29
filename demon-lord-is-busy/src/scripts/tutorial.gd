class_name Tutorial
extends CanvasLayer
## First-run guide, voiced by the butler imp: a mission card at the top (portrait + one task)
## and a bouncing arrow on the map pointing at what to use. Each step ends when the game state
## shows it was done. Finishing or skipping saves Settings "tutorial_done".

## Tests and demos turn this off.
static var enabled := true

## [task text, Callable(game) -> bool done, Callable(game) -> Vector2 world target (INF = none)]
var _steps: Array = []
var _i: int = 0
var _game: Game
var _box: PanelContainer
var _label: Label
var _count: Label
var _pointer: Control
var _t: float = 0.0


## True when the player hasn't finished (or skipped) the tutorial yet.
static func pending() -> bool:
	if Settings.values.is_empty():
		Settings.load_and_apply()
	return enabled and not bool(Settings.values.get("tutorial_done", false))


func setup(game: Game) -> void:
	_game = game
	var none := func(_g: Game) -> Vector2: return Vector2.INF
	var on_path := func(g: Game) -> Vector2:
		if g.grid.preview_paths.is_empty() or g.grid.preview_paths[0].size() < 3:
			return Vector2.INF
		var p: PackedVector2Array = g.grid.preview_paths[0]
		return g.grid.to_global(p[p.size() / 3])
	var gate := func(g: Game) -> Vector2:
		return Vector2.INF if g.grid.gates.is_empty() else g.grid.cell_center(g.grid.gates[0].cells[0])
	var hero := func(g: Game) -> Vector2:
		var best := Vector2.INF
		for h in g.get_tree().get_nodes_in_group("heroes"):
			var p := (h as Node2D).global_position
			if best == Vector2.INF or p.distance_to(g.lord.global_position) < best.distance_to(g.lord.global_position):
				best = p
		return best
	var core := func(g: Game) -> Vector2: return g.grid.core_position()
	_steps = [
		["빨간 [용사 입구!]에서 나온 용사는 점선을 따라 코어로 와요.\n[2] 가시 함정을 점선 위에 놓으세요.",
			func(g: Game) -> bool: return g.grid.traps.values().has("spike"), on_path],
		["[3] 스프링을 점선 위에! 밟은 용사를 근처 함정·용암·몬스터 쪽으로 날려버려요.",
			func(g: Game) -> bool: return g.grid.traps.values().has("push"), on_path],
		["[7~9] 몬스터를 배치하세요. 죽어도 %d초 뒤 제자리에서 부활해요." % int(Units.MONSTER_RESPAWN_TIME),
			func(g: Game) -> bool: return not g.monster_cells().is_empty(), on_path],
		["반짝이는 [철문]을 클릭하면 닫혀서 용사가 돌아가요. (건설 단계에서만)",
			func(g: Game) -> bool:
				return g.phase != Game.Phase.BUILD or g.grid.gates.is_empty() \
					or g.grid.gates.any(func(x: Dictionary) -> bool: return x.closed), gate],
		["준비됐으면 [Enter] 로 침입 시작!", func(g: Game) -> bool: return g.phase == Game.Phase.RAID, none],
		["용사에게 다가가 [F] 걷어차기! 함정·용암·벽·다른 용사에 처박으면 콤보 + 골드!",
			func(g: Game) -> bool: return g.lord.kick_cd > 0.0, hero],
		["좌클릭 근접 · 우클릭 마법탄. 용사 몸이 빨개지면(공격 예고) [Space] 대시로 피하세요.",
			func(g: Game) -> bool: return g.kills > 0, hero],
		["용사가 코어에 붙으면 계속 때려요. 코어 HP가 0이면 끝! 코어를 지키세요.",
			func(g: Game) -> bool: return g.wave >= 1, core],
	]


func _ready() -> void:
	layer = 4
	process_mode = Node.PROCESS_MODE_ALWAYS
	_pointer = Control.new()
	_pointer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_pointer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_pointer.draw.connect(_draw_pointer)
	add_child(_pointer)
	_box = PanelContainer.new()
	_box.theme = UiTheme.get_theme()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.1, 0.06, 0.12, 0.92)
	sb.border_color = Color(1.0, 0.65, 0.35)
	sb.set_border_width_all(2)
	sb.set_corner_radius_all(4)
	sb.content_margin_left = 6
	sb.content_margin_right = 6
	sb.content_margin_top = 4
	sb.content_margin_bottom = 4
	_box.add_theme_stylebox_override("panel", sb)
	_box.position = Vector2(150, 44)
	_box.custom_minimum_size = Vector2(340, 0)
	add_child(_box)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	_box.add_child(row)
	var face := TextureRect.new()
	face.texture = Sprites.tex("imp")
	face.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	face.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	face.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	face.custom_minimum_size = Vector2(36, 36)
	row.add_child(face)
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 1)
	row.add_child(v)
	var top := HBoxContainer.new()
	v.add_child(top)
	_count = Label.new()
	_count.add_theme_font_size_override("font_size", 8)
	_count.add_theme_color_override("font_color", Color(1.0, 0.65, 0.35))
	_count.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top.add_child(_count)
	var skip := Button.new()
	skip.text = "건너뛰기"
	skip.focus_mode = Control.FOCUS_NONE
	skip.add_theme_font_size_override("font_size", 7)
	skip.pressed.connect(finish)
	top.add_child(skip)
	_label = Label.new()
	_label.add_theme_font_size_override("font_size", 9)
	_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_label.custom_minimum_size.x = 286
	v.add_child(_label)
	_show()


func _process(delta: float) -> void:
	_t += delta
	_pointer.queue_redraw()
	if _game == null or _i >= _steps.size():
		return
	var done: Callable = _steps[_i][1]
	if done.call(_game):
		_i += 1
		Audio.play("coin", -8.0)
		if _i >= _steps.size():
			finish()
		else:
			_show()


func _show() -> void:
	_count.text = "집사 임프의 안내  %d / %d" % [_i + 1, _steps.size()]
	_label.text = _steps[_i][0]
	_box.scale = Vector2(1.04, 1.04)
	_box.create_tween().tween_property(_box, "scale", Vector2.ONE, 0.15)


## Bouncing arrow + pulsing ring over the current step's target on the map.
func _draw_pointer() -> void:
	if _game == null or _i >= _steps.size() or _game.hud_dialog_open():
		return
	var w: Vector2 = (_steps[_i][2] as Callable).call(_game)
	if w == Vector2.INF:
		return
	var p := _pointer.get_viewport().get_canvas_transform() * w
	var bob := 4.0 * sin(_t * 7.0)
	var col := Color(1.0, 0.8, 0.3)
	_pointer.draw_arc(p, 10.0 + 3.0 * sin(_t * 5.0), 0.0, TAU, 24, col, 2.0)
	var tip := p + Vector2(0, -14 + bob)
	_pointer.draw_colored_polygon(PackedVector2Array([tip, tip + Vector2(-8, -12), tip + Vector2(8, -12)]), col)
	_pointer.draw_rect(Rect2(tip + Vector2(-3, -22), Vector2(6, 10)), col)


## Index of the current step (0-based); steps.size() when finished.
func step() -> int:
	return _i


## Ends the tutorial for good (also the 건너뛰기 button).
func finish() -> void:
	_i = _steps.size()
	Settings.set_value("tutorial_done", true)
	queue_free()
