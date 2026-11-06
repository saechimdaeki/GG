# Story 002 evidence — 트럭 적재 + 보수

- 2026-11-06, Godot 4.7.2, macOS
- `story-002-result.png`: 호스트 — 상자·의자·램프 적재 후 출발, 기본 130 + 보너스 43 (빈틈 없이 67%) = 173원
- `story-002-client-result.png`: 참가자 화면에 같은 결과가 표시됨 (호스트 계산 → RPC)
- smoke PASS: 보수 계산 4건, 떨어뜨린 의자 내구도 감소, 짐칸 출발 1개 적재. damage 0·짐칸 이동 시 FAIL 확인
- 미확인: 사람이 직접 들고 와서 싣는 손맛
