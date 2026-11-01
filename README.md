# GG

제제이야

게임을 여러 개 만들어 모아두는 레포. 게임 하나 = 루트의 폴더 하나.

## 게임 목록

| 게임 | 폴더 | 장르 | 엔진 | 상태 |
|---|---|---|---|---|
| 마왕님은 바쁘다 | [`demon-lord-is-busy/`](demon-lord-is-busy/) | 던전 디펜스 + 액션 로그라이크 | Godot 4.7.2 | 플레이 가능, 플레이테스트 중 |
| 저주받은 전당포 | [`cursed-pawnshop/`](cursed-pawnshop/) | 크툴루풍 감정·장사 시뮬 (28일 캠페인) | Godot 4.7.2 | 완성, 플레이테스트 대기 |
| 가라앉은 도시 | [`sunken-city/`](sunken-city/) | 잠수 탐험 + 영역 재건 (2D 사이드뷰) | Godot 4.7.2 | M1 플레이 가능 (잠수·배수) |

## 아이디어 보관함

나중에 만들 후보. 2026-10 리서치 기준(데스크톱 컴패니언·상점/크래프팅 심·덱빌더가 꾸준히 강세).

| 아이디어 | 한 줄 | 왜 괜찮은가 |
|---|---|---|
| 저주받은 전당포 | 괴물 손님 물건을 감정·흥정, 28일 동안 빚 갚기 | → **완성** |
| 책상 위 미니 던전 | 화면 아래 투명 띠에 붙어 일하는 동안 작은 모험가들이 알아서 탐험하는 방치형 | 데스크톱 컴패니언 유행, Godot 투명 창 지원 |
| 레시피 덱빌더 | 카드 = 재료, 콤보 = 요리. 제한 시간 안에 몬스터 손님 주문을 덱으로 맞춤 | 로그라이크 덱빌더 수요 큼 (슬더스2 흥행) |

### 큰 게임 후보 — "나가서 파고 싸우기 ↔ 돌아와서 키우고 운영하기"

테라리아(파고 짓기·보스 단계), 던전 키퍼(악당 경영), 데이브 더 다이버(두 장르를 한 루프로)의 재미를 가져온 오리지널 아이디어. 전부 2D 사이드뷰, 마일스톤마다 플레이 가능하게.

| 아이디어 | 한 줄 | 섞은 재미 |
|---|---|---|
| 걷는 거인 | 거인 등에 마을을 짓고 몸속(혈관·위장·뼈 동굴)을 파고 들어가 재료·보스를 찾음. 거인이 걸으면 바깥 지역이 바뀜 | 파고 짓기 + 이동하는 세계 |
| 용 목장 | 낮엔 화산 지하를 파 내려가 알·광석 채집, 밤엔 용을 키우고 교배. 키운 용으로 더 깊은 층·보스 공략 | 파고 내려가기 + 키우기 |
| 마녀의 밤장사 | 밤엔 저주받은 숲에서 괴물 사냥·재료 수집, 낮엔 약을 만들어 마을에 팖. 팔린 약이 마을 사건을 바꿈 | 탐험 액션 + 가게 운영 |
| 가라앉은 도시 | 물에 잠긴 고대 도시를 잠수 탐사. 펌프를 고쳐 물을 빼면 그 구역이 마을이 되고 주민이 들어옴. 뺄수록 더 깊은 수몰 구역 | 잠수 탐험 + 영역 재건 |
| 요괴 온천 여관 | 낮엔 요괴 손님에게 온천·요리, 밤엔 산속 요괴 영역에서 약초·광석·새 원천 탐색. 깊은 원천일수록 신기한 탕 | 산속 탐험 + 여관 운영 |
| 괴물 서커스단 | 괴물을 잡아 단원으로 훈련, 마차로 마을을 돌며 공연해 번 돈으로 다음 지역으로. 단원끼리 궁합·사고 | 포획·육성 + 순회 경영 |
| 무너지는 광산 마을 | 마을이 매일 밤 무너지고 하루가 다시 시작됨. 남는 건 지식뿐, 지름길·주민 비밀로 매일 더 깊이 내려가 붕괴 원인을 찾음 | 파고 내려가기 + 시간 루프 수수께끼 |
| 등대지기 | 밤엔 등대 불빛으로 바다 괴물을 막고, 낮엔 해안 동굴·난파선에서 부품을 모아 등대를 증축. 불빛이 닿을수록 새 항로·손님 | 밤 방어 + 낮 탐험 + 증축 |

## 실행

각 게임 폴더의 README에 자세한 실행법·조작·진행 상황이 있음.

**다운로드** — [Releases](https://github.com/saechimdaeki/GG/releases/latest)에서 `<게임>-mac.zip` / `<게임>-windows.exe`

- Mac: zip을 풀면 `.app`. 서명이 없어서 처음엔 **우클릭 → 열기** (또는 시스템 설정 → 개인정보 보호 및 보안 → 그래도 열기)
- Windows: `.exe` 하나에 다 들어 있음. SmartScreen이 뜨면 **추가 정보 → 실행**

**릴리스 내기** — 태그를 푸시하면 GitHub Actions(`.github/workflows/release.yml`)가 모든 게임을 Mac·Windows로 빌드해 릴리스에 올림.

```bash
git tag v0.2.0 && git push origin v0.2.0
```

**소스로 실행** (Godot 4.7.2 필요)

```bash
cd demon-lord-is-busy       # 또는 cursed-pawnshop
godot --headless --import   # 클론 직후 한 번
godot
```

**실행 파일 직접 만들기** (Mac `.app` · Windows `.exe`)

Godot 4.7.2 export 템플릿이 설치돼 있어야 함 (에디터 → Editor → Manage Export Templates → Download).

```bash
cd cursed-pawnshop          # 또는 demon-lord-is-busy
mkdir -p build/macos build/windows
godot --headless --export-release "macOS" build/macos/CursedPawnshop.zip
godot --headless --export-release "Windows" build/windows/CursedPawnshop.exe
```

- `build/` 는 git에 안 올라감

## 패치노트

릴리스 태그를 푸시하면 지난 태그 이후 `feat:` / `fix:` 커밋이 정리돼 [`patch-notes` 이슈](https://github.com/saechimdaeki/GG/issues?q=label%3Apatch-notes)로 자동 등록됨 — 게임마다 이슈 하나, 게임 이름 라벨(`저주받은 전당포`, `마왕님은 바쁘다`)이 붙음. 그 게임 폴더를 건드린 커밋만 들어감 (`.github/patch-notes.sh`). 커밋 메시지 첫 줄이 그대로 패치노트가 되니 플레이어가 읽을 말로 쓰기.

## 새 게임 추가하기

1. 루트에 폴더를 하나 만들고 그 안에 엔진 프로젝트를 통째로 둔다 (`<game-name>/project.godot` 등)
2. 폴더 안에 `README.md` — 실행법, 조작, 어디까지 했는지
3. 위 게임 목록 표에 한 줄 추가
4. 자동 테스트가 있으면 `.github/workflows/smoke.yml` 에 그 폴더용 job 추가

## 레포 구조

| 경로 | 내용 |
|---|---|
| `<game>/` | 게임 하나 (코드, 에셋, 테스트, 툴, 기획서 `<game>/design/`) |
| `.claude/`, `CLAUDE.md`, `project.yaml` | Claude Code Game Studios — AI 에이전트 개발 프레임워크 설정 |
| `design/`, `production/` | 프레임워크 공용 (엔티티 레지스트리 · 진행 로그 · QA 스크린샷). 게임 기획서는 각 게임 폴더에 |
| `docs/` | 프레임워크 문서, 엔진 레퍼런스 |
| `.github/workflows/` | 푸시마다 게임별 자동 테스트(`smoke.yml`), 태그 푸시 → 릴리스 빌드(`release.yml`) |
