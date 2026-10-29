# Story 001: 물건 생성기 + 돋보기·저울 감정

> **Epic**: 저주받은 전당포
> **Status**: Done (M1 에 흡수 — 감정대 화면은 가게 화면으로 대체)
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 2`, `Brief MVP feature 3` (돋보기·저울)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 물건 종류·재질·각인·무게 범위가 데이터 파일 한 곳에 정의됨 (`src/data/`)
- [ ] 생성기가 시드를 받아 진품 / 가짜 / 저주품 중 하나를 만들고, 같은 시드면 같은 물건이 나옴
- [ ] 진품: 각인·흠집·무게가 손님이 주장한 재질과 전부 맞음
- [ ] 가짜: 각인 또는 무게 중 최소 1개가 주장한 재질과 어긋남 (단서 없는 가짜는 생성 안 됨)
- [ ] 물건 클로즈업 화면: 돋보기를 물건 위로 움직이면 확대 영역에 각인·흠집이 보임
- [ ] 저울: 물건을 올리면 무게가 표시되고, 화면에 재질별 기준 무게표가 있음
- [ ] 판정 버튼 3개(진품/가짜/저주품) → 정답이면 맞음, 오답이면 틀림 + 놓친 단서 표시
- [ ] 이 스토리만으로 물건 하나를 감정해 가짜를 잡아낼 수 있음 (재미 검증)

---

## Implementation Notes

- 물건 = 데이터(Resource 또는 Dictionary). 화면은 데이터를 그리기만
- 각인·흠집은 단순한 도형/텍스처 조합이면 충분. 도트 아트는 나중에
- 저주품 판정은 이 스토리에서 버튼만 있음. 촛불 단서는 Story 004

---

## Out of Scope

- Story 002: 가격·흥정
- Story 003: 손님 등장·대사
- Story 004: 촛불, 저주 효과

---

## Files

- `cursed-pawnshop/project.godot`
- `cursed-pawnshop/src/data/items.gd`
- `cursed-pawnshop/src/appraisal/item_generator.gd`
- `cursed-pawnshop/src/appraisal/item_drawer.gd`
- `cursed-pawnshop/src/appraisal/appraisal_view.gd`
- `cursed-pawnshop/src/appraisal/appraisal_view.tscn`

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*`qa.level: minimal` — tests advisory.*

**Story Type**: Logic
**Required evidence**: `tests/unit/appraisal/item_generator_test.gd` (advisory) + 감정 화면 스크린샷 `production/qa/evidence/appraisal-view.png`

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: `production/epics/cursed-pawnshop/story-002-haggle.md`, `production/epics/cursed-pawnshop/story-004-candle-night-settlement.md`
