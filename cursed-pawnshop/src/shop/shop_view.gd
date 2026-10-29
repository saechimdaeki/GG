class_name ShopView
extends Control
## The pawnshop: morning report, monsters at the counter, the appraisal window
## with a loupe, questions / accusations / price talk, and the night report.
## Debug seed: `godot -- --seed=123`.

const MAT_RECT := Rect2(316, 12, 316, 176)
const ITEM_CENTER := Vector2(474, 106)
const ITEM_UNIT := 58.0
const LOUPE_SIZE := 120.0
const LOUPE_ZOOM := 3.0
const CUSTOMER_AT := Vector2(150, 112)
const COUNTER_Y := 196.0
const CANDLE_AT := Vector2(44, 192)
const GOLD := Color(0.95, 0.78, 0.4)
const INK := Color(0.93, 0.88, 0.8)
## Optional painted back wall (art/raw/shop_bg.png, see art/PROMPTS.md).
const BACKGROUND := "res://assets/sprites/shop_bg.png"

enum Phase { MORNING, ENTER, TALK, HAGGLE, SELL, RESULT, NIGHT }

var _run: RunState
var _phase := Phase.MORNING
var _visit := {}
var _offer := 0
var _seen := {}
var _confronted := {}
var _notes: Array[String] = []
var _cust_x := -120.0
var _t := 0.0
var _ping := Vector2.INF
var _ping_t := 0.0
var _bought := false
var _background: Texture2D
var _ask_price := 0
var _sale_price := 0
var _caught := false
var _shelf_panel: ShelfPanel
var _upgrade_panel: UpgradePanel

var _hud: Label
var _speaker: Label
var _text: Label
var _choices: GridContainer
var _offer_label: Label
var _notes_label: Label
var _weight_label: Label
var _scale_button: Button
var _loupe: Loupe
var _overlay: Panel
var _overlay_text: Label
var _overlay_button: Button


func _ready() -> void:
	# ponytail: load once here; a load() inside _draw renders the texture white
	if ResourceLoader.exists(BACKGROUND):
		_background = load(BACKGROUND)
	set_anchors_preset(Control.PRESET_FULL_RECT)
	theme = _make_theme()
	_build_ui()
	_run = RunState.new(_start_seed())
	_show_morning()


func _start_seed() -> int:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="):
			return int(arg.get_slice("=", 1))
	return randi()


# --- UI construction -------------------------------------------------------

func _make_theme() -> Theme:
	var th := Theme.new()
	th.set_stylebox("normal", "Button", _box(Color(0.2, 0.12, 0.1), Color(0.55, 0.38, 0.22)))
	th.set_stylebox("hover", "Button", _box(Color(0.32, 0.19, 0.12), GOLD))
	th.set_stylebox("pressed", "Button", _box(Color(0.14, 0.08, 0.06), GOLD))
	th.set_stylebox("disabled", "Button", _box(Color(0.12, 0.08, 0.07), Color(0.25, 0.18, 0.14)))
	th.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	th.set_color("font_color", "Button", INK)
	th.set_color("font_hover_color", "Button", Color(1, 0.95, 0.75))
	th.set_color("font_disabled_color", "Button", Color(0.45, 0.4, 0.36))
	th.set_font_size("font_size", "Button", 9)
	th.set_stylebox("panel", "Panel", _box(Color(0.09, 0.055, 0.06, 0.94), Color(0.6, 0.42, 0.24)))
	th.set_color("font_color", "Label", INK)
	th.set_font_size("font_size", "Label", 10)
	return th


func _box(bg: Color, border: Color) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(1)
	s.set_corner_radius_all(2)
	s.content_margin_left = 4
	s.content_margin_right = 4
	return s


func _build_ui() -> void:
	_hud = _label(self, Vector2(8, 4), 9, GOLD)

	_notes_label = _label(self, MAT_RECT.position + Vector2(6, 4), 8, Color(0.95, 0.85, 0.7))
	_notes_label.size = Vector2(150, 70)

	_scale_button = Button.new()
	_scale_button.text = "⚖ 저울에 올리기"
	_scale_button.position = Vector2(316, 200)
	_scale_button.size = Vector2(96, 22)
	_scale_button.pressed.connect(_on_weigh)
	add_child(_scale_button)
	_weight_label = _label(self, Vector2(420, 202), 12, Color(1, 1, 1))

	var dialog := _panel(Rect2(8, 250, 412, 104))
	_speaker = _label(dialog, Vector2(8, 4), 10, GOLD)
	_text = _label(dialog, Vector2(8, 20), 10, INK)
	_text.size = Vector2(396, 80)
	_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART

	var side := _panel(Rect2(426, 250, 206, 104))
	_offer_label = _label(side, Vector2(6, 3), 9, GOLD)
	_choices = GridContainer.new()
	_choices.columns = 2
	_choices.position = Vector2(4, 4)
	_choices.size = Vector2(198, 96)
	_choices.add_theme_constant_override("h_separation", 3)
	_choices.add_theme_constant_override("v_separation", 3)
	side.add_child(_choices)

	_loupe = Loupe.new()
	_loupe.size = Vector2(LOUPE_SIZE, LOUPE_SIZE)
	_loupe.clip_contents = true
	_loupe.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_loupe.visible = false
	add_child(_loupe)

	_overlay = _panel(Rect2(90, 34, 460, 290))
	_overlay_text = _label(_overlay, Vector2(18, 16), 10, INK)
	_overlay_text.size = Vector2(424, 230)
	_overlay_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_overlay_button = Button.new()
	_overlay_button.position = Vector2(150, 252)
	_overlay_button.size = Vector2(160, 26)
	_overlay_button.add_theme_font_size_override("font_size", 11)
	_overlay.add_child(_overlay_button)

	_shelf_panel = ShelfPanel.new()
	_shelf_panel.visible = false
	add_child(_shelf_panel)
	_upgrade_panel = UpgradePanel.new()
	_upgrade_panel.visible = false
	add_child(_upgrade_panel)


func _label(parent: Node, pos: Vector2, font_size: int, color: Color) -> Label:
	var l := Label.new()
	l.position = pos
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", color)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l


func _panel(rect: Rect2) -> Panel:
	var p := Panel.new()
	p.position = rect.position
	p.size = rect.size
	add_child(p)
	return p


# --- Day flow --------------------------------------------------------------

func _show_morning() -> void:
	_phase = Phase.MORNING
	_visit = {}
	var lines := _run.start_day()
	_show_overlay("\n\n".join(lines), "🏺 진열대 정리", func():
		_overlay.visible = false
		_shelf_panel.open(_run, false, "🔔 가게 문 열기", _open_shop))


func _open_shop() -> void:
	var tips := _run.open_shop()
	if tips.is_empty():
		_overlay.visible = false
		_next_customer()
	else:
		_show_overlay("\n\n".join(tips), "손님 맞기", func():
			_overlay.visible = false
			_next_customer())


func _show_night() -> void:
	_phase = Phase.NIGHT
	_visit = {}
	_show_overlay("\n\n".join(_run.end_day()), "📦 창고 · 도매상", func():
		_overlay.visible = false
		_shelf_panel.open(_run, true, "🛠 밤의 상점", func():
			_upgrade_panel.open(_run, "💀 바알을 맞는다" if _run.payday else "🌙 잠들기 → 다음 날", _after_shop)))


## After the night shop: Baal on payday, then sleep — or an ending.
func _after_shop() -> void:
	if not _run.payday:
		_show_morning()
		return
	var lines := _run.pay_debt()
	_update_hud()
	match _run.ended:
		"paid":
			_show_overlay("💀 바알의 방문\n\n" + "\n\n".join(lines), "📜 엔딩 보기", func(): _show_ending(true))
		"foreclose":
			_show_overlay("💀 바알의 방문\n\n" + "\n\n".join(lines), "📜 엔딩 보기", func(): _show_ending(false))
		_:
			_show_overlay("💀 바알의 방문\n\n" + "\n\n".join(lines), "🌙 잠들기 → 다음 날", _show_morning)


func _show_ending(won: bool) -> void:
	var text: String
	if won:
		text = "— 엔딩: 빚 청산 —\n\n%d일 만에 할아버지의 빚 3,000G를 모두 갚았다.\n소지금 %dG · 창고 %d개 · 상점 업그레이드 %d개\n\n그런데 장부 마지막 장에 할아버지의 글씨가 남아 있다.\n\"조각은 일곱 개다. 절대 팔지 마라.\"\n\n(1부 끝 — 이야기는 M4에서 이어집니다)" % [
			_run.day - 1, _run.money, _run.inventory.size(), _run.upgrades.size()]
	else:
		text = "— 엔딩: 차압 —\n\n바알은 가게의 열쇠를, 그리고 당신의 그림자를 가져갔다.\n모로 전당포의 간판이 내려졌다.\n\n%d일째 밤." % (_run.day - 1)
	_show_overlay(text, "🔄 처음부터", func(): get_tree().reload_current_scene())


func _show_overlay(text: String, button: String, action: Callable) -> void:
	_overlay.visible = true
	_overlay_text.text = text
	_overlay_button.text = button
	for c in _overlay_button.pressed.get_connections():
		_overlay_button.pressed.disconnect(c["callable"])
	_overlay_button.pressed.connect(action)
	_clear_choices()
	_speaker.text = ""
	_text.text = ""
	_offer_label.text = ""
	_update_hud()


func _next_customer() -> void:
	_visit = _run.next_visit()
	if _visit.is_empty():
		_show_night()
		return
	_seen = {}
	_confronted = {}
	_notes = []
	_bought = false
	_notes_label.text = ""
	_weight_label.text = ""
	_phase = Phase.ENTER
	_clear_choices()
	_speaker.text = ""
	_text.text = "(문 종소리가 울린다)"
	_cust_x = -120.0
	var tw := create_tween()
	tw.tween_property(self, "_cust_x", CUSTOMER_AT.x, 0.8).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.tween_callback(func():
		if _visit.is_empty():
			return
		if _visit["role"] == "buy":
			_phase = Phase.SELL
			_ask_price = _visit["entry"]["tag"]
			var greet: Array = CustomerData.LINES["buy_greet"]
			_say(greet[_visit["line_seed"] % greet.size()] % RunState.item_name(_visit["item"], true)
				+ " " + CustomerData.LINES["buy_offer"] % _visit["offer"])
		else:
			_phase = Phase.TALK
			_say(Customer.greet(_visit))
		_refresh_choices())
	_update_hud()


func _say(text: String) -> void:
	var m: Dictionary = _visit["member"]
	_speaker.text = "%s (%s)" % [m["name"], CustomerData.SPECIES[m["species"]]["name"]]
	_text.text = text
	_text.visible_ratio = 0.0
	create_tween().tween_property(_text, "visible_ratio", 1.0, clampf(text.length() * 0.025, 0.2, 1.6))


func _update_hud() -> void:
	_hud.text = "%d일째   소지금 %dG   남은 손님 %d   %s\n%s" % [_run.day, _run.money, _run.visitors_left(), _run.debt_line(), _run.price_line()]


# --- Choices ---------------------------------------------------------------

func _clear_choices() -> void:
	for c in _choices.get_children():
		_choices.remove_child(c)
		c.queue_free()


func _add_choice(text: String, action: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(97 if _choices.columns == 2 else 63, 21)
	b.pressed.connect(action)
	_choices.add_child(b)


func _refresh_choices() -> void:
	_clear_choices()
	_offer_label.text = ""
	_choices.position.y = 4
	match _phase:
		Phase.TALK:
			_choices.columns = 2
			_add_choice("어디서 났어요?", _ask.bind("origin"))
			_add_choice("왜 파세요?", _ask.bind("reason"))
			if _seen.has("mark") and not _confronted.has("mark"):
				_add_choice("각인이 이상한데요?", _confront.bind("mark"))
			if _seen.has("weight") and not _confronted.has("weight"):
				_add_choice("무게가 안 맞는데요?", _confront.bind("weight"))
			if _seen.has("seam") and not _confronted.has("seam"):
				_add_choice("도금이 벗겨졌네요?", _confront.bind("seam"))
			if _run.level("touchstone") > 0 and not _seen.has("scratch"):
				_add_choice("🪨 시금석에 긁기", _scratch)
			if _seen.has("scratch") and not _confronted.has("scratch") and ItemGenerator.has_clue(_visit["item"], "scratch"):
				_add_choice("속이 다른 금속인데요?", _confront.bind("scratch"))
			_add_choice("💰 가격 얘기하죠", _start_haggle)
			_add_choice("안 사요", _turn_away)
		Phase.HAGGLE:
			_choices.columns = 3
			_choices.position.y = 40
			_update_offer()
			_add_choice("-10", _bump.bind(-10))
			_add_choice("-5", _bump.bind(-5))
			_add_choice("+5", _bump.bind(5))
			_add_choice("+10", _bump.bind(10))
			_add_choice("💰 이 값에!", _submit_offer)
			_add_choice("다시 볼게요", _back_to_talk)
		Phase.SELL:
			_choices.columns = 3
			_choices.position.y = 40
			_update_sale()
			_add_choice("-10", _bump_price.bind(-10))
			_add_choice("+10", _bump_price.bind(10))
			_add_choice("손님 값 OK", func():
				_ask_price = _visit["offer"]
				_submit_sale())
			_add_choice("💰 이 값에!", _submit_sale)
			_add_choice("안 팔아요", func():
				var lines: Array = CustomerData.LINES["buy_refused"]
				_say(lines[_visit["line_seed"] % lines.size()])
				_finish_sale(false, 0))
		Phase.RESULT:
			_choices.columns = 2
			_add_choice("다음 손님" if _run.visitors_left() > 0 else "🌙 가게 문 닫기", _leave_and_next)


func _ask(question: String) -> void:
	_say(Customer.answer(_visit, question))
	_after_line()


func _confront(kind: String) -> void:
	_confronted[kind] = true
	_say(Customer.confront(_visit, ItemGenerator.has_clue(_visit["item"], kind)))
	_after_line()


## Touchstone: scratch the item to see the metal under the surface. The seller hates it.
func _scratch() -> void:
	_seen["scratch"] = true
	_visit["patience"] -= ProgressionData.TOUCHSTONE_PATIENCE
	var fake := ItemGenerator.has_clue(_visit["item"], "scratch")
	_note("시금석: " + ("긁힌 자리 아래 다른 금속이 보인다!" if fake else "속까지 같은 금속"))
	_say("어이! 내 물건에 뭐 하는 거야?!" + (" ...!" if fake and Customer.is_lying(_visit) else ""))
	if _visit["patience"] <= 0:
		_visit["walked_out"] = true
		_visit["mood"] = "angry"
	else:
		_visit["mood"] = "annoyed"
	_after_line()


func _after_line() -> void:
	if _visit["walked_out"]:
		_finish(false, 0)
	else:
		_refresh_choices()


func _start_haggle() -> void:
	_phase = Phase.HAGGLE
	_offer = mini(_run.money, Customer.snap5(_visit["ask"] * 0.5))
	_say(CustomerData.LINES["ask"] % _visit["ask"])
	_refresh_choices()


func _back_to_talk() -> void:
	_phase = Phase.TALK
	_refresh_choices()


func _bump(delta: int) -> void:
	_offer = clampi(_offer + delta, 0, _run.money)
	_update_offer()


func _update_offer() -> void:
	_offer_label.text = "손님 호가 %dG\n내 제시가 %dG  (소지금 %dG)" % [_visit["ask"], _offer, _run.money]


func _submit_offer() -> void:
	var res := Haggle.respond(_visit, _offer)
	_say(res["line"])
	match res["result"]:
		"accept":
			_finish(true, _offer)
		"walkout":
			_finish(false, 0)
		_:
			_refresh_choices()


func _turn_away() -> void:
	var lines: Array = CustomerData.LINES["turned_away"]
	_say(lines[_visit["line_seed"] % lines.size()])
	_finish(false, 0)


func _finish(bought: bool, price: int) -> void:
	_bought = bought
	var parting := _run.finish_visit(_visit, bought, price)
	if parting != "":
		var said := _text.text
		_say(said + "\n" + parting)
	_phase = Phase.RESULT
	_refresh_choices()
	_update_hud()


func _bump_price(delta: int) -> void:
	_ask_price = maxi(5, _ask_price + delta)
	_update_sale()


func _update_sale() -> void:
	var e: Dictionary = _visit["entry"]
	_offer_label.text = "손님 제시 %dG · 내 요구 %dG\n(가격표 %d · 산 값 %d · 시세 %d)" % [
		_visit["offer"], _ask_price, e["tag"], e["paid"], _run.market_value(e["item"])]


## The buyer inspects first: a sharp-eyed one may catch a fake right here.
func _submit_sale() -> void:
	if _visit["spots_fake"] and _run.is_scam(_visit["item"], _ask_price):
		var caught: Array = CustomerData.LINES["buy_caught"]
		_visit["mood"] = "angry"
		_say(caught[_visit["line_seed"] % caught.size()])
		_finish_sale(false, 0, true)
		return
	var res := Haggle.respond_buy(_visit, _ask_price)
	_say(res["line"])
	match res["result"]:
		"accept":
			_finish_sale(true, _ask_price)
		"walkout":
			_finish_sale(false, 0)
		_:
			_refresh_choices()


func _finish_sale(sold: bool, price: int, caught: bool = false) -> void:
	_bought = sold
	_sale_price = price
	_caught = caught
	var parting := _run.finish_sale(_visit, sold, price, caught)
	if parting != "":
		_say(_text.text + "\n" + parting)
	_phase = Phase.RESULT
	_refresh_choices()
	_update_hud()


func _leave_and_next() -> void:
	_clear_choices()
	var tw := create_tween()
	tw.tween_property(self, "_cust_x", -120.0, 0.5).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tw.tween_callback(_next_customer)


# --- Appraisal ------------------------------------------------------------

func _on_weigh() -> void:
	if _phase != Phase.TALK and _phase != Phase.HAGGLE:
		return
	if not _seen.has("weight"):
		_seen["weight"] = true
		_note("무게 %dg" % _visit["item"]["weight"])
		_refresh_choices()
	var item: Dictionary = _visit["item"]
	_weight_label.text = "%d g" % item["weight"]
	if _run.level("scale") > 0:
		# precision scale: deviation from the claimed metal's true weight
		var off: float = (float(item["weight"]) / item["expected_weight"] - 1.0) * 100.0
		_weight_label.text += "  (%+.1f%%)" % off


func _note(line: String) -> void:
	_notes.append("· " + line)
	_notes_label.text = "단서 노트\n" + "\n".join(_notes)


func _input(event: InputEvent) -> void:
	if not (event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT):
		return
	if _phase != Phase.TALK and _phase != Phase.HAGGLE:
		return
	var m := get_local_mouse_position()
	if MAT_RECT.has_point(m):
		_inspect(m)


## Clicking under the loupe: finds the mark or a bare seam if the cursor is on it.
func _inspect(m: Vector2) -> void:
	_ping = m
	_ping_t = 0.4
	var item: Dictionary = _visit["item"]
	var u := (m - ITEM_CENTER) / ITEM_UNIT
	var reach := ProgressionData.LENS_CLICK_BONUS if _run.level("lens") > 0 else 1.0
	if item["mark"] != "" and not _seen.has("mark") and u.distance_to(item["mark_pos"]) < ItemDrawer.MARK_SIZE[item["kind"]] * 1.6 * reach:
		_seen["mark"] = true
		_note("각인: %s%s" % [ItemData.MARKS[item["mark"]], " (두 겹으로 번짐)" if item["mark_blurred"] else ""])
		_refresh_choices()
	elif item["seam_pos"] != Vector2.INF and not _seen.has("seam") and u.distance_to(item["seam_pos"]) < ItemDrawer.SEAM_RADIUS * 2.2 * reach:
		_seen["seam"] = true
		_note("가장자리에 속 금속이 비침")
		_refresh_choices()


func _process(delta: float) -> void:
	_t += delta
	_ping_t = maxf(0.0, _ping_t - delta)
	var m := get_local_mouse_position()
	var looking := (_phase == Phase.TALK or _phase == Phase.HAGGLE) and MAT_RECT.has_point(m)
	_notes_label.visible = _phase != Phase.RESULT
	_loupe.visible = looking
	_loupe.size = Vector2.ONE * (ProgressionData.LENS_LOUPE_SIZE if _run.level("lens") > 0 else LOUPE_SIZE)
	if looking:
		_loupe.position = m - _loupe.size * 0.5
		_loupe.item = _visit["item"]
		_loupe.focus = m - ITEM_CENTER
		_loupe.queue_redraw()
	queue_redraw()


# --- Drawing ---------------------------------------------------------------

func _draw() -> void:
	_draw_room()
	if not _visit.is_empty():
		CustomerDrawer.draw(self, _visit, Vector2(_cust_x, CUSTOMER_AT.y), _t)
	_draw_counter()
	_draw_mat()
	_draw_vignette()


func _draw_room() -> void:
	if _background:
		draw_texture_rect(_background, Rect2(0, 0, 640, COUNTER_Y), false)
		_draw_candle_light()
		return
	draw_rect(Rect2(0, 0, 640, COUNTER_Y), Color(0.12, 0.07, 0.09))
	for x in range(0, 640, 34):
		draw_line(Vector2(x, 0), Vector2(x, COUNTER_Y), Color(0.09, 0.05, 0.07), 2)
	# rainy window
	var win := Rect2(206, 20, 74, 54)
	draw_rect(win, Color(0.08, 0.12, 0.2))
	for i in 7:
		var x := win.position.x + fmod(i * 13.0 + _t * 40.0 * (0.6 + i * 0.07), win.size.x)
		var y := win.position.y + fmod(i * 23.0 + _t * 90.0, win.size.y - 8)
		draw_line(Vector2(x, y), Vector2(x - 2, y + 7), Color(0.5, 0.6, 0.8, 0.6), 1)
	draw_rect(win, Color(0.3, 0.18, 0.12), false, 3)
	draw_line(win.position + Vector2(win.size.x / 2, 0), win.position + Vector2(win.size.x / 2, win.size.y), Color(0.3, 0.18, 0.12), 2)
	# shelves with junk
	for row in 2:
		var y := 62.0 + row * 62.0
		draw_rect(Rect2(8, y, 186, 6), Color(0.33, 0.2, 0.12))
		draw_rect(Rect2(8, y + 6, 186, 2), Color(0.15, 0.08, 0.06))
		for i in 6:
			var x := 22.0 + i * 30.0 + row * 9.0
			match (i + row * 2) % 4:
				0:
					draw_rect(Rect2(x - 7, y - 18, 14, 18), Color(0.25, 0.4, 0.35, 0.9))
					draw_rect(Rect2(x - 4, y - 22, 8, 4), Color(0.4, 0.3, 0.2))
				1:
					draw_rect(Rect2(x - 8, y - 22, 5, 22), Color(0.45, 0.15, 0.15))
					draw_rect(Rect2(x - 3, y - 19, 5, 19), Color(0.2, 0.25, 0.45))
					draw_rect(Rect2(x + 2, y - 20, 5, 20), Color(0.4, 0.35, 0.2))
				2:
					draw_circle(Vector2(x, y - 8), 8, Color(0.85, 0.82, 0.72))
					draw_circle(Vector2(x - 3, y - 9), 2, Color(0.1, 0.05, 0.05))
					draw_circle(Vector2(x + 3, y - 9), 2, Color(0.1, 0.05, 0.05))
				3:
					draw_circle(Vector2(x, y - 9), 9, Color(0.5, 0.3, 0.55, 0.9))
					draw_rect(Rect2(x - 3, y - 22, 6, 6), Color(0.35, 0.25, 0.2))
	_draw_candle_light()


func _draw_candle_light() -> void:
	var flick := sin(_t * 9.0) * 4.0 + sin(_t * 23.0) * 2.0
	for i in 7:
		draw_circle(CANDLE_AT + Vector2(0, -30), 230.0 - i * 30.0 + flick, Color(1.0, 0.65, 0.3, 0.03))


func _draw_counter() -> void:
	draw_rect(Rect2(0, COUNTER_Y, 640, 10), Color(0.46, 0.29, 0.16))
	draw_line(Vector2(0, COUNTER_Y), Vector2(640, COUNTER_Y), Color(0.7, 0.48, 0.28), 1)
	draw_rect(Rect2(0, COUNTER_Y + 10, 640, 360 - COUNTER_Y), Color(0.22, 0.13, 0.08))
	for y in range(int(COUNTER_Y) + 30, 360, 24):
		draw_line(Vector2(0, y), Vector2(640, y), Color(0.15, 0.08, 0.05), 2)
	# candle
	var c := CANDLE_AT
	draw_rect(Rect2(c.x - 12, c.y - 2, 24, 6), Color(0.35, 0.3, 0.25))
	draw_rect(Rect2(c.x - 5, c.y - 30, 10, 28), Color(0.92, 0.88, 0.75))
	var h := 10.0 + sin(_t * 11.0) * 1.5
	draw_colored_polygon(PackedVector2Array([c + Vector2(0, -30 - h), c + Vector2(4, -32), c + Vector2(0, -29), c + Vector2(-4, -32)]), Color(1, 0.75, 0.3))
	draw_circle(c + Vector2(0, -33), 3, Color(1, 0.95, 0.7))
	_draw_shelf()


## The shelf: displayed items with price tags on the front of the counter.
func _draw_shelf() -> void:
	var font := ThemeDB.fallback_font
	var wanted: Dictionary = _visit.get("entry", {})
	var shelf := _run.shelf()
	var slots := _run.shelf_slots()
	var step := minf(54.0, 232.0 / slots)
	var half := step * 0.42
	for i in slots:
		var at := Vector2(74 + step * (i + 0.5), 222)
		draw_rect(Rect2(at - Vector2(half, 14), Vector2(half * 2, 34)), Color(0.15, 0.08, 0.06, 0.8))
		if i >= shelf.size():
			continue
		var e: Dictionary = shelf[i]
		if e == wanted:
			draw_rect(Rect2(at - Vector2(half + 1, 15), Vector2(half * 2 + 2, 36)), GOLD, false, 2)
		ItemDrawer.draw(self, e["item"], at - Vector2(0, 2), minf(12.0, half * 0.6), false)
		draw_string(font, at + Vector2(-half, 18), "%dG" % e["tag"], HORIZONTAL_ALIGNMENT_CENTER, half * 2, 8, GOLD)


func _draw_mat() -> void:
	draw_rect(MAT_RECT, Color(0.2, 0.05, 0.08))
	draw_rect(MAT_RECT.grow(-4), Color(0.25, 0.07, 0.1))
	draw_rect(MAT_RECT, GOLD.darkened(0.3), false, 2)
	var font := ThemeDB.fallback_font
	draw_string(font, MAT_RECT.position + Vector2(MAT_RECT.size.x - 74, 12), "🔍 감정 확대창", HORIZONTAL_ALIGNMENT_LEFT, -1, 8, Color(0.8, 0.65, 0.5))
	if _visit.is_empty():
		return
	var item: Dictionary = _visit["item"]
	draw_set_transform(ITEM_CENTER + Vector2(0, ITEM_UNIT * 0.95), 0.0, Vector2(1, 0.25))
	draw_circle(Vector2.ZERO, ITEM_UNIT * 0.8, Color(0, 0, 0, 0.35))
	draw_set_transform(Vector2.ZERO)
	ItemDrawer.draw(self, item, ITEM_CENTER, ITEM_UNIT, false)
	draw_string(font, MAT_RECT.position + Vector2(6, MAT_RECT.size.y - 6), "돋보기로 살피고 수상한 곳 클릭 · 진품 무게 오차 ±2%", HORIZONTAL_ALIGNMENT_LEFT, -1, 8, Color(0.7, 0.55, 0.45))

	# reference strip under the scale: true weight and mark of each material
	var volume: int = ItemData.KINDS[item["kind"]]["volume"]
	var y := 240.0
	draw_string(font, Vector2(316, y), "진품 기준", HORIZONTAL_ALIGNMENT_LEFT, -1, 8, Color(0.75, 0.65, 0.55))
	for i in ItemData.GENUINE_MATERIALS.size():
		var mat: Dictionary = ItemData.MATERIALS[ItemData.GENUINE_MATERIALS[i]]
		var px := 364.0 + i * 90.0
		draw_circle(Vector2(px, y - 3), 4, mat["color"])
		draw_string(font, Vector2(px + 7, y), "%s %dg" % [mat["name"], int(round(volume * float(mat["density"])))], HORIZONTAL_ALIGNMENT_LEFT, -1, 8, INK)
		ItemDrawer.draw_mark(self, mat["mark"], Vector2(px + 62, y - 3), 6.0, Color(0.85, 0.75, 0.6))

	if _ping_t > 0.0:
		draw_arc(_ping, 6.0 + (0.4 - _ping_t) * 30.0, 0.0, TAU, 20, Color(1, 0.9, 0.5, _ping_t * 2.0), 1.5)

	if _phase == Phase.RESULT:
		draw_rect(MAT_RECT.grow(-2), Color(0.05, 0.02, 0.03, 0.82))
		var fake: bool = item["truth"] == ItemGenerator.Truth.FAKE
		var title := ("가짜! 실은 %s" % RunState.item_name(item, false)) if fake else "진품 %s" % RunState.item_name(item, true)
		draw_string(font, MAT_RECT.position + Vector2(14, 26), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(1, 0.45, 0.4) if fake else Color(0.55, 1, 0.6))
		var lines := ItemGenerator.clue_lines(item)
		lines.append("오늘 시세 가치 %dG" % _run.market_value(item))
		if _visit["role"] == "buy":
			var paid: int = _visit["entry"]["paid"]
			if _caught:
				lines.append("✘ 가짜를 들켰다! 손님이 앙심을 품었다")
			elif _bought:
				lines.append("✔ %dG에 팔았다 (산 값 %d → %+dG)" % [_sale_price, paid, _sale_price - paid])
				if _run.is_scam(item, _sale_price):
					lines.append("…가짜를 비싸게 팔았다. 속은 손님은 언젠가 알아챌 것이다")
			else:
				lines.append("✘ 안 팔렸다")
		else:
			lines.append("✔ 창고로 (진열대 칸이 있으면 바로 진열)" if _bought else "✘ 안 샀음")
		for i in lines.size():
			draw_string(font, MAT_RECT.position + Vector2(14, 48 + i * 15), lines[i], HORIZONTAL_ALIGNMENT_LEFT, 290, 9, INK)


func _draw_vignette() -> void:
	for i in 6:
		draw_rect(Rect2(i * 5, i * 5, 640 - i * 10, 360 - i * 10), Color(0, 0, 0, 0.09), false, 5)


## Square magnifier: draws the item big around whatever is under the cursor.
class Loupe extends Control:
	var item: Dictionary
	## Offset of the cursor from the item's center, in view pixels.
	var focus := Vector2.ZERO

	func _draw() -> void:
		draw_rect(Rect2(Vector2.ZERO, size), Color(0.25, 0.07, 0.1))
		if item.is_empty():
			return
		var center := size * 0.5 - focus * LOUPE_ZOOM
		ItemDrawer.draw(self, item, center, ITEM_UNIT * LOUPE_ZOOM, true)
		draw_rect(Rect2(Vector2.ONE * 1.5, size - Vector2.ONE * 3.0), Color(0.9, 0.8, 0.5), false, 3.0)
		draw_line(size * 0.5 - Vector2(4, 0), size * 0.5 + Vector2(4, 0), Color(1, 1, 1, 0.5), 1)
		draw_line(size * 0.5 - Vector2(0, 4), size * 0.5 + Vector2(0, 4), Color(1, 1, 1, 0.5), 1)
