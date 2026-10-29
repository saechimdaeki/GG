class_name EventData
## Morning events: revenge from grudging monsters, gifts from friendly ones, news.

## Revenge id -> line ({name} = monster, {n} = amount) and effect.
## money_ratio: share of money lost. money_flat: fixed loss. customers: change to today's count.
const REVENGE := {
	"rob": {"text": "💢 밤사이 {name}{ga} 금고를 털어갔다! 소지금 -{n}G", "money_ratio": 0.25},
	"window": {"text": "💢 아침에 와보니 유리창이 박살. 문틈에 {name}의 쪽지: '두고 보자 했지?' 수리비 -{n}G", "money_flat": 40},
	"rumor": {"text": "💢 {name}{ga} 골목에 '저 가게 사기꾼'이라는 소문을 냈다. 오늘 손님 -2", "customers": -2},
}

## Gift id -> line and effect. gold: random amount in range. tip: names a liar coming today.
## {ga} = 이/가 after the name.
const GIFT := {
	"gold": {"text": "🎁 {name}{ga} 고맙다며 금화 주머니를 두고 갔다. +{n}G", "gold": Vector2i(30, 70)},
	"tip": {"text": "🎁 {name}의 귀띔: '오늘 오는 {liar}, 사기꾼이에요. 조심하세요.'"},
}

## Flavor news for the morning letter.
const NEWS := [
	"골목에 사흘째 비가 그치지 않는다.",
	"경비대가 장물을 단속한다는 소문이 돈다.",
	"옆 골목 대장간이 문을 닫았다. 무쇠 값이 떨어지려나.",
	"마왕성에서 연회가 열린다. 금붙이 수요가 늘 것 같다.",
	"고리대금업자 바알의 편지: '이자, 잊지 않았겠지?'",
	"밤마다 골목에서 이상한 종소리가 들린다고 한다.",
]
