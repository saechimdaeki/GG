# 온라인 참가 복구 — 2026-11-09

## 동작

- 연결 성공 시에만 메뉴를 닫음.
- 연결 중 안내·취소 버튼, 10초 타임아웃(@export).
- 실패/취소 후 오프라인 peer와 가구 물리 복구, LAN 검색 재시작.
- 이전 시도의 타이머가 새 연결을 취소하지 않음.

## 검증 결과

`python3 haunted-movers/tests/integration/run_connection_checks.py` → PASS network integration, exit 0, ERROR 없음.

실패·취소·재시도·이전 타임아웃 격리·취소 후 호스트 전환 PASS. 실제 독립 호스트/참가 프로세스에서 2명 복제, 2일차/팀 돈 250원/활성 가구 9개 동기화, 참가자의 기절 회복 PASS.

최종 전체 `tests/smoke/run_check.gd` → PASS, exit 0. `godot --headless --path haunted-movers --quit-after 60` → exit 0. 두 검증 ERROR 없음. 기존 CanvasItem/ObjectDB 종료 WARNING 남음.

연결 중과 실패 후 화면을 실제 Metal 렌더러로 캡처하여 확인: connection-pending-2026-11-09.png, connection-failed-2026-11-09.png. 취소 버튼·실패 안내·재시도 메뉴 가독성 확인.

CI에 테스트 단계 추가, 원격 실행은 아직 안 함. 외부 인터넷·다른 PC·실제 사람 협동·실제 마이크 검증은 별도 필요.
