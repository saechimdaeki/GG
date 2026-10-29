# Story 003: M2 장사 — 진열대·사는 손님·시세

> **Epic**: 저주받은 전당포
> **Status**: In Progress
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP milestone 2`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 금·은·청동 시세가 매일 바뀜(×0.6~×1.8). 아침 소식 일부가 특정 금속 시세를 움직임. HUD에 항상 표시
- [ ] 산 물건은 밤에 자동으로 팔리지 않고 창고에 쌓임. 매입가·시세 가치가 보임
- [ ] 아침 진열대 정리: 창고에서 진열할 물건(칸 4개) 고르고 가격표 매기기
- [ ] 사는 손님: 진열품 중 하나를 골라 값을 부름 → 내가 부르고 → 손님이 올리거나 떠남. 진열품이 없으면 그냥 떠남
- [ ] 가짜를 진품 값에 팔 수 있음. 깐깐한 손님은 가짜를 알아채고 화냄(원한), 속은 손님은 나중에 알아채 원한이 쌓임
- [ ] 밤 도매상: 창고 물건을 시세의 70%에 즉시 처분
- [ ] 카운터 앞에 진열품과 가격표가 보이고, 손님이 고른 물건이 감정 확대창에 뜸
- [ ] 수치(시세 폭·소식 효과·진열 칸·사는 손님 수·흥정)는 `src/data/` 에

---

## Implementation Notes

- 매입 손님 호가도 시세를 따름 (청구 재질 시세 × 성격 배율)
- 판매 흥정은 매입 흥정의 거울: 손님 최대 지불액은 숨김, 역제안으로 드러남

---

## Out of Scope

- M3: 진열 칸 업그레이드, 주간 빚 / M4: 평판·경비대(가짜 판매 적발)

---

## Files

- `cursed-pawnshop/src/data/market.gd`, `customers.gd`, `events.gd`
- `cursed-pawnshop/src/shop/customer.gd`, `haggle.gd`, `run_state.gd`, `shelf_panel.gd`, `shop_view.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: 진열대 정리·판매·밤 도매 스크린샷 `production/qa/evidence/story-003-m2-trade/`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-002-m1-day-cycle.md`
- Unlocks: M3
