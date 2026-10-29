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

var _rng := RandomNumberGenerator.new()
## Today's visitors: {member: index, role: "sell" | "buy"}.
var _queue: Array = []
var _next := 0
var _sellers_today := CustomerData.DAY_CUSTOMERS
var _tippers: Array = []
var _bought_today: Array = []
var _sold_today: Array = []


func _init(seed_value: int) -> void:
	_rng.seed = seed_value
	roster = Customer.make_roster(_rng)


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

	_sellers_today = CustomerData.DAY_CUSTOMERS + level("sign")
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
	return lines


## Picks today's sellers and buyers (buyers only as many as the shelf can feed).
## Returns tip lines from friendly monsters, which name a liar among them.
func open_shop() -> Array:
	var order := range(roster.size())
	_shuffle(order)
	var sellers := maxi(1, _sellers_today)
	var buyers := mini(MarketData.BUYERS_PER_DAY, shelf().size())
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
			v = Customer.start_visit(member, _rng.randi(), prices)
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
	if v["exposed"] and not Customer.is_lying(v):
		m["affection"] += CustomerData.AFFECTION_HONEST_EXPOSE
	if v["wrong_accused"]:
		m["grudge"] += CustomerData.GRUDGE_WRONG_ACCUSE
	return _settle(v)


## Settles a buying visit. [param caught] = the buyer spotted a fake before paying.
func finish_sale(v: Dictionary, sold: bool, price: int, caught: bool = false) -> String:
	var m: Dictionary = v["member"]
	var entry: Dictionary = v["entry"]
	if caught:
		m["grudge"] += MarketData.GRUDGE_CAUGHT_FAKE
	elif sold:
		money += price
		inventory.erase(entry)
		_sold_today.append({"entry": entry, "price": price, "buyer": m["name"]})
		if is_scam(entry["item"], price):
			# ponytail: they find out later; the grudge waits silently for revenge
			m["grudge"] += MarketData.GRUDGE_SOLD_FAKE
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
	return true


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
	return int(round(item["claimed_value"] * float(prices[item["claimed_material"]])))


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
