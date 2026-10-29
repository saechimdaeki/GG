# Story 009: UI 개편·튜토리얼·컷신

> **Epic**: 저주받은 전당포
> **Status**: Done (컷신 그림 대기 · 플레이테스트 대기)
> **Layer**: Presentation
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-30

## Context

**GDD**: `design/game-brief.md`
**Requirement**: 유저 피드백 — "PPT 같은 UI", "튜토리얼 없음", "뜬금없음, 컷신 있으면", "확대하면 잘 안 보임"

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] 갈무리 도트 폰트(OFL) 전체 적용, 크기는 픽셀 정배수로만 (UIFont)
- [x] 도트 9-slice 창·버튼 테두리
- [x] 타이틀: 발광 도트 로고, 상자 없는 텍스트 메뉴 + 촛불 커서, 키보드 조작
- [x] 돋보기용 348px 고해상도 그림
- [x] 컷신 플레이어(레터박스·줌인·타자기 자막·건너뛰기): 오프닝 6장, 2·3·4주차, 금고 발견, 마지막 밤
- [x] 장면 전환 페이드 (가게 열기·닫기·다음 날)
- [x] 첫날 첫 손님 단계별 튜토리얼(강조 영역 + 피프 말풍선, 실제 행동해야 진행, 건너뛰기, 한 번만)
- [ ] 컷신 전용 그림 11장 (art/PROMPTS.md 묶음 6 — Grok. 없으면 기존 배경으로 대체)

---

## Files

- `cursed-pawnshop/src/ui/ui_font.gd`, `src/ui/cutscene_player.gd`, `src/data/cutscenes.gd`, `src/shop/tutorial.gd`
- `cursed-pawnshop/src/shop/shop_view.gd`, `shelf_panel.gd`, `story_panel.gd`, `upgrade_panel.gd`, `src/title/title.gd`, `src/title/pause_menu.gd`, `src/appraisal/item_drawer.gd`
- `cursed-pawnshop/assets/fonts/` (Galmuri, OFL), `assets/ui/` (frames), `assets/sprites/items_hd/`

---

## Test Evidence

`production/qa/evidence/story-009-ui-tutorial/` (오프닝 컷신·튜토리얼 단계), `story-007-m6-polish/title.png`

---

## Dependencies

- Depends on: `production/epics/cursed-pawnshop/story-008-prerelease.md`
