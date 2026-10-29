# Story 007: M6 완성 — 타이틀·저장/이어하기·사운드·튜토리얼 팁·엔딩 컷

> **Epic**: 저주받은 전당포
> **Status**: In Progress (엔딩 컷·타이틀 그림 대기)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-30

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP milestone 6`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 타이틀 화면: 이어하기(세이브 있을 때, 날짜 표시)·새로 시작·종료
- [x] 매일 아침 자동 저장, 이어하면 그 아침이 똑같이 재현됨 (왕복 검사 PASS)
- [x] 엔딩 후 세이브 삭제, 타이틀로
- [x] 효과음 12종(문 종·동전·저울·시금석·촛불·저주·분노·거래·바알·엔딩·종이·클릭) + 빗소리 앰비언스 루프
- [x] 1~3일째 피프의 상황별 팁
- [ ] 엔딩 컷 7장·타이틀 그림 (아트 묶음 5 — 유저 생성. 없으면 텍스트만)
- [ ] 배경 음악 (합성 앰비언스만. 실제 곡은 외부 음악 AI 필요)

---

## Files

- `cursed-pawnshop/src/title/title.gd` (+ `.tscn`), `src/audio/audio.gd`
- `cursed-pawnshop/src/shop/run_state.gd`, `shop_view.gd`, `story_panel.gd`
- `cursed-pawnshop/tools/gen_audio.py`, `tools/sprite_convert.py`

---

## Test Evidence

**Story Type**: UI
**Required evidence**: `production/qa/evidence/story-007-m6-polish/` (타이틀·피프 팁)

**Status**: [x] screenshots retained

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-006-m5-curse.md`
- Unlocks: 출시 준비
