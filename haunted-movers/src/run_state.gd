## One run = `days` days, each with a quota. Pure functions over a plain Dictionary
## (so it can be sent over RPC as-is and tested without nodes).
##
## phase: "day" (moving) → "shop" (between days) → "day" … → "over"
## Missing the quota `max_misses` times = bankrupt. Surviving the last day = cleared.


static func new_run(cfg: Dictionary) -> Dictionary:
	return {day = 1, money = 0, misses = 0, phase = "day", time_left = float(cfg.day_seconds),
		strength_mult = 1.0, reach_bonus = 0.0, tapes = 0, owned = [], cleared = false, text = ""}


static func quota(run: Dictionary, cfg: Dictionary) -> int:
	return int(cfg.quotas[mini(run.day, cfg.quotas.size()) - 1])


## Ends the current day with `earned` money. `why` explains a zero (time out, knocked out).
static func end_day(run: Dictionary, earned: int, cfg: Dictionary, why := "") -> Dictionary:
	var r := run.duplicate(true)
	var q := quota(r, cfg)
	r.money += earned
	var head := "%d일차 끝%s — %d원 벌음 (할당량 %d원)" % [r.day, (": " + why) if why != "" else "", earned, q]
	if earned < q:
		r.misses += 1
		head += " · 미달 %d/%d" % [r.misses, cfg.max_misses]
	if r.misses >= cfg.max_misses:
		r.phase = "over"
		r.text = head + "\n회사 파산! 런 종료"
	elif r.day >= cfg.quotas.size():
		r.phase = "over"
		r.cleared = true
		r.text = head + "\n런 클리어! 최종 %d원" % r.money
	else:
		r.phase = "shop"
		r.day += 1
		r.text = head
	return r


## Buys `item` (a key of cfg.shop) if affordable. Cart/rope once per run; tapes stack.
static func buy(run: Dictionary, item: String, cfg: Dictionary) -> bool:
	var it: Dictionary = cfg.shop[item]
	if run.phase != "shop" or run.money < it.price or (not it.get("stack", false) and item in run.owned):
		return false
	run.money -= int(it.price)
	run.owned.append(item)
	run.strength_mult += it.get("strength", 0.0)
	run.reach_bonus += it.get("reach", 0.0)
	run.tapes += int(it.get("tapes", 0))
	return true


static func start_day(run: Dictionary, cfg: Dictionary) -> void:
	run.phase = "day"
	run.time_left = float(cfg.day_seconds)
	run.text = ""
