# Story 002: 트럭 적재 + 보수 계산

> **Epic**: 유령집 이삿짐센터
> **Status**: In Review — 코드·자동 체크 완료
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-06

## Context

**GDD**: `haunted-movers/design/game-brief.md`
**Requirement**: Brief MVP feature 4

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 트럭 짐칸 = 3D 칸 격자. 가구가 차지하는 칸을 바운딩박스로 계산
- [ ] 짐칸 안에 놓인 가구가 "실림"으로 집계, 밖으로 떨어지면 빠짐
- [ ] 가구 내구도: 충돌 세기에 비례해 깎이고 0이면 부서짐(보수 0)
- [ ] 보수 = Σ(가구 가치 × 남은 내구도 비율) + 적재 효율 보너스(빈칸 적을수록)
- [ ] "출발" 버튼 → 결과 화면에 실은 수·파손·총 보수 표시
- [ ] 가구 가치·내구도·보너스 계수는 데이터 파일(.tres/JSON)에서 읽음
- [ ] 결과는 호스트가 계산해 모두에게 동일하게 표시

---

## Implementation Notes

- 보수 계산은 노드와 분리된 순수 함수로 → 단위 테스트 가능

---

## Out of Scope

- Story 005: 할당량·상점에 보수 쓰기

---

## Files

- haunted-movers/src/truck.gd (짐칸·출발 버튼·evaluate)
- haunted-movers/src/payout.gd (순수 보수 계산)
- haunted-movers/src/furniture.gd (value·durability·충격 피해)
- haunted-movers/src/main.gd (출발 RPC·결과 표시), src/player.gd (버튼 클릭)
- haunted-movers/data/tuning.json (value, impact_threshold, damage_per_impact, truck)
- haunted-movers/tests/smoke/run_check.gd (gdUnit4 미설치 — 레포 관례대로 smoke에 포함)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*qa.level: minimal — tests waived (advisory).*

**Story Type**: Logic
**Required evidence**: `tests/unit/truck/payout_test.gd` (권장)
**Status**: [x] smoke PASS (깨뜨리면 FAIL 확인) + `production/qa/evidence/story-002-*.png`

---

## Dependencies

- Depends on: `haunted-movers/production/epics/haunted-movers/story-001-co-op-carry.md`
- Unlocks: story-005
