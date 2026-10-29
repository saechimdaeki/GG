class_name CutsceneData
## Cutscenes: full-screen pictures with captions, played by CutscenePlayer.
## Each frame: image (assets/sprites/scenes/<name>.png), fallback (an existing scene
## to show until the dedicated picture exists), text, optional speaker.

const SCENES := {
	"intro": [
		{"image": "cut_intro_letter", "fallback": "night_shop", "text": "비 오는 밤, 낯선 편지가 도착했다.\n\"모로 전당포의 주인, 엘리아스 모로가 실종되었음을 알립니다.\n유일한 혈육인 귀하께 가게와... 그 채무를 상속합니다.\""},
		{"image": "cut_intro_ship", "fallback": "harbor", "text": "안개 낀 바다를 건너 도착한 곳은 지도에 없는 항구.\n녹슨 등대 항구. 사람보다 괴물이 많은 도시였다."},
		{"image": "cut_intro_alley", "fallback": "morning_street", "text": "뒷골목 끝, 놋쇠 구슬 세 개가 매달린 간판.\n할아버지의 가게는 아직 거기 있었다."},
		{"image": "cut_intro_door", "fallback": "title_bg", "text": "열쇠를 돌리자 종이 울렸다.\n먼지 쌓인 진열대, 병 속의 무언가, 그리고..."},
		{"image": "cut_intro_pip", "fallback": "night_shop", "speaker": "???", "text": "\"...영감님? 아니네. 너, 영감님 손주구나!\""},
		{"image": "cut_intro_baal", "fallback": "baal_office", "speaker": "바알", "text": "\"환영하네, 새 주인. 할아버지의 빚은 3,000골드.\n매주 갚으면 되네. 못 갚으면... 가게와 자네를 가져가지.\""},
	],
	"week2": [
		{"image": "cut_week2", "fallback": "harbor", "text": "— 2주째 —\n밤마다 부두에 젖은 로브를 입은 행렬이 나타났다.\n그들은 바다를 향해 이름 하나를 속삭였다. '흐르그.'"},
	],
	"week3": [
		{"image": "cut_week3", "fallback": "harbor", "text": "— 3주째 —\n바다가 끓기 시작했다. 어부들은 그물 대신 비늘을 건져 올렸다.\n익사한 자들이 하나둘 뭍으로 걸어 나왔다."},
	],
	"week4": [
		{"image": "cut_week4", "fallback": "ritual", "text": "— 마지막 주 —\n별들이 바른 자리로 움직이고 있다.\n금고 아래에서 무언가가 숨을 쉰다."},
	],
	"vault": [
		{"image": "cut_vault", "fallback": "vault", "text": "할아버지 장부의 마지막 장을 따라 내려간 지하.\n일곱 개의 받침대가 원을 그리고 있었다. 하나에만 무언가가 놓여 있었다."},
	],
	"final_night": [
		{"image": "cut_final", "fallback": "ritual", "text": "28일째 밤. 바알이 마지막 장부를 펼친다.\n그리고 바다 저편, 형제단의 횃불이 절벽 위로 모여든다."},
	],
}
