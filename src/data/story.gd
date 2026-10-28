class_name Story
## Dialogue shown in the build phase: intro on a new run, a few lines before key waves, and
## one line on the end screen. Each line is [speaker, text]. Speaker colors in SPEAKERS.

## Tests and screenshot demos turn this off (dialogue pauses the game until clicked).
static var enabled := true

const SPEAKERS := {
	"마왕": Color(1.0, 0.45, 0.55),
	"집사 임프": Color(1.0, 0.65, 0.35),
	"용사 보스": Color(1.0, 0.85, 0.4),
}

const INTRO := [
	["집사 임프", "마왕님! 큰일났습니다! 왕국이 '용사 육성 지원금'을 풀었답니다."],
	["집사 임프", "덕분에 동네 청년들이 죄다 용사가 돼서 몰려오고 있어요."],
	["마왕", "...또? 이번 달만 세 번째다. 결재 서류도 산더미인데."],
	["집사 임프", "던전을 고치고, 몬스터를 배치하고, 정 안 되면 직접 베시면 됩니다!"],
	["마왕", "그걸 다 나 혼자 하라고? ...마왕님은 바쁘다."],
]

## Wave index (0-based) -> lines shown when that wave's build phase starts.
const WAVE_LINES := {
	3: [
		["집사 임프", "입구가 하나 더 뚫렸습니다! 이제 두 군데서 몰려와요."],
		["마왕", "철문 하나 닫아두면 돌아오겠지. 광산도 지켜라, 그거 내 월급이다."],
	],
	7: [
		["집사 임프", "모든 입구가 열렸어요. 소문 듣고 성직자랑 마법사까지 왔습니다."],
	],
	9: [
		["용사 보스", "마왕! 지원금 받은 만큼은 일하겠다!"],
		["마왕", "세금으로 나를 잡으러 오다니... 오늘 퇴근은 글렀군."],
	],
}

const VICTORY := "집사 임프: 해냈습니다! 지원금 사업이 '성과 미달'로 폐지됐대요!"
const DEFEAT := "집사 임프: 코어가... 다음엔 더 단단히 짓죠. 영혼석은 챙겨뒀습니다."
