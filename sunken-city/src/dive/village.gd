class_name Village
extends RefCounted
## The town that grows on drained ground: plots, buildings, gear levels and resident
## requests. Pure logic; every purchase takes from the inventory dictionary it is given.

var plots: Array = []        # {pos: Vector2, building: String} — "" while empty
var levels := {"pick": 0, "tank": 0, "suit": 0}
var requests_done := {}      # resident index -> true


func _init(plot_positions: Array[Vector2]) -> void:
	for p in plot_positions:
		plots.append({"pos": p, "building": ""})


func has(kind: String) -> bool:
	return plots.any(func(p): return p["building"] == kind)


func unbuilt() -> Array:
	return VillageData.BUILDINGS.keys().filter(func(k): return not has(k))


func build(plot: int, kind: String, inventory: Dictionary) -> bool:
	if plots[plot]["building"] != "" or has(kind):
		return false
	if not pay(VillageData.BUILDINGS[kind]["cost"], inventory):
		return false
	plots[plot]["building"] = kind
	return true


## Next level's cost, or {} when maxed.
func upgrade_cost(kind: String) -> Dictionary:
	var costs: Array = VillageData.UPGRADES[kind]["costs"]
	return costs[levels[kind]] if levels[kind] < costs.size() else {}


func upgrade(kind: String, inventory: Dictionary) -> bool:
	var cost := upgrade_cost(kind)
	if not has("workshop") or cost.is_empty() or not pay(cost, inventory):
		return false
	levels[kind] += 1
	return true


func residents() -> Array:
	return VillageData.RESIDENTS if has("lodge") else []


func fulfil(resident: int, inventory: Dictionary) -> bool:
	var r: Dictionary = VillageData.RESIDENTS[resident]
	if not has("lodge") or requests_done.has(resident) or not pay(r["need"], inventory):
		return false
	for k in r["reward"]:
		inventory[k] += r["reward"][k]
	requests_done[resident] = true
	return true


static func can_pay(cost: Dictionary, inventory: Dictionary) -> bool:
	return cost.keys().all(func(k): return inventory.get(k, 0) >= cost[k])


static func pay(cost: Dictionary, inventory: Dictionary) -> bool:
	if not can_pay(cost, inventory):
		return false
	for k in cost:
		inventory[k] -= cost[k]
	return true


static func cost_text(cost: Dictionary) -> String:
	const LABELS := {"scrap": "황동 조각", "pipe": "파이프", "gear": "톱니"}
	var parts := []
	for k in cost:
		parts.append("%s %d" % [LABELS[k], cost[k]])
	return ", ".join(parts)
