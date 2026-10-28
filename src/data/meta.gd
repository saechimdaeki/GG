class_name Meta
## Permanent upgrades kept between runs. Runs earn souls (영혼석); souls buy levels on the
## game-over screen. Saved in save_path (ConfigFile).

## Overridable so tests do not touch the real save.
static var save_path := "user://meta.cfg"

const SOULS_PER_WAVE := 4
const WIN_BONUS := 25

## price = cost + step * current level. value = bonus per level.
const UPGRADES := {
	"start_gold": {"name": "금고 상속", "desc": "시작 골드 +%d", "value": 30, "max": 5, "cost": 10, "step": 8},
	"core_hp": {"name": "단단한 코어", "desc": "코어 HP +%d", "value": 4, "max": 5, "cost": 12, "step": 8},
	"lord_hp": {"name": "마왕의 체력", "desc": "마왕 HP +%d", "value": 2, "max": 5, "cost": 12, "step": 10},
	"lord_atk": {"name": "마왕의 힘", "desc": "근접 데미지 +%d", "value": 1, "max": 3, "cost": 20, "step": 15},
	"fire_start": {"name": "화염 비전", "desc": "화염 함정 해금 상태로 시작", "value": 1, "max": 1, "cost": 30, "step": 0},
}


## {"souls": int, "levels": {id: int}} from disk (empty on first run).
static func load_data() -> Dictionary:
	var cfg := ConfigFile.new()
	cfg.load(save_path)  # ponytail: missing/corrupt file = fresh save
	var levels := {}
	for id: String in UPGRADES:
		levels[id] = int(cfg.get_value("levels", id, 0))
	return {"souls": int(cfg.get_value("meta", "souls", 0)), "levels": levels}


static func save_data(data: Dictionary) -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("meta", "souls", data.souls)
	for id: String in data.levels:
		cfg.set_value("levels", id, data.levels[id])
	cfg.save(save_path)


## Souls a run earns: waves cleared plus a win bonus.
static func souls_for(waves_cleared: int, won: bool) -> int:
	return waves_cleared * SOULS_PER_WAVE + (WIN_BONUS if won else 0)


## Total bonus from an upgrade at its current level.
static func bonus(data: Dictionary, id: String) -> int:
	return int(data.levels.get(id, 0)) * int(UPGRADES[id].value)


## Price of the next level, or -1 when maxed.
static func price(data: Dictionary, id: String) -> int:
	var up: Dictionary = UPGRADES[id]
	var lv := int(data.levels.get(id, 0))
	return -1 if lv >= int(up.max) else int(up.cost) + int(up.step) * lv


## Buys one level if affordable; saves. Returns true on success.
static func buy(data: Dictionary, id: String) -> bool:
	var p := price(data, id)
	if p < 0 or data.souls < p:
		return false
	data.souls -= p
	data.levels[id] = int(data.levels.get(id, 0)) + 1
	save_data(data)
	return true
