extends SceneTree
## Smoke check: generates every item kind, then a candle-using bot plays two full
## 28-day runs through the story, trading, debt and curse systems. Prints PASS when
## every item is sane and both runs reach an ending; any script error fails CI.
##   godot --headless --path . -s res://tests/smoke/run_check.gd

var _fails := 0


func _initialize() -> void:
	_check_items()
	for seed_value in [1, 2]:
		_play(seed_value)
	print("PASS" if _fails == 0 else "FAIL %d" % _fails)
	quit(0 if _fails == 0 else 1)


func _check_items() -> void:
	for kind in ItemData.KINDS:
		for s in 50:
			for truth in [ItemGenerator.Truth.GENUINE, ItemGenerator.Truth.FAKE, ItemGenerator.Truth.CURSED]:
				var it := ItemGenerator.generate(s, truth, kind)
				if truth == ItemGenerator.Truth.FAKE and it["clues"].is_empty():
					_fail("fake %s without a clue" % kind)
				if truth == ItemGenerator.Truth.CURSED and not it.has("curse"):
					_fail("cursed %s without a curse" % kind)


## Exposes fakes/curses, buys at the floor, sells at the tag, keeps relics, purifies.
func _play(seed_value: int) -> void:
	var run := RunState.new(seed_value)
	while run.ended == "":
		run.start_day()
		for b in run.beats_today():
			var choice: Dictionary = b["choices"].filter(func(c): return run.check(c.get("if", {})))[0]
			run.apply_effects(choice.get("effects", {}))
		run.open_shop()
		while true:
			var v := run.next_visit()
			if v.is_empty():
				break
			if v["role"] == "sell":
				if v["item"]["truth"] != ItemGenerator.Truth.GENUINE:
					Customer.confront(v, true)
				var bought := false
				for i in 6:
					if v["walked_out"]:
						break
					var offer := mini(run.money, int(v["floor"]) + 1)
					var r := Haggle.respond(v, offer)
					if r["result"] == "accept":
						bought = true
						run.finish_visit(v, true, offer)
						break
				if not bought:
					run.finish_visit(v, false, 0)
			else:
				var ask: int = v["entry"]["tag"]
				var sold := false
				for i in 6:
					var r := Haggle.respond_buy(v, ask)
					if r["result"] == "accept":
						sold = true
						run.finish_sale(v, true, ask)
						break
					if r["result"] == "walkout":
						break
					ask = maxi(v["offer"], int(ask * 0.9))
				if not sold:
					run.finish_sale(v, false, 0)
		run.end_day()
		for e in run.inventory.duplicate():
			if e["item"]["truth"] == ItemGenerator.Truth.CURSED:
				run.purify(e)
			run.set_shelved(e, true)
		if run.payday:
			run.pay_debt()
		if run.day > 40:
			_fail("seed %d never ended" % seed_value)
			return
	print("seed %d: %s on day %d, money %d" % [seed_value, run.ending()["title"], run.day - 1, run.money])


func _fail(msg: String) -> void:
	_fails += 1
	print("FAIL: ", msg)
