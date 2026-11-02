class_name VillageData
## Buildings, gear upgrades and residents for the drained zone (M2).
## Costs use the pump materials: scrap, pipe, gear.

const BUILDINGS := {
	"workshop": {"name": "공방", "cost": {"scrap": 2}, "desc": "장비를 업그레이드할 수 있다"},
	"lodge": {"name": "숙소", "cost": {"scrap": 3}, "desc": "떠돌던 생존자들이 들어와 산다"},
	"tower": {"name": "조명탑", "cost": {"scrap": 2, "pipe": 1}, "desc": "랜턴 빛이 넓어진다"},
}

## costs[i] buys level i + 1.
const UPGRADES := {
	"pick": {"name": "곡괭이", "costs": [{"scrap": 2, "gear": 1}], "desc": "한 번에 더 세게 — 단단한 벽도 부순다"},
	"tank": {"name": "산소통", "costs": [{"scrap": 3}, {"scrap": 4, "gear": 1}], "desc": "산소를 더 오래 버틴다"},
	"suit": {"name": "잠수복", "costs": [{"scrap": 3, "pipe": 1}], "desc": "깊은 물의 수압을 견딘다"},
}

const TANK_MAX := [60.0, 90.0, 120.0]
const PRESSURE_DRAIN := 4.0     # oxygen drain multiplier below PRESSURE_ROW without the suit
const LIGHT_SCALE := [2.5, 4.0]  # lantern radius without / with the light tower
const RUBBLE_REGROW := 60.0     # seconds before broken rubble silts back up (keeps scrap renewable)

## Arrive when the lodge is built. Each has one request.
const RESIDENTS := [
	{
		"name": "모루", "look": "고물상 노인",
		"hello": "물을 뺐다고? 허, 몇십 년 만에 마른 돌을 밟아보는군.",
		"ask": "아래 깊은 곳에 쓸 만한 파이프가 굴러다닐 거야. 하나만 갖다 주겠나?",
		"need": {"pipe": 1}, "reward": {"scrap": 5},
		"thanks": "좋은 파이프야. 고철 좀 챙겨가게, 내가 모아둔 거야.",
	},
	{
		"name": "물방울", "look": "물갈퀴 손 꼬마",
		"hello": "아저씨가 물 뺀 사람이야? 우와, 바닥이 이렇게 생겼었구나!",
		"ask": "황동 조각 두 개만 주면… 내가 숨겨둔 보물 줄게!",
		"need": {"scrap": 2}, "reward": {"gear": 1},
		"thanks": "자, 반짝반짝 톱니! 아래층 벽 속에서 주웠어.",
	},
]
