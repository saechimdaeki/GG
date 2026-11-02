# 3D 지형 아트 인계 (GPT → AI 3D) — 가라앉은 도시

왜: 데이브 더 다이버처럼 **배경은 3D 아트, 캐릭터는 2D 도트**로 간다. 지금 배경은 코드로 만든 도형이라 초라함 → 진짜 3D 에셋으로 교체. (리서치: [`design/research-dave-like.md`](../design/research-dave-like.md) "2차 리서치")

---

## 👉 지금 할 일 (GPT에게)

**방법: 그림 → 3D.** AI 3D는 "깨끗한 콘셉트 그림 한 장"을 줄 때 가장 잘 나온다.

1. **스타일 시트 1장**을 먼저 그린다 (아래 S-0). 마음에 들 때까지. 이후 모든 그림은 이걸 기준으로.
2. 아래 **에셋 표**의 각 줄마다:
   1. **콘셉트 그림**을 그린다: 공통 스타일 C3 + 그 줄의 설명 → `raw3d/concepts/<이름>.png`
   2. 그 그림을 **AI 3D(Tripo 또는 Meshy, image-to-3D)** 에 넣어 모델을 만든다
   3. **GLB로 내보내** `raw3d/<폴더>/<이름>.glb`에 저장 (아래 "3D 규격" 지키기)
3. 다 되면 맨 아래 "생성 상태"에 체크. 툴·설정·크레딧은 `generation-manifest.json`의 `bundle_3d`에 기록.
4. AI 3D 툴은 **유료 플랜(상업 이용 가능)** 으로. 무료 플랜은 CC BY(출처 표기)나 비상업 조건이 붙을 수 있음 — 어느 플랜으로 했는지 manifest에 적기.

> 3D 툴을 직접 못 쓰면: 1~2.1(콘셉트 그림)까지만 하고 멈춘다. 사람이 Tripo/Meshy 사이트에 그림을 올려서 GLB를 받는다.

---

## 스타일 — 데이브 더 다이버 방식에서 가져온 것

- **3D 공간 위에 평면적인 도트 캐릭터**가 올라가므로, 3D는 **단순하고 덩어리감 있는 저폴리**. 디테일은 모양이 아니라 **손그림 질감**으로
- **"2D 감성으로 만든 3D"**: 실루엣이 또렷하고, 색 면이 크고, 그림자는 질감에 살짝만
- 색: 물에 잠긴 돌은 **따뜻한 회갈색 돌 + 청록 이끼·따개비**, 산호는 **채도 높은 주황·분홍·보라·노랑**. 물(파랑)과 지형이 **다른 색상**이어야 구분됨
- 가라앉은 **고대 도시**: 깎은 돌 블록, 아치, 기둥, 둥근 창, 놋쇠 장식이 이끼에 덮인 모습
- 사실적이지 않게, 무섭지 않게. 밝고 모험적인 느낌

### S-0 스타일 시트 (먼저 1장)
```
Style sheet for a cheerful 2.5D underwater adventure game: a lineup of 8 stylized
low-poly 3D environment assets on a plain light grey background, three-quarter view,
evenly lit, no shadows on the ground. Chunky simple shapes with big flat color areas
and hand-painted textures (not realistic, not noisy): warm grey-brown carved stone
blocks of a sunken ancient city with teal moss and barnacles, a broken stone column,
a round stone arch with a porthole window and brass trim, a big rounded seabed rock,
a cluster of saturated orange and pink coral, purple sea fans, yellow tube sponges,
green kelp. Clean silhouettes, readable from far away, adventurous and charming mood.
```

### C3 — 콘셉트 그림 공통 (에셋 1개마다 앞에 붙임)
```
Single stylized low-poly 3D game asset for a cheerful 2.5D underwater adventure game,
same style as the style sheet: chunky simple shapes, big flat color areas, hand-painted
texture, warm grey-brown stone with teal moss and barnacles, saturated coral colors.
Three-quarter view from slightly above, the whole object visible and centered, plain
light grey background, soft even lighting, no ground, no shadow, no other objects,
no text. Object:
```

---

## 3D 규격 (GLB 내보낼 때)

| 항목 | 값 |
|---|---|
| 형식 | **GLB** (텍스처 포함, 한 파일) |
| 크기 | 표의 "크기" 칸 = 게임 타일 수. **1 유닛(1m) = 1 타일**. AI 툴에서 크기 조절이 안 되면 그냥 두기 — Claude가 맞춤 |
| 방향 | Y 위쪽, 정면이 +Z |
| 피벗 | 바닥 가운데 |
| 폴리곤 | 바위·폐허 5,000 삼각형 이하, 산호·소품 2,000 이하 (툴의 "low poly" / "game ready" 옵션) |
| 텍스처 | 1장, 1024px 이하. 조명·그림자 굽지 않기(no baked lighting) |
| 재질 | 불투명. 해초처럼 얇은 건 알파 컷아웃 OK |

---

## 에셋 표 — 묶음 3D-1: 레벨을 만들 수 있는 최소 키트 (22개)

레벨은 Claude가 이 조각들을 Godot에서 배치해 만든다. 같은 조각을 크기·회전만 바꿔 여러 번 씀.

### 바위 (`raw3d/rocks/`)
| 이름 | 크기(타일, 폭×높이×깊이) | 설명 (C3 뒤에 붙임) |
|---|---|---|
| `rock_big` | 8×5×5 | a big rounded seabed boulder, flat-ish top to stand on, teal moss on top, barnacles |
| `rock_mid` | 4×3×3 | a medium rounded seabed rock, moss on top |
| `rock_small` | 2×1×2 | a small flat seabed stone |
| `cliff_wall` | 6×10×4 | a tall vertical rock cliff section, flat front, rough sides, moss streaks, can be stacked side by side |
| `overhang` | 8×3×4 | a rock ledge that sticks out sideways like a shelf, flat top, rough underside |
| `seabed_slab` | 10×2×6 | a long flat slab of seabed with sand and pebbles on top, rocky front edge |
| `rock_arch` | 8×6×3 | a natural rock arch you can swim through |

### 가라앉은 도시 (`raw3d/ruins/`)
| 이름 | 크기 | 설명 |
|---|---|---|
| `ruin_wall` | 6×6×1 | a section of carved stone city wall with a round porthole window, brass trim, moss |
| `ruin_wall_broken` | 6×5×1 | the same carved stone wall section but broken, jagged top, missing blocks |
| `ruin_column` | 1.5×8×1.5 | a tall carved stone column with a brass ring, slightly cracked |
| `ruin_column_broken` | 1.5×4×1.5 | a broken column stump with fallen pieces at its base |
| `ruin_arch` | 6×7×1.5 | a carved stone city arch / doorway with a keystone and brass ornament |
| `ruin_stairs` | 6×3×3 | a short flight of wide carved stone stairs |
| `ruin_tower_top` | 6×6×6 | the top of a drowned round tower: dome roof with a brass spire, round windows |
| `ruin_floor` | 6×1×6 | a square slab of carved stone floor tiles, some cracked |

### 산호·식물 (`raw3d/life/`)
| 이름 | 크기 | 설명 |
|---|---|---|
| `coral_orange` | 2×2×2 | a cluster of bright orange branching coral |
| `coral_pink` | 2×1.5×2 | a round cluster of pink brain coral and small polyps |
| `sea_fan` | 2×3×0.5 | a flat purple sea fan |
| `tube_sponge` | 1×2×1 | a bunch of yellow tube sponges |
| `anemone` | 1×1×1 | a bright anemone with soft tentacles |
| `kelp_tall` | 1×5×1 | a tall strand of green kelp with a few leaves (alpha cutout ok) |

### 난파 (`raw3d/wreck/`)
| 이름 | 크기 | 설명 |
|---|---|---|
| `wreck_bow` | 10×6×4 | the broken front half of a small old wooden boat stuck in the sand, tilted |

---

## 생성 상태

- [x] S-0 스타일 시트 생성·후속 콘셉트 기준 채택 (사용자 최종 검토 전) (`raw3d/concepts/style_sheet.png`)
- [x] 콘셉트 그림 22장 (`raw3d/concepts/`)
- [ ] GLB 22개 (`raw3d/<폴더>/`)
- [x] 비용 없이 가기로 함 (2026-11-02): GLB 대신 콘셉트 22장을 **종이 무대 컷아웃**으로 사용 — `tools/art_build.py`가 배경을 따서 `assets/stage/`에 저장, `src/view3d/sea_view.gd`가 앞·중간·먼 곳 3층으로 배치
- [ ] (선택) 나중에 무료 GLB가 생기면 같은 이름으로 `raw3d/<폴더>/`에 넣기 — 컷아웃 자리를 3D로 바꿔 끼움

### 2026-11-02 인계

내장 image_gen으로 스타일 시트 1장 + 이를 참조한 단일 콘셉트 22장 생성. PNG 디코딩 검증 완료. 전체 프롬프트·크기·목표 GLB 경로는 `generation-manifest.json`의 `bundle_3d`에 기록. 이미지 크레딧은 도구에서 제공하지 않음.

현재 Tripo/Meshy 연결 도구가 없어 문서의 대체 절차(1~2.1)까지 완료. **GLB는 아직 없음.** 사람이 상업 이용 가능한 유료 플랜으로 `raw3d/concepts/<이름>.png`를 각각 image-to-3D에 넣고, 위 3D 규격으로 `raw3d/<폴더>/<이름>.glb`에 저장한 뒤 플랜·설정·사용 크레딧을 manifest에 보완해야 함. 스타일 시트는 생성 기준으로 채택했으며 사용자 최종 확정은 대기.

모델 검수 시 얇은 식물 형태, 아치 통로, 창문에 그려진 청록색의 유리/구멍 처리, 장식의 폴리곤 수를 확인. 게임 연결은 Claude 후속 작업.
