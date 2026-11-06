# Story 001 evidence — 둘이서 같이 들기

- 2026-11-06, Godot 4.7.2, macOS, 같은 머신 창 2개 (`--host --demo-grab`, `--join --demo-grab`)
- `story-001-client.png`: 참가자 화면 — 호스트(초록)가 소파 반대편에서 함께 들어 올린 상태가 동기화되어 보임
- `story-001-host.png`: 호스트 화면 (참가자가 먼저 종료된 뒤 촬영)
- 무게 규칙: `tests/smoke/run_check.gd` PASS (힘 1000으로 올리면 FAIL — 실패 확인)
- 문 통과: smoke `_check_carry_to_truck` — 봇 2명이 소파를 90° 돌려 현관(1.6 m)을 지나 트럭에 싣고 출발 PASS. 안 돌리면 문에 걸려 FAIL (z=0.55) 확인
- 미확인: "웃긴가" — 사람 플레이테스트 필요
