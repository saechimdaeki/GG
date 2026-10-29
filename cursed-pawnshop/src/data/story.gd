class_name StoryData
## The 28-day story: NPCs, morning scenes with choices, Hrrg's seven relics,
## reputation / suspicion rules, and the endings. See design/story.md.

const NPCS := {
	"pip": {"name": "피프", "title": "가게 꼬마 유령", "color": Color(0.7, 0.85, 1.0)},
	"baal": {"name": "바알", "title": "고리대금업자", "color": Color(0.85, 0.25, 0.2)},
	"morg": {"name": "모르그 할멈", "title": "건너편 전당포", "color": Color(0.45, 0.6, 0.35)},
	"greta": {"name": "그레타 대장", "title": "항구 경비대", "color": Color(0.55, 0.6, 0.75)},
	"rat_king": {"name": "쥐왕", "title": "장물 조직", "color": Color(0.55, 0.4, 0.3)},
	"bishop": {"name": "주교 오르무스", "title": "가라앉은 별의 형제단", "color": Color(0.3, 0.55, 0.5)},
	"amelia": {"name": "아멜리아", "title": "인간 학자", "color": Color(0.8, 0.55, 0.35)},
	"drachma": {"name": "드라크마 백작", "title": "흡혈귀 수집가", "color": Color(0.75, 0.15, 0.25)},
	"herald": {"name": "심연의 사자", "title": "???", "color": Color(0.4, 0.3, 0.7)},
	"elias": {"name": "엘리아스 모로", "title": "할아버지", "color": Color(0.6, 0.9, 0.7)},
}

## Hrrg's relics: id -> name. Kept relics sit in the vault; selling one to the
## Brotherhood brings the god closer to waking.
const RELICS := {
	"tooth": "흐르그의 이빨", "scale": "흐르그의 비늘", "eye": "흐르그의 눈", "tentacle": "흐르그의 촉수",
	"heart": "흐르그의 심장", "voice": "흐르그의 목소리(소라)", "name": "흐르그의 이름(석판)",
}

## Reputation (명성) and guard suspicion (의심) rules.
const REP_FAIR_DEAL := 2
const REP_EXPOSE_LIAR := 4
const REP_SCAM := -8
const REP_CAUGHT := -12
const REP_REPORT_STOLEN := 6
## Reputation >= this brings one extra buyer a day; <= LOW loses a seller.
const REP_HIGH := 40
const REP_LOW := -30

## Chance a seller's item is stolen (hot goods: cheap, but the watch is looking).
const STOLEN_CHANCE := {"liar": 0.25, "desperate": 0.15}
const STOLEN_SPECIES_BONUS := {"rat_man": 0.3, "goblin": 0.2, "shadow": 0.2}
const STOLEN_PARTNER_BONUS := 0.2
const STOLEN_DISCOUNT := 0.55
const SUSPICION_PER_STOLEN := 15
const SUSPICION_DECAY := 4
const RAID_AT := 60
const RAID_FINE := 80
const RAID_RESET := 25
const REPORT_REWARD := 40
## The seller you reported holds this much grudge (they know who talked).
const REPORT_GRUDGE := 3

## Count Drachma, once he is your patron, buys your best genuine shelf item at
## market * this every few mornings.
const PATRON_EVERY := 3
const PATRON_RATE := 2.0

## Morning scenes. Each: day, npc, text, optional "if" (all must hold), choices.
## A choice: label, reply (what the NPC says back), optional "if", effects.
## Effects: money, rep, suspicion, flag (set true), unflag, relic (add to vault),
## lose_relic (the Brotherhood gets it), sell_relics (sell N kept relics at "price" each, -1 = all).
## Conditions: flag, not_flag, relics_min, money_min.
const BEATS := [
	{"day": 1, "npc": "pip", "text": "안녕! 난 피프야. 영감님... 그러니까 네 할아버지가 사라진 지 한 달째야.\n남은 건 이 가게랑, 바알한테 진 빚 3,000골드. 매주 갚아야 해. 못 갚으면... 바알이 다 가져가.\n괴물 손님들 물건을 싸게 사서 비싸게 팔면 돼. 근데 가짜가 많으니까 꼭 돋보기로 살펴봐!",
		"choices": [{"label": "알았어, 해볼게", "reply": "좋아! 영감님 장부는 카운터 밑에 있어. 이상한 메모가 많더라... '조각은 일곱'이라든가."}]},
	{"day": 2, "npc": "morg", "text": "흐흐흐, 모로 영감 손주로구나. 이 골목에 전당포는 하나면 충분해.\n내 가게 손님 뺏어갈 생각 말거라. 아니면... 우리 서로 돕고 살든가.",
		"choices": [
			{"label": "경쟁이라면 이겨주지", "reply": "흐흐, 건방지긴. 두고 보자꾸나.", "effects": {"flag": "morg_rival"}},
			{"label": "서로 돕고 삽시다", "reply": "영리하구나. 가끔 내가 감당 못 할 손님을 보내주마.", "effects": {"flag": "morg_friend", "rep": 3}},
		]},
	{"day": 4, "npc": "greta", "text": "경비대장 그레타다. 요즘 항구에 장물이 돈다.\n'출처는 묻지 마'라는 놈들 물건은 대개 훔친 거지. 사면 너도 공범이야.\n신고하면 보상을 주겠다. 협조하겠나?",
		"choices": [
			{"label": "협조하겠소", "reply": "좋아. 수상한 놈은 '장물 아니오?'로 캐물어라. 맞으면 보상이다.", "effects": {"flag": "greta_ally", "rep": 5}},
			{"label": "내 가게 일엔 상관 마시오", "reply": "...그래? 눈여겨보겠다.", "effects": {"suspicion": 15}},
		]},
	{"day": 6, "npc": "rat_king", "text": "찍찍... 우리는 쥐왕이다. 수십의 입, 하나의 뜻.\n항구에서 '떨어진' 물건들이 많지. 네 가게로 흘려보내 주마. 싸게. 대신 입은 닫아라.",
		"choices": [
			{"label": "좋은 거래군", "reply": "찍찍찍! 현명하다. 물건이 곧 흘러들 것이다.", "effects": {"flag": "rat_partner", "suspicion": 10}},
			{"label": "꺼져, 쥐새끼들", "reply": "...우리는 잊지 않는다. 찍.", "effects": {"flag": "rat_enemy", "rep": 3}},
		]},
	{"day": 8, "npc": "bishop", "text": "(바닥에 물이 고인다)\n가라앉은 별의 형제단의 주교, 오르무스라 하오. 이 가게 주인은 오래전부터 '그분의 조각'을 모아왔지.\n혹시 이빨, 비늘, 눈... 그런 걸 보거든 내게 파시오. 하나에 700골드를 드리리다.",
		"choices": [
			{"label": "조각이 뭐요?", "reply": "깊은 곳에서 잠든 분, 흐르그의 몸이오. 일곱이 모이면... 그분이 깨어나시지."},
			{"label": "흥미롭군", "reply": "언제든. 별이 바른 자리에 서기 전에.", "effects": {"flag": "bishop_met"}},
		]},
	{"day": 9, "npc": "pip", "text": "야야! 장부 마지막 장 봤어? 지하실 지도가 그려져 있어!\n...내려가 봤는데, 돌로 된 방에 받침대가 일곱 개야. 하나에 이게 놓여 있었어.\n(검고 거대한 이빨. 손을 대니 바닷소리가 들린다)",
		"choices": [{"label": "금고에 다시 넣어둬", "reply": "응... 근데 이거, 밤마다 울어.", "effects": {"relic": "tooth"}}]},
	{"day": 11, "npc": "amelia", "text": "실례합니다! 아멜리아예요, 대학에서 고대 유물을 연구해요.\n이 가게에 '흐르그의 조각'이 있다는 소문을 들었어요. 딱 하루만 연구하게 빌려주실 수 있나요?\n보답으로 다른 조각의 위치를 알려드릴게요!",
		"if": {"relics_min": 1},
		"choices": [
			{"label": "빌려주지", "reply": "감사해요! ...이상해요, 만지니까 꿈에서 본 바다가 떠올라요. 다음 조각은 '눈알 많은 자'가 갖고 있어요.", "effects": {"flag": "amelia_trust", "rep": 4}},
			{"label": "안 됩니다", "reply": "아... 알겠어요. 하지만 조심하세요. 그건 그냥 돌이 아니에요."},
		]},
	{"day": 12, "npc": "pip", "text": "익사한 선원이 왔어. 축축한 자루에서 비늘을 꺼내더라.\n방패만 한 검푸른 비늘이야. 300골드래. 안 사면... 주교한테 간대.",
		"choices": [
			{"label": "300골드에 산다", "reply": "금고에 넣었어. 받침대 두 개가 찼어...", "if": {"money_min": 300}, "effects": {"money": -300, "relic": "scale"}},
			{"label": "돈이 없어 / 안 산다", "reply": "...주교가 기뻐하겠네.", "effects": {"lose_relic": "scale"}},
		]},
	{"day": 15, "npc": "drachma", "text": "흐음, 이 골목에 진짜 감정사가 있다더니. 드라크마 백작이오.\n나는 진품만 모으지. 자네가 진열대에 진품을 올려두면, 내 하인이 가끔 두 배 값에 사 가겠소.\n대신 가짜를 섞으면... 우리 관계는 끝이오.",
		"choices": [
			{"label": "영광입니다, 백작님", "reply": "좋소. 실망시키지 마시오.", "effects": {"flag": "patron"}},
			{"label": "흡혈귀와는 거래 안 합니다", "reply": "후회할 텐데.", "effects": {"rep": -3}},
		]},
	{"day": 16, "npc": "bishop", "text": "주인장, 조각을 갖고 있다는 걸 아오. 금고에서 그분의 숨소리가 들리거든.\n하나에 700골드. 바알의 빚을 갚기엔 충분하지 않소?",
		"if": {"relics_min": 1},
		"choices": [
			{"label": "전부 판다", "reply": "현명하오! 별들이 기뻐하리다.", "effects": {"sell_relics": -1, "price": 700}},
			{"label": "하나만 판다", "reply": "하나라도 감사하오. 나머지도... 곧.", "effects": {"sell_relics": 1, "price": 700}},
			{"label": "팔지 않는다", "reply": "...그분은 기다리실 수 있소. 우리는 덜 참을성 있지만.", "effects": {"flag": "bishop_refused"}},
		]},
	{"day": 18, "npc": "eyes", "species": true, "text": "(눈알 군체가 일곱 개의 눈으로 동시에 당신을 본다)\n...보았다. 너는 조각을 모으는 자. 이것도 가져가라. 우리는 더는 보고 싶지 않다.\n(주먹만 한 석화된 눈. 동공이 세로로 갈라져 있다)",
		"if": {"flag": "amelia_trust"},
		"choices": [{"label": "받아든다", "reply": "...이제 잠들 수 있겠다.", "effects": {"relic": "eye", "rep": 2}}]},
	{"day": 18, "npc": "pip", "text": "눈알 군체가 왔다 갔대. 뭔가 큰 걸 갖고 있었는데, 형제단한테 팔았다나 봐.\n...아멜리아 말을 들었으면 우리가 받았을 텐데.",
		"if": {"not_flag": "amelia_trust"},
		"choices": [{"label": "아쉽군", "reply": "응...", "effects": {"lose_relic": "eye"}}]},
	{"day": 20, "npc": "greta", "text": "모로. 쥐왕의 물건이 네 가게에서 나왔다는 제보가 있다.\n...이번 한 번은 넘어가지. 하지만 다음엔 없다.",
		"if": {"flag": "rat_partner"},
		"choices": [
			{"label": "쥐왕과 손을 끊겠소", "reply": "현명한 선택이다.", "effects": {"unflag": "rat_partner", "suspicion": -20, "rep": 3}},
			{"label": "증거 있소?", "reply": "...곧 찾게 될 거다.", "effects": {"suspicion": 20}},
		]},
	{"day": 20, "npc": "greta", "text": "협조 고맙다, 모로. 덕분에 장물 조직 하나를 잡았다.\n경비대 이름으로 보상금이다.",
		"if": {"flag": "greta_ally", "not_flag": "rat_partner"},
		"choices": [{"label": "감사합니다", "reply": "항구에 너 같은 상인이 더 있었으면 좋겠군.", "effects": {"money": 150, "rep": 5}}]},
	{"day": 22, "npc": "rat_king", "text": "찍... 우리 굴에서 이상한 게 나왔다. 꿈틀대는 돌 촉수.\n250골드. 경비대엔 비밀이다.",
		"choices": [
			{"label": "250골드에 산다", "reply": "찍찍! 거래 성립.", "if": {"money_min": 250}, "effects": {"money": -250, "relic": "tentacle", "suspicion": 15}},
			{"label": "필요 없다", "reply": "그럼 형제단 놈들한테 팔지. 찍.", "effects": {"lose_relic": "tentacle"}},
		]},
	{"day": 24, "npc": "herald", "text": "(가게 안의 모든 촛불이 동시에 꺼진다. 어둠 속에 별빛 같은 점들이 떠 있다)\n...너는 모으고 있구나. 왜? 가두기 위해? 깨우기 위해?\n선물을 주마. 그분의 심장이다. 무엇을 할지는 네가 정해라.",
		"choices": [
			{"label": "금고에 봉인한다", "reply": "...가두는 자로구나. 너의 할아버지처럼.", "effects": {"relic": "heart"}},
			{"label": "거절한다", "reply": "그렇다면 다른 손으로 가겠지.", "effects": {"lose_relic": "heart"}},
		]},
	{"day": 25, "npc": "bishop", "text": "주인장, 마지막 제안이오. 조각 하나에 1,000골드.\n별이 바른 자리에 서는 날이 사흘 남았소.",
		"if": {"relics_min": 1},
		"choices": [
			{"label": "전부 판다", "reply": "그분이 기뻐하시리라!", "effects": {"sell_relics": -1, "price": 1000}},
			{"label": "팔지 않는다", "reply": "그렇다면... 우리 방식대로 하겠소.", "effects": {"flag": "bishop_angry"}},
		]},
	{"day": 26, "npc": "drowned", "species": true, "text": "(익사한 선원이 소라 껍데기를 내민다)\n...바다가 이걸 돌려주래. 귀에 대면 노래가 들려. 너무 아름다워서 무서워.\n그냥 가져가.",
		"choices": [{"label": "받는다", "reply": "...고마워. 이제 잠이 오겠어.", "effects": {"relic": "voice"}}]},
	{"day": 27, "npc": "elias", "text": "(카운터 너머로 초록빛 노인이 웃고 있다)\n...잘했구나, 얘야. 나는 사십 년 동안 그분의 조각을 이 가게에 가뒀단다.\n바알의 빚은 그 대가였지. 마지막 조각, 이름을 새긴 석판은 내가 갖고 있었다.\n네가 끝까지 지킨다면... 이걸 주마.",
		"if": {"relics_min": 4},
		"choices": [{"label": "할아버지...", "reply": "가게를 부탁한다. 그리고 바알에게 지지 마라.", "effects": {"relic": "name"}}]},
	{"day": 27, "npc": "elias", "text": "(카운터 너머로 초록빛 노인이 슬프게 웃고 있다)\n...얘야, 조각이 너무 많이 흩어졌구나. 형제단이 내일 밤 의식을 치를 거다.\n나는 이 석판만큼은 지켜왔다. 하지만 혼자서는... 이제 늦었을지도 모르겠구나.",
		"if": {"not_relics_min": 4},
		"choices": [{"label": "어떻게든 해볼게요", "reply": "...그래. 바알의 빚만은 꼭 갚거라."}]},
]

## Endings, checked in order at the final payday (after Baal is paid).
## A condition missing from an entry is not checked.
const ENDINGS := [
	{"id": "awaken", "title": "각성", "if": {"relics_sold_min": 4},
		"text": "의식의 밤, 바다가 끓어올랐다. 항구가 기울고, 거대한 촉수가 등대를 감았다.\n당신의 금고에는 금화가 가득하다. 그 금화를 쓸 세상은 더 이상 없다."},
	{"id": "keeper", "title": "봉인지기", "if": {"relics_min": 7},
		"text": "일곱 개의 받침대가 모두 찼다. 금고 바닥의 보랏빛이 꺼지고, 처음으로 안개가 걷혔다.\n문을 열자 아침 해가 들었다. 옆에서 할아버지가 웃고 있었다.\n모로 전당포는 오늘도 문을 연다. 가두는 자의 가게로서."},
	{"id": "rat_lord", "title": "장물왕", "if": {"stolen_min": 6, "suspicion_max": 50},
		"text": "쥐왕이 당신 앞에 무릎을 꿇었다. 수십의 입이 동시에 말했다. \"새로운 왕이여.\"\n항구의 모든 뒷거래가 이 가게를 거친다. 그레타 대장은 끝내 증거를 찾지 못했다."},
	{"id": "master", "title": "마계 최고 감정사", "if": {"rep_min": 60},
		"text": "드라크마 백작의 연회장. 수백의 괴물이 당신의 감정을 기다린다.\n\"이 자의 눈은 속일 수 없다.\" 마계의 모든 귀한 물건이 이제 당신의 돋보기를 거친다."},
	{"id": "cleared", "title": "빚 청산", "if": {},
		"text": "할아버지의 빚 3,000골드를 모두 갚았다.\n하지만 금고의 빈 받침대들이 마음에 걸린다. 흩어진 조각들은 지금 어디에 있을까.\n(조각을 모두 지키면 다른 결말이 기다린다)"},
]
const FORECLOSE := {"id": "foreclose", "title": "차압",
	"text": "바알은 가게의 열쇠를, 그리고 당신의 그림자를 가져갔다.\n모로 전당포의 간판이 내려졌다. 금고 속 조각들이 낮게 웃었다."}
