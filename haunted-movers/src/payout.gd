## Payout math for one delivery. Pure: no nodes, so the smoke test calls it directly.
##
## base       = Σ value × durability            (a broken item, durability 0, pays 0)
## efficiency = cells used / cells in the box around them   (1.0 = packed with no gaps)
## bonus      = round(base × bonus_rate × efficiency)
## total      = round(base) + bonus


## Grid cells an AABB touches. Shrunk by 1 cm so an exact fit doesn't spill into the next cell.
static func cells_of(box: AABB, cell: float) -> Array[Vector3i]:
	var lo := ((box.position + Vector3.ONE * 0.01) / cell).floor()
	var hi := ((box.end - Vector3.ONE * 0.01) / cell).floor()
	var out: Array[Vector3i] = []
	for x in range(lo.x, hi.x + 1):
		for y in range(lo.y, hi.y + 1):
			for z in range(lo.z, hi.z + 1):
				out.append(Vector3i(x, y, z))
	return out


## items: [{value: float, durability: 0..1, aabb: AABB}] — only what is on the truck.
static func compute(items: Array, cell: float, bonus_rate: float) -> Dictionary:
	var base := 0.0
	var broken := 0
	var used := {}
	for it in items:
		if it.durability <= 0.0:
			broken += 1
		base += it.value * it.durability
		for c in cells_of(it.aabb, cell):
			used[c] = true
	var efficiency := 0.0
	if not used.is_empty():
		var lo: Vector3i = used.keys()[0]
		var hi := lo
		for c in used:
			lo = Vector3i(mini(lo.x, c.x), mini(lo.y, c.y), mini(lo.z, c.z))
			hi = Vector3i(maxi(hi.x, c.x), maxi(hi.y, c.y), maxi(hi.z, c.z))
		var span := hi - lo + Vector3i.ONE
		efficiency = float(used.size()) / (span.x * span.y * span.z)
	var bonus := roundi(base * bonus_rate * efficiency)
	return {loaded = items.size(), broken = broken, base = roundi(base), bonus = bonus,
		efficiency = efficiency, total = roundi(base) + bonus}
