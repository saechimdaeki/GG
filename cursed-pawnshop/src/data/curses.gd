class_name CurseData
## Cursed items: which sellers bring them, who collects them, what they do at
## night, the curse gauge, and purification.

## Curse id -> name and the night event it causes (see RunState.end_day).
## Text placeholders: {item} = the cursed item, {n} = amount, {victim} = eaten item.
const CURSES := {
	"whisper": {"name": "속삭임의 저주", "event": "🕯 밤새 {item}이(가) 속삭였다. 잠을 설쳤다. (명성 -3, 저주 +5)"},
	"hunger": {"name": "굶주림의 저주", "event": "🕯 아침에 보니 {item} 옆의 {victim}이(가) 사라졌다. 이빨 자국만 남았다."},
	"bleed": {"name": "피흘림의 저주", "event": "🕯 금고에서 금화가 피처럼 흘러내렸다. 소지금 -{n}G"},
	"gaze": {"name": "응시의 저주", "event": "🕯 {item}이(가) 밤새 창밖을 노려봤다. 소문이 돌아 내일 손님 -1"},
}

## Chance a seller's item is cursed, by personality (checked before fakes).
const SELLER_CHANCE := {"honest": 0.12, "desperate": 0.3, "haggler": 0.1, "liar": 0.15}
## Once exposed by the candle, a seller just wants rid of it: ask = true value * this.
const EXPOSED_ASK := 0.4

## Collectors pay this multiple of the normal budget for a cursed item.
## Anyone else who buys one gets the curse and holds a grudge.
const COLLECTORS := ["faceless", "plague_doctor", "masked_noble", "moth_man", "bog_witch", "shadow"]
const COLLECTOR_MULT := 2.5
const GRUDGE_SOLD_CURSED := 2
const REP_SOLD_CURSED := -6

## Curse gauge: + per cursed item in stock each night, chance its curse fires,
## natural decay per night, and the madness ending at the top.
const GAUGE_PER_ITEM := 10
const EVENT_CHANCE := 0.35
const GAUGE_DECAY := 5
const GAUGE_MAX := 100
## From here the night report shouts a warning and how to bring it down.
const GAUGE_WARN := 50
const BLEED_RATIO := 0.1
const WHISPER_REP := -3
const WHISPER_GAUGE := 5

## Purifying an item removes its curse (and the collector premium) for gold.
const PURIFY_COST := 60
const PURIFY_GAUGE := -10
## Purifying salt from the night shop: gauge reduction per bag.
const SALT_GAUGE := -30
