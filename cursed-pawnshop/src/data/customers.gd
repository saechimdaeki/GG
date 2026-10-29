class_name CustomerData
## Monster customers: species, personalities, haggling numbers, grudge/affection
## thresholds and every line they say.

const ROSTER_SIZE := 12
const DAY_CUSTOMERS := 5
const START_MONEY := 400

## Species id -> display name, skin color, name pool, one-line look (for the art prompts).
const SPECIES := {
	"deep_one": {"name": "어인", "skin": Color(0.32, 0.52, 0.48), "names": ["브락", "글루브", "오스"]},
	"faceless": {"name": "얼굴 없는 수도사", "skin": Color(0.85, 0.82, 0.76), "names": ["무명", "침묵", "회색 손"]},
	"tentacle": {"name": "촉수 상인", "skin": Color(0.55, 0.38, 0.62), "names": ["즐락", "쓰르", "옴"]},
	"eyes": {"name": "눈알 군체", "skin": Color(0.72, 0.42, 0.42), "names": ["백안", "응시", "깜빡"]},
	"ghoul": {"name": "구울", "skin": Color(0.5, 0.52, 0.46), "names": ["갉작", "무덤지기", "헐벗"]},
	"drowned": {"name": "익사한 선원", "skin": Color(0.62, 0.74, 0.78), "names": ["노르", "바다안개", "짠물"]},
	"spider": {"name": "거미 귀부인", "skin": Color(0.88, 0.86, 0.9), "names": ["실크 부인", "아라크", "그물"]},
	"goblin": {"name": "고블린", "skin": Color(0.47, 0.6, 0.3), "names": ["그릭", "스닉", "닙스"]},
}

## Robe / coat colors: muted, grim.
const CLOTHES := [Color(0.22, 0.14, 0.24), Color(0.16, 0.2, 0.26), Color(0.3, 0.18, 0.14), Color(0.18, 0.22, 0.17), Color(0.12, 0.1, 0.12)]

## Personality id -> how they behave.
## fake_chance: chance their item is fake. knows_fake: lies about it (nervous, story changes).
## markup: ask = claimed value * markup. floor: lowest they take, as a fraction of the first ask.
## patience: questions and bad offers spend it; 0 = they storm out. sweaty: always sweats.
const PERSONALITIES := {
	"honest": {"name": "정직", "weight": 0.35, "fake_chance": 0.3, "knows_fake": false, "markup": 1.0, "floor": 0.75, "patience": 6, "sweaty": false},
	"desperate": {"name": "급함", "weight": 0.2, "fake_chance": 0.2, "knows_fake": false, "markup": 0.85, "floor": 0.5, "patience": 3, "sweaty": true},
	"haggler": {"name": "깐깐함", "weight": 0.2, "fake_chance": 0.25, "knows_fake": false, "markup": 1.4, "floor": 0.7, "patience": 8, "sweaty": false},
	"liar": {"name": "사기꾼", "weight": 0.25, "fake_chance": 0.85, "knows_fake": true, "markup": 1.3, "floor": 0.85, "patience": 5, "sweaty": false},
}

## After a fake is exposed the seller drops to these multiples of the item's true value.
const EXPOSED_ASK := 1.6
const EXPOSED_FLOOR := 1.0
const QUESTION_PATIENCE := 1
const WRONG_ACCUSE_PATIENCE := 2
## An offer below floor * this is an insult: double patience loss and a grudge.
const INSULT_RATIO := 0.6
## A deal at or above ask * this counts as fair and earns affection.
const FAIR_DEAL_RATIO := 0.9

## Grudge / affection changes, and the levels that trigger events.
const GRUDGE_INSULT := 1
const GRUDGE_WRONG_ACCUSE := 2
const GRUDGE_WALKOUT := 1
## Any deal without an insult or a false accusation.
const AFFECTION_DEAL := 1
## Extra when the price is close to their first ask.
const AFFECTION_FAIR_DEAL := 1
const AFFECTION_HONEST_EXPOSE := 1
const REVENGE_AT := 3
const GIFT_AT := 3
## Days until a threatened revenge (or a promised gift) arrives.
const EVENT_DELAY := Vector2i(1, 3)

const LINES := {
	"greet": {
		"honest": ["...문이 열려 있길래. 이것 좀 봐주시오.", "감정을 부탁하오. 이걸 계속 갖고 있으면... 안 될 것 같아서."],
		"desperate": ["빨리, 빨리! 이게 밤마다 나를 불러!", "제발 이것 좀 가져가! 지금 당장!"],
		"haggler": ["흐음. 이 골목 감정사는 눈이 몇 개지? 두 개? ...부족하군.", "제값을 쳐주겠지. 쳐주지 않으면... 곤란해질 텐데."],
		"liar": ["크크, 주인장~ 오늘 심연에서 건져온 귀한 물건이야.", "이런 물건, 인간 세상엔 다시 안 나와. 진짜로."],
	},
	"greet_grudge": ["...또 왔다. 이번엔 제대로 쳐줘.", "지난번 일, 우리는 잊지 않는다."],
	"greet_friend": ["주인장. 또 왔소. 여기가 제일 덜 무섭거든.", "역시 여기뿐이오. 다른 가게는... 사라졌으니."],
	"claim": "순%s %s이오.",
	"origin": {
		"honest": ["침몰한 사원에서 건져 올렸소.", "돌아가신 어머니의 무덤에 같이 묻혀 있던 거요.", "꿈에서 깼더니 손에 쥐고 있었소."],
		"desperate": ["그런 건 됐고! 빨리 좀 봐줘!", "주, 주웠... 아니 원래 내 거야! 빨리!"],
		"haggler": ["대대로 우리 교단이 지켜온 성물이다. 싸게는 못 넘겨.", "별들이 바른 자리에 섰을 때 만들어진 거다. 출처는 확실해."],
		"liar": ["심해 왕의 무덤에서 직접 꺼냈지.", "어머니 유품이야.", "귀족 흡혈귀한테 선물받았어.", "시장에서... 아니 꿈에서 받은 거야."],
	},
	"reason": {
		"honest": ["밤마다 이게 속삭이오. 더는 못 견디겠소.", "아이가 이걸 보고 나서 말을 안 하오."],
		"desperate": ["이게 날 쳐다봐! 계속 쳐다본다고!", "오늘 밤 안에 처분해야 돼! 안 그러면 그것들이 와!"],
		"haggler": ["더 큰 제물을 사려고. 제값만 받으면 돼.", "팔 생각은 없었는데, 값만 맞으면."],
		"liar": ["그냥... 필요 없어서. 크크.", "이사 가. 급하게. 아니 천천히."],
	},
	"nervous": ["어... 그, 그러니까...", "왜 자꾸 물어보는 거지?", "(촉수가 꼬인다)", "(시선이 흔들린다)"],
	"annoyed": ["질문이 많군, 인간.", "살 건가, 말 건가?"],
	"exposed": {
		"honest": "가짜...? 그럼 그 속삭임도 거짓이었단 말이오... %d골드라도 받겠소.",
		"desperate": "몰라! 상관없어! %d골드만 줘, 빨리!",
		"haggler": "...감정사의 눈은 속일 수 없군. %d골드에 넘기지.",
		"liar": "쳇, 눈이 좋군. ...%d골드. 더는 안 깎아.",
	},
	"wrong": ["감히 심연의 물건을 가짜라고?!", "지금 나를 사기꾼 취급하는 건가, 인간?!"],
	"ask": "%d골드는 받아야겠소.",
	"counter": ["그건 좀... %d골드.", "흐음... %d골드까지는 해주지.", "%d골드. 더는 안 돼."],
	"insult": ["장난하나? %d골드라니까.", "그 돈 받을 바엔 심해에 던지지! %d골드!"],
	"accept": ["좋아. 거래지.", "...알겠소. 가져가시오. 밤엔 덮어두는 게 좋을 거요."],
	"walkout": ["됐어. 다른 데 가지.", "이런 가게, 다시는 안 와!"],
	"threat": ["두고 보자. 오늘 밤 창문을 잠가두는 게 좋을 거야.", "우리는 기억한다, 인간.", "밤길 조심해, 주인장. 그림자가 길어질 거야."],
	"thanks": ["고맙소. 이 은혜, 심연에서도 기억하겠소.", "주인장 최고야. 다음엔 더 좋은 걸 가져오지."],
	"turned_away": ["...알았소.", "칫, 다른 데 가지."],
	# buyers
	"buy_greet": ["저 %s... 내 눈길을 끄는군.", "%s. 저거 얼마지?", "오늘은 %s을(를) 찾으러 왔소."],
	"buy_offer": "%d골드 주지.",
	"buy_counter": ["%d골드. 그 이상은 무리야.", "흠... %d골드까지 올려주지.", "%d골드. 이게 마지막이야."],
	"buy_insult": ["%d골드? 날 바보로 보나. %d골드면 생각해보지."],
	"buy_accept": ["좋아, 사지.", "거래지. 포장은 필요 없어. 이건... 스스로 걸어갈 테니."],
	"buy_walkout": ["됐어, 너무 비싸군.", "다른 데서 사지."],
	"buy_nothing": ["...진열대가 텅 비었군. 다음에 오지.", "살 게 없군."],
	"buy_caught": ["가짜를 팔려고 해? 이 사기꾼!", "내 눈은 못 속인다. 이건 가짜야!"],
	"buy_refused": ["...안 판다고? 흥.", "아쉽군."],
}
