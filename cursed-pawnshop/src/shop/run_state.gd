class_name RunState
extends RefCounted
## One run: day, money, the monster roster and what they remember, scheduled
## revenge/gift events, and today's queue of visitors.

var day := 1
var money := CustomerData.START_MONEY
var roster: Array = []
## Scheduled events: {day, type: "revenge" | "gift", member: index}.
var pending: Array = []
## Today's bought items: {item, price, seller}.
var bought_today: Array = []

var _rng := RandomNumberGenerator.new()
var _queue: Array = []
var _next := 0


func _init(seed_value: int) -> void:
	_rng.seed = seed_value
	roster = Customer.make_roster(_rng)


## Applies today's events and picks today's visitors. Returns the morning report lines.
func start_day() -> Array:
	var lines := ["— %d일째 아침 —" % day, EventData.NEWS[_rng.randi() % EventData.NEWS.size()]]
	var customers := CustomerData.DAY_CUSTOMERS
	var tippers := []
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
			customers += r.get("customers", 0)
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
				tippers.append(m)

	var order := range(roster.size())
	_shuffle(order)
	_queue = order.slice(0, maxi(1, customers))
	_next = 0
	bought_today = []

	for m in tippers:
		var liars := _queue.filter(func(i): return roster[i]["personality"] == "liar")
		var liar_name: String = roster[liars[0]]["name"] if not liars.is_empty() else "아무도 없어요. 오늘은 안심하세요"
		lines.append(EventData.GIFT["tip"]["text"].format({"name": m["name"], "liar": liar_name}))
	return lines


## Visitors left today.
func visitors_left() -> int:
	return _queue.size() - _next


## The next visitor, or an empty Dictionary when the day is over.
func next_visit() -> Dictionary:
	if _next >= _queue.size():
		return {}
	var idx: int = _queue[_next]
	_next += 1
	var v := Customer.start_visit(roster[idx], _rng.randi())
	v["member_idx"] = idx
	return v


## Settles a visit: money, grudge / affection, and any event it schedules.
## Returns the seller's parting line ("" when nothing special).
func finish_visit(v: Dictionary, bought: bool, price: int) -> String:
	var m: Dictionary = v["member"]
	if bought:
		money -= price
		bought_today.append({"item": v["item"], "price": price, "seller": m["name"]})
		if not v["insulted"] and not v["wrong_accused"]:
			m["affection"] += CustomerData.AFFECTION_DEAL
		if price >= v["first_ask"] * CustomerData.FAIR_DEAL_RATIO:
			m["affection"] += CustomerData.AFFECTION_FAIR_DEAL
	if v["exposed"] and not Customer.is_lying(v):
		m["affection"] += CustomerData.AFFECTION_HONEST_EXPOSE
	if v["insulted"]:
		m["grudge"] += CustomerData.GRUDGE_INSULT
	if v["wrong_accused"]:
		m["grudge"] += CustomerData.GRUDGE_WRONG_ACCUSE
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


## Sells today's purchases and moves to the next day. Returns the night report lines.
func end_day() -> Array:
	var lines := ["— %d일째 밤 —" % day]
	var total := 0
	for b in bought_today:
		var item: Dictionary = b["item"]
		var sold := int(round(item["value"] * ItemData.RESALE_MULTIPLIER))
		money += sold
		total += sold - b["price"]
		lines.append("%s의 %s → 실제 %s. %dG에 사서 %dG에 팖 (%+d)" % [
			b["seller"], item_name(item, true), item_name(item, false), b["price"], sold, sold - b["price"]])
	if bought_today.is_empty():
		lines.append("오늘은 아무것도 사지 않았다.")
	lines.append("오늘 손익 %+dG · 소지금 %dG" % [total, money])
	var grudges := roster.filter(func(m): return m["grudge"] > 0 and not m["event_pending"])
	if not grudges.is_empty():
		lines.append("앙심을 품은 괴물: " + ", ".join(grudges.map(func(m): return m["name"])))
	day += 1
	return lines


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
