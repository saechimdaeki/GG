# Story 002: M1 하루 순환 — 손님·대화·감정·추궁·흥정·원한·밤 정산

> **Epic**: 저주받은 전당포
> **Status**: In Progress
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP milestone 1`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 런 시작 시 괴물 손님 명부(10명 이상)가 시드로 만들어짐: 종족·이름·성격·호감도. 같은 괴물이 여러 날에 다시 옴
- [ ] 가게 화면: 촛불 조명 가게, 카운터 뒤 괴물(표정: 보통/땀/짜증/화남/기쁨), 대화창(타자기), 감정 확대창 + 돋보기
- [ ] 대화: 질문(어디서 났나/왜 파나). 정직한 손님은 답이 일정, 사기꾼은 물을 때마다 말이 바뀌고 땀을 흘림
- [ ] 감정이 어려움: 각인이 물건 어딘가에 작게 숨어 있어 클릭해서 찾아야 함, 무게 차이 5~14%, 무게를 맞춘 가짜·도금 벗겨진 가짜, 진품도 흠집 많음
- [ ] 추궁: 찾은 단서(각인/무게/도금)로 추궁. 맞으면 값이 떨어지고, 틀리면 손님이 화냄
- [ ] 흥정: 호가 → 제시가 → 역제안/수락/퇴장. 너무 낮으면 모욕으로 인내심 크게 감소
- [ ] 원한·호감: 헐값·억울한 추궁·퇴장이면 원한 +, 좋은 거래면 호감 +. 원한이 쌓인 괴물은 "두고 보자" 후 며칠 뒤 복수 이벤트(돈 뺏기·유리창·나쁜 소문), 호감 높은 괴물은 선물 이벤트
- [ ] 하루: 아침(소식 + 가게 열기) → 낮(손님 N명) → 밤(매입품 되팔기 정산·이벤트 결과·소지금) → 다음 날. 날짜 표시
- [ ] 수치(성격·흥정·원한·이벤트·하루 손님 수)는 전부 `src/data/` 에 있음

---

## Implementation Notes

- 손님·흥정·하루 진행 판정은 순수 함수 (테스트·밸런스 조정 쉽게)
- 그림은 코드로 그린 임시 도트. 진짜 도트는 M6 (GPT 파이프라인)
- 되팔기는 M2 진열대 전까지 밤에 자동 판매(실제 가치 × 배율)

---

## Out of Scope

- M2: 진열대·사는 손님·시세 / M3: 주간 빚·상점 / M4: 단골 아크·경비대 / M5: 저주·촛불 / M6: 아트·저장

---

## Files

- `cursed-pawnshop/src/data/items.gd`, `customers.gd`, `events.gd`
- `cursed-pawnshop/src/appraisal/item_generator.gd`, `item_drawer.gd`
- `cursed-pawnshop/src/shop/customer.gd`, `haggle.gd`, `run_state.gd`, `customer_drawer.gd`, `shop_view.gd` (+ `.tscn`)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*`qa.level: minimal` — tests advisory; screenshot still required.*

**Story Type**: Integration
**Required evidence**: 가게·대화·흥정·밤 정산 스크린샷 `production/qa/evidence/story-002-m1-day-cycle/`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-001-item-appraisal.md`
- Unlocks: M2
