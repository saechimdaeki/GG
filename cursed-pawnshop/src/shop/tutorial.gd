class_name Tutorial
extends Control
## Day-one walkthrough with Pip. Each step dims everything except one area, says
## what to do, and waits for the player to actually do it (ShopView calls notify()).
## Steps with no event wait for the "다음" button. Done once, remembered in settings.

const SETTINGS_PATH := "user://settings.cfg"
const PIP := "res://assets/sprites/portraits/pip.png"

## rect = highlighted area, wait = event that completes the step ("" = button).
const STEPS := [
	{"rect": Rect2(316, 12, 316, 176), "wait": "loupe", "text": "손님이 물건을 올려놨어! 오른쪽 감정창 위에 마우스를 올려봐. 돋보기가 따라올 거야."},
	{"rect": Rect2(316, 12, 316, 176), "wait": "mark", "text": "물건 어딘가에 아주 작은 각인(문양)이 숨어 있어. 돋보기로 훑다가 찾으면 클릭!"},
	{"rect": Rect2(316, 196, 316, 50), "wait": "", "text": "찾았다! 아래 '진품 기준'을 봐. 금은 태양, 은은 달, 청동은 불꽃 각인이야. 손님이 말한 재질이랑 각인이 맞아?"},
	{"rect": Rect2(316, 196, 100, 26), "wait": "weigh", "text": "이번엔 저울! 왼쪽 아래 '저울에 올리기'를 눌러봐."},
	{"rect": Rect2(316, 196, 316, 50), "wait": "", "text": "기준 무게랑 비교해. ±2% 넘게 다르면 수상한 거야. 각인이나 무게가 이상하면 가짜!"},
	{"rect": Rect2(366, 250, 266, 104), "wait": "candle", "text": "하나 더. 저주는 무게로도 각인으로도 안 보여. '촛불 비추기'를 눌러봐."},
	{"rect": Rect2(366, 250, 266, 104), "wait": "", "text": "수상하면 '추궁하기'로 따져서 값을 깎아. 틀리게 따지면 손님이 화내니까 조심! 괜찮으면 '가격 얘기하죠'로 흥정해. 손님 호가보다 낮게 불러봐."},
	{"rect": Rect2(8, 196, 300, 50), "wait": "", "text": "산 물건은 창고로 가고, 매일 아침 진열대에 올려서 팔 수 있어. 매주 바알한테 빚 갚는 것도 잊지 마! 그럼, 행운을 빌어 👻"},
]

var _step := -1
var _bubble: Panel
var _text: Label
var _next: Button
var _rect := Rect2()


## True once the player finished or skipped the tutorial.
static func done() -> bool:
	var cfg := ConfigFile.new()
	cfg.load(SETTINGS_PATH)
	return cfg.get_value("tutorial", "done", false)


static func _mark_done() -> void:
	var cfg := ConfigFile.new()
	cfg.load(SETTINGS_PATH)
	cfg.set_value("tutorial", "done", true)
	cfg.save(SETTINGS_PATH)


func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	size = Vector2(640, 360)
	mouse_filter = Control.MOUSE_FILTER_IGNORE  # clicks reach the shop underneath
	visible = false
	_bubble = Panel.new()
	_bubble.size = Vector2(300, 70)
	add_child(_bubble)
	var pip := TextureRect.new()
	if ResourceLoader.exists(PIP):
		pip.texture = load(PIP)
	pip.position = Vector2(6, 6)
	pip.size = Vector2(52, 58)
	pip.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	pip.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_bubble.add_child(pip)
	_text = Label.new()
	_text.position = Vector2(62, 6)
	_text.size = Vector2(232, 44)
	_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	UIFont.style(_text, 9)
	_bubble.add_child(_text)
	_next = Button.new()
	_next.text = "다음 ▶"
	_next.position = Vector2(232, 48)
	_next.size = Vector2(60, 18)
	_next.pressed.connect(func(): _advance())
	_bubble.add_child(_next)
	var skip := Button.new()
	skip.text = "건너뛰기"
	skip.position = Vector2(166, 48)
	skip.size = Vector2(62, 18)
	skip.pressed.connect(func(): _finish())
	_bubble.add_child(skip)


func start() -> void:
	visible = true
	_step = -1
	_advance()


## Called by the shop when the player does something ("loupe", "mark", "weigh", "candle").
func notify(event: String) -> void:
	if visible and _step >= 0 and _step < STEPS.size() and STEPS[_step]["wait"] == event:
		Audio.play("coin", -8.0)
		_advance()


func _advance() -> void:
	_step += 1
	if _step >= STEPS.size():
		_finish()
		return
	var s: Dictionary = STEPS[_step]
	_rect = s["rect"]
	_text.text = s["text"]
	_next.visible = s["wait"] == ""
	# put the bubble where it doesn't cover the highlighted area
	_bubble.position = Vector2(8, 12) if _rect.position.x > 300 and _rect.position.y < 200 else Vector2(330, 12 if _rect.position.y > 150 else 260)
	if _rect.position.x < 300 and _rect.position.y > 150:
		_bubble.position = Vector2(330, 12)
	queue_redraw()


func _finish() -> void:
	visible = false
	_mark_done()


func _process(_d: float) -> void:
	if visible:
		queue_redraw()


func _draw() -> void:
	# dim everything except the highlighted rect, then a pulsing gold frame around it
	var dim := Color(0, 0, 0, 0.55)
	var r := _rect
	draw_rect(Rect2(0, 0, 640, r.position.y), dim)
	draw_rect(Rect2(0, r.end.y, 640, 360 - r.end.y), dim)
	draw_rect(Rect2(0, r.position.y, r.position.x, r.size.y), dim)
	draw_rect(Rect2(r.end.x, r.position.y, 640 - r.end.x, r.size.y), dim)
	var pulse := 0.6 + 0.4 * sin(Time.get_ticks_msec() / 200.0)
	draw_rect(r.grow(2), Color(1, 0.85, 0.4, pulse), false, 2)
