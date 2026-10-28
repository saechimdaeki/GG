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
	{"id": "skill_storm", "title": "스킬: 마력 폭풍", "desc": "사방으로 마법탄 16발"},
	{"id": "skill_fear", "title": "스킬: 공포의 포효", "desc": "주변 용사 2.5초 기절"},
	{"id": "skill_imps", "title": "스킬: 임프 소환", "desc": "임프 3마리 12초"},
	{"id": "lord_hp", "title": "마왕의 심장", "desc": "최대 HP +4, 전부 회복"},
]

const BUFF := Vector2(0.4, 0.3)
const GOLD_REWARD := 80


## Returns `count` distinct rewards still available to this run.
static func roll(count: int, game: Game) -> Array:
	var pool := ALL.filter(func(r: Dictionary) -> bool:
		if r.id == "fire_trap":
			return not game.unlocked_traps.has("fire")
		if r.id.begins_with("skill_"):
			return not game.lord.skills.has(r.id.trim_prefix("skill_"))
		return true)
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
			game.lord.melee_bonus += 1
		"lord_hp":
			game.lord.max_health += 4
			game.lord.health = game.lord.max_health
		"lord_bolts":
			game.lord.max_bolts += 1
			game.lord.bolts = game.lord.max_bolts
		"lord_dash":
			game.lord.dash_cooldown *= 0.75
		_:
			if id.begins_with("skill_"):
				game.lord.learn(id.trim_prefix("skill_"))
			elif id.begins_with("buff_"):
				game.buff_monster(id.trim_prefix("buff_"), BUFF)
