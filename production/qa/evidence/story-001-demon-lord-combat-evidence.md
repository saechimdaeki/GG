# Evidence — Story 001: 마왕 조작 전투

- Screenshot: `production/qa/evidence/story-001-arena.png` (movie-writer frame 140: 테스트 용사가 마왕을 추적해 1회 타격, HUD `HP 9/10 BOLT ooo`)
- Headless check: `godot --headless --path . -s res://tests/smoke/story_001_check.gd` → PASS melee / bolt / dash-invuln (2026-10-28)
- 부팅: `godot --headless --path . --quit-after 120` 에러 없음

## Not verified by automation
- 타격감(화면 흔들림·히트스톱)과 조작감은 사람이 직접 플레이해서 확인 필요 → **sign-off: pending (user)**
