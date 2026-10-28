# Story 003: 몬스터 3종 배치 + 자동 전투 + 영구 손실

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (playtest sign-off pending)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 4`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 몬스터 3종: 슬라임(탱커, 느림), 고블린(근접 딜러), 해골 궁수(원거리)
- [x] 건설 페이즈에 골드로 바닥 타일에 배치
- [x] 침입 페이즈에 사거리 안 용사를 자동 공격, 자기 자리 근처에서만 움직임
- [x] 몬스터는 HP가 있고, 0이 되면 그 런에서 영구 제거 (다음 웨이브에 부활 없음)
- [x] 살아남은 몬스터는 다음 웨이브 시작 시 HP 회복

---

## Implementation Notes

- 몬스터 종류는 하나의 스크립트 + 데이터(Resource 또는 Dictionary)로 구분

---

## Out of Scope

- Story 006: 몬스터 강화 보상

---

## Files

- `src/scripts/monster.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: playtest doc (waived at qa.level minimal)

**Status**: [x] `production/qa/evidence/mvp-stories-002-007-evidence.md`

---

## Dependencies

- Depends on: `production/epics/demon-lord-is-busy/story-002-grid-build-phase.md`
- Unlocks: `production/epics/demon-lord-is-busy/story-006-wave-reward.md`
