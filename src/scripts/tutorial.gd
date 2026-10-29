class_name Tutorial
extends CanvasLayer
## First-run guide: a small non-blocking hint box under the wave title that walks through
## one action at a time and moves on when the game state shows it was done.
## Finishing or skipping saves Settings "tutorial_done", so it never shows again.

## Tests and demos turn this off.
static var enabled := true

## [hint text, Callable(game) -> bool that is true once the step is done]
var _steps: Array = []
var _i: int = 0
var _game: Game
var _box: PanelContainer
var _label: Label
var _count: Label


## True when the player hasn't finished (or skipped) the tutorial yet.
static func pending() -> bool:
	if Settings.values.is_empty():
		Settings.load_and_apply()
	return enabled and not bool(Settings.values.get("tutorial_done", false))


func setup(game: Game) -> void:
	_game = game
	_steps = [
		["점선 = 용사가 올 길. [2] 가시 함정을 점선 위에 놓아보세요", func(g: Game) -> bool: return not g.grid.traps.is_empty()],
		["[4~6] 몬스터를 배치하세요. 몬스터가 용사를 붙잡아 줍니다", func(g: Game) -> bool: return not g.monster_cells().is_empty()],
		["복도의 철문을 클릭하면 닫힙니다 — 용사가 돌아가요 (다시 클릭하면 열림)", func(g: Game) -> bool:
			return g.grid.gates.is_empty() or g.grid.gates.any(func(x: Dictionary) -> bool: return x.closed)],
		["준비됐으면 [Enter] 로 침입 시작!", func(g: Game) -> bool: return g.phase == Game.Phase.RAID],
		["좌클릭 공격 · 우클릭 마법탄. 빨간 원(공격 예고)은 [Space] 대시로 피하세요", func(g: Game) -> bool: return g.kills > 0],
		["용사 발밑 게이지가 금색이면 곧 스킬! 웨이브를 막아내세요", func(g: Game) -> bool: return g.wave >= 1],
		["[Tab] 상점 · [9] 임프 광산은 웨이브마다 골드. 용사가 털면 0원!", func(g: Game) -> bool:
			return g.wave >= 1 and g.phase == Game.Phase.RAID],
	]


func _ready() -> void:
	layer = 4
	process_mode = Node.PROCESS_MODE_ALWAYS
	_box = PanelContainer.new()
	_box.theme = UiTheme.get_theme()
	_box.position = Vector2(150, 40)
	_box.custom_minimum_size = Vector2(340, 0)
	add_child(_box)
	var v := VBoxContainer.new()
	_box.add_child(v)
	var top := HBoxContainer.new()
	v.add_child(top)
	_count = Label.new()
	_count.add_theme_font_size_override("font_size", 8)
	_count.add_theme_color_override("font_color", Color(1.0, 0.8, 0.35))
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
	_label.custom_minimum_size.x = 330
	v.add_child(_label)
	_show()


func _process(_delta: float) -> void:
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
	_count.text = "튜토리얼 %d / %d" % [_i + 1, _steps.size()]
	_label.text = _steps[_i][0]


## Index of the current step (0-based); steps.size() when finished.
func step() -> int:
	return _i


## Ends the tutorial for good (also the 건너뛰기 button).
func finish() -> void:
	_i = _steps.size()
	Settings.set_value("tutorial_done", true)
	queue_free()
