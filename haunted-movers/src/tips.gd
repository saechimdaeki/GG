## First-day guide: one short tip at a time, picked from what the player has done.
## Pure, so the smoke test can walk it.


## s: {captured, grabbed_once, heavy_alone, loaded_any} (bools). "" = nothing to say.
static func tip_for(day: int, phase: String, s: Dictionary) -> String:
	if day != 1 or phase != "day":
		return ""
	if not s.captured:
		return "화면을 클릭하면 시작해요"
	if not s.grabbed_once:
		return "가구를 바라보고 왼쪽 클릭을 누르고 있으면 잡혀요"
	if s.heavy_alone:
		return "너무 무거워요! 친구와 같이 잡으면 들려요"
	if not s.loaded_any:
		return "현관 밖 주황색 트럭 짐칸에 실어요 — 문에서는 돌려야 나가요"
	return "다 실었으면 트럭 옆 빨간 버튼으로 출발!"
