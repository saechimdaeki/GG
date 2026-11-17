## One run = quota cycles of `cycle_days` days each, until the team misses one.
## Pure functions over a plain Dictionary (so it can be sent over RPC as-is and tested
## without nodes).
##
## phase: "day" (scavenging) → "shop" (between days) → "day" … → "over" (fired)
## Each day's pay goes into `haul` (not yet paid). On the last day of a cycle the haul is
## checked against the quota: met → it is paid into `money` and a bigger quota starts;
## missed → fired, run over. Everyone dying on a day loses the whole haul so far.


static func new_run(cfg: Dictionary) -> Dictionary:
	return {day = 1, cycle = 1, cycle_day = 1, haul = 0, money = int(cfg.start_money), phase = "day",
		time_left = float(cfg.day_seconds), seed = randi(), battery_bonus = 0.0, speed_bonus = 0.0,
		tapes = 0, flashes = 0, owned = [], text = "", site = "mansion"}


## Quota of the current cycle: grows by `quota_growth` every cycle.
static func quota(run: Dictionary, cfg: Dictionary) -> int:
	return roundi(cfg.quota_base * pow(cfg.quota_growth, run.cycle - 1))


## Days left in this cycle after today (0 = today is the deadline).
static func days_left(run: Dictionary, cfg: Dictionary) -> int:
	return int(cfg.cycle_days) - int(run.cycle_day)


## Ends the current day with `earned` money. `why` explains a zero. `wiped` = everyone
## died: the haul collected this cycle is lost.
static func end_day(run: Dictionary, earned: int, cfg: Dictionary, why := "", wiped := false) -> Dictionary:
	var r := run.duplicate(true)
	var q := quota(r, cfg)
	var head := "%d일차 끝%s — %d원" % [r.day, (": " + why) if why != "" else "", earned]
	if wiped:
		head += "\n전원 사망 — 이번 할당량 기간에 모은 %d원을 전부 잃음!" % r.haul
		r.haul = 0
	else:
		r.haul += earned
	head += "\n할당량 %d / %d원" % [r.haul, q]
	r.day += 1
	if r.cycle_day < int(cfg.cycle_days):
		r.cycle_day += 1
		r.phase = "shop"
		r.text = head + " · 마감까지 %d일" % (days_left(r, cfg) + 1)
	elif r.haul >= q:
		var paid: int = r.haul
		r.money += paid
		r.haul = 0
		r.cycle += 1
		r.cycle_day = 1
		r.phase = "shop"
		r.text = head + "\n할당량 달성! %d원 입금 · 다음 할당량 %d원" % [paid, quota(r, cfg)]
	else:
		r.phase = "over"
		r.text = head + "\n할당량 미달 — 해고! %d번째 할당량에서 끝 (총 %d일 버팀)" % [r.cycle, r.day - 1]
	return r


## Buys `item` (a key of cfg.shop) if affordable. Tools (flashlight, shoes, shovel, walkie)
## once per run; tapes and flashbangs stack.
static func buy(run: Dictionary, item: String, cfg: Dictionary) -> bool:
	var it: Dictionary = cfg.shop[item]
	if run.phase != "shop" or run.money < it.price or (not it.get("stack", false) and item in run.owned):
		return false
	run.money -= int(it.price)
	run.owned.append(item)
	run.battery_bonus += it.get("battery", 0.0)
	run.speed_bonus += it.get("speed", 0.0)
	run.tapes += int(it.get("tapes", 0))
	run.flashes = int(run.get("flashes", 0)) + int(it.get("flashes", 0))
	return true


## Shop: pick tomorrow's job site (a key of cfg.sites). Paid when the day starts. True if set.
static func choose_site(run: Dictionary, site: String, cfg: Dictionary) -> bool:
	if run.phase != "shop" or not cfg.sites.has(site) or run.money < int(cfg.sites[site].price):
		return false
	run.site = site
	return true


static func start_day(run: Dictionary, cfg: Dictionary) -> void:
	var sites: Dictionary = cfg.get("sites", {})
	var price: int = int(sites.get(run.get("site", "mansion"), {}).get("price", 0))
	if run.money < price:
		run.site = "mansion"  # can't afford the trip any more
	else:
		run.money -= price
	run.phase = "day"
	run.time_left = float(cfg.day_seconds)
	run.text = ""
