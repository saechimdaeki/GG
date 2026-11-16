# 기절·다음 날 회복 회귀 검증 — 2026-11-09

- 대상: story 004 유령 기절 / story 005 다음 날 초기화
- 수정: 기절 중 hold 요청 거부; _respawn_local에서 Player.recover 호출로 기절·타이머·들기 상태 초기화
- 추가 검사: 기절 직후 재잡기 거부, 다음 날 즉시 회복, 회복 후 정상 잡기
- 최종 전체 테스트: `godot --headless --path haunted-movers --log-file /tmp/gg-haunted-final-smoke.log -s res://tests/smoke/run_check.gd` → PASS, exit 0, ERROR 없음
- 시작 검증: `godot --headless --path haunted-movers --log-file /tmp/gg-haunted-final-start.log --quit-after 60` → exit 0, ERROR 없음
- 기존 baseline과 최종 테스트 모두 종료 시 CanvasItem RID 1개 및 ObjectDB 누수 WARNING 발생. 이번 수정으로 해결하지 않음.
- 최초 샌드박스 실행의 macOS 인증서 접근 ERROR는 sandbox 밖 동일 Godot 실행에서 사라짐.
- 실제 다른 PC 협동·지연 패킷 재현·사람 플레이테스트는 미실시. 자동 테스트는 호스트 hold 경로 및 로컬 respawn 경로를 검증함.
- 시각·아트 사인오프 및 기존 스토리 완료 상태 변경 없음.
