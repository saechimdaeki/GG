class_name ItemData
## Static item tables: materials, item kinds, marks and the generator's tuning numbers.
## Everything the player can read off an item is derived from these.

## Material id -> display name, density (grams per volume unit), color, the mark (각인)
## a genuine piece carries, the materials that pass for it, and value (gold per 60 volume).
const MATERIALS := {
	"gold": {"name": "금", "density": 3.0, "color": Color(1.0, 0.8, 0.28), "mark": "sun", "fakes": ["brass", "lead"], "value": 100},
	"silver": {"name": "은", "density": 2.0, "color": Color(0.8, 0.85, 0.92), "mark": "moon", "fakes": ["tin", "lead"], "value": 50},
	"bronze": {"name": "청동", "density": 1.5, "color": Color(0.78, 0.5, 0.28), "mark": "flame", "fakes": ["iron", "brass"], "value": 25},
	"brass": {"name": "황동", "density": 2.7, "color": Color(0.88, 0.72, 0.3), "mark": "", "fakes": [], "value": 10},
	"tin": {"name": "주석", "density": 1.2, "color": Color(0.66, 0.68, 0.72), "mark": "", "fakes": [], "value": 6},
	"lead": {"name": "납", "density": 2.4, "color": Color(0.45, 0.47, 0.53), "mark": "", "fakes": [], "value": 5},
	"iron": {"name": "무쇠", "density": 1.2, "color": Color(0.35, 0.33, 0.35), "mark": "", "fakes": [], "value": 4},
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
