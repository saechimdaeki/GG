# 근접 음성 채팅 evidence

- 2026-11-06, Godot 4.7.2, macOS, 창 2개 (`--audio-driver Dummy`, 참가자는 `--voice-test`로 마이크 대신 440 Hz 신호)
- 호스트 로그: `voice heard from <참가자>: 79 chunks` (≈3초) — 네트워크로 음성 조각이 가고 그 플레이어의 3D 스피커에 들어감
- `voice-host-hears-friend.png`: 호스트 화면 — 말하는 친구 머리 위 ♪, 오른쪽 위 마이크 상태
- `voice-client-talking.png`: 참가자 화면 — 오른쪽 위 "● 말하는 중"
- `net-host-left-menu.png`: 호스트가 나가면 2초 뒤 메뉴로, "호스트와 연결이 끊겼어요" 표시 (에러 0)
- Mac 앱 Info.plist에 마이크 권한 문구 들어감
- smoke PASS: PCM 왕복, 리샘플 길이, 음성 게이트, 원격 플레이어 재생, 남의 이름으로 보낸 음성 무시 (확인 빼면 FAIL 확인)
- 같이 고친 것: 동기화 노드 이름 고정("Sync") — 자동 이름(@MultiplayerSynchronizer@N)이 피어마다 달라 "Node not found" 에러가 나던 문제
- 미확인: 실제 마이크로 사람 목소리 (이 컴퓨터에서 마이크 권한 창이 뜨면 테스트가 멈춰서 Dummy 드라이버로 대체)
