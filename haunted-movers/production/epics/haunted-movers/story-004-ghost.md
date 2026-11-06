# Story 004: 유령 1종

> **Epic**: 유령집 이삿짐센터
> **Status**: In Review — 코드·자동 체크 완료 (음악 뒤틀림: 절차 생성 라운지 재즈 + 유령 근처 피치 흔들림, 2026-11-06 추가)
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-06

## Context

**GDD**: `haunted-movers/design/game-brief.md`
**Requirement**: Brief MVP feature 5

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 상태: 순찰 → 발견(가구 든 사람 우선) → 방해 → 쿨다운
- [ ] 방해: 든 가구를 밀치거나 들고 있는 사람을 잠깐 기절(놓치게 됨)
- [ ] 기절은 몇 초 후 회복. 전원이 동시에 기절 → 그날 실패
- [ ] 접근 시 음악이 뒤틀리고 화면 노이즈
- [ ] 퇴마 테이프(Story 005) 맞으면 일정 시간 무력화 — 훅만 준비
- [ ] 속도·시야·쿨다운은 데이터 값
- [ ] AI는 호스트에서만 실행, 위치 동기화

---

## Implementation Notes

- 순수 상태 전이 함수로 분리 → 단위 테스트
- 구현: 유령은 벽을 통과 → 내비 없이 직선 이동 (ponytail: 벽 통과가 문제면 NavigationAgent3D)

---

## Out of Scope

- 유령 여러 종류

---

## Files

- haunted-movers/src/ghost.gd (상태 전이 next_state, 순찰·추격·공격, exorcise 훅)
- haunted-movers/src/main.gd (stun_player, 전원 기절 → day_failed, announce, 화면 노이즈)
- haunted-movers/src/player.gd (stun, 쓰러진 모습 동기화)
- haunted-movers/src/vhs.gdshader (noise)
- haunted-movers/data/tuning.json (ghost)
- haunted-movers/tests/smoke/run_check.gd (상태 전이 6건 + 공격 통합)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*qa.level: minimal — tests waived (advisory).*

**Story Type**: Logic
**Required evidence**: `tests/unit/ghost/ghost_state_test.gd` (권장)
**Status**: [x] smoke PASS (시야 0이면 FAIL 4건 확인) + `production/qa/evidence/story-004-client.png`

---

## Dependencies

- Depends on: `haunted-movers/production/epics/haunted-movers/story-003-house-and-furniture.md`
- Unlocks: story-005
