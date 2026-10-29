class_name Customer
## Roster members (who a monster is and what it remembers about you) and visits
## (one trip to the counter with one item). Pure functions over Dictionaries.


## The run's monster roster: species, name, personality, clothes, grudge, affection.
static func make_roster(rng: RandomNumberGenerator) -> Array:
	var roster := []
	var used := {}
	var species_ids: Array = CustomerData.SPECIES.keys()
	while roster.size() < CustomerData.ROSTER_SIZE:
		var sp: String = species_ids[rng.randi() % species_ids.size()]
		var names: Array = CustomerData.SPECIES[sp]["names"]
		var nm: String = names[rng.randi() % names.size()]
		if used.has(nm):
			continue
		used[nm] = true
		roster.append({
			"species": sp,
			"name": nm,
			"personality": _pick_personality(rng),
			"clothes": CustomerData.CLOTHES[rng.randi() % CustomerData.CLOTHES.size()],
			"grudge": 0,
			"affection": 0,
			"event_pending": false,
		})
	return roster


## A selling visit: the member, their item, the price talk and their mood.
## [param prices] = today's metal multipliers (RunState.prices); sellers know the market.
## [param stolen_chance] = chance the item is stolen goods (cheap, but the watch is looking).
static func start_visit(member: Dictionary, seed_value: int, prices: Dictionary = {}, stolen_chance := 0.0) -> Dictionary:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	var p: Dictionary = CustomerData.PERSONALITIES[member["personality"]]
	var truth := ItemGenerator.Truth.GENUINE
	if rng.randf() < float(CurseData.SELLER_CHANCE.get(member["personality"], 0.0)):
		truth = ItemGenerator.Truth.CURSED
	elif rng.randf() < float(p["fake_chance"]):
		truth = ItemGenerator.Truth.FAKE
	var item := ItemGenerator.generate(rng.randi(), truth)
	var market: float = prices.get(item["claimed_material"], 1.0)
	item["stolen"] = rng.randf() < stolen_chance
	var discount: float = StoryData.STOLEN_DISCOUNT if item["stolen"] else 1.0
	var ask := maxi(5, snap5(item["claimed_value"] * market * float(p["markup"]) * discount))
	return {
		"role": "sell",
		"member": member,
		"item": item,
		"ask": ask,
		"first_ask": ask,
		"floor": ask * float(p["floor"]),
		"patience": p["patience"],
		"nervous": 0,
		"asked": {"origin": 0, "reason": 0},
		"mood": "angry" if member["grudge"] > 0 else "neutral",
		"exposed": false,
		"line_seed": rng.randi() % 1000,
		"insulted": false,
		"wrong_accused": false,
		"walked_out": false,
	}


## A buying visit: the member picks one shelved entry and opens with an offer.
## [param believed] = what they think it is worth (claimed metal at today's price).
## Returns {} when [param shelf] is empty.
static func start_buy_visit(member: Dictionary, seed_value: int, shelf: Array, believed: Callable) -> Dictionary:
	if shelf.is_empty():
		return {}
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	var b: Dictionary = MarketData.BUYERS[member["personality"]]
	var entry: Dictionary = shelf[rng.randi() % shelf.size()]
	var budget := float(believed.call(entry["item"])) * float(b["budget"])
	var fake: bool = entry["item"]["truth"] == ItemGenerator.Truth.FAKE
	var collector: bool = member["species"] in CurseData.COLLECTORS
	if collector and entry["item"]["truth"] == ItemGenerator.Truth.CURSED:
		budget *= CurseData.COLLECTOR_MULT
	return {
		"role": "buy",
		"member": member,
		"entry": entry,
		"item": entry["item"],
		"budget": budget,
		"offer": maxi(5, snap5_down(budget * float(b["first"]))),
		"patience": b["patience"],
		"spots_fake": fake and rng.randf() < float(b["detect"]),
		"collector": collector,
		"mood": "angry" if member["grudge"] > 0 else "neutral",
		"line_seed": rng.randi() % 1000,
		"nervous": 0,
		"exposed": false,
		"insulted": false,
		"wrong_accused": false,
		"walked_out": false,
		"first_ask": 0,
	}


## Rounds down to a multiple of 5 gold.
static func snap5_down(x: float) -> int:
	return int(floor(x / 5.0)) * 5


## Rounds up to a multiple of 5 gold.
static func snap5(x: float) -> int:
	return int(ceil(x / 5.0)) * 5


## True when this seller knowingly lies about this item.
static func is_lying(v: Dictionary) -> bool:
	var p: Dictionary = CustomerData.PERSONALITIES[v["member"]["personality"]]
	return p["knows_fake"] and v["item"]["truth"] == ItemGenerator.Truth.FAKE


static func greet(v: Dictionary) -> String:
	var m: Dictionary = v["member"]
	var line: String
	if m["grudge"] > 0:
		line = _pick(CustomerData.LINES["greet_grudge"], v, 0)
	elif m["affection"] >= 2:
		line = _pick(CustomerData.LINES["greet_friend"], v, 0)
	else:
		line = _pick(CustomerData.LINES["greet"][m["personality"]], v, 0)
	var kind: Dictionary = ItemData.KINDS[v["item"]["kind"]]
	var lore: Array = kind["lore"]
	var hot: String = " " + CustomerData.LINES["stolen_hint"][v["line_seed"] % CustomerData.LINES["stolen_hint"].size()] if v["item"].get("stolen", false) else ""
	return line + " " + CustomerData.LINES["claim"] % [ItemData.MATERIALS[v["item"]["claimed_material"]]["name"], kind["name"]] \
		+ hot + "\n" + lore[v["line_seed"] % lore.size()]


## Answer to "origin" or "reason". Liars change their story each time and get nervous.
static func answer(v: Dictionary, question: String) -> String:
	v["patience"] -= CustomerData.QUESTION_PATIENCE
	var lines: Array = CustomerData.LINES[question][v["member"]["personality"]]
	var line: String
	if is_lying(v):
		line = lines[(v["line_seed"] + v["asked"][question]) % lines.size()]
		v["nervous"] += 1
		if v["nervous"] >= 2:
			line = _pick(CustomerData.LINES["nervous"], v, v["nervous"]) + " " + line
			v["mood"] = "nervous"
	else:
		line = lines[v["line_seed"] % lines.size()]
	v["asked"][question] += 1
	return line + _patience_tail(v)


## The player points at a clue. [param valid] = the clue is real.
static func confront(v: Dictionary, valid: bool) -> String:
	var p: String = v["member"]["personality"]
	if valid:
		if v["exposed"]:
			return "알았다니까요... %d골드요." % v["ask"]
		v["exposed"] = true
		if v["item"]["truth"] == ItemGenerator.Truth.CURSED:
			# they knew; now they just want rid of it
			v["ask"] = maxi(5, snap5(v["item"]["value"] * CurseData.EXPOSED_ASK))
			v["floor"] = v["ask"] * 0.8
			v["mood"] = "nervous"
			return _pick(CustomerData.LINES["cursed_exposed"], v, 0) % v["ask"]
		v["ask"] = maxi(5, snap5(v["item"]["value"] * CustomerData.EXPOSED_ASK))
		v["floor"] = v["item"]["value"] * CustomerData.EXPOSED_FLOOR
		v["mood"] = "sad" if not is_lying(v) else "annoyed"
		return CustomerData.LINES["exposed"][p] % v["ask"]
	v["patience"] -= CustomerData.WRONG_ACCUSE_PATIENCE
	v["wrong_accused"] = true
	v["mood"] = "angry"
	return _pick(CustomerData.LINES["wrong"], v, 1) + _patience_tail(v)


## Appends a walkout or an annoyed remark when patience runs low.
static func _patience_tail(v: Dictionary) -> String:
	if v["patience"] <= 0:
		v["walked_out"] = true
		v["mood"] = "angry"
		return " " + _pick(CustomerData.LINES["walkout"], v, 2)
	if v["patience"] <= 2:
		if v["mood"] == "neutral":
			v["mood"] = "annoyed"
		return " " + _pick(CustomerData.LINES["annoyed"], v, v["patience"])
	return ""


static func _pick(lines: Array, v: Dictionary, salt: int) -> String:
	return lines[(v["line_seed"] + salt) % lines.size()]


static func _pick_personality(rng: RandomNumberGenerator) -> String:
	var total := 0.0
	for id in CustomerData.PERSONALITIES:
		total += CustomerData.PERSONALITIES[id]["weight"]
	var roll := rng.randf() * total
	for id in CustomerData.PERSONALITIES:
		roll -= CustomerData.PERSONALITIES[id]["weight"]
		if roll < 0.0:
			return id
	return "honest"
