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

	_sellers_today = CustomerData.DAY_CUSTOMERS
	_tippers = []
	for e in pending.filter(func(ev): return ev["day"] <= day):
		pending.erase(e)
		var m: Dictionary = roster[e["member"]]
		m["event_pending"] = false
		if e["type"] == "revenge":
			m["grudge"] = 0
			var id: String = EventData.REVENGE.keys()[_rng.randi() % EventData.REVENGE.size()]
			var r: Dictionary = EventData.REVENGE[id]
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
			"shelved": shelf().size() < MarketData.SHELF_SLOTS}
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
	var fake: bool = entry["item"]["truth"] == ItemGenerator.Truth.FAKE
	if caught:
		m["grudge"] += MarketData.GRUDGE_CAUGHT_FAKE
	elif sold:
		money += price
		inventory.erase(entry)
		_sold_today.append({"entry": entry, "price": price, "buyer": m["name"]})
		if fake:
			# ponytail: they find out later; the grudge waits silently for revenge
			m["grudge"] += MarketData.GRUDGE_SOLD_FAKE
		elif not v["insulted"]:
			m["affection"] += CustomerData.AFFECTION_DEAL
	return _settle(v)


## Sells a stockroom entry to the night wholesaler. Returns the gold received.
func wholesale(entry: Dictionary) -> int:
	var gold := int(round(market_value(entry["item"]) * MarketData.WHOLESALE))
	money += gold
	inventory.erase(entry)
	return gold


## Puts an entry on the shelf (if there is room) or takes it off. Returns its new state.
func set_shelved(entry: Dictionary, on: bool) -> bool:
	entry["shelved"] = on and (entry["shelved"] or shelf().size() < MarketData.SHELF_SLOTS)
	return entry["shelved"]


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
	day += 1
	return lines


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
	if v["walked_out"]:
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
