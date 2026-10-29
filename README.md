# GG

제제이야

게임을 여러 개 만들어 모아두는 레포. 게임 하나 = 루트의 폴더 하나.

## 게임 목록

| 게임 | 폴더 | 장르 | 엔진 | 상태 |
|---|---|---|---|---|
| 마왕님은 바쁘다 | [`demon-lord-is-busy/`](demon-lord-is-busy/) | 던전 디펜스 + 액션 로그라이크 | Godot 4.7.2 | 플레이 가능, 플레이테스트 중 |

## 실행

각 게임 폴더의 README에 자세한 실행법·조작·진행 상황이 있음.

**마왕님은 바쁘다** (Godot 4.7.2 필요)

```bash
cd demon-lord-is-busy
godot --headless --import   # 클론 직후 한 번
godot
```

## 새 게임 추가하기

1. 루트에 폴더를 하나 만들고 그 안에 엔진 프로젝트를 통째로 둔다 (`<game-name>/project.godot` 등)
2. 폴더 안에 `README.md` — 실행법, 조작, 어디까지 했는지
3. 위 게임 목록 표에 한 줄 추가
4. 자동 테스트가 있으면 `.github/workflows/smoke.yml` 에 그 폴더용 job 추가

## 레포 구조

| 경로 | 내용 |
|---|---|
| `<game>/` | 게임 하나 (코드, 에셋, 테스트, 툴) |
| `.claude/`, `CLAUDE.md`, `project.yaml` | Claude Code Game Studios — AI 에이전트 개발 프레임워크 설정 |
| `design/`, `production/` | 지금 작업 중인 게임의 기획서 · 진행 로그 · QA 스크린샷 (프레임워크가 이 경로를 읽음) |
| `docs/` | 프레임워크 문서, 엔진 레퍼런스 |
| `.github/workflows/` | 푸시마다 게임별 자동 테스트 |
