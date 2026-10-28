# Active Session State — 진행상황 (handoff)

> Claude Code / Codex 누구든 이 파일을 먼저 읽고 이어서 작업. 작업할 때마다 갱신.

## 프로젝트
- 게임: **마왕님은 바쁘다** — 던전 짓기 + 몬스터 키우기 + 마왕 직접 전투(하데스식) 2D 로그라이크
- 기획: `design/game-brief.md` (유일한 기획서, workflow: minimal)
- 엔진: Godot 4.7.2, GDScript (`project.godot` at repo root). `godot` CLI on PATH (macOS)
- 스토리: `production/epics/demon-lord-is-busy/story-00N-*.md` — 각 파일의 Acceptance Criteria 가 완료 기준
- 코드 위치: `src/` (scenes `src/scenes/`, scripts `src/scripts/`, data `src/data/`)
- 아트: GPT 생성 원본 13종 art/raw/, 공유 32색 도트 스프라이트 assets/sprites/ 적용. 목표 스타일은 Enter the Gungeon 풍 픽셀아트

## 스토리 진행표

| # | Story | Status |
|---|-------|--------|
| 001 | 마왕 조작 전투 (대시·근접·마법탄) | Done (타격감 플레이 확인 대기) |
| 002 | 그리드 던전 + 방/함정 배치 | Done (플레이 확인 대기) |
| 003 | 몬스터 3종 + 영구 손실 | Done (플레이 확인 대기) |
| 004 | 용사 AI 길찾기 + 전투 | Done (플레이 확인 대기) |
| 005 | 웨이브 10개 + 보스 | Done (플레이 확인 대기) |
| 006 | 웨이브 보상 3택 1 | Done (플레이 확인 대기) |
| 007 | 코어 HP + 런 종료/재시작 | Done (플레이 확인 대기) |

Status 값: Ready → In Progress → Done. Done 일 때 스토리 파일의 AC 체크박스도 체크.

## 검증 방법
- 파싱/부팅: `godot --headless --path . --quit-after 60` 출력에 `ERROR`/`SCRIPT ERROR` 없어야 함
- 직접 플레이: `godot --path .`

## 지금 하던 일 / 다음 할 일
- **MVP 7개 스토리 전부 구현 완료** (2026-10-28). 사람 플레이테스트만 남음
- 체크: `godot --headless --path . -s res://tests/smoke/story_001_check.gd` (24 PASS)
- 스크린샷 데모: `tests/smoke/raid_demo.gd`, `tests/smoke/reward_demo.gd` (--write-movie 로 실행)

### 코드 구조
- `src/scripts/main.gd` = `Game`: 페이즈(BUILD→RAID→REWARD→OVER), 골드, 웨이브, 코어 HP, 건설 툴(키 1-7), HUD 문자열
- `dungeon_grid.gd` = `DungeonGrid`: 40x22 타일, 벽 충돌(StaticBody2D), AStarGrid2D 길찾기, 함정 틱
- `unit.gd` = `Unit` 베이스(HP/넉백/예고 공격/원거리) → `hero.gd` `Hero`(코어로 길찾기), `monster.gd` `Monster`(집 지킴, 영구 사망)
- `demon_lord.gd` = 플레이어. `magic_bolt.gd` = 모든 투사체. `hud.gd`, `reward_screen.gd` = UI
- 수치: `src/data/units.gd`, `waves.gd`, `rewards.gd` (밸런스는 여기만 고치면 됨)

### 2026-10-28 2차 (유저 피드백: "형편없다", 건설 툴이 안 먹힘)
- 버그 수정: 건설 클릭/호버를 `get_global_mouse_position()` 대신 입력 이벤트 위치(`make_input_local`)로 처리
  - 실제 입력 파이프라인 체크: `godot --path . -s res://tests/smoke/input_check.gd` (창 모드 필요, 3 PASS)
- 건설 UX: 클릭 가능한 툴바(Hud), 설치 실패 사유 토스트, 용사 경로 점선 미리보기, 다음 웨이브 구성 표시
- 연출: `src/scripts/fx.gd` (파티클, 데미지 숫자, 대시 잔상, PointLight2D), CanvasModulate 어둠 + 횃불/코어 조명, 그림자, 베기 이펙트
- 시작 골드 120 → 150
- 남은 것: 진짜 픽셀아트 스프라이트 (유저가 에셋 결정), 밸런스는 유저 플레이 피드백 대기

### 2026-10-28 3차: 아트 파이프라인 (itch.io 가 이 네트워크에서 차단 → 0x72 에셋 못 받음)
- 유저가 GPT 로 스프라이트 생성 예정. 프롬프트: `art/PROMPTS.md` (파일명 표 포함)
- 유저가 `art/raw/<name>.png` 에 넣으면: `python3 tools/pixelate.py` → `assets/sprites/` (16x16, 보스 32x32, 공유 팔레트 32색) + `art/preview.png`
  - 그 다음 `godot --headless --path . --import` 필수 (import 안 하면 load 안 됨)
- 게임은 `Sprites.tex(name)` 로 스프라이트가 있으면 쓰고, 없으면 기존 도형으로 그림 (`src/scripts/sprites.gd`)
- 스프라이트 이름: demon_lord, warrior, archer, boss_hero, slime, goblin, skeleton, core, floor, wall, (선택) spike, fire_trap
- 가짜 이미지로 파이프라인 + 게임 로드 검증 완료 (임시 스프라이트는 삭제함)
- 대안: OpenGameArt DawnLike (CC-BY 4.0, 접속 가능) — GPT 결과가 별로면 이걸로

### 2026-10-28 4차: GPT 스프라이트 적용 완료
- 유저가 `art/raw/` 에 13장 넣음 → `python3 tools/pixelate.py` → `assets/sprites/` → `godot --headless --path . --import`
- pixelate 방식: 스프라이트별 20색 팔레트 + 블록 최빈색 축소 + 1px 외곽선. 캐릭터 24px, 보스 40px, 코어 22px
  - 공유 팔레트/BOX 축소는 색이 탁해져서 버림 (마왕 보라색이 갈색이 됨)
- floor/wall 은 64x64 텍스처 1장을 4x4 칸에 나눠 그림 (`DungeonGrid._draw_tiled`). 16px 로 줄이면 노이즈가 됨
- 유닛마다 작은 PointLight2D (어두운 바닥에서 안 보이는 문제)
- `art/.gdignore` 로 원본 이미지는 Godot import 대상에서 제외

### 2026-10-28 5차: 절차적 애니메이션
- `Sprites.draw_on(..., anim_t, moving, windup)`: 걷기=통통+기울기+찌그러짐, 대기=숨쉬기, 공격 예고=뒤로 젖힘. 한 장 스프라이트로 동작
- 유저 피드백: "맵이 너무 재미없다" → 맵 개편 방향 유저 결정 대기

### 2026-10-28 6차: 로그라이크 재미 보강 (유저: "로그라이크로 재밌게 해달라 했잖아", A+B+무기/스킬 요청)
- **A 랜덤 던전** (`DungeonGrid._generate`): 런마다 방 8개 + 2칸 통로(Prim 연결 + 루프 1개), 오른쪽 코어 방, 입구 3개(좌/상/하)
  - 지형: 물(용사 속도 45%), 용암(용사 0.7초마다 1뎀), 기둥(연결 유지될 때만), 장식(뼈/해골/상자)
  - `seed_value` 로 고정 맵 가능 (테스트용)
- **B 전략 요소**: 웨이브 1-3 입구 1곳, 4-7 2곳, 8-10 3곳 — 건설 중 빨간 ! 로 미리 표시 + 입구별 경로 점선
  - 건설 툴 8 `미끼 보물`(lure): 반경 10칸 용사가 들러서 2.5초 약탈 → 시간 끌기. 9 `슬라임 둥지`(nest): 침입 중 7초마다 약한 슬라임(최대 2)
  - `src/scripts/build_object.gd`
- **마왕 무기**: `src/data/arsenal.gd` WEAPONS 5종 (손톱/대검/창/쌍단검/망치). 용사 12% 드랍, 보스 100%. 밟으면 장착 (`weapon_pickup.gd`)
- **마왕 스킬** Q/E: 지옥불(시작), 마력 폭풍, 공포의 포효(기절), 임프 소환 — 보상 카드로 획득 (`DemonLord.try_skill`)
- 보상 추가: 스킬 3종, 마왕의 심장(HP+4). `lord_melee` 는 이제 `melee_bonus`
- 체크 36 PASS (`story_001_check.gd` 는 랜덤 맵 대응으로 칸을 동적으로 찾음), input_check 3 PASS

### 2026-10-28 7차: 적 강화 + 몬스터 성장 + 상점 + 걷기 애니 파이프라인
- **새 적 4종** (`Units.HEROES`): 성직자(주변 회복), 마법사(바닥 경고 후 화염 폭발 `telegraph.gd`), 도적(빠름, 35% 회피 MISS), 방패병(HP 22, 피해 50% 감소)
  - 전용 스프라이트 없으면 `fallback` 스프라이트에 `tint` 색을 입혀 그림
- **엘리트**: 웨이브 4부터 확률(웨이브당 3%) — HP x2.2, 크기 1.25, 금빛 테두리, 골드 x3
- **보스 패턴 3종 순환**: 16방향 탄막 → 돌진 → 전사 2명 소환 (`Hero.summon_requested` → `Game._spawn_hero`)
- 웨이브 10개 재구성 (`waves.gd`), 다음 웨이브 구성 표시는 `Units.HEROES[kind].name`
- **몬스터 성장**: 근처(80px)에서 용사 죽으면 XP 1 → Lv2(3XP), Lv3(8XP). HP x1.45, 공격 x1.35/레벨, 별 표시
  - 툴 0 `강화`: 몬스터 클릭 → 즉시 레벨업 (35G x 현재레벨)
- **상점** (건설 중 Tab): 코어 수리, 마왕 공격+1, 마왕 HP+3, 마법탄+1, 함정 데미지+1 (`Units.SHOP`, 살수록 비싸짐)
- **걷기 애니**: `art/PROMPTS.md` 3차 섹션 = GPT 용 4프레임 시트 프롬프트. `art/raw/<name>_walk.png` → pixelate 가 프레임 분리 → `assets/sprites/<name>_walk.png` (가로 스트립)
  - 게임: `Sprites.draw_on(..., walk)` 가 걸을 때 8fps 로 재생, 없으면 기존 통통 애니
- 체크 48 PASS + input_check 3 PASS. 웨이브 N 데모: `WAVE=6 godot --path . -s res://tests/smoke/raid_demo.gd --write-movie ...`

### 2026-10-28 8차: GPT 걷기 시트 + 새 적 스프라이트 적용
- 유저가 `art/raw/` 에 cleric/mage/rogue/guardian + `*_walk.png` 11장(2172x724, 4프레임 1줄) 추가
- `python3 tools/pixelate.py` → 28 스프라이트, walk 는 96x24 스트립(보스 160x40) → import → 게임에서 걸을 때 재생 확인
- 체크 48 PASS. 증거: `production/qa/evidence/wave6-raid.png`

### 2026-10-28 9차: 가독성 + 큰 맵 + 마왕 레벨업 (유저: 벽/바닥 구분 안 됨, 답답함, 보석 작음, 맵 작음)
- 벽: 평평한 어두운 윗면 + 바닥 쪽만 벽돌 앞면, 바닥은 밝게(FLOOR_TINT) + 벽 경계 외곽선 + 벽 아래 그림자 (`DungeonGrid._draw`)
- 맵 40x22 → **60x36**, 방 최대 13개. 건설 중 카메라 줌 0.62(전체 보기), 침입 중 줌 1 + 마왕 추적 + 맵 경계 제한 (`Game._zoom_to`)
- 마우스 월드좌표는 매 프레임 `get_canvas_transform().affine_inverse() * _mouse_screen` (카메라 이동 대응)
- 코어 40px 스프라이트 + HP 링(`grid.core_ratio`), 빈 공간 clear color 어둡게 (project.godot)
- **굴착 삭제** (유저 요청). 툴 1-9 = 벽, 가시, 화염, 슬라임, 고블린, 해골, 미끼, 둥지, 강화
- **마왕 레벨업**: 처치 XP(일반1/엘리트3/보스5), 필요 4+3*Lv → 레벨업 시 게임 일시정지 + 보상 카드 3택1 (`Game._level_up`)
- 처치 시 "+NG" 팝업
- 체크 50 PASS + input_check 3 PASS

- 난이도 (유저: "공격모션이 너무 길어서 한 대도 안 맞고 깸"): 용사 windup ~40% 단축(전사 0.4→0.22, 궁수 0.6→0.35, 마법사 0.8→0.45, 보스 0.8→0.5 등), 쿨다운 단축, aggro 확대, 적 탄속 160→230, 화염 경고 1.0→0.65초

### 다음 후보 (유저가 정함)
1. 유저 플레이테스트 → 밸런스/조작감 튜닝
2. 아트 교체 (Gungeon 풍 픽셀 스프라이트, 발광 이펙트)
3. 메타 업그레이드 (brief out-of-scope 1순위)

## 결정 로그
- 2026-10-28: 엔진 Godot 4.7.2 + GDScript, 플랫폼 PC(mac/win), 키보드+마우스
- 2026-10-28: 마왕 전투 = 근접 메인 + 마법탄 3발 충전식 + 무적 대시
- 2026-10-28: 함정은 길을 막지 않음. 길 막기 규칙은 "벽 메우기"(툴 2)에만 적용
- 2026-10-28: 마왕 사망 시 3초 후 코어 옆 부활, 패배 조건은 코어 HP 0 뿐

### 2026-10-28 4차: GPT 스프라이트 생성 및 적용
- 내장 이미지 생성 도구로 art/PROMPTS.md 공통 스타일 + 개별 프롬프트 사용. 16x16 축소 가독성, 타일 불투명 조건 보강.
- 원본 13종을 art/raw/에 파일명 그대로 저장 (선택 spike, fire_trap, torch 포함).
- python3 tools/pixelate.py 실행: 기본 16x16, boss_hero 32x32, 공유 32색. art/preview.png 시각 확인 완료.
- Godot import 완료. godot --headless --path . --quit-after 60 권한 제한 밖 재검증: exit 0, ERROR 없음.
- 지금 하던 일: 스프라이트 생성·변환·기존 Sprites.tex 경로 적용 완료.
- 다음 할 일: 실제 플레이에서 스프라이트 가독성/타일 반복감 확인. torch 원본은 제공했으나 기존 횃불 연출 연결 여부는 별도 작업.

### 2026-10-28 8차: 새 적 및 걷기 이미지 적용
- 내장 이미지 생성 도구로 새 적 cleric/mage/rogue/guardian 4종 및 걷기 11종(기존 7 + 새 적 4) 생성. art/raw/에 지정 파일명 저장, 기존 정적 원본 유지.
- 프롬프트: 공통/개별 스타일 + 캐릭터 원본 참조, 가로 4프레임과 투명 간격 강조. 해골 시트는 간격 문제로 1회 재생성.
- python3 tools/pixelate.py: 총 28개 에셋. 최신 크기 설정 유지(캐릭터 24px, 보스 40px). 걷기 11종 모두 4구간 분리/비어 있지 않은 4프레임 검사 통과.
- 미리보기: art/new_enemies_preview.png, art/walk_preview.png, art/walk_preview.gif.
- Godot import 및 godot --headless --path . --quit-after 60: exit 0, ERROR/SCRIPT ERROR 없음. 기존 8fps 재생 경로로 자동 적용.
- 유저 승인으로 codegraph init -i 완료. GDScript 인덱싱은 제한적.
- 지금 하던 일: 새 적 4종 + 걷기 11종 생성/변환/적용 완료.
- 다음 할 일: 실제 플레이에서 동작 가독성과 프레임 전환 확인.
- 난이도 2차: 웨이브별 용사 공격력 +보너스 (`waves.gd` dmg_bonus: 1~3웨이브 0, 4~6 +1, 7~9 +2, 보스웨이브 +1), 엘리트 확률 웨이브당 3%→5%. 체크 50 PASS
