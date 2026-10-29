# Story 006: 웨이브 보상 3택 1

> **Epic**: 마왕님은 바쁘다
> **Status**: Done (playtest sign-off pending)
> **Layer**: Feature
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-28

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 6`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 웨이브 클리어 후 카드 3장이 뜨고 1장을 클릭해서 고름
- [x] 보상 풀: 골드 보너스, 새 함정 해금, 몬스터 강화(종류별 HP/공격 +%), 마왕 스킬 강화(근접 데미지/마법탄 수/대시 쿨다운)
- [x] 몬스터 강화는 그 런 동안 누적됨 (키우기 요소)
- [x] 같은 화면에 같은 카드가 두 번 안 나옴
- [x] 고른 뒤 건설 페이즈로 이동

---

## Implementation Notes

- 보상 정의는 데이터 배열 하나 + 적용 함수

---

## Out of Scope

- 메타 업그레이드 (brief out of scope)

---

## Files

- `src/scripts/reward_screen.gd`, `src/data/rewards.gd`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**: screenshot of reward screen in `production/qa/evidence/`

**Status**: [x] `production/qa/evidence/mvp-stories-002-007-evidence.md`

---

## Dependencies

- Depends on: `production/epics/demon-lord-is-busy/story-003-monsters.md`, `production/epics/demon-lord-is-busy/story-005-waves.md`
- Unlocks: None
