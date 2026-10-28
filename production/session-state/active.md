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

### 2026-10-29: 메타 업그레이드 + 발광 (유저: "메타 업그레이드 + 아트 다듬기")
- **메타 업그레이드** `src/data/meta.gd` (`Meta`): 런 종료 시 영혼석 = 클리어 웨이브 x4 (+승리 25). 저장 `user://meta.cfg` (ConfigFile)
  - 게임오버 화면에서 구매: 금고 상속(시작 골드 +30/Lv, 5), 단단한 코어(+4/Lv, 5), 마왕의 체력(+2/Lv, 5), 마왕의 힘(+1/Lv, 3), 화염 비전(화염 함정 시작 해금, 1)
  - 적용: `Game._apply_meta()` (런 시작), 구매: `Game.buy_meta()` → `Hud.show_end(text, items)`
  - 테스트는 `Meta.save_path` 를 바꿔서 실제 세이브 안 건드림. 화면 데모: `tests/smoke/meta_demo.gd`
- **발광**: project.godot `rendering/viewport/hdr_2d=true` + `Game._glow_env()` WorldEnvironment glow. 색 채널 >1.0 이면 번짐
  - 적용: 마법탄 중심, 코어(halo + 스프라이트), 횃불 불꽃, 용암 거품, 화염 함정 (`DungeonGrid.HDR`)
  - 횃불 스프라이트는 이미 연결돼 있었음
- 체크 56 PASS + input_check 3 PASS. 증거: `production/qa/evidence/meta-upgrade-end.png`, `glow-raid.png`
- 다음: 유저 플레이테스트 (메타 비용/보상 밸런스, 블룸 강도 `glow_intensity` 0.6)

### 2026-10-29 2차: 넉백 너프 + HUD 개편 (유저: "제자리 클릭만 해도 다 넉백돼서 못 다가옴", "UI/UX 후짐")
- 넉백: `Unit.take_damage` 가 더 이상 공격 예고(windup)를 캔슬 안 함. 넉백 후 0.8초 넉백 면역(데미지는 들어감). 속도 220→150
  - 수치: `Units.KNOCKBACK_SPEED`, `Units.KNOCKBACK_IMMUNE`
- HUD: 텍스트 2줄 → `src/scripts/status_panel.gd` (`StatusPanel`, _draw 로 그림). 좌상 HP 바/마법탄 구슬(충전 표시)/XP 바, 상단 중앙 WAVE, 우상 골드/코어 바, 우하 무기·Q·E 슬롯(쿨다운 덮개, 침입 중만)
  - 데이터: `Game._status()` → `Hud.set_status(state, hint)`
- 버튼/패널 공통 테마 `src/scripts/ui_theme.gd` (`UiTheme.get_theme()`): 툴바, 상점, 보상 카드, 게임오버
- 체크 56 PASS + input 3 PASS. 증거: `production/qa/evidence/hud-build.png`, `hud-raid.png`
- 다음: 유저 플레이 (넉백 체감, HUD 가독성)

### 2026-10-29 3차: 난이도 3차 (유저: "여전히 쉬움, 한 대도 안 맞음, 몬스터 배치할 이유 없음, 마왕 HP 너무 많음, 용사 공격 느림")
- 마왕 HP 10 → 6 (`demon_lord.gd max_health`)
- 근접 용사 사거리 4 → 12 + 공격 시 돌진(`Units.LUNGE_SPEED` 200, 추가 판정 `LUNGE_REACH` 12). 마왕 손톱(34)과 사거리 차이 축소
- 쿨다운: 전사 0.8→0.5(windup 0.18), 궁수 1.1→0.75(windup 0.25), 성직자 1.0→0.7, 도적 0.5→0.4, 방패병 1.0→0.7, 마법사 1.5→1.1, 보스 1.2→0.9
- 적 탄속 230 → 280
- 의도: 마왕 혼자 버티기 어렵게 → 몬스터가 어그로/탱킹 역할. 아직 부족하면 다음 후보: 마왕 사망 시 코어 피해 or 부활 시간 증가
- 체크 56 PASS

### 2026-10-29 4차: 용사 스킬 + 맵 가독성 (유저: "용사들도 스킬 쓰게", "맵이 사람 눈으로 보기 어려움")
- 용사 스킬 (`Hero._tick_skill`, 데이터 `Units.HEROES[*].skill/skill_cd`):
  - 전사 `charge` 돌진(110px 안, 5초), 궁수 `volley` 3갈래 화살(3.5초), 도적 `blink` 대상 뒤로 순간이동 후 즉시 공격(뒤→양옆, 벽이면 취소, 5초), 방패병 `slam` 주변 0.5초 경고 후 강타(4초)
  - 돌진은 보스와 공용 `Hero._charge()`
- 맵: 바닥 = 단색(`FLOOR_BASE`) + 돌 텍스처 28% 투명하게(`FLOOR_DETAIL`) → 유닛이 바닥에서 뜸. 벽 앞면 어둡게, 경계선 진하게, 전체 어둠(CanvasModulate) 0.5→0.62
- 체크 57 PASS (용사 스킬 4종 발동 체크 추가). 증거: `production/qa/evidence/map-readable.png`

### 2026-10-29 5차: 용사 스킬 게이지 (유저: "보자마자 스킬 갈김, 게이지 차면 쓰게")
- `Hero._skill_cd` 가 스폰 시 skill_cd 로 시작(게이지 빈 상태), 전투 대상(`_target`)이 있을 때만 참 → 마주친 뒤 skill_cd 초 지나야 첫 스킬
- 발밑에 게이지 바(파랑, 다 차면 금색 발광). 증거: `production/qa/evidence/hero-skill-gauge.png`
- 체크 58 PASS

### 2026-10-29 6차: 맵 디자인 개편 (유저: "지저분해 보임, 굴착 말고, 방 테마+모양+상호작용 지형 + 리서치")
- 리서치 요점: 벽/바닥 명도 대비, 잔디테일 줄이기, 방 템플릿(모양 마스크), 방마다 테마로 구분, 병목+루프
- 데이터 `src/data/rooms.gd` (`Rooms`): 테마 7종(석실/감옥/서고/납골당/용광로/수몰실/제단실, 각 최대 2개), 모양(rect/rounded/cross/oval), 지형 경로비용, 제단 수치
- `DungeonGrid._generate`: 방 최대 13→10, 크기 7-11 x 6-9, 간격 3. `_carve_room` 모양 마스크 + `floor_theme`. `_tidy()` 막다른 1칸·1칸 벽 돌기 정리
- `_decorate(i)`: 테마별 한 가지 지형 — 석실 대칭 기둥 4개, 용광로 2칸 용암 줄기 + 가운데 2칸 다리, 수몰실 물웅덩이, 제단실 제단. 소품은 윗벽을 따라 정렬(책장/창살) 또는 줄(관). 랜덤 뼈/상자 흩뿌리기 삭제
- 상호작용: 경로비용(물 3, 용암 8) → 용사가 다리/우회로로 몰림(병목). 제단: 침입 중 반경 2.5칸 마왕·몬스터 2초마다 HP+1
- 횃불: 7칸마다 → 방마다 윗벽 1/3, 2/3 지점 2개. 건설 중 방 이름 표시
- 체크 60 PASS + input 3. 증거: `production/qa/evidence/map-themes-build.png`, `map-themes-raid.png`
- 다음 후보: 철문(닫으면 우회), 부술 수 있는 기둥, 테마별 바닥 텍스처

### 2026-10-29 7차: 용암/물 그래픽 (유저: "용암과 물이 후져 보임")
- `DungeonGrid._draw_water`: 깊은 남색 + 흘러가는 물결 대시 + 가끔 반짝임, 가장자리 돌 테두리(2px) + 물선, 윗변은 깊이 그림자
- `_draw_lava`: 2px 블록 플라즈마(어두운 굳은 용암/주황 용융/HDR 백열 줄기, 블룸됨) + 거품 링 + 가장자리 굳은 껍질 + 발광 이음선
- 이웃 지형 비교 `_open_sides`, 셀별 해시 `_hash` 로 애니 위상 분산
- 확대 데모 `tests/smoke/terrain_demo.gd`. 증거 `production/qa/evidence/lava-closeup.png`, `water-closeup.png`. 체크 60 PASS
