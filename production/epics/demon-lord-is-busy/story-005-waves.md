# Story 005: 웨이브 10개 + 마지막 용사 보스

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (playtest sign-off pending)
> **Layer**: Core
> **Type**: Config/Data
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 5`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 웨이브 1~10이 데이터 한 곳에 정의됨 (용사 종류·수·생성 간격)
- [x] 웨이브가 진행될수록 용사 수/HP 증가
- [x] 웨이브 10은 용사 보스 1명 (큰 HP, 탄막 패턴 1개)
- [x] 화면에 현재 웨이브 번호 표시
- [x] 웨이브의 용사가 모두 처치되면 웨이브 클리어 → 건설 페이즈로 복귀
- [x] 웨이브 10 클리어 시 승리 화면

---

## Implementation Notes

- 웨이브 데이터는 `src/data/waves.gd`(const 배열) 하나로

---

## Out of Scope

- Story 006: 클리어 보상

---

## Files

- `src/data/waves.gd`, `src/scripts/game.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: smoke check pass

**Status**: [x] `production/qa/evidence/mvp-stories-002-007-evidence.md`

---

## Dependencies

- Depends on: `production/epics/demon-lord-is-busy/story-004-hero-ai.md`
- Unlocks: `production/epics/demon-lord-is-busy/story-006-wave-reward.md`, `production/epics/demon-lord-is-busy/story-007-core-run-end.md`
