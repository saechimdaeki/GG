class_name Haggle
## One round of price talk. The seller's floor is hidden; counters walk the ask
## down toward it, bad offers burn patience.


## Selling to a buyer: the player names [param ask]. The buyer's budget is hidden;
## counters walk their offer up toward it, greedy asks burn patience.
## Returns {result: "accept" | "counter" | "walkout", line}. Mutates the visit.
static func respond_buy(v: Dictionary, ask: int) -> Dictionary:
	var lines: Dictionary = CustomerData.LINES
	if ask <= v["offer"] or ask <= v["budget"]:
		v["mood"] = "happy"
		return {"result": "accept", "line": lines["buy_accept"][v["line_seed"] % lines["buy_accept"].size()]}

	var insult: bool = ask > v["budget"] * MarketData.BUY_INSULT_RATIO
	if insult:
		v["insulted"] = true
	v["patience"] -= 2 if insult else 1
	if v["patience"] <= 0:
		v["walked_out"] = true
		v["mood"] = "angry"
		return {"result": "walkout", "line": lines["buy_walkout"][v["line_seed"] % lines["buy_walkout"].size()]}

	v["offer"] = mini(Customer.snap5_down(v["budget"]), Customer.snap5((v["offer"] + ask) / 2.0))
	if insult:
		v["mood"] = "angry"
		return {"result": "counter", "line": lines["buy_insult"][0] % [ask, v["offer"]]}
	if v["patience"] <= 2:
		v["mood"] = "annoyed"
	var pool: Array = lines["buy_counter"]
	return {"result": "counter", "line": pool[(v["line_seed"] + v["patience"]) % pool.size()] % v["offer"]}


## Buying from a seller: the player offers [param offer].
## Returns {result: "accept" | "counter" | "walkout", line}. Mutates the visit.
static func respond(v: Dictionary, offer: int) -> Dictionary:
	var lines: Dictionary = CustomerData.LINES
	if offer >= v["ask"] or offer >= v["floor"]:
		v["mood"] = "happy"
		return {"result": "accept", "line": lines["accept"][v["line_seed"] % lines["accept"].size()]}

	var insult: bool = offer < v["floor"] * CustomerData.INSULT_RATIO
	if insult:
		v["insulted"] = true
	v["patience"] -= 2 if insult else 1
	if v["patience"] <= 0:
		v["walked_out"] = true
		v["mood"] = "angry"
		return {"result": "walkout", "line": lines["walkout"][v["line_seed"] % lines["walkout"].size()]}

	v["ask"] = maxi(Customer.snap5(v["floor"]), Customer.snap5((v["ask"] + offer) / 2.0))
	if insult:
		v["mood"] = "angry"
	elif v["patience"] <= 2:
		v["mood"] = "annoyed"
	var pool: Array = lines["insult"] if insult else lines["counter"]
	return {"result": "counter", "line": pool[(v["line_seed"] + v["patience"]) % pool.size()] % v["ask"]}
