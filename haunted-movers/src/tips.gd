## First-day guide: one short tip at a time, picked from what the player has done.
## Pure, so the smoke test can walk it.


## s: {captured, entered, picked_once, carrying, loaded_any} (bools). "" = nothing to say.
static func tip_for(day: int, phase: String, s: Dictionary) -> String:
	if day != 1 or phase != "day":
		return ""
	if not s.captured:
		return "화면을 클릭하면 시작해요"
	if not s.picked_once and not s.entered:
		return "저택 현관문을 바라보고 E — 안에 고물이 있어요"
	if not s.picked_once:
		return "우클릭으로 스캔하면 고물과 값이 보여요 · 바라보고 E로 주워요"
	if s.carrying and not s.loaded_any:
		return "트럭 짐칸에 들어가서 G로 내려놓아야 돈이 돼요 (4칸까지 들고 다녀요)"
	if not s.loaded_any:
		return "주운 고물은 트럭 짐칸에 내려놓아요"
	return "할당량만큼 실었으면 트럭 옆 빨간 레버(E)로 출발 — 시간이 끝나면 저절로 떠나요"
