class_name ItemGenerator
## Builds pawn items from a seed. Same seed (and same truth/kind arguments)
## always gives the same item.

## What an item really is.
enum Truth { GENUINE, FAKE, CURSED }

const TRUTH_IDS := ["genuine", "fake", "cursed"]


## Returns an item Dictionary:
## kind, claimed_material, true_material, truth (Truth), weight, expected_weight (grams),
## value / claimed_value (gold), mark ("" = none), mark_pos (unit coords), mark_blurred,
## seam_pos (unit coords, Vector2.INF = no seam), clues (Array of FAKE_CLUES ids),
## flaws (Array of {pos, len, angle}).
## [param truth] and [param kind] force those fields; -1 / "" pick from the seed.
static func generate(seed_value: int, truth: int = -1, kind: String = "") -> Dictionary:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	if truth < 0:
		truth = _pick_weighted(rng, ItemData.TRUTH_WEIGHTS, TRUTH_IDS)
	if kind == "":
		kind = ItemData.KINDS.keys()[rng.randi() % ItemData.KINDS.size()]

	var claimed: String = ItemData.GENUINE_MATERIALS[rng.randi() % ItemData.GENUINE_MATERIALS.size()]
	var claimed_mat: Dictionary = ItemData.MATERIALS[claimed]
	var volume: int = ItemData.KINDS[kind]["volume"]
	var expected := int(round(volume * float(claimed_mat["density"])))
	var item := {
		"kind": kind,
		"claimed_material": claimed,
		"true_material": claimed,
		"truth": truth,
		"expected_weight": expected,
		"weight": int(round(expected * (1.0 + rng.randf_range(-ItemData.WEIGHT_JITTER, ItemData.WEIGHT_JITTER)))),
		"mark": claimed_mat["mark"],
		"mark_pos": _mark_pos(rng, kind),
		"mark_blurred": false,
		"seam_pos": Vector2.INF,
		"clues": [],
	}

	if truth == Truth.FAKE:
		var fakes: Array = claimed_mat["fakes"]
		item["true_material"] = fakes[rng.randi() % fakes.size()]
		_add_clues(item, rng)

	if truth == Truth.CURSED:
		item["curse"] = CurseData.CURSES.keys()[rng.randi() % CurseData.CURSES.size()]

	item["value"] = _value(item["true_material"], volume)
	item["claimed_value"] = _value(claimed, volume)
	item["flaws"] = _make_flaws(rng)
	return item


## True when the item carries a clue the player can confront the seller with.
## [param kind] is "mark" (wrong or blurred mark), "weight", "seam", or "scratch"
## (touchstone: any fake shows another metal underneath).
static func has_clue(item: Dictionary, kind: String) -> bool:
	match kind:
		"mark":
			return "mark" in item["clues"] or "blur" in item["clues"]
		"scratch":
			return item["truth"] == Truth.FAKE
		"stolen":
			return item.get("stolen", false)
		"cursed":
			return item["truth"] == Truth.CURSED
	return kind in item["clues"]


## One line per clue, for the reveal after a deal.
static func clue_lines(item: Dictionary) -> Array:
	var claimed_mat: Dictionary = ItemData.MATERIALS[item["claimed_material"]]
	var lines := []
	for c in item["clues"]:
		match c:
			"weight":
				lines.append("무게 %dg — %s이면 %dg" % [item["weight"], claimed_mat["name"], item["expected_weight"]])
			"mark":
				lines.append("각인이 %s — %s은 %s" % [ItemData.MARKS[item["mark"]], claimed_mat["name"], ItemData.MARKS[claimed_mat["mark"]]])
			"blur":
				lines.append("각인이 두 겹으로 번짐 — 찍지 않고 흉내냄")
			"seam":
				lines.append("가장자리 도금이 벗겨짐")
	return lines


static func _value(material: String, volume: int) -> int:
	return int(round(float(ItemData.MATERIALS[material]["value"]) * volume / 60.0))


static func _pick_weighted(rng: RandomNumberGenerator, weights: Dictionary, ids: Array) -> int:
	var total := 0.0
	for id in ids:
		total += weights.get(id, 0.0)
	var roll := rng.randf() * total
	for i in ids.size():
		roll -= weights.get(ids[i], 0.0)
		if roll < 0.0:
			return i
	return 0


## One clue, sometimes two (never both kinds of mark clue).
static func _add_clues(item: Dictionary, rng: RandomNumberGenerator) -> void:
	var ids: Array = ItemData.FAKE_CLUES.keys()
	var first: String = ids[_pick_weighted(rng, ItemData.FAKE_CLUES, ids)]
	item["clues"].append(first)
	if rng.randf() < ItemData.SECOND_CLUE_CHANCE:
		var rest := ids.filter(func(c): return c != first and not (c in ["mark", "blur"] and first in ["mark", "blur"]))
		item["clues"].append(rest[rng.randi() % rest.size()])

	var claimed_mat: Dictionary = ItemData.MATERIALS[item["claimed_material"]]
	for c in item["clues"]:
		match c:
			"weight":
				var lighter: bool = ItemData.MATERIALS[item["true_material"]]["density"] < claimed_mat["density"]
				var off := rng.randf_range(ItemData.WEIGHT_OFF_RANGE.x, ItemData.WEIGHT_OFF_RANGE.y)
				item["weight"] = int(round(item["expected_weight"] * (1.0 - off if lighter else 1.0 + off)))
			"mark":
				var others: Array = ItemData.MARKS.keys().filter(func(m): return m != item["mark"])
				item["mark"] = others[rng.randi() % others.size()]
			"blur":
				item["mark_blurred"] = true
			"seam":
				item["seam_pos"] = _edge_pos(rng, item["kind"])


## Somewhere on the body of the item, so the player has to look for it.
static func _mark_pos(rng: RandomNumberGenerator, kind: String) -> Vector2:
	var k: Dictionary = ItemData.KINDS[kind]
	if k.has("mark_area"):
		var r: Rect2 = k["mark_area"]
		return r.position + Vector2(rng.randf() * r.size.x, rng.randf() * r.size.y)
	match kind:
		"medal":
			return Vector2.from_angle(rng.randf() * TAU) * rng.randf_range(0.0, 0.4)
		"goblet":
			return Vector2(rng.randf_range(-0.35, 0.35), rng.randf_range(-0.7, -0.4))
		"idol":
			return Vector2(rng.randf_range(-0.25, 0.25), rng.randf_range(-0.1, 0.35))
		"crown":
			return Vector2(rng.randf_range(-0.5, 0.5), rng.randf_range(0.05, 0.3))
		_:
			return Vector2(0.0, rng.randf_range(-0.55, 0.1))


static func _edge_pos(rng: RandomNumberGenerator, kind: String) -> Vector2:
	var k: Dictionary = ItemData.KINDS[kind]
	if k.has("edges"):
		return k["edges"][rng.randi() % k["edges"].size()]
	match kind:
		"medal":
			return Vector2.from_angle(rng.randf() * TAU) * 0.74
		"goblet":
			return Vector2(rng.randf_range(-0.6, 0.6), -0.84)
		"idol":
			return Vector2(0.38 * (1 if rng.randf() < 0.5 else -1), rng.randf_range(0.0, 0.4))
		"crown":
			return Vector2(rng.randf_range(-0.7, 0.7), 0.42)
		_:
			return Vector2(0.12 * (1 if rng.randf() < 0.5 else -1), rng.randf_range(-0.5, 0.2))


static func _make_flaws(rng: RandomNumberGenerator) -> Array:
	var flaws := []
	var count := rng.randi_range(ItemData.FLAW_COUNT.x, ItemData.FLAW_COUNT.y)
	for i in count:
		flaws.append({
			"pos": Vector2(rng.randf_range(-0.7, 0.7), rng.randf_range(-0.7, 0.7)),
			"len": rng.randf_range(0.05, 0.2),
			"angle": rng.randf_range(0.0, PI),
		})
	return flaws
