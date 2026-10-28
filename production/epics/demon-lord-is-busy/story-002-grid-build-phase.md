# Story 002: 그리드 던전 맵 + 방/함정 배치 (건설 페이즈)

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (playtest sign-off pending)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 1`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 던전은 타일 그리드 (벽 / 바닥 / 입구 / 코어)
- [x] 건설 페이즈와 침입 페이즈가 나뉨. 건설 중엔 용사가 안 옴, "시작" 버튼(또는 Enter)으로 침입 시작
- [x] 건설 중 마우스 클릭으로 바닥 타일에 함정 배치 (가시 함정 1종: 밟은 용사에게 데미지)
- [x] 벽 타일 클릭으로 파서 바닥으로 만들 수 있음 (방 넓히기)
- [x] 입구→코어 경로를 완전히 막는 배치는 거부됨
- [x] 배치에는 골드가 들고, 골드 잔량 표시

---

## Implementation Notes

- 그리드는 `TileMapLayer` 또는 2D 배열 + 사각형 그리기 중 단순한 쪽
- 경로 막힘 체크는 BFS 하나면 충분

---

## Out of Scope

- Story 003: 몬스터 배치
- Story 004: 용사의 실제 길찾기 이동

---

## Files

- `src/scripts/dungeon_grid.gd`, `src/scripts/game.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/dungeon/grid_build_phase_test.gd` OR playtest doc (waived at qa.level minimal)

**Status**: [x] `production/qa/evidence/mvp-stories-002-007-evidence.md`

---

## Dependencies

- Depends on: `production/epics/demon-lord-is-busy/story-001-demon-lord-combat.md`
- Unlocks: `production/epics/demon-lord-is-busy/story-003-monsters.md`, `production/epics/demon-lord-is-busy/story-004-hero-ai.md`
