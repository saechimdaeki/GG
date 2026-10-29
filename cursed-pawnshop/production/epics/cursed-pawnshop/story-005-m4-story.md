# Story 005: M4 이야기 — 날짜별 사건·NPC·조각 7개·평판·장물·엔딩

> **Epic**: 저주받은 전당포
> **Status**: Done (플레이테스트 대기)
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-29

## Context

**GDD**: `design/game-brief.md`, `design/story.md`
**Requirement**: `Brief MVP milestone 4`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 아침마다 날짜별 스토리 장면(초상화·대사·선택지). 선택이 돈·명성·의심·플래그·조각에 영향
- [x] 흐르그의 조각 7개: 금고에 지키거나, 형제단에 팔거나, 빼앗김
- [x] 명성: 공정 거래·사기꾼 적발·장물 신고 +, 사기·적발 −. 높으면 사는 손님 +1, 낮으면 파는 손님 −1
- [x] 장물: "출처는 묻지 마" 판매자. 사면 의심↑, 60이면 경비대 단속. "장물 아니오?" 추궁 → 맞으면 보상·원한
- [x] 드라크마 백작 후원: 사흘마다 진품을 시세 2배에 매입, 가짜가 진열되면 관계 끝
- [x] 엔딩 6종: 각성·봉인지기·장물왕·마계 최고 감정사·빚 청산·차압
- [x] NPC 초상화 10명 (아트 묶음 2, 단독 그림 사용. 바알 방문 장면에도 초상화)

---

## Files

- `cursed-pawnshop/src/data/story.gd`, `customers.gd`
- `cursed-pawnshop/src/shop/story_panel.gd`, `run_state.gd`, `customer.gd`, `shop_view.gd`
- `cursed-pawnshop/src/appraisal/item_generator.gd`

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `production/qa/evidence/story-005-m4-story/` + 28일 봇 시뮬 3경로 모두 엔딩 도달

**Status**: [x] screenshots retained

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-004-m3-debt-growth.md`
- Unlocks: M5
