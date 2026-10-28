class_name Rewards
## Wave-clear reward pool (pick 1 of 3). Monster buffs stack for the rest of the run.

const ALL := [
	{"id": "gold", "title": "금고 털기", "desc": "골드 +80"},
	{"id": "fire_trap", "title": "화염 함정", "desc": "강한 함정 해금"},
	{"id": "buff_slime", "title": "슬라임 강화", "desc": "HP +40% 공격 +30%"},
	{"id": "buff_goblin", "title": "고블린 강화", "desc": "HP +40% 공격 +30%"},
	{"id": "buff_skeleton", "title": "해골궁수 강화", "desc": "HP +40% 공격 +30%"},
	{"id": "lord_melee", "title": "마왕의 손톱", "desc": "근접 데미지 +1"},
	{"id": "lord_bolts", "title": "마력 증폭", "desc": "마법탄 최대 +1"},
	{"id": "lord_dash", "title": "그림자 발걸음", "desc": "대시 쿨다운 -25%"},
]

const BUFF := Vector2(0.4, 0.3)
const GOLD_REWARD := 80


## Returns `count` distinct rewards still available to this run.
static func roll(count: int, game: Game) -> Array:
	var pool := ALL.filter(func(r: Dictionary) -> bool:
		return r.id != "fire_trap" or not game.unlocked_traps.has("fire"))
	pool.shuffle()
	return pool.slice(0, count)


## Applies the reward with this id to the run.
static func apply(id: String, game: Game) -> void:
	match id:
		"gold":
			game.gold += GOLD_REWARD
		"fire_trap":
			game.unlocked_traps.append("fire")
		"lord_melee":
			game.lord.melee_damage += 1
		"lord_bolts":
			game.lord.max_bolts += 1
			game.lord.bolts = game.lord.max_bolts
		"lord_dash":
			game.lord.dash_cooldown *= 0.75
		_:
			if id.begins_with("buff_"):
				game.buff_monster(id.trim_prefix("buff_"), BUFF)
