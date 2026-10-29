class_name Rewards
## Wave-clear / level-up reward pool (pick 1 of 3). Cards have a rarity; "perk" cards change a
## rule for the rest of the run (Game.perks, checked with Game.has_perk). Stat buffs stack.

## Rarity -> roll weight, card border color, label.
const RARITY := {
	"common": {"weight": 60, "color": Color(0.6, 0.7, 0.85), "label": "일반"},
	"rare": {"weight": 30, "color": Color(0.7, 0.45, 1.0), "label": "희귀"},
	"legendary": {"weight": 10, "color": Color(1.0, 0.75, 0.25), "label": "전설"},
}

const ALL := [
	# Traps and plain stats (common)
	{"id": "fire_trap", "title": "화염 함정", "desc": "화염 함정 해금\n불붙은 용사는 계속 타들어감", "rarity": "common"},
	{"id": "oil_trap", "title": "기름 함정", "desc": "기름 함정 해금\n기름 + 불 = 폭발!", "rarity": "common"},
	{"id": "gold", "title": "금고 털기", "desc": "골드 +80", "rarity": "common"},
	{"id": "buff_slime", "title": "슬라임 강화", "desc": "HP +40% 공격 +30%", "rarity": "common"},
	{"id": "buff_goblin", "title": "고블린 강화", "desc": "HP +40% 공격 +30%", "rarity": "common"},
	{"id": "buff_skeleton", "title": "해골궁수 강화", "desc": "HP +40% 공격 +30%", "rarity": "common"},
	{"id": "lord_melee", "title": "마왕의 손톱", "desc": "근접 데미지 +1", "rarity": "common"},
	{"id": "lord_hp", "title": "마왕의 심장", "desc": "최대 HP +4, 전부 회복", "rarity": "common"},
	{"id": "lord_bolts", "title": "마력 증폭", "desc": "마법탄 최대 +1", "rarity": "common"},
	{"id": "lava_pump", "title": "강화 스프링", "desc": "스프링 함정이 40% 더 멀리 날림", "rarity": "common", "perk": true},
	# Rule changers (rare)
	{"id": "kick_master", "title": "마왕의 발차기", "desc": "걷어차기 쿨다운 -40%\n사거리 증가", "rarity": "rare", "perk": true},
	{"id": "bowling", "title": "용사 볼링", "desc": "날아간 용사에 맞은 용사\n데미지 2배 + 1초 기절", "rarity": "rare", "perk": true},
	{"id": "wall_breaker", "title": "벽꽝 장인", "desc": "벽에 부딪힌 용사\n데미지 2배", "rarity": "rare", "perk": true},
	{"id": "burning_claws", "title": "지옥불 손톱", "desc": "마왕 근접 공격이\n용사에게 불을 붙임", "rarity": "rare", "perk": true},
	{"id": "sticky_slime", "title": "끈적 슬라임", "desc": "슬라임에게 맞은 용사\n2초간 느려짐", "rarity": "rare", "perk": true},
	{"id": "goblin_hunter", "title": "고블린 사냥본능", "desc": "고블린이 느려지거나 불타는\n용사에게 2배 데미지", "rarity": "rare", "perk": true},
	{"id": "skill_storm", "title": "스킬: 마력 폭풍", "desc": "사방으로 마법탄 16발", "rarity": "rare"},
	{"id": "skill_fear", "title": "스킬: 공포의 포효", "desc": "주변 용사 2.5초 기절", "rarity": "rare"},
	{"id": "skill_imps", "title": "스킬: 임프 소환", "desc": "임프 3마리 12초", "rarity": "rare"},
	# Run-defining (legendary)
	{"id": "oil_rain", "title": "기름 비", "desc": "모든 용사가\n기름 범벅으로 입장", "rarity": "legendary", "perk": true},
	{"id": "chain_fire", "title": "연쇄 폭발", "desc": "기름 폭발에 휘말린 용사에게\n불이 옮겨붙음", "rarity": "legendary", "perk": true},
	{"id": "lord_dash", "title": "그림자 발걸음", "desc": "대시 쿨다운 -25%\n대시가 용사를 걷어참", "rarity": "legendary"},
]

const BUFF := Vector2(0.4, 0.3)
const GOLD_REWARD := 80


## Returns `count` distinct rewards still available to this run, drawn by rarity weight.
static func roll(count: int, game: Game) -> Array:
	var pool := ALL.filter(func(r: Dictionary) -> bool:
		if r.id == "fire_trap":
			return not game.unlocked_traps.has("fire")
		if r.id == "oil_trap":
			return not game.unlocked_traps.has("oil")
		if r.id.begins_with("skill_"):
			return not game.lord.skills.has(r.id.trim_prefix("skill_"))
		if r.get("perk", false):
			return not game.perks.has(r.id)
		return true)
	var out: Array = []
	while out.size() < count and not pool.is_empty():
		var total := 0
		for r: Dictionary in pool:
			total += int(RARITY[r.rarity].weight)
		var pick := randi() % total
		for r: Dictionary in pool:
			pick -= int(RARITY[r.rarity].weight)
			if pick < 0:
				out.append(r)
				pool.erase(r)
				break
	return out


## Applies the reward with this id to the run.
static func apply(id: String, game: Game) -> void:
	var card: Dictionary = {}
	for r: Dictionary in ALL:
		if r.id == id:
			card = r
	if card.get("perk", false):
		game.perks[id] = true
		return
	match id:
		"gold":
			game.gold += GOLD_REWARD
		"fire_trap":
			game.unlocked_traps.append("fire")
		"oil_trap":
			game.unlocked_traps.append("oil")
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
			game.lord.dash_kicks = true
		_:
			if id.begins_with("skill_"):
				game.lord.learn(id.trim_prefix("skill_"))
			elif id.begins_with("buff_"):
				game.buff_monster(id.trim_prefix("buff_"), BUFF)
