# AGENTS.md (Codex 등 다른 에이전트용)

이 프로젝트는 Claude Code 와 번갈아 작업함. 시작 전에 반드시:

1. `ROADMAP.md`(남은 일) + `production/session-state/active.md` 읽기 — 진행표, 다음 할 일, 결정 로그
2. 진행 중인 스토리 파일(`production/epics/demon-lord-is-busy/story-00N-*.md`)의 Acceptance Criteria 대로 구현
3. 기획 원본은 `design/game-brief.md`. 범위 밖(Out of scope) 기능은 만들지 말 것
4. Godot 4.7 API 는 학습 데이터 이후 버전 — 불확실하면 `docs/engine-reference/godot/` 확인
5. 작업 끝날 때마다 `ROADMAP.md` 체크박스와 `active.md` 의 진행표, "지금 하던 일 / 다음 할 일" 갱신

규칙: GDScript, 정적 타입, snake_case 파일/변수, PascalCase 클래스. 튜닝 수치는 `@export` 또는 `src/data/` 에.
검증: `godot --headless --path . --quit-after 60` 출력에 ERROR 없어야 함.

## 릴 던전 (`reel-dungeon/`, Unity) 작업일 때

위 Godot 규칙 대신 **`reel-dungeon/HANDOFF.md`를 먼저 읽는다.** 작업 폴더·커밋 절차(별도 worktree), 빌드·테스트·밸런스 봇 명령, 코드 지도, 남은 일이 다 있다. 사용자에게는 항상 한국어로 답한다.
