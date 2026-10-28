class_name Arsenal
## Demon lord weapons (picked up from fallen heroes) and Q/E skills (from rewards).

## damage, range (px), arc (deg, 360 = all around), cooldown (s), knockback, shake.
## shape/len/color only drive how the weapon is drawn in the lord's hand.
const WEAPONS := {
	"claws": {"name": "마왕의 손톱", "damage": 3, "range": 34.0, "arc": 120.0, "cooldown": 0.3,
		"knockback": 1.0, "shake": 4.0, "shape": "claw", "len": 8.0, "color": Color(0.95, 0.85, 0.6)},
	"greatsword": {"name": "용사의 대검", "damage": 7, "range": 46.0, "arc": 170.0, "cooldown": 0.65,
		"knockback": 1.8, "shake": 7.0, "shape": "blade", "len": 18.0, "color": Color(0.85, 0.88, 0.95)},
	"spear": {"name": "기사의 창", "damage": 4, "range": 64.0, "arc": 40.0, "cooldown": 0.35,
		"knockback": 1.2, "shake": 4.0, "shape": "spear", "len": 22.0, "color": Color(0.8, 0.7, 0.5)},
	"daggers": {"name": "도적의 쌍단검", "damage": 2, "range": 28.0, "arc": 140.0, "cooldown": 0.13,
		"knockback": 0.5, "shake": 2.0, "shape": "blade", "len": 7.0, "color": Color(0.7, 0.95, 0.8)},
	"hammer": {"name": "성기사의 망치", "damage": 6, "range": 40.0, "arc": 360.0, "cooldown": 0.8,
		"knockback": 2.2, "shake": 9.0, "shape": "hammer", "len": 14.0, "color": Color(1.0, 0.8, 0.3)},
}

## Chance a normal hero drops a weapon on death; the boss always drops one.
const DROP_CHANCE := 0.12

## cooldown (s). Effects are implemented in DemonLord._cast().
const SKILLS := {
	"hellfire": {"name": "지옥불", "desc": "주변 폭발 데미지 6", "cooldown": 7.0, "color": Color(1.0, 0.4, 0.1)},
	"storm": {"name": "마력 폭풍", "desc": "사방으로 마법탄 16발", "cooldown": 9.0, "color": Color(0.7, 0.4, 1.0)},
	"fear": {"name": "공포의 포효", "desc": "주변 용사 2.5초 기절", "cooldown": 11.0, "color": Color(0.9, 0.2, 0.3)},
	"imps": {"name": "임프 소환", "desc": "임프 3마리 12초 소환", "cooldown": 15.0, "color": Color(1.0, 0.5, 0.3)},
}
