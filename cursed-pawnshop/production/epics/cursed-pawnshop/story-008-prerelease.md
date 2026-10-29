# Story 008: 플레이 전 마무리 — 스토리 후속 사건·일시정지/설정·비금속 물건·CI

> **Epic**: 저주받은 전당포
> **Status**: Done (플레이테스트 대기)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-30

## Context

**GDD**: `design/game-brief.md`, `design/story.md`
**Requirement**: 플레이테스트 전 빈 곳 메우기

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 결과 없던 선택지 5개에 후속 사건: 모르그 경쟁(손님 빼돌림)/협력(손님·선물), 쥐왕 거절(쥐떼 습격), 주교 거절(문 앞 저주), 주교 분노(28일 금고 습격)
- [x] ESC 일시정지: 계속·음악/효과음 볼륨·전체화면·타이틀로·종료. 설정 저장. 타이틀에도 설정
- [x] 비금속 물건 12종 등장: 보석(천연/유리: 무게·기포), 유기물(진품/밀랍: 무게·이음새), 문서(원본/위조: 잉크 번짐)
- [x] CI: 푸시마다 두 게임 스모크 검사. 전당포는 모든 물건 생성 + 28일 두 판 (단서 하나짜리 물건 크래시를 잡는 것 확인)

---

## Files

- `cursed-pawnshop/src/data/story.gd`, `items.gd`, `customers.gd`
- `cursed-pawnshop/src/shop/run_state.gd`, `customer.gd`, `shop_view.gd`, `story_panel.gd`
- `cursed-pawnshop/src/appraisal/item_generator.gd`, `item_drawer.gd`
- `cursed-pawnshop/src/title/pause_menu.gd`, `title.gd`
- `cursed-pawnshop/tests/smoke/run_check.gd`, `.github/workflows/smoke.yml`

---

## Test Evidence

- `tests/smoke/run_check.gd` — PASS. 수정 전 코드로 돌리면 SCRIPT ERROR 45건(실패 확인)
- 화면: `production/qa/evidence/story-007-m6-polish/pause.png`, `production/qa/evidence/story-008-nonmetal/gem-glass.png`

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-007-m6-polish.md`
