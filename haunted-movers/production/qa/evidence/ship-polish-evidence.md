# 출시 기본기 evidence

- 2026-11-06, Godot 4.7.2, macOS
- `ship-main-menu.png`: 메뉴에 버전 v0.1.0, "게임 종료" 버튼
- `ship-esc-menu.png`: 플레이 중 마우스가 풀리면 Esc 메뉴 (계속하기·전체 화면 F11·메뉴로 나가기·게임 종료)
- 메뉴로 나가기 런타임 확인: 호스트 → 나가기 → 오프라인·플레이어 0 → 같은 포트로 다시 호스트 OK
- 호스트 끊김 시 참가자에게 안내 + 마우스 풀림 → Esc 메뉴에서 나가기
- 앱 아이콘(icon.svg → Mac icon.icns) 확인, Mac zip 60 MB · Windows exe 109 MB 재빌드, 내보낸 Mac 앱 실행 OK
- Windows exe 아이콘은 rcedit 없이 기본 아이콘 (modify_resources=false)
