# Story 001: 마왕 조작 전투 (대시·근접·마법탄)

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (feel sign-off pending user playtest)
> **Layer**: Core
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 3`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] WASD로 마왕이 8방향 이동
- [x] Space(또는 Shift)로 대시: 짧은 거리 급이동, 대시 중 무적, 쿨다운 있음
- [x] 좌클릭: 마우스 방향으로 근접 휘두르기, 범위 안 적에게 데미지 (메인 딜)
- [x] 우클릭: 마우스 방향으로 마법탄 발사, 최대 3발, 시간 지나면 1발씩 충전
- [x] 테스트용 용사 1명이 마왕에게 다가와 공격하고, HP 0이면 사라짐
- [x] 마왕도 HP가 있고 피격 시 깜빡임
- [x] 타격감: 적중 시 화면 흔들림 + 짧은 히트스톱
- [x] 화면에 마왕 HP와 마법탄 잔량 표시

---

## Implementation Notes

- 아트 없음: 색 사각형/원(ColorRect·draw)으로 대체. 스프라이트는 나중에 교체
- 수치(속도, 쿨다운, 데미지, 충전 시간)는 `@export` 로 노출해 튜닝 가능하게

---

## Out of Scope

- Story 002: 그리드 맵, 방/함정
- Story 004: 용사 길찾기 (여기 용사는 마왕을 직선 추적만)

---

## Files

- `project.godot` (main scene, 640x360 pixel viewport)
- `src/scenes/main.tscn`, `src/scripts/main.gd`, `src/scripts/demon_lord.gd`, `src/scripts/hero.gd`, `src/scripts/magic_bolt.gd`, `src/scripts/hud.gd`, `src/scripts/hit_feedback.gd`
- `tests/smoke/story_001_check.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: retained screenshot in `production/qa/evidence/` + `production/qa/evidence/story-001-demon-lord-combat-evidence.md`

**Status**: [x] `production/qa/evidence/story-001-demon-lord-combat-evidence.md`

---

## Dependencies

- Depends on: None
- Unlocks: `production/epics/demon-lord-is-busy/story-003-monsters.md`, `production/epics/demon-lord-is-busy/story-004-hero-ai.md`
