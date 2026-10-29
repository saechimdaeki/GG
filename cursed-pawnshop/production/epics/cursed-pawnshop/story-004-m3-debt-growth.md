# Story 004: M3 빚과 성장 — 주간 상환·상점·감정 도구

> **Epic**: 저주받은 전당포
> **Status**: In Progress
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP milestone 3`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 빚 3,000G를 4주에 나눠 상환(7·14·21·28일 밤, 300/600/900/1200). HUD에 다음 상환일·금액
- [ ] 상환일 밤 바알 방문: 돈이 모자라면 창고 물건 압류(시세 80%), 그래도 모자라면 이자 50% 붙여 다음 주로. 4주차에 모자라면 차압 배드엔딩
- [ ] 4주차까지 다 갚으면 빚 청산 엔딩 화면(통계) + 새로 시작
- [ ] 밤 상점: 진열대 확장(최대 8칸), 정밀 저울(% 표시), 심연 확대경(큰 돋보기·쉬운 클릭), 시금석(긁어서 속 금속 확인, 손님 인내심 -1), 경비 고블린(금고 털이 방지), 새 간판(손님 +1)
- [ ] 수치는 `src/data/progression.gd`

---

## Implementation Notes

- 업그레이드 효과는 `RunState.level(id)` 하나로 조회
- 시금석 단서는 "scratch" — 가짜면 항상 참, 추궁 가능

---

## Out of Scope

- M4: 바알·피프 초상화와 대사 이벤트, 단골 스토리 / M5: 촛불

---

## Files

- `cursed-pawnshop/src/data/progression.gd`
- `cursed-pawnshop/src/shop/run_state.gd`, `upgrade_panel.gd`, `shop_view.gd`, `shelf_panel.gd`
- `cursed-pawnshop/src/appraisal/item_generator.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: 상점·바알 방문·엔딩 스크린샷 `production/qa/evidence/story-004-m3-debt-growth/`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-003-m2-trade.md`
- Unlocks: M4
