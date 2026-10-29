class_name ProgressionData
## Baal's weekly debt schedule and the shop's upgrades.

const WEEK_DAYS := 7
## Due on the night of day 7, 14, 21, 28. Sums to the inherited 3,000 gold.
const DEBT_SCHEDULE := [250, 500, 900, 1350]
## A shortfall rolls into next week's payment with this much interest.
const SHORTFALL_INTEREST := 0.5
## When gold runs short Baal takes stock instead, valued at market * this.
const SEIZE_RATE := 0.8

## Upgrade id -> name, description, price per level (array length = max level).
## Effects are read by RunState / ShopView through RunState.level(id).
const UPGRADES := {
	"shelf": {"name": "진열대 확장", "desc": "진열 칸 +1", "prices": [150, 250, 400, 600]},
	"scale": {"name": "정밀 저울", "desc": "무게가 기준과 몇 % 다른지 바로 보여줌", "prices": [120]},
	"lens": {"name": "심연 확대경", "desc": "돋보기가 커지고 수상한 곳을 더 쉽게 찾음", "prices": [150]},
	"touchstone": {"name": "시금석", "desc": "물건을 긁어 속 금속 확인 (손님이 싫어함)", "prices": [220]},
	"guard": {"name": "경비 고블린", "desc": "복수로 금고가 털리는 걸 막음", "prices": [200]},
	"sign": {"name": "새 간판", "desc": "파는 손님 하루 +1", "prices": [180, 320]},
	"salt": {"name": "정화 소금", "desc": "저주 게이지 -30 (살 때마다)", "prices": [80, 80, 80, 80, 80, 80, 80, 80]},
}

const SHELF_SLOTS_BASE := 4
## Loupe size and click radius multiplier with the lens upgrade.
const LENS_LOUPE_SIZE := 160.0
const LENS_CLICK_BONUS := 1.6
## Patience a seller loses when you scratch their item with the touchstone.
const TOUCHSTONE_PATIENCE := 1

const BAAL_LINES := {
	"paid": "바알: \"훌륭해. 이번 주 %dG, 확실히 받았네. 다음 주엔 %dG일세. 기대하지.\"",
	"seized": "바알: \"돈이 모자라군. 대신 이걸 가져가지.\" — %s",
	"short": "바알: \"아직 %dG 모자라. 다음 주에 이자 붙여 %dG 더 받겠네.\"",
	"final_paid": "바알: \"...전부 갚았군. 할아버지보다 낫네. 이 가게, 이제 진짜 자네 것이야.\"",
	"foreclose": "바알: \"계약은 계약이지. 가게는 내가 가져가겠네. ...그리고 자네도.\"",
}
