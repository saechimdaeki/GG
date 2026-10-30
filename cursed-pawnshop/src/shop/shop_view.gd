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

## Set by the title screen: load the morning save instead of starting fresh.
static var resume := false

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
var _story_panel: StoryPanel
## Full-screen scene picture behind the overlays (morning street, night shop, endings...).
var _backdrop: TextureRect
var _accuse_menu := false
var _cutscene: CutscenePlayer
var _tutorial: Tutorial
var _fader: ColorRect
var _tutorial_started := false
var _final_shown := false

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
	theme = make_theme()
	_build_ui()
	if resume:
		_run = RunState.load_save()
	if _run == null:
		_run = RunState.new(_start_seed())
	var fresh := not resume or _run.day == 1
	resume = false
	Audio.ambience("ambience")
	if fresh and _run.day == 1:
		_cutscene.play("intro", _begin_day)
	else:
		_begin_day()


func _start_seed() -> int:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--seed="):
			return int(arg.get_slice("=", 1))
	return randi()


# --- UI construction -------------------------------------------------------

## The shop's dark wood-and-gold UI theme (the title screen uses it too).
static func make_theme() -> Theme:
	var th := Theme.new()
	th.default_font = UIFont.BODY
	th.default_font_size = 12
	th.set_stylebox("normal", "Button", _frame("button"))
	th.set_stylebox("hover", "Button", _frame("button_hover"))
	th.set_stylebox("pressed", "Button", _frame("button_pressed"))
	th.set_stylebox("disabled", "Button", _frame("button_disabled"))
	th.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	th.set_font("font", "Button", UIFont.SMALL)
	th.set_font_size("font_size", "Button", 10)
	th.set_color("font_color", "Button", INK)
	th.set_color("font_hover_color", "Button", Color(1, 0.92, 0.6))
	th.set_color("font_pressed_color", "Button", GOLD)
	th.set_color("font_disabled_color", "Button", Color(0.45, 0.4, 0.36))
	th.set_stylebox("panel", "Panel", _frame("panel", 6))
	th.set_font("font", "Label", UIFont.BODY)
	th.set_font_size("font_size", "Label", 12)
	th.set_color("font_color", "Label", INK)
	th.set_color("font_shadow_color", "Label", Color(0, 0, 0, 0.6))
	th.set_constant("shadow_offset_x", "Label", 1)
	th.set_constant("shadow_offset_y", "Label", 1)
	th.set_font("font", "CheckButton", UIFont.SMALL)
	th.set_font_size("font_size", "CheckButton", 10)
	return th


## 9-slice pixel frame from assets/ui (drawn by hand-made PNGs, nearest filtered).
static func _frame(name: String, margin: int = 5) -> StyleBoxTexture:
	var s := StyleBoxTexture.new()
	s.texture = load("res://assets/ui/%s.png" % name)
	s.set_texture_margin_all(margin)
	s.set_content_margin_all(margin + 2)
	s.content_margin_top = margin
	s.content_margin_bottom = margin
	return s


static func _box(bg: Color, border: Color) -> StyleBoxFlat:
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

	var dialog := _panel(Rect2(8, 250, 352, 104))
	_speaker = _label(dialog, Vector2(8, 4), 10, GOLD)
	_text = _label(dialog, Vector2(8, 20), 10, INK)
	_text.size = Vector2(336, 80)
	_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART

	var side := _panel(Rect2(366, 250, 266, 104))
	_offer_label = _label(side, Vector2(6, 3), 9, GOLD)
	_choices = GridContainer.new()
	_choices.columns = 2
	_choices.position = Vector2(4, 4)
	_choices.size = Vector2(258, 96)
	_choices.add_theme_constant_override("h_separation", 3)
	_choices.add_theme_constant_override("v_separation", 3)
	side.add_child(_choices)

	_loupe = Loupe.new()
	_loupe.size = Vector2(LOUPE_SIZE, LOUPE_SIZE)
	_loupe.clip_contents = true
	_loupe.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_loupe.visible = false
	add_child(_loupe)

	_backdrop = TextureRect.new()
	_backdrop.position = Vector2.ZERO
	_backdrop.size = Vector2(640, 360)
	_backdrop.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_backdrop.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	_backdrop.visible = false
	add_child(_backdrop)
	_overlay = _panel(Rect2(90, 34, 460, 290))
	# long night reports scroll instead of running under the button
	var scroll := ScrollContainer.new()
	scroll.position = Vector2(18, 16)
	scroll.size = Vector2(430, 228)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_overlay.add_child(scroll)
	_overlay_text = _label(scroll, Vector2.ZERO, 10, INK)
	_overlay_text.custom_minimum_size = Vector2(418, 0)
	_overlay_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_overlay_button = Button.new()
	_overlay_button.position = Vector2(150, 252)
	_overlay_button.size = Vector2(160, 26)
	UIFont.style(_overlay_button, 11)
	_overlay.add_child(_overlay_button)

	_shelf_panel = ShelfPanel.new()
	_shelf_panel.visible = false
	add_child(_shelf_panel)
	_upgrade_panel = UpgradePanel.new()
	_upgrade_panel.visible = false
	add_child(_upgrade_panel)
	_story_panel = StoryPanel.new()
	_story_panel.visible = false
	_story_panel.on_scene = _set_backdrop
	add_child(_story_panel)
	_tutorial = Tutorial.new()
	add_child(_tutorial)
	_cutscene = CutscenePlayer.new()
	add_child(_cutscene)
	_fader = ColorRect.new()
	_fader.color = Color(0, 0, 0, 0)
	_fader.size = Vector2(640, 360)
	_fader.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_fader)
	var pause := PauseMenu.new()
	add_child(pause)


func _label(parent: Node, pos: Vector2, font_size: int, color: Color) -> Label:
	var l := Label.new()
	l.position = pos
	UIFont.style(l, font_size)
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

## Week openings (and the vault discovery) get a cutscene before the morning report.
func _begin_day() -> void:
	var cut: String = {8: "week2", 9: "vault", 15: "week3", 22: "week4"}.get(_run.day, "")
	if cut != "":
		_cutscene.play(cut, _show_morning)
	else:
		_show_morning()


## Fades to black, runs [param then], fades back.
func _fade(then: Callable) -> void:
	var tw := create_tween()
	tw.tween_property(_fader, "color:a", 1.0, 0.35)
	tw.tween_callback(then)
	tw.tween_property(_fader, "color:a", 0.0, 0.45)


## Tutorial customer: an honest seller with a fake whose wrong mark is the lesson.
func _tutorial_item() -> void:
	_tutorial_started = true
	for s in range(1, 200):
		var item := ItemGenerator.generate(s, ItemGenerator.Truth.FAKE, "medal")
		if "mark" in item["clues"] and "weight" in item["clues"]:
			_visit["item"] = item
			_visit["ask"] = Customer.snap5(item["claimed_value"] * float(_run.prices.get(item["claimed_material"], 1.0)))
			_visit["first_ask"] = _visit["ask"]
			_visit["floor"] = _visit["ask"] * 0.75
			break
	_tutorial.start()


func _show_morning() -> void:
	_phase = Phase.MORNING
	_visit = {}
	_run.save()  # morning save: continuing replays this morning exactly
	Audio.music("music_title")
	Audio.play("page")
	var lines := _run.start_day()
	_set_backdrop("morning_street")
	var beats := _run.beats_today()
	_show_overlay("\n\n".join(lines), "📜 오늘의 손님" if not beats.is_empty() else "🏺 진열대 정리", func():
		_overlay.visible = false
		_story_panel.play(_run, beats, func():
			_update_hud()
			_shelf_panel.open(_run, false, "🔔 가게 문 열기", _open_shop)))


func _open_shop() -> void:
	_fade(_open_shop_now)


func _open_shop_now() -> void:
	Audio.music("music_day")
	_set_backdrop("")
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
	var report := _run.end_day()
	Audio.music("music_night")
	_set_backdrop("night_shop")
	if report.any(func(l): return l.begins_with("🕯 ") and not "게이지" in l):
		Audio.play("curse")
	if _run.ended == "madness":
		_show_overlay("\n\n".join(report), "📜 엔딩 보기", func(): _show_ending(false))
		return
	_show_overlay("\n\n".join(report), "📦 창고 · 도매상", func():
		_overlay.visible = false
		_shelf_panel.open(_run, true, "🛠 밤의 상점", func():
			_upgrade_panel.open(_run, "💀 바알을 맞는다" if _run.payday else "🌙 잠들기 → 다음 날", _after_shop)))


## After the night shop: Baal on payday, then sleep — or an ending.
func _after_shop() -> void:
	if not _run.payday:
		_fade(_begin_day)
		return
	if _run.debt_week == ProgressionData.DEBT_SCHEDULE.size() - 1 and not _final_shown:
		_final_shown = true
		_cutscene.play("final_night", _after_shop)
		return
	var lines := _run.pay_debt()
	_update_hud()
	Audio.play("baal")
	# Baal speaks through the story panel so his portrait shows
	var scene := {"npc": "baal", "text": "(문이 열리지 않았는데 그가 카운터 앞에 서 있다)\n\n" + "\n\n".join(lines),
		"choices": [{"label": "📜 엔딩 보기" if _run.ended != "" else "🌙 잠들기 → 다음 날"}]}
	_set_backdrop("baal_office")
	_story_panel.play(_run, [scene], func():
		if _run.ended != "":
			_show_ending(_run.ended == "paid")
		else:
			_fade(_begin_day))


func _show_ending(_won: bool) -> void:
	var e := _run.ending()
	var text := "— 엔딩: %s —\n\n%s\n\n%d일 · 소지금 %dG · 명성 %d · 의심 %d\n조각: 금고 %d · 판매 %d · 빼앗김 %d · 장물 거래 %d" % [
		e["title"], e["text"], _run.day - 1, _run.money, _run.rep, _run.suspicion,
		_run.relics.size(), _run.relics_sold, _run.relics_lost, _run.stolen_deals]
	RunState.delete_save()
	Audio.music("")
	Audio.play("ending")
	# ending picture (art/raw/scenes/ending_<id>.png) behind the text when it exists
	_set_backdrop("ending_" + e["id"])
	_show_overlay(text, "🏠 타이틀로", func(): get_tree().change_scene_to_file("res://src/title/title.tscn"))


## Shows assets/sprites/scenes/<name>.png full-screen behind the panels; "" (or a
## missing picture) clears it. Panels go slightly see-through while a picture shows.
func _set_backdrop(name: String) -> void:
	var path := "res://assets/sprites/scenes/%s.png" % name
	var on := name != "" and ResourceLoader.exists(path)
	_backdrop.visible = on
	if on:
		_backdrop.texture = load(path)
	var a := 0.86 if on else 1.0
	for panel in [_overlay, _story_panel, _shelf_panel, _upgrade_panel]:
		panel.self_modulate = Color(1, 1, 1, a)


func _show_overlay(text: String, button: String, action: Callable) -> void:
	_overlay.visible = true
	_overlay_text.text = text
	(_overlay_text.get_parent() as ScrollContainer).scroll_vertical = 0
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
		_fade(_show_night)
		return
	if not _tutorial_started and _run.day == 1 and not Tutorial.done() and _visit["role"] == "sell":
		_tutorial_item()
	_seen = {}
	_confronted = {}
	_accuse_menu = false
	_notes = []
	_bought = false
	_notes_label.text = ""
	_weight_label.text = ""
	_phase = Phase.ENTER
	_clear_choices()
	_speaker.text = ""
	_text.text = "(문 종소리가 울린다)"
	Audio.play("bell")
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
			var opener := ""
			if _visit.get("collector", false) and _visit["item"]["truth"] == ItemGenerator.Truth.CURSED:
				opener = CustomerData.LINES["collector_greet"][0] + "\n"
			_say(opener + greet[_visit["line_seed"] % greet.size()] % RunState.item_name(_visit["item"], true)
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
	# three short lines: they must stay left of the appraisal window (x < 310)
	_hud.text = "%d일째 · %dG · 손님 %d · %s\n%s\n명성 %d · 의심 %d · 저주 %d · 조각 %d/7" % [
		_run.day, _run.money, _run.visitors_left(), _run.debt_line().replace("바알 상환 ", "바알 "),
		_run.price_line().replace("시세  ", "").replace(" · ", "  "), _run.rep, _run.suspicion, _run.curse, _run.relics.size()]


# --- Choices ---------------------------------------------------------------

func _clear_choices() -> void:
	for c in _choices.get_children():
		_choices.remove_child(c)
		c.queue_free()


func _add_choice(text: String, action: Callable) -> void:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(127 if _choices.columns == 2 else 83, 21)
	b.pressed.connect(func():
		Audio.play("click")
		action.call())
	_choices.add_child(b)


func _refresh_choices() -> void:
	_clear_choices()
	_offer_label.text = ""
	_choices.position.y = 4
	match _phase:
		Phase.TALK:
			_choices.columns = 2
			if _accuse_menu:
				# confronting: one button per clue found, plus the stolen-goods question
				if _seen.has("mark") and not _confronted.has("mark"):
					_add_choice("각인이 이상한데요?", _confront.bind("mark"))
				if _seen.has("weight") and not _confronted.has("weight"):
					_add_choice("무게가 안 맞는데요?", _confront.bind("weight"))
				if _seen.has("seam") and not _confronted.has("seam"):
					_add_choice(ItemData.CATEGORY_CLUES[ItemGenerator.category(_visit["item"])]["accuse"], _confront.bind("seam"))
				if _seen.has("scratch") and not _confronted.has("scratch"):
					_add_choice("속이 다른 금속인데요?", _confront.bind("scratch"))
				if _seen.has("candle") and not _confronted.has("cursed"):
					_add_choice("저주받은 물건이잖소?", _confront.bind("cursed"))
				if not _confronted.has("stolen"):
					_add_choice("🚨 장물 아니오?", _report)
				_add_choice("◀ 뒤로", func():
					_accuse_menu = false
					_refresh_choices())
			else:
				_add_choice("어디서 났어요?", _ask.bind("origin"))
				_add_choice("왜 파세요?", _ask.bind("reason"))
				_add_choice("🔍 추궁하기 ▸", func():
					_accuse_menu = true
					_refresh_choices())
				if not _seen.has("candle"):
					_add_choice("🕯 촛불 비추기", _candle)
				if _run.level("touchstone") > 0 and not _seen.has("scratch"):
					_add_choice("🪨 시금석에 긁기", _scratch)
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


## "This is stolen, isn't it?" Right: the seller flees cursing you, the watch pays a
## reward. Wrong: an insulted seller, like any false accusation.
func _report() -> void:
	_confronted["stolen"] = true
	_accuse_menu = false
	if not ItemGenerator.has_clue(_visit["item"], "stolen"):
		_say(Customer.confront(_visit, false))
		_after_line()
		return
	_visit["reported"] = true
	_visit["mood"] = "angry"
	var lines: Array = CustomerData.LINES["reported"]
	_say(lines[_visit["line_seed"] % lines.size()] + "\n(경비대 보상 +%dG)" % StoryData.REPORT_REWARD)
	_finish(false, 0)


func _confront(kind: String) -> void:
	_confronted[kind] = true
	_accuse_menu = false
	var valid := ItemGenerator.has_clue(_visit["item"], kind)
	Audio.play("coin" if valid else "angry")
	_say(Customer.confront(_visit, valid))
	_after_line()


## Touchstone: scratch the item to see the metal under the surface. The seller hates it.
func _scratch() -> void:
	_seen["scratch"] = true
	Audio.play("scratch")
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


## Candle: a cursed item turns the flame violet. Weight and marks can't show a curse.
func _candle() -> void:
	_seen["candle"] = true
	_tutorial.notify("candle")
	var cursed := ItemGenerator.has_clue(_visit["item"], "cursed")
	Audio.play("curse" if cursed else "candle")
	if cursed:
		_note("촛불: 보랏빛으로 일렁인다 — %s" % CurseData.CURSES[_visit["item"]["curse"]]["name"])
		_visit["mood"] = "nervous"
		_say("(촛불이 보랏빛으로 일렁이자 손님이 움찔한다)")
	else:
		_note("촛불: 변화 없음")
		_say("뭘 그렇게 비춰보는 거요?")
	_refresh_choices()


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
	Audio.play("deal" if bought else ("angry" if _visit["walked_out"] or _visit.get("reported", false) else "click"))
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
	Audio.play("coin" if sold else ("angry" if caught or _visit["walked_out"] else "click"))
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
	Audio.play("weigh")
	_tutorial.notify("weigh")
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
	if item["mark"] != "" and not _seen.has("mark") and u.distance_to(item["mark_pos"]) < ItemDrawer.mark_size(item["kind"]) * 1.6 * reach:
		_seen["mark"] = true
		_tutorial.notify("mark")
		_note("각인: %s%s" % [ItemData.MARKS[item["mark"]], " (두 겹으로 번짐)" if item["mark_blurred"] else ""])
		_refresh_choices()
	elif item["seam_pos"] != Vector2.INF and not _seen.has("seam") and u.distance_to(item["seam_pos"]) < ItemDrawer.SEAM_RADIUS * 2.2 * reach:
		_seen["seam"] = true
		_note(ItemData.CATEGORY_CLUES[ItemGenerator.category(item)]["note"])
		_refresh_choices()


func _process(delta: float) -> void:
	_t += delta
	_ping_t = maxf(0.0, _ping_t - delta)
	var m := get_local_mouse_position()
	var looking := (_phase == Phase.TALK or _phase == Phase.HAGGLE) and MAT_RECT.has_point(m)
	_notes_label.visible = _phase != Phase.RESULT
	_loupe.visible = looking
	if looking:
		_tutorial.notify("loupe")
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
		# a coin dot instead of "G": at this size the G reads as a 6
		var tag := str(e["tag"])
		var w := UIFont.font(8).get_string_size(tag, HORIZONTAL_ALIGNMENT_LEFT, -1, UIFont.px(8)).x
		draw_circle(at + Vector2(-w / 2.0 - 2, 15), 2.0, GOLD)
		draw_string(UIFont.font(8), at + Vector2(-w / 2.0 + 2, 18), tag, HORIZONTAL_ALIGNMENT_LEFT, -1, UIFont.px(8), GOLD)


func _draw_mat() -> void:
	draw_rect(MAT_RECT, Color(0.2, 0.05, 0.08))
	draw_rect(MAT_RECT.grow(-4), Color(0.25, 0.07, 0.1))
	draw_rect(MAT_RECT, GOLD.darkened(0.3), false, 2)
	draw_string(UIFont.font(8), MAT_RECT.position + Vector2(MAT_RECT.size.x - 74, 12), "🔍 감정 확대창", HORIZONTAL_ALIGNMENT_LEFT, -1, UIFont.px(8), Color(0.8, 0.65, 0.5))
	if _visit.is_empty():
		return
	var item: Dictionary = _visit["item"]
	draw_set_transform(ITEM_CENTER + Vector2(0, ITEM_UNIT * 0.95), 0.0, Vector2(1, 0.25))
	draw_circle(Vector2.ZERO, ITEM_UNIT * 0.8, Color(0, 0, 0, 0.35))
	draw_set_transform(Vector2.ZERO)
	if _seen.has("candle") and item["truth"] == ItemGenerator.Truth.CURSED:
		# violet aura once the candle has shown the curse
		var pulse := 0.5 + 0.5 * sin(_t * 3.0)
		for i in 4:
			draw_circle(ITEM_CENTER, ITEM_UNIT * (1.25 - i * 0.12) + pulse * 4.0, Color(0.6, 0.2, 0.9, 0.06 + i * 0.02))
	ItemDrawer.draw(self, item, ITEM_CENTER, ITEM_UNIT, false)
	var tip := _pip_tip()
	draw_string(UIFont.font(8), MAT_RECT.position + Vector2(6, MAT_RECT.size.y - 6), tip if tip != "" else "돋보기로 살피고 수상한 곳 클릭 · 진품 무게 오차 ±2%",
		HORIZONTAL_ALIGNMENT_LEFT, MAT_RECT.size.x - 12, UIFont.px(8), Color(0.7, 0.85, 1.0) if tip != "" else Color(0.7, 0.55, 0.45))

	# reference strip under the scale: true weight and mark of each material
	var volume: int = ItemData.KINDS[item["kind"]]["volume"]
	var y := 240.0
	draw_string(UIFont.font(8), Vector2(316, y), "진품 기준", HORIZONTAL_ALIGNMENT_LEFT, -1, UIFont.px(8), Color(0.75, 0.65, 0.55))
	var cat := ItemGenerator.category(item)
	if cat != "metal":
		# non-metal: one reference weight (or none for paper) and what to look for
		var hint: String = {"gem": "%s %dg · 유리는 가볍고 기포가 있다", "organic": "%s %dg · 밀랍 모조품은 가볍고 이음새가 있다", "document": "%s · 무게로는 모름, 잉크 번짐을 찾아라"}[cat]
		var kname: String = ItemData.KINDS[item["kind"]]["name"]
		draw_string(UIFont.font(8), Vector2(364, y), hint % ([kname, item["expected_weight"]] if cat != "document" else [kname]), HORIZONTAL_ALIGNMENT_LEFT, 268, UIFont.px(8), INK)
	for i in (ItemData.GENUINE_MATERIALS.size() if cat == "metal" else 0):
		var mat: Dictionary = ItemData.MATERIALS[ItemData.GENUINE_MATERIALS[i]]
		var px := 364.0 + i * 90.0
		draw_circle(Vector2(px, y - 3), 4, mat["color"])
		draw_string(UIFont.font(8), Vector2(px + 7, y), "%s %dg" % [mat["name"], int(round(volume * float(mat["density"])))], HORIZONTAL_ALIGNMENT_LEFT, -1, UIFont.px(8), INK)
		ItemDrawer.draw_mark(self, mat["mark"], Vector2(px + 62, y - 3), 6.0, Color(0.85, 0.75, 0.6))

	if _ping_t > 0.0:
		draw_arc(_ping, 6.0 + (0.4 - _ping_t) * 30.0, 0.0, TAU, 20, Color(1, 0.9, 0.5, _ping_t * 2.0), 1.5)

	if _phase == Phase.RESULT:
		draw_rect(MAT_RECT.grow(-2), Color(0.05, 0.02, 0.03, 0.82))
		var fake: bool = item["truth"] == ItemGenerator.Truth.FAKE
		var cursed: bool = item["truth"] == ItemGenerator.Truth.CURSED
		var title := ("가짜! 실은 %s" % RunState.item_name(item, false)) if fake else "진품 %s" % RunState.item_name(item, true)
		if cursed:
			title = "저주품! %s" % CurseData.CURSES[item["curse"]]["name"]
		draw_string(UIFont.font(13), MAT_RECT.position + Vector2(14, 26), title, HORIZONTAL_ALIGNMENT_LEFT, -1, UIFont.px(13), Color(1, 0.45, 0.4) if fake else Color(0.55, 1, 0.6))
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
				elif item["truth"] == ItemGenerator.Truth.CURSED:
					lines.append("수집가가 기뻐하며 품에 안았다" if _visit.get("collector", false) else "…저주가 손님을 따라갔다. 그는 누가 팔았는지 알 것이다")
			else:
				lines.append("✘ 안 팔렸다")
		else:
			lines.append("✔ 창고로 (진열대 칸이 있으면 바로 진열)" if _bought else "✘ 안 샀음")
		for i in lines.size():
			draw_string(UIFont.font(9), MAT_RECT.position + Vector2(14, 48 + i * 15), lines[i], HORIZONTAL_ALIGNMENT_LEFT, 290, UIFont.px(9), INK)


## Pip's coaching line for the first days, by what the player has (not) done yet.
func _pip_tip() -> String:
	if _run.day > 3 or _visit.is_empty():
		return ""
	match _phase:
		Phase.TALK:
			if not _seen.has("mark"):
				return "👻 피프: 돋보기로 훑다가 각인이나 수상한 곳을 찾으면 클릭해!"
			if not _seen.has("weight"):
				return "👻 피프: 저울에도 올려봐. 아래 기준표 무게랑 비교하는 거야"
			if not _seen.has("candle") and _run.day >= 2:
				return "👻 피프: 촛불도 비춰봐. 저주는 저울로 안 보여!"
			return "👻 피프: 이상하면 '추궁하기'로 따져. 틀리면 손님이 화내니 조심!"
		Phase.HAGGLE:
			return "👻 피프: 호가보다 낮게 불러. 너무 낮으면 모욕이야!"
		Phase.SELL:
			return "👻 피프: 가격표보다 올려 불러봐. 예산 안이면 사 가!"
	return ""


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
