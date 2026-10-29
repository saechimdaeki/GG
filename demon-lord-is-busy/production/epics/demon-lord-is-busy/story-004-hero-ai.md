# Story 004: 용사 AI — 코어까지 길찾기 + 전투

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (playtest sign-off pending)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 2`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 용사는 입구에서 생성되어 그리드 경로(A* 또는 BFS)로 코어를 향해 이동
- [x] 이동 중 일정 거리 안에 마왕이나 몬스터가 있으면 멈추고 공격
- [x] 목표가 죽거나 멀어지면 다시 코어로 이동
- [x] 코어에 닿으면 코어에 데미지를 주고 사라짐
- [x] 용사 2종: 전사(근접), 궁수(원거리, 탄 발사 — 마왕이 대시로 피할 수 있음)

---

## Implementation Notes

- Godot `AStarGrid2D` 사용 가능 여부는 `docs/engine-reference/godot/` 확인 후 사용

---

## Out of Scope

- Story 005: 웨이브 구성, 보스

---

## Files

- `src/scripts/hero.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/hero/hero_ai_test.gd` (waived at qa.level minimal)

**Status**: [x] `production/qa/evidence/mvp-stories-002-007-evidence.md`

---

## Dependencies

- Depends on: `production/epics/demon-lord-is-busy/story-002-grid-build-phase.md`
- Unlocks: `production/epics/demon-lord-is-busy/story-005-waves.md`
