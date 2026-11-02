# 이미지 인계 (GPT) — 가라앉은 도시

기획: [`design/game-brief.md`](../design/game-brief.md) · 생성 기록: `generation-manifest.json`

## 현재 승인된 그림체 — 2026-11-02

기준: [`raw/sprites/diver.png`](raw/sprites/diver.png), 승인 원본 `raw/sprites/diver_original_v1.png`.
둥근 사각 황동 투구·가로 창·작은 얼굴·청록 소매·주황 멜빵복·크림 공기통·작업 가방의 독자적 잠수부.
과도한 치비·큰 반짝이는 눈·볼터치·콩 모양 몸, 특정 게임 캐릭터의 얼굴/체형 모방은 사용하지 않는다.

## 지금 할 일

묶음 R 10장 새 스타일 생성·지정 경로 교체 완료. 다음 단계는 Claude의 `python3 tools/art_build.py` 실행 및 게임 연결·플레이 검증.
이전 치비 스타일 지시보다 현재 승인된 기준을 우선한다. 세계 배경·타일·건물은 기존 아트를 유지한다.

### 묶음 R — 완료된 10장

| # | 저장 경로 | 스타일 | 개별 프롬프트 |
|---|---|---|---|
| 1 | `raw/sprites/diver_stand.png` | K | Same exact approved diver, rounded-square brass helmet, horizontal rectangular blue viewport showing youthful clean-shaven face with dark hair and tiny simple eyes, left-side lamp, burnt-orange overalls, teal sleeves and flippers, cream air cylinder, little brown crossbody salvage satchel. Standing upright, waving one gloved hand. Preserve reference design precisely. |
| 2 | `raw/sprites/eel_lantern.png` | K | A green eel creature with a gently rounded but elongated tapering body, little warm lantern bulb dangling over its head, small mischievous eyes, tiny toothy grin, long wiggling swimming silhouette. Endearing strange underwater wildlife, recognizable eel anatomy, fins and tail. No humanoid body or clothing. |
| 3 | `raw/sprites/crab_rubble.png` | K | Small rusty red crab, broad squat body under a simple broken carved grey stone slab shell with a little moss, raised little pinchers, small eyes on short stalks and grumpy brows. Readable crab anatomy, slightly comical but not baby mascot. No human clothing. |
| 4 | `raw/sprites/jelly_ghost.png` | K | Small pale cyan ghostlike jellyfish, translucent bell-shaped dome, two tiny drooping dark eyes and small worried mouth, short softly curling tentacles. Simple aquatic creature anatomy, wistful and charming, few clean pixel clusters. |
| 5 | `raw/sprites/moru.png` | K | Moru, older salvage workshop keeper. Tousled short white hair, bushy eyebrows, medium fluffy white beard reaching chest but NOT hiding torso. Brass goggles on forehead, warm tan skin, tiny simple dark eyes and grumpy kind expression. Brown leather work apron over faded teal shirt, small wrench in pocket, dark trousers, sturdy boots. Slightly hunched modest stocky adult, hands behind back. No helmet, no diving tank. |
| 6 | `raw/sprites/mulbangul.png` | K | Mulbangul, a cheerful young kid, tousled orange hair under bright mustard-yellow raincoat hood, warm pale skin, small gill marks on cheeks, tiny dark eyes, small open happy grin, webbed bare hands. Oversized yellow raincoat over teal shorts and little brown boots. Small youngster with natural cartoon child proportions, not baby. Jumping and proudly holding one little brass gear overhead. No helmet, no visor, no air tank. |
| 7 | `raw/portraits/moru.png` | KP | Dialogue portrait head and shoulders facing viewer. Same Moru as the additional full-body reference: white hair and beard, bushy eyebrows, goggles on forehead, tan skin, faded teal shirt under brown work apron. Grumpy but kind small half-smile, simple dark eyes. Match his identity and outfit exactly, larger readable face but no shiny anime eyes. |
| 8 | `raw/portraits/mulbangul.png` | KP | Dialogue portrait head and shoulders facing viewer. Same Mulbangul as additional full-body reference: tousled orange hair, mustard yellow hood up, pale warm skin, small cheek gill marks, simple small dark eyes, broad cheerful open grin, yellow raincoat. Match identity and outfit exactly. No visor or helmet. |
| 9 | `raw/props/scrap_brass.png` | K-item | Item icon, small single grouped pile of worn golden brass salvage scraps: two bent metal offcuts, short pipe segment and one bent bolt. Simple chunky silhouettes, muted brass ochre with limited highlights and dark edge, no face or ornamental details. |
| 10 | `raw/props/gear.png` | K-item | Item icon, one simple round chunky bronze gear, 8 clear broad teeth, round central hole. Warm brass ochre, restrained edge highlights, 2-3 shades, clean practical worn metal. No face, no sparkles, no ornate bolts. |

### 공통 스타일 K / KP / K-item

```
Original cute pixel art for Sunken City, matching the attached approved diver's pixel scale, simple clean face, modest rounded proportions, soft angular silhouettes, restrained warm colors with teal accents and 2-3 flat shades per material. Crisp hard square pixels like a 48x64 native sprite enlarged, large readable pixel clusters. Human characters about 3 to 3.5 heads tall, simple dot or short-line eyes and small expressive mouths. No enormous glossy anime eyes, no blush, no baby chibi, no bean body, no photorealism, no texture noise or elaborate ornament. Full body side view facing right unless portrait. Single static subject centered, square transparent background with padding. No text, frame, ground, shadow, effects, sparkles or bubbles.
```

KP는 전신 그림을 추가 참조하여 같은 얼굴·머리색·의상을 유지한 머리와 어깨 초상화로 그린다. K-item은 얼굴 없는 단일 아이템 아이콘으로 그린다.

---

## 규칙 (모든 그림)

- 저장: `sunken-city/art/` 아래의 표 경로 그대로. PNG, 배경 투명 (안 되면 단색 마젠타 #FF00FF)
- 글자·테두리·바닥 그림자 없이. 애니메이션 시트 말고 정지 포즈 한 장
- **캐릭터·아이템 = 독자적이고 담백한 귀여운 도트(스타일 K)**, **세계(타일·배경·건물) = 어둡고 자세하게** — 대비로 캐릭터가 눈에 띄게
- 거품·반짝이가 붙어 나와도 괜찮음 — `tools/art_build.py`가 캐릭터 주변의 작은 조각은 지움
- 게임 적용은 Claude가 함: `python3 tools/art_build.py` → `assets/` → 게임 연결

---

## 이미 끝난 그림 (다시 그리지 않음)

| 경로 | 내용 | 상태 |
|---|---|---|
| `raw/sprites/diver.png` | 독자적 도트 잠수부 — **기준 그림** | ✅ 2026-11-02 확정 |
| `raw/tiles/` 6장 | 돌벽·금 간 벽·마른 돌·단단한 벽·산호 잔해·모래 | ✅ 게임에 적용 |
| `raw/props/` 건물 3·펌프 2·수문·공기 식물·파이프 | 세계 쪽 물건 | ✅ 게임에 적용 |
| `raw/scenes/` 3장 | 물속 원경·물 빠진 거리·마을 | ✅ 게임에 적용 |

### 세계 쪽 스타일 (다음 묶음에서 쓸 것)

**B — 타일**
```
Pixel art 2D side-view platformer terrain tile, 32-bit style, seamless tileable
square texture, flooded ancient stone city, deep teal and navy palette, subtle
algae and barnacles, viewed straight from the side, no perspective, no text,
no frame, square image.
```

**C — 물건·건물**
```
Pixel art game object, 32-bit style, single object centered, side view, ancient
waterlogged machinery and ruins from a sunken city, brass and green-stained copper,
soft top-left highlight, bold dark outline, transparent background (or solid #FF00FF
magenta background), no text, no frame, no hands, square image.
```

**D — 배경·컷신**
```
Pixel art 2D side-view game background, 32-bit style, vast flooded ancient city
underwater, layers of sunken towers fading into darkness, god rays from the surface,
deep teal and navy with glowing cyan accents, atmospheric and lonely, no characters,
no text, no UI, no frame. Wide 16:9 image.
```

---

## 다음 묶음 (마일스톤 시작할 때 Claude가 채움)

| 묶음 | 내용 | 장수 | 시점 |
|---|---|---|---|
| 3 | 수문·물 생태(해초 숲·산호) + 괴물 4(스타일 K) + 구역 2개 배경 | 12 | M3 |
| 4 | 구역 보스 3(스타일 K) + 깊은 층 시대별 유물 6 | 9 | M4 |
| 5 | 타이틀·오프닝·엔딩 컷 | 6 | M5~M6 |

---

## 생성 상태

- [x] 묶음 1·2 세계 아트 생성·기존 게임 적용
- [x] 승인 잠수부 시안을 `raw/sprites/diver.png`에 반영 (2026-11-02)
- [x] 묶음 R 전신 6·초상화 2·아이템 2, 새 스타일로 재생성·교체 (2026-11-02)
- [x] PNG 11장(기준 잠수부 포함) 정사각·RGBA·투명도 검증
- [x] 새 스타일 원본 게임용 가공·런타임 연결 (Claude, 2026-11-02 — 스크린샷 `production/qa/evidence/art-bundle-r/`)

도구: 내장 GPT image_gen. 새 생성 프롬프트·참조·크기는 `generation-manifest.json`의 `bundle_original`에 기록. 이전 `bundle_r`는 폐기된 치비 스타일 기록이다.
