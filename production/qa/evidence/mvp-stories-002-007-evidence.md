# Evidence — Stories 002–007

- Smoke check (24/24 PASS, 2026-10-28): `godot --headless --path . -s res://tests/smoke/story_001_check.gd`
  - 002 build phase, dig cost, wall refused when it cuts the last lane, spike placed, fire locked
  - 003 goblin placed, one per cell, dead monster permanently gone
  - 004 raid starts, hero path ends at core
  - 005 wave 1 spawns at entrance, clear advances wave, 10 waves with boss last
  - 006 reward phase after clear, 3 distinct cards, fire unlock, pick returns to build
  - 007 core takes damage, core 0 ends run
- Screenshots (movie writer, unattended):
  - `production/qa/evidence/build-phase.png` — grid, traps, monsters, build HUD
  - `production/qa/evidence/raid-phase.png` — warrior pathing in, slime engaging (telegraph ring)
  - `production/qa/evidence/reward-screen.png` — 3 reward cards (story 006 UI evidence)

## Not verified by automation (needs a human playtest)
- 궁수 탄을 대시로 피하는 감각, 보스 탄막(10방향) 모양, 승리 화면 표시
- 밸런스 전반 (웨이브 난이도, 골드 수급)
