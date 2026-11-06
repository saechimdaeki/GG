# Story 005: 상점 + 할당량 3일 런

> **Epic**: 유령집 이삿짐센터
> **Status**: In Review — 코드·자동 체크 완료
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-06

## Context

**GDD**: `haunted-movers/design/game-brief.md`
**Requirement**: Brief MVP feature 6 + Player goal & fail state

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 하루 = 제한 시간 1판. 시간 초과 시 출발 못 하고 그날 보수 0
- [ ] 런 = 3일, 날마다 할당량 상승. 할당량 미달 누적 3회 → 회사 파산(런 종료 화면)
- [ ] 3일 모두 통과 → 런 클리어 화면
- [ ] 날 사이 상점: 수레(무게 감소)·밧줄(둘이 묶어 끌기)·퇴마 테이프(유령 무력화)
- [ ] 팀 공용 돈. 산 아이템은 다음 날 집에 들고 들어감
- [ ] 할당량·가격·시간은 데이터 값
- [ ] 런 상태는 호스트 소유, 모두 같은 화면

---

## Implementation Notes

- 런 상태(날짜·돈·미달 횟수) 전이를 순수 로직으로 → 단위 테스트
- 구현: 수레·밧줄은 들고 다니는 물건이 아니라 런 동안 팀 효과(힘 +35%, 손 거리 +1.5m), 테이프는 충전식(E). ponytail: 실제 물건화는 재미 확인 후
- 할당량 250/450/700, 미달 3회 파산(브리프 그대로) — 3일 런이라 사실상 3일 다 실패해야 파산. 튜닝 필요하면 data/tuning.json run.max_misses

---

## Out of Scope

- 런 간 저장, 메타 진행

---

## Files

- haunted-movers/src/run_state.gd (순수 런 규칙)
- haunted-movers/src/main.gd (타이머, 하루 종료·상점·다음 날 리셋·테이프, HUD, RPC 동기화)
- haunted-movers/src/player.gd (E 테이프, 손 거리 보너스, 상점 중 마우스)
- haunted-movers/src/ghost.gd (하루 중에만 행동, exorcise(0) 버그 수정)
- haunted-movers/data/tuning.json (run)
- haunted-movers/tests/smoke/run_check.gd (런 규칙 + 리셋·테이프)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*qa.level: minimal — tests waived (advisory).*

**Story Type**: Logic
**Required evidence**: `tests/unit/run/run_state_test.gd` (권장)
**Status**: [x] smoke PASS (파산 비교·테이프 거리 깨뜨리면 FAIL 확인) + `production/qa/evidence/story-005-*-shop.png`

---

## Dependencies

- Depends on: story-002, story-004
- Unlocks: None
