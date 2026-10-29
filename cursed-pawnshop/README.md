# 저주받은 전당포

몬스터 손님이 가져온 물건을 감정해 가짜·저주를 골라내고 흥정하는 상점 심 + 조사 퍼즐.

- 기획서: [`design/game-brief.md`](design/game-brief.md)
- 스토리: [`production/epics/cursed-pawnshop/`](production/epics/cursed-pawnshop/EPIC.md) (5개)
- 상태: M4(이야기)까지 플레이 가능 — 28일 스토리·NPC 사건·조각 7개·평판·장물·엔딩 6종. NPC 초상화 포함

## 실행 (Godot 4.7.2 필요)

```bash
cd cursed-pawnshop
godot --headless --import   # 클론 직후 한 번
godot
```

조작: 물건 위에 마우스 → 돋보기, 수상한 곳 클릭 → 단서 노트, 저울 버튼 → 무게. 오른쪽 아래 선택지로 질문·추궁·흥정.
디버그용 고정 시드: `godot -- --seed=36`

## 아트 넣기

1. `art/PROMPTS.md` 프롬프트로 이미지 생성 (마젠타 배경) → `art/raw/portraits/`, `art/raw/items/`, `art/raw/scenes/`
2. 변환:
   ```bash
   python3 tools/sprite_convert.py art/raw/portraits assets/sprites/portraits 75
   python3 tools/sprite_convert.py art/raw/items assets/sprites/items 58
   godot --headless --import
   ```
   없는 그림은 코드 그림이 대신 그려짐.
