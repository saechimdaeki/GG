# Story 006: M5 저주 — 저주품·촛불·밤의 저주 사건·게이지·정화

> **Epic**: 저주받은 전당포
> **Status**: Done (플레이테스트 대기)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-30

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP milestone 5`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 판매자가 저주품을 가져옴(성격별 확률). 저주품은 진품 재질·각인·무게라 저울/돋보기로는 못 잡음
- [x] 촛불 비추기: 저주품이면 보랏빛 오라 + 단서 노트. "저주받은 물건이잖소?" 추궁 → 헐값
- [x] 저주 수집가(수도사·역병의사·가면 귀족·나방 인간·늪 마녀·그림자)는 저주품에 2.5배. 다른 손님에게 팔면 원한·명성 −
- [x] 밤마다 창고의 저주품 하나당 저주 게이지 +10, 35% 확률로 저주 사건(속삭임·굶주림·피흘림·응시)
- [x] 저주 게이지 100 → 광기 엔딩. 매일 −5
- [x] 정화: 창고에서 60G로 저주 제거(게이지 −10). 밤 상점 "정화 소금" 80G = 게이지 −30
- [x] 수치는 `src/data/curses.gd`

---

## Files

- `cursed-pawnshop/src/data/curses.gd`, `story.gd`, `progression.gd`
- `cursed-pawnshop/src/appraisal/item_generator.gd`, `item_drawer.gd`
- `cursed-pawnshop/src/shop/customer.gd`, `run_state.gd`, `shelf_panel.gd`, `shop_view.gd`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: 촛불 오라·저주 사건·정화 스크린샷 `production/qa/evidence/story-006-m5-curse/`

**Status**: [x] screenshots retained · 28일 봇: 촛불 안 쓰면 5판 중 2판 광기, 촛불+정화면 5판 모두 생존

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-005-m4-story.md`
- Unlocks: M6
