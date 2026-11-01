# 가라앉은 도시

물에 잠긴 고대 도시를 잠수해 탐사하고, 펌프를 고쳐 물을 빼면 그 구역이 마을이 되는 2D 탐험·재건 게임.

**상태:** M1 플레이 가능 — 잠수, 산소, 벽 부수기, 재료 줍기, 펌프 수리 → 물이 빠짐. 그림은 아직 코드 그림

## 실행
```bash
godot --path sunken-city
```
| 키 | 동작 |
|---|---|
| 방향키 / WASD | 이동 (물속은 헤엄, 마른 땅은 걷기) |
| Space | 점프 (수면 가까이서 누르면 물 밖으로) |
| J / X | 바라보는 칸 부수기 (금 간 벽 2번, 잔해 1번 → 황동 조각) |
| E | 펌프 수리 (황동 조각 3 · 파이프 1 · 톱니 1) |

산소는 물속에서 줄고 공기 식물·물 밖에서 다시 참. 다 떨어지면 출발점에서 깨어나고 재료 절반을 잃음.

테스트: `godot --headless --path sunken-city -s res://tests/smoke/run_check.gd` → `PASS`

| 문서 | 내용 |
|---|---|
| [`design/game-brief.md`](design/game-brief.md) | 기획서 — 루프, 마일스톤 M1~M6 |
| [`art/PROMPTS.md`](art/PROMPTS.md) | 이미지 인계 — Gemini 프롬프트, 파일명, 저장 위치 |

## 그림 넣는 법
1. [`art/PROMPTS.md`](art/PROMPTS.md)의 묶음 1부터 Gemini로 생성
2. 파일명 그대로 `art/raw/<폴더>/`에 저장
3. Claude에게 "이미지 넣었어"
