class_name ItemData
## Static item tables: materials, item kinds, marks and the generator's tuning numbers.
## Everything the player can read off an item is derived from these.

## Material id -> display name, density (grams per volume unit), color, the mark (각인)
## a genuine piece carries, the materials that pass for it, and value (gold per 60 volume).
const MATERIALS := {
	"gold": {"name": "금", "density": 3.0, "color": Color(1.0, 0.8, 0.28), "mark": "sun", "fakes": ["brass", "lead"], "value": 150},
	"silver": {"name": "은", "density": 2.0, "color": Color(0.8, 0.85, 0.92), "mark": "moon", "fakes": ["tin", "lead"], "value": 75},
	"bronze": {"name": "청동", "density": 1.5, "color": Color(0.78, 0.5, 0.28), "mark": "flame", "fakes": ["iron", "brass"], "value": 40},
	"brass": {"name": "황동", "density": 2.7, "color": Color(0.88, 0.72, 0.3), "mark": "", "fakes": [], "value": 15},
	"tin": {"name": "주석", "density": 1.2, "color": Color(0.66, 0.68, 0.72), "mark": "", "fakes": [], "value": 9},
	"lead": {"name": "납", "density": 2.4, "color": Color(0.45, 0.47, 0.53), "mark": "", "fakes": [], "value": 8},
	"iron": {"name": "무쇠", "density": 1.2, "color": Color(0.35, 0.33, 0.35), "mark": "", "fakes": [], "value": 6},
}

## Materials a genuine item can be made of (the ones that have a mark).
const GENUINE_MATERIALS := ["gold", "silver", "bronze"]

## Mark id -> display name. A fake that gets a wrong mark draws one of the others.
const MARKS := {"sun": "태양", "moon": "달", "flame": "불꽃", "eye": "눈"}

## Item kind id -> display name, volume (weight = volume * density), and what you
## notice when it lands on the counter (one picked per visit).
const KINDS := {
	"medal": {"name": "속삭이는 부적", "volume": 60, "lore": ["(부적에 귀를 대면 바닷소리 같은 속삭임이 들린다)", "(부적 가운데 문양이 천천히 회전하는 것 같다)"]},
	"goblet": {"name": "눈 달린 성배", "volume": 100, "lore": ["(성배 받침에 박힌 눈알이 당신을 따라 움직인다)", "(성배 안쪽이 젖어 있다. 비어 있는데도)"]},
	"dagger": {"name": "의식용 단검", "volume": 80, "lore": ["(칼날에 말라붙은 무언가가 있다)", "(손잡이가 미지근하다. 누가 방금까지 쥐고 있던 것처럼)"]},
	"idol": {"name": "촉수 우상", "volume": 120, "lore": ["(우상의 촉수가 아까와 다른 방향을 향하고 있다)", "(우상을 오래 보면 머리가 아프다)"]},
	"crown": {"name": "심해 왕관", "volume": 90, "lore": ["(왕관에서 짠내와 썩은 해초 냄새가 난다)", "(왕관 안쪽에 비늘이 몇 장 붙어 있다)"]},
	# Kinds below carry their own mark area and seam points (unit coords, item fits a 2x2 box).
	"ring": {"name": "촉수 인장 반지", "volume": 30, "lore": ["(반지의 촉수가 손가락을 조이려는 것 같다)", "(보석이 빠진 자리에서 미지근한 바람이 분다)"],
		"mark_area": Rect2(-0.3, 0.35, 0.6, 0.25), "edges": [Vector2(-0.55, 0.35), Vector2(0.55, 0.35), Vector2(0.0, 0.75)]},
	"brooch": {"name": "감은 눈 브로치", "volume": 40, "lore": ["(브로치의 눈꺼풀이 아주 살짝 떨렸다)", "(눈을 뜨면 무엇이 보일지 생각하지 않기로 한다)"],
		"mark_area": Rect2(-0.45, -0.15, 0.9, 0.3), "edges": [Vector2(-0.88, 0.0), Vector2(0.88, 0.0), Vector2(0.0, 0.55)]},
	"censer": {"name": "녹색 연기 향로", "volume": 110, "lore": ["(향로 구멍에서 초록 연기가 저절로 새어 나온다)", "(연기가 잠깐 얼굴 모양이 됐다)"],
		"mark_area": Rect2(-0.25, 0.1, 0.5, 0.4), "edges": [Vector2(-0.48, 0.3), Vector2(0.48, 0.3), Vector2(0.0, 0.75)]},
	"bell": {"name": "비명 지르는 종", "volume": 90, "lore": ["(손잡이의 얼굴이 소리 없이 비명을 지르고 있다)", "(흔들지 않았는데 종이 한 번 울렸다)"],
		"mark_area": Rect2(-0.25, 0.1, 0.5, 0.45), "edges": [Vector2(-0.55, 0.78), Vector2(0.55, 0.78), Vector2(0.0, 0.82)]},
	"candlestick": {"name": "말라붙은 손 촛대", "volume": 100, "lore": ["(촛대의 손가락이 아까보다 하나 더 접혀 있다)", "(손바닥에 손금이 그대로 남아 있다)"],
		"mark_area": Rect2(-0.25, 0.15, 0.45, 0.35), "edges": [Vector2(-0.45, 0.25), Vector2(0.4, 0.3), Vector2(-0.3, -0.6)]},
	"pocket_watch": {"name": "열세 시 회중시계", "volume": 50, "lore": ["(시계판에 숫자가 열셋이다)", "(바늘들이 제각각 다른 시간을 가리킨다)"],
		"mark_area": Rect2(-0.3, -0.15, 0.6, 0.55), "edges": [Vector2(-0.78, 0.1), Vector2(0.78, 0.1), Vector2(0.0, 0.85)]},
	"key": {"name": "눈알 열쇠", "volume": 45, "lore": ["(열쇠 머리의 눈이 문을 찾듯 두리번거린다)", "(이 열쇠에 맞는 문은 가게 지하에 있을 것 같다)"],
		"mark_area": Rect2(-0.08, -0.1, 0.16, 0.45), "edges": [Vector2(-0.12, 0.1), Vector2(0.12, 0.3), Vector2(0.0, 0.85)]},
}

## Relative chance of genuine / fake / cursed when nothing forces the truth.
## Cursed stays 0 until the candle (story 004) gives the player a way to find it.
const TRUTH_WEIGHTS := {"genuine": 0.5, "fake": 0.5, "cursed": 0.0}

## A genuine item's (or a weight-matched fake's) weight is off by at most this fraction.
const WEIGHT_JITTER := 0.02
## When a fake's clue is its weight, it is off by a fraction in this range.
const WEIGHT_OFF_RANGE := Vector2(0.05, 0.14)

## Clue kinds a fake can carry, with relative chance. A fake gets one, sometimes two.
## weight = 무게가 다름, mark = 다른 각인, blur = 번진 각인, seam = 도금이 벗겨진 자리
const FAKE_CLUES := {"weight": 1.0, "mark": 0.8, "blur": 0.6, "seam": 0.8}
const SECOND_CLUE_CHANCE := 0.25

## How much of a fake's real metal color shows through its plating (0 = none, 1 = all).
const PLATING_SHOW_THROUGH := 0.06

## Scratches drawn on every item. Genuine heirlooms are worn too, so these are not clues.
const FLAW_COUNT := Vector2i(2, 7)

## Selling price of a bought item, as a multiple of its true value.
const RESALE_MULTIPLIER := 1.3
