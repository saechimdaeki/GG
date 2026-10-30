class_name RunState
extends RefCounted
## One run: day, money, metal prices, the stockroom and shelf, the monster roster
## and what they remember, scheduled revenge/gift events, and today's visitors.

var day := 1
var money := CustomerData.START_MONEY
var roster: Array = []
## Today's multiplier per genuine metal ("gold", "silver", "bronze").
var prices := {"gold": 1.0, "silver": 1.0, "bronze": 1.0}
## Owned items: {item, paid, tag, shelved, seller}.
var inventory: Array = []
## Scheduled events: {day, type: "revenge" | "gift", member: index}.
var pending: Array = []
## Upgrade id -> level bought (see ProgressionData.UPGRADES).
var upgrades := {}
## Index into ProgressionData.DEBT_SCHEDULE of the next payment, and unpaid carry-over.
var debt_week := 0
var carry := 0
## "" while playing, "paid" (debt cleared) or "foreclose" (lost the shop).
var ended := ""
## True on the night a payment is due (set by end_day, cleared by pay_debt).
var payday := false
## Story state (see StoryData): flags set by choices, reputation, guard suspicion,
## relics kept in the vault, relics sold / lost to the Brotherhood, stolen goods bought.
var flags := {}
var rep := 0
var suspicion := 0
var relics: Array = []
var relics_sold := 0
var relics_lost := 0
var stolen_deals := 0
## Curse gauge (0..CurseData.GAUGE_MAX). At the top: the madness ending.
var curse := 0
var _scared_tomorrow := 0

var _rng := RandomNumberGenerator.new()
## Today's visitors: {member: index, role: "sell" | "buy"}.
var _queue: Array = []
var _next := 0
var _sellers_today := CustomerData.DAY_CUSTOMERS
var _tippers: Array = []
var _bought_today: Array = []
var _sold_today: Array = []


const SAVE_PATH := "user://save.dat"
## Everything a morning save needs. Visits in progress are never saved: the game
## saves only at the start of a day.
const SAVED := ["day", "money", "roster", "prices", "inventory", "pending", "upgrades", "debt_week",
	"carry", "ended", "flags", "rep", "suspicion", "relics", "relics_sold", "relics_lost",
	"stolen_deals", "curse", "_scared_tomorrow"]


func _init(seed_value: int) -> void:
	_rng.seed = seed_value
	roster = Customer.make_roster(_rng)


## Writes the run to SAVE_PATH (binary Variant, keeps Vector2 / Color as they are).
func save() -> void:
	var data := {"rng_seed": _rng.seed, "rng_state": _rng.state}
	for k in SAVED:
		data[k] = get(k)
	var f := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if f:
		f.store_var(data)


static func has_save() -> bool:
	return FileAccess.file_exists(SAVE_PATH)


static func delete_save() -> void:
	if has_save():
		DirAccess.remove_absolute(SAVE_PATH)


## The saved run, or null when there is none or it can't be read.
static func load_save() -> RunState:
	var f := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if f == null:
		return null
	var data = f.get_var()
	if typeof(data) != TYPE_DICTIONARY or not data.has("day"):
		return null
	var run := RunState.new(data["rng_seed"])
	for k in SAVED:
		if data.has(k):
			run.set(k, data[k])
	run._rng.state = data["rng_state"]
	return run


## Moves prices, applies today's events. Returns the morning report lines.
## Call open_shop() once the shelf is arranged.
func start_day() -> Array:
	var news: Dictionary = EventData.NEWS[_rng.randi() % EventData.NEWS.size()]
	var old := prices.duplicate()
	for metal in prices:
		var p: float = prices[metal]
		p += (1.0 - p) * MarketData.MEAN_REVERT + _rng.randf_range(-MarketData.DAILY_DRIFT, MarketData.DAILY_DRIFT)
		p *= news.get("prices", {}).get(metal, 1.0)
		prices[metal] = clampf(p, MarketData.PRICE_RANGE.x, MarketData.PRICE_RANGE.y)
	var lines := ["— %d일째 아침 —" % day, news["text"], price_line(old)]

	_sellers_today = CustomerData.DAY_CUSTOMERS + level("sign") - _scared_tomorrow
	_scared_tomorrow = 0
	_tippers = []
	for e in pending.filter(func(ev): return ev["day"] <= day):
		pending.erase(e)
		var m: Dictionary = roster[e["member"]]
		m["event_pending"] = false
		if e["type"] == "revenge":
			m["grudge"] = 0
			var id: String = EventData.REVENGE.keys()[_rng.randi() % EventData.REVENGE.size()]
			var r: Dictionary = EventData.REVENGE[id]
			if id == "rob" and level("guard") > 0:
				lines.append("🛡 밤사이 %s%s 금고를 노렸지만 경비 고블린이 쫓아냈다!" % [m["name"], josa_ga(m["name"])])
				continue
			var n := 0
			if r.has("money_ratio"):
				n = int(money * float(r["money_ratio"]))
			elif r.has("money_flat"):
				n = mini(money, r["money_flat"])
			_sellers_today += r.get("customers", 0)
			money -= n
			lines.append(r["text"].format({"name": m["name"], "ga": josa_ga(m["name"]), "n": n}))
		else:
			m["affection"] = 0
			if _rng.randf() < 0.5:
				var g: Vector2i = EventData.GIFT["gold"]["gold"]
				var n := _rng.randi_range(g.x, g.y)
				money += n
				lines.append(EventData.GIFT["gold"]["text"].format({"name": m["name"], "ga": josa_ga(m["name"]), "n": n}))
			else:
				_tippers.append(m)
	_bought_today = []
	_sold_today = []
	lines.append_array(_story_morning())
	return lines


## Guard raids, the patron's purchase, suspicion decay.
func _story_morning() -> Array:
	var lines := []
	suspicion = maxi(0, suspicion - StoryData.SUSPICION_DECAY)
	if suspicion >= StoryData.RAID_AT:
		var hot := inventory.filter(func(e): return e["item"].get("stolen", false))
		for e in hot:
			inventory.erase(e)
		var fine := mini(money, StoryData.RAID_FINE)
		money -= fine
		suspicion = StoryData.RAID_RESET
		rep -= 5
		lines.append("🚨 경비대 단속! 장물 %d개 압수, 벌금 -%dG" % [hot.size(), fine])
	if flags.get("patron", false) and day % StoryData.PATRON_EVERY == 0:
		var shelved := shelf()
		if shelved.any(func(e): return e["item"]["truth"] == ItemGenerator.Truth.FAKE):
			flags.erase("patron")
			rep -= 5
			lines.append("🧛 드라크마 백작의 하인이 진열대를 보고 혀를 찼다. \"가짜를 섞다니. 백작님과의 관계는 끝이오.\"")
		elif not shelved.is_empty():
			shelved.sort_custom(func(a, b): return market_value(a["item"]) > market_value(b["item"]))
			var e: Dictionary = shelved[0]
			var gold := int(round(market_value(e["item"]) * StoryData.PATRON_RATE))
			money += gold
			inventory.erase(e)
			lines.append("🧛 드라크마 백작의 하인이 %s을(를) %dG에 사 갔다." % [item_name(e["item"], true), gold])
	return lines


## Today's story scenes whose conditions hold.
func beats_today() -> Array:
	return StoryData.BEATS.filter(func(b): return b["day"] == day and check(b.get("if", {})))


## True when every condition in [param cond] holds (see StoryData.BEATS / ENDINGS).
func check(cond: Dictionary) -> bool:
	for k in cond:
		var v = cond[k]
		var ok: bool
		match k:
			"flag": ok = flags.get(v, false)
			"not_flag": ok = not flags.get(v, false)
			"relics_min": ok = relics.size() >= v
			"not_relics_min": ok = relics.size() < v
			"money_min": ok = money >= v
			"relics_sold_min": ok = relics_sold >= v
			"stolen_min": ok = stolen_deals >= v
			"suspicion_max": ok = suspicion <= v
			"rep_min": ok = rep >= v
			_: ok = false
		if not ok:
			return false
	return true


## Applies a story choice's effects. Returns a short summary ("" when silent).
func apply_effects(eff: Dictionary) -> String:
	var notes := []
	money += eff.get("money", 0)
	rep += eff.get("rep", 0)
	suspicion = maxi(0, suspicion + eff.get("suspicion", 0))
	curse = maxi(0, curse + eff.get("curse", 0))
	_sellers_today += eff.get("sellers", 0)
	for i in mini(eff.get("lose_item", 0), inventory.size()):
		var gone: Dictionary = inventory.pop_at(_rng.randi() % inventory.size())
		notes.append("📦 %s이(가) 사라졌다" % item_name(gone["item"], true))
	for i in mini(eff.get("give_relic", 0), relics.size()):
		var r: String = relics.pop_back()
		relics_lost += 1
		notes.append("🔮 %s을(를) 빼앗겼다 (%d/7)" % [StoryData.RELICS[r], relics.size()])
	if eff.has("flag"):
		flags[eff["flag"]] = true
	if eff.has("unflag"):
		flags.erase(eff["unflag"])
	if eff.has("relic"):
		relics.append(eff["relic"])
		notes.append("🔮 %s을(를) 금고에 넣었다 (%d/7)" % [StoryData.RELICS[eff["relic"]], relics.size()])
	if eff.has("lose_relic"):
		relics_lost += 1
		notes.append("🔮 %s이(가) 형제단에게 넘어갔다" % StoryData.RELICS[eff["lose_relic"]])
	if eff.has("sell_relics"):
		var n: int = relics.size() if eff["sell_relics"] < 0 else mini(eff["sell_relics"], relics.size())
		for i in n:
			relics.pop_back()
		relics_sold += n
		money += n * int(eff["price"])
		notes.append("🔮 조각 %d개를 팔았다 +%dG" % [n, n * int(eff["price"])])
	if eff.get("money", 0) != 0:
		notes.append("%+dG" % eff["money"])
	return "  ".join(notes)


## The ending once the debt is settled (or the shop is lost).
func ending() -> Dictionary:
	if ended == "foreclose":
		return StoryData.FORECLOSE
	if ended == "madness":
		return StoryData.MADNESS
	for e in StoryData.ENDINGS:
		if check(e["if"]):
			return e
	return StoryData.ENDINGS[-1]


## Chance today's seller brings stolen goods.
func stolen_chance(member: Dictionary) -> float:
	var c: float = StoryData.STOLEN_CHANCE.get(member["personality"], 0.05)
	c += StoryData.STOLEN_SPECIES_BONUS.get(member["species"], 0.0)
	if flags.get("rat_partner", false):
		c += StoryData.STOLEN_PARTNER_BONUS
	return c


## Picks today's sellers and buyers (buyers only as many as the shelf can feed).
## Returns tip lines from friendly monsters, which name a liar among them.
func open_shop() -> Array:
	var order := range(roster.size())
	_shuffle(order)
	var sellers := maxi(1, _sellers_today - (1 if rep <= StoryData.REP_LOW else 0))
	var buyers := mini(MarketData.BUYERS_PER_DAY + (1 if rep >= StoryData.REP_HIGH else 0), shelf().size())
	_queue = []
	for i in mini(order.size(), sellers + buyers):
		_queue.append({"member": order[i], "role": "sell" if i < sellers else "buy"})
	_shuffle(_queue)
	_next = 0

	var lines := []
	for m in _tippers:
		var liars := _queue.filter(func(q): return roster[q["member"]]["personality"] == "liar")
		var liar_name: String = roster[liars[0]["member"]]["name"] if not liars.is_empty() else "아무도 없어요. 오늘은 안심하세요"
		lines.append(EventData.GIFT["tip"]["text"].format({"name": m["name"], "liar": liar_name}))
	return lines


func visitors_left() -> int:
	return _queue.size() - _next


## The next visitor, or {} when the day is over. A buyer who finds the shelf
## emptied by earlier sales is skipped.
func next_visit() -> Dictionary:
	while _next < _queue.size():
		var q: Dictionary = _queue[_next]
		_next += 1
		var member: Dictionary = roster[q["member"]]
		var v: Dictionary
		if q["role"] == "sell":
			v = Customer.start_visit(member, _rng.randi(), prices, stolen_chance(member))
		else:
			v = Customer.start_buy_visit(member, _rng.randi(), shelf(), believed_value)
			if v.is_empty():
				continue
		v["member_idx"] = q["member"]
		return v
	return {}


## Settles a selling visit. Returns the seller's parting line ("" = nothing special).
func finish_visit(v: Dictionary, bought: bool, price: int) -> String:
	var m: Dictionary = v["member"]
	if bought:
		money -= price
		var entry := {"item": v["item"], "paid": price, "seller": m["name"],
			"tag": maxi(5, Customer.snap5(market_value(v["item"]) * MarketData.DEFAULT_TAG)),
			"shelved": shelf().size() < shelf_slots()}
		inventory.append(entry)
		_bought_today.append(entry)
		if not v["insulted"] and not v["wrong_accused"]:
			m["affection"] += CustomerData.AFFECTION_DEAL
		if price >= v["first_ask"] * CustomerData.FAIR_DEAL_RATIO:
			m["affection"] += CustomerData.AFFECTION_FAIR_DEAL
			rep += StoryData.REP_FAIR_DEAL
		if v["item"].get("stolen", false):
			suspicion += StoryData.SUSPICION_PER_STOLEN
			stolen_deals += 1
	if v["exposed"]:
		if Customer.is_lying(v):
			rep += StoryData.REP_EXPOSE_LIAR
		else:
			m["affection"] += CustomerData.AFFECTION_HONEST_EXPOSE
	if v.get("reported", false):
		rep += StoryData.REP_REPORT_STOLEN
		money += StoryData.REPORT_REWARD
		m["grudge"] += StoryData.REPORT_GRUDGE
	if v["wrong_accused"]:
		m["grudge"] += CustomerData.GRUDGE_WRONG_ACCUSE
	return _settle(v)


## Settles a buying visit. [param caught] = the buyer spotted a fake before paying.
func finish_sale(v: Dictionary, sold: bool, price: int, caught: bool = false) -> String:
	var m: Dictionary = v["member"]
	var entry: Dictionary = v["entry"]
	if caught:
		m["grudge"] += MarketData.GRUDGE_CAUGHT_FAKE
		rep += StoryData.REP_CAUGHT
	elif sold:
		money += price
		inventory.erase(entry)
		_sold_today.append({"entry": entry, "price": price, "buyer": m["name"]})
		if is_scam(entry["item"], price):
			# ponytail: they find out later; the grudge waits silently for revenge
			m["grudge"] += MarketData.GRUDGE_SOLD_FAKE
			rep += StoryData.REP_SCAM
		elif entry["item"]["truth"] == ItemGenerator.Truth.CURSED and not v.get("collector", false):
			# the curse goes home with them; they will know who sold it
			m["grudge"] += CurseData.GRUDGE_SOLD_CURSED
			rep += CurseData.REP_SOLD_CURSED
		elif not v["insulted"]:
			m["affection"] += CustomerData.AFFECTION_DEAL
	return _settle(v)


## Selling a fake for far more than it is really worth.
func is_scam(item: Dictionary, price: int) -> bool:
	return item["truth"] == ItemGenerator.Truth.FAKE and price > market_value(item) * MarketData.SCAM_RATIO


## Sells a stockroom entry to the night wholesaler. Returns the gold received.
func wholesale(entry: Dictionary) -> int:
	var gold := int(round(market_value(entry["item"]) * MarketData.WHOLESALE))
	money += gold
	inventory.erase(entry)
	return gold


## Puts an entry on the shelf (if there is room) or takes it off. Returns its new state.
func set_shelved(entry: Dictionary, on: bool) -> bool:
	entry["shelved"] = on and (entry["shelved"] or shelf().size() < shelf_slots())
	return entry["shelved"]


func shelf_slots() -> int:
	return ProgressionData.SHELF_SLOTS_BASE + level("shelf")


## Level bought of an upgrade (0 = not bought).
func level(id: String) -> int:
	return upgrades.get(id, 0)


## Price of the next level, or -1 when maxed.
func upgrade_price(id: String) -> int:
	var prices: Array = ProgressionData.UPGRADES[id]["prices"]
	return prices[level(id)] if level(id) < prices.size() else -1


## Buys the next level if affordable. Returns true on success.
func buy_upgrade(id: String) -> bool:
	var price := upgrade_price(id)
	if price < 0 or money < price:
		return false
	money -= price
	upgrades[id] = level(id) + 1
	if id == "salt":
		curse = maxi(0, curse + CurseData.SALT_GAUGE)
	return true


## Removes an entry's curse for gold. Returns true on success.
func purify(entry: Dictionary) -> bool:
	if entry["item"]["truth"] != ItemGenerator.Truth.CURSED or money < CurseData.PURIFY_COST:
		return false
	money -= CurseData.PURIFY_COST
	entry["item"]["truth"] = ItemGenerator.Truth.GENUINE
	entry["item"].erase("curse")
	curse = maxi(0, curse + CurseData.PURIFY_GAUGE)
	return true


## Each cursed item in stock feeds the gauge and may fire its curse.
## At the top of the gauge the run ends in madness.
func _curse_night() -> Array:
	var lines := []
	curse = maxi(0, curse - CurseData.GAUGE_DECAY)
	for e in inventory.duplicate():
		var item: Dictionary = e["item"]
		if item["truth"] != ItemGenerator.Truth.CURSED or not inventory.has(e):
			continue
		curse += CurseData.GAUGE_PER_ITEM
		if _rng.randf() >= CurseData.EVENT_CHANCE:
			continue
		var c: Dictionary = CurseData.CURSES[item["curse"]]
		var fmt := {"item": item_name(item, true), "n": 0, "victim": ""}
		match item["curse"]:
			"whisper":
				rep += CurseData.WHISPER_REP
				curse += CurseData.WHISPER_GAUGE
			"hunger":
				var others := inventory.filter(func(o): return o != e)
				if others.is_empty():
					continue
				var victim: Dictionary = others[_rng.randi() % others.size()]
				inventory.erase(victim)
				fmt["victim"] = item_name(victim["item"], true)
			"bleed":
				fmt["n"] = int(money * CurseData.BLEED_RATIO)
				money -= fmt["n"]
			"gaze":
				_scared_tomorrow += 1
		lines.append(c["event"].format(fmt))
	if curse >= CurseData.GAUGE_MAX:
		ended = "madness"
		lines.append("🕯 저주 게이지가 가득 찼다. 금고 속의 속삭임이 당신의 목소리로 바뀌었다...")
	elif curse >= CurseData.GAUGE_WARN:
		lines.append("⚠ 저주 게이지 %d/%d — %d가 되면 광기에 빠진다! 창고에서 정화하거나 밤의 상점에서 정화 소금을 사라." % [curse, CurseData.GAUGE_MAX, CurseData.GAUGE_MAX])
	elif curse > 0:
		lines.append("🕯 저주 게이지 %d/%d" % [curse, CurseData.GAUGE_MAX])
	return lines

## Gold due at the next payday (this week's share plus any carry-over).
func debt_due() -> int:
	return ProgressionData.DEBT_SCHEDULE[debt_week] + carry if debt_week < ProgressionData.DEBT_SCHEDULE.size() else 0


## Days until the next payday (0 = tonight).
func days_to_payday() -> int:
	return (ProgressionData.WEEK_DAYS - day % ProgressionData.WEEK_DAYS) % ProgressionData.WEEK_DAYS


## Baal collects: gold first, then the most valuable stock, then the rest rolls
## over with interest — or, in the last week, he takes the shop. Returns report lines.
func pay_debt() -> Array:
	payday = false
	var due := debt_due()
	var lines := []
	var from_gold := mini(money, due)
	money -= from_gold
	var short := due - from_gold
	if short > 0:
		var by_value := inventory.duplicate()
		by_value.sort_custom(func(a, b): return market_value(a["item"]) > market_value(b["item"]))
		var taken := []
		for e in by_value:
			if short <= 0:
				break
			short -= int(round(market_value(e["item"]) * ProgressionData.SEIZE_RATE))
			inventory.erase(e)
			taken.append(item_name(e["item"], true))
		if not taken.is_empty():
			lines.append(ProgressionData.BAAL_LINES["seized"] % ", ".join(taken))
		if short < 0:
			money += -short  # change back from the last seized item
			short = 0
	debt_week += 1
	var last := debt_week >= ProgressionData.DEBT_SCHEDULE.size()
	if short > 0:
		if last:
			ended = "foreclose"
			lines.append(ProgressionData.BAAL_LINES["foreclose"])
			return lines
		carry = int(round(short * (1.0 + ProgressionData.SHORTFALL_INTEREST)))
		lines.append(ProgressionData.BAAL_LINES["short"] % [short, carry])
		return lines
	carry = 0
	if last:
		ended = "paid"
		lines.append(ProgressionData.BAAL_LINES["final_paid"])
	else:
		lines.append(ProgressionData.BAAL_LINES["paid"] % [due, debt_due()])
	return lines


func shelf() -> Array:
	return inventory.filter(func(e): return e["shelved"])


## What the item is really worth today.
func market_value(item: Dictionary) -> int:
	return int(round(item["value"] * float(prices.get(item["true_material"], 1.0))))


## What a buyer who trusts the claim thinks it is worth today.
func believed_value(item: Dictionary) -> int:
	return int(round(item["claimed_value"] * float(prices.get(item["claimed_material"], 1.0))))


## Night report: today's trades and the stockroom. Advances the day.
func end_day() -> Array:
	var lines := ["— %d일째 밤 —" % day]
	var bought := 0
	for e in _bought_today:
		bought += e["paid"]
		lines.append("매입: %s의 %s %dG (실은 %s)" % [e["seller"], item_name(e["item"], true), e["paid"], item_name(e["item"], false)])
	var sold := 0
	for s in _sold_today:
		var e: Dictionary = s["entry"]
		sold += s["price"]
		var fake: bool = e["item"]["truth"] == ItemGenerator.Truth.FAKE
		lines.append("판매: %s → %s %dG (산 값 %d, %+d)%s" % [
			item_name(e["item"], true), s["buyer"], s["price"], e["paid"], s["price"] - e["paid"], "  ※가짜" if fake else ""])
	if _bought_today.is_empty() and _sold_today.is_empty():
		lines.append("오늘은 아무 거래도 없었다.")
	var stock := 0
	for e in inventory:
		stock += market_value(e["item"])
	lines.append("매입 %dG · 판매 %dG · 소지금 %dG · 창고 %d개(시세 %dG)" % [bought, sold, money, inventory.size(), stock])
	var grudges := roster.filter(func(m): return m["grudge"] > 0 and not m["event_pending"])
	if not grudges.is_empty():
		lines.append("앙심을 품은 괴물: " + ", ".join(grudges.map(func(m): return m["name"])))
	lines.append_array(_curse_night())
	payday = days_to_payday() == 0 and ended == ""
	if payday:
		lines.append("⚠ 오늘 밤 바알이 온다. 갚을 돈: %dG" % debt_due())
	day += 1
	return lines


## "상환 D-3 · 600G" for the HUD.
func debt_line() -> String:
	if ended != "":
		return ""
	var d := days_to_payday()
	return "바알 상환 %s · %dG" % ["오늘 밤" if d == 0 else "D-%d" % d, debt_due()]


## "시세 금 ×1.20▲ · 은 ×0.90▼ · 청동 ×1.00" (arrows vs [param old] when given).
func price_line(old: Dictionary = {}) -> String:
	var parts := []
	for metal in ["gold", "silver", "bronze"]:
		var p: float = prices[metal]
		var arrow := ""
		if not old.is_empty():
			arrow = "▲" if p > old[metal] + 0.04 else ("▼" if p < old[metal] - 0.04 else "")
		parts.append("%s ×%.2f%s" % [ItemData.MATERIALS[metal]["name"], p, arrow])
	return "시세  " + " · ".join(parts)


## "금 메달" from the claim, or the real material when [param claimed] is false.
static func item_name(item: Dictionary, claimed: bool) -> String:
	var mat: String = item["claimed_material"] if claimed else item["true_material"]
	return "%s %s" % [ItemData.MATERIALS[mat]["name"], ItemData.KINDS[item["kind"]]["name"]]


## 이 or 가 after [param word], by whether its last syllable has a final consonant.
static func josa_ga(word: String) -> String:
	var c := word.unicode_at(word.length() - 1)
	if c < 0xAC00 or c > 0xD7A3:
		return "이(가)"
	return "이" if (c - 0xAC00) % 28 != 0 else "가"


## Shared grudge bookkeeping, then schedules revenge or a gift. Returns the parting line.
func _settle(v: Dictionary) -> String:
	var m: Dictionary = v["member"]
	if v["insulted"]:
		m["grudge"] += CustomerData.GRUDGE_INSULT
	# a buyer leaving over price is just business; a seller driven off is not
	if v["walked_out"] and v["role"] == "sell":
		m["grudge"] += CustomerData.GRUDGE_WALKOUT
	if m["event_pending"]:
		return ""
	var lines: Dictionary = CustomerData.LINES
	if m["grudge"] >= CustomerData.REVENGE_AT:
		_schedule("revenge", v["member_idx"])
		return lines["threat"][v["line_seed"] % lines["threat"].size()]
	if m["affection"] >= CustomerData.GIFT_AT:
		_schedule("gift", v["member_idx"])
		return lines["thanks"][v["line_seed"] % lines["thanks"].size()]
	return ""


func _schedule(type: String, member_idx: int) -> void:
	roster[member_idx]["event_pending"] = true
	pending.append({"day": day + _rng.randi_range(CustomerData.EVENT_DELAY.x, CustomerData.EVENT_DELAY.y), "type": type, "member": member_idx})


## Seeded Fisher-Yates so the run is reproducible.
func _shuffle(arr: Array) -> void:
	for i in range(arr.size() - 1, 0, -1):
		var j := _rng.randi() % (i + 1)
		var t = arr[i]
		arr[i] = arr[j]
		arr[j] = t
