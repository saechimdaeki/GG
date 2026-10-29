# Story 007: 코어 HP + 런 종료/재시작

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (playtest sign-off pending)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 7`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 던전 코어는 HP가 있고 화면에 표시됨
- [x] 코어 HP 0 → 패배 화면 ("도달 웨이브 N"), 재시작 버튼
- [x] 마왕 HP 0 → 마왕은 몇 초 뒤 코어 옆에서 부활 (런은 계속), 코어만 패배 조건
- [x] 재시작 시 골드·몬스터·강화·웨이브 전부 초기화

---

## Implementation Notes

- 재시작은 `get_tree().reload_current_scene()` 로 충분

---

## Out of Scope

- 세이브 (brief out of scope)

---

## Files

- `src/scripts/game.gd`, `src/scripts/core.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/core/core_run_end_test.gd` (waived at qa.level minimal)

**Status**: [x] `production/qa/evidence/mvp-stories-002-007-evidence.md`

---

## Dependencies

- Depends on: `production/epics/demon-lord-is-busy/story-005-waves.md`
- Unlocks: None
