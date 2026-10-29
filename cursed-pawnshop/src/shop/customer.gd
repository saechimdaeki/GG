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


## A visit: the member, their item, the price talk and their mood.
static func start_visit(member: Dictionary, seed_value: int) -> Dictionary:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	var p: Dictionary = CustomerData.PERSONALITIES[member["personality"]]
	var truth := ItemGenerator.Truth.FAKE if rng.randf() < float(p["fake_chance"]) else ItemGenerator.Truth.GENUINE
	var item := ItemGenerator.generate(rng.randi(), truth)
	var ask := maxi(5, snap5(item["claimed_value"] * float(p["markup"])))
	return {
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
	return line + " " + CustomerData.LINES["claim"] % [ItemData.MATERIALS[v["item"]["claimed_material"]]["name"], kind["name"]] \
		+ "\n" + lore[v["line_seed"] % lore.size()]


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
