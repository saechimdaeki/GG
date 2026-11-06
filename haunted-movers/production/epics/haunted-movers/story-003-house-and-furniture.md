# Story 003: 유령집 1채 + 가구 6종

> **Epic**: 유령집 이삿짐센터
> **Status**: In Review — 집·가구·저주 구현, 자동 체크 PASS. 아트 사인오프 대기
> **Layer**: Feature
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-06

## Context

**GDD**: `haunted-movers/design/game-brief.md`
**Requirement**: Brief MVP feature 3

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 2층 유령집 1채: 계단·좁은 복도·문 — 큰 가구는 돌려야 통과
- [ ] 집 앞에 트럭 주차
- [ ] 가구 6종 (예: 의자·램프·소파·옷장·냉장고·피아노), 무게·크기 다름
- [ ] 저주 행동 2종: 옷장 = 아무도 안 잡으면 슬금슬금 도망 / 피아노 = 잡은 사람을 가끔 튕겨냄
- [ ] 로우폴리 + 따뜻한 VHS 톤, 가구에 귀여운 얼굴
- [ ] 저주 행동이 두 플레이어 화면에 똑같이 보임

---

## Implementation Notes

- 가구 = 공통 씬 + 데이터(무게·가치·저주 스크립트)
- 모델은 CSG/기본 메시로 시작, 아트는 나중에 교체

---

## Out of Scope

- Story 004: 유령

---

## Files

- haunted-movers/src/house.gd (2층 집·좁은 문·계단 램프·지붕·실내 조명)
- haunted-movers/src/furniture.gd (얼굴, 저주 runaway/buck)
- haunted-movers/src/main.gd (집 배치, 노을·안개, VHS 오버레이, buck → 강제 놓기 RPC, --cam)
- haunted-movers/src/vhs.gdshader
- haunted-movers/data/tuning.json (가구 6종 배치, curses)
- haunted-movers/tests/smoke/run_check.gd (runaway·buck 체크)

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: 집·가구 스크린샷 `production/qa/evidence/` + `story-003-house-and-furniture-evidence.md` 사인오프 (minimal에서도 필수)
**Status**: [x] 스크린샷 `story-003-client.png`, `story-003-faces.png` + `story-003-house-and-furniture-evidence.md` (사인오프 칸 비어 있음)

---

## Dependencies

- Depends on: `haunted-movers/production/epics/haunted-movers/story-001-co-op-carry.md`
- Unlocks: story-004
