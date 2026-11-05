# GPT 인계 — 가라앉은 도시 (2026-11-02)

읽을 순서: 이 파일 → [`design/research-dave-like.md`](../design/research-dave-like.md) "2차 리서치" → [`art/PROMPTS_3D.md`](../art/PROMPTS_3D.md)

## 지금 상태 (요약)
- 게임 규칙: `src/dive/` (World 격자·Diver·Village), 데이터 `src/data/`, 맵은 `tools/gen_zone1.py`로 생성 → `src/data/zone1.gd`
- 화면: 데이브 더 다이버식 2.5D. `src/view3d/sea_view.gd`가 3D 바다를 그림 (격자→마칭스퀘어 3D 암벽 + 돌 질감 셰이더 `spatial_env_rock.gdshader`, 안개, 빛줄기, 물고기 떼, 해초, 부유물). 캐릭터·물건은 2x 도트 `Sprite3D`
- 배경 장식은 **종이 무대**: `art/raw3d/concepts/*.png` 22장을 `tools/art_build.py`가 오려 `assets/stage/`에 저장 → `SeaView._build_stage()`가 앞·중간·먼 곳 3층에 세움
- 테스트: `godot --headless --path . -s res://tests/smoke/run_check.gd` → `PASS` 나와야 함
- 화면 확인(스크린샷): `EVIDENCE_AT="40,13" godot --path . --windowed --resolution 1280x720 --write-movie <경로>/shot.png --quit-after 90 res://tests/evidence/dive.tscn` (타일 좌표 아무 데나), 물 빠진 마을은 `EVIDENCE_NO_MENU=1 ... res://tests/evidence/village.tscn`
- 결과 스크린샷은 `production/qa/evidence/<작업이름>/`에 남기기

---

## 할 일 1 — 물 밖 하늘을 맑게 (완료)

**문제:** 물 밖(시작 지점 위, 물 빠진 마을)이 탁한 청록색. 물속 안개 색이 물 밖까지 번져서 그럼.
스크린샷: `production/qa/evidence/paper-stage/04-town.png`, `05-start.png`

**고칠 곳:** `src/view3d/sea_view.gd`의 `sync()` 안 환경 설정 (`_env.fog_light_color`, `_env.fog_density`, `_env.background_color`, `_env.ambient_light_*`), 상수 `SKY`, `AIR_FOG`

**원하는 결과**
- 잠수부가 물 밖에 있을 때: 배경은 **맑은 하늘색 그라데이션**(위는 진한 하늘색, 수평선 쪽은 밝게). 가장 쉬운 방법: `Environment.BG_SKY` + `ProceduralSkyMaterial`(sky_top_color / sky_horizon_color)로 바꾸고, 물속일 땐 `BG_COLOR`로 되돌리기. 안개는 거의 없게(`fog_density` 0.0005 이하)
- 물이 빠진 뒤(`dry` 값이 1에 가까움): 하늘을 **따뜻한 햇살 톤**(살짝 주황빛 수평선)으로, 햇빛(`_sun`) 색도 따뜻하게
- 물속은 지금 그대로 (건드리지 않기)
- 물 밖 ↔ 물속 전환이 한 프레임에 확 바뀌지 않게 0.5초 정도 섞기 (`move_toward`)

**완료 조건:** smoke `PASS` + 시작 지점·마을 스크린샷 2장을 `production/qa/evidence/sky/`에 저장. 마을 화면에서 하늘이 하늘처럼 보일 것.

---

> **2026-11-05: 할 일 2는 중단.** 컷아웃이 더 나아서 GLB를 더 만들지 않음 (`art/PROMPTS_3D.md` 맨 아래 "결정").

## 할 일 2 — 종이 컷아웃을 진짜 3D로 (첫 모델 검증 완료)

- [x] 무료 TRELLIS `rock_big` GLB 생성본 연결 및 스모크 검증.
- [x] 같은 위치·크기의 [GLB](qa/evidence/glb-first/rock-big-glb.png)와 [컷아웃](qa/evidence/glb-first/rock-big-cutout.png) 비교.
- [x] 사용자 요청으로 후속 변환 재개. `rock_mid` 생성·연결 및 스모크 검증.
- [ ] 나머지 20개 변환. `rock_small`부터 무료 ZeroGPU 한도 초기화 후 재개.

### 2-1. GLB 만들기 (사람 또는 GPT가 웹에서)
1. Hugging Face의 **TRELLIS** 무료 데모(MIT 라이선스, 상업 이용 가능)를 연다
2. `art/raw3d/concepts/<이름>.png`를 올리고 Generate → **Extract GLB** → Download
3. `art/raw3d/<폴더>/<이름>.glb`로 저장 (폴더는 `PROMPTS_3D.md` 에셋 표: rocks / ruins / life / wreck)
4. 하루 무료 사용량이 있으니 **`rock_big` 하나 먼저** → 할 일 2-2로 게임에 넣어 컷아웃보다 나은지 확인 → 낫다면 나머지
5. 유료 서비스(Tripo·Meshy 유료 플랜 등)는 쓰지 않는다 — 비용 안 쓰기로 함

### 2-2. 게임에 연결 (코드)
- `art/`는 `.gdignore`라 Godot이 못 읽음 → `tools/art_build.py`에 단계 추가: `art/raw3d/<폴더>/*.glb`를 `assets/models/<이름>.glb`로 복사
- `src/view3d/sea_view.gd`의 `_stage_piece(name, width, base, tint, flip)`:
  - `res://assets/models/<name>.glb`가 있으면 컷아웃(`Sprite3D`) 대신 그 GLB를 `instantiate()`해서 같은 자리에 둔다
  - 크기: 모델 AABB 폭이 `width`(타일 = 월드 유닛)가 되게 균일 스케일, 바닥이 `base.y`에 닿게(AABB 아래쪽 기준)
  - 방향: 정면(+Z)이 카메라 쪽. `flip`이면 Y축 180° 회전
  - `tint`는 재질 색에 곱하기 (먼 층일수록 어둡게 — 지금 컷아웃과 같은 값)
  - 없으면 지금처럼 컷아웃 → 하나씩 바꿔 끼워도 게임이 깨지지 않게
- 함수 시그니처는 그대로 두기 (`_build_stage()`가 부름)

**완료 조건:** smoke `PASS` + `rock_big`이 3D로 보이는 스크린샷 1장 (`production/qa/evidence/glb-first/`). 컷아웃과 3D 중 뭐가 나은지 한 줄 의견.

---

## 지키기
- 게임 규칙(`src/dive/`, `src/data/tuning.gd`)은 이 작업에서 건드리지 않기
- 커밋은 사람이 하라고 할 때만. 커밋 메시지는 `feat:`/`fix:` 형식, 한국어
- 바꾼 것·확인한 것·못 한 것을 `production/session-state/active.md` 체크포인트에 적기
