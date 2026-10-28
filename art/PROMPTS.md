# GPT 스프라이트 프롬프트

## 사용법
1. GPT(이미지 생성)에 아래 **공통 스타일** + **개별 프롬프트**를 이어 붙여서 하나씩 요청
2. 받은 PNG를 `art/raw/` 에 **표의 파일명 그대로** 저장 (예: `art/raw/demon_lord.png`)
3. Claude 에게 "이미지 넣었어" → `python3 tools/pixelate.py` 로 16x16 도트화 후 게임에 적용

팁
- 한 대화창에서 연달아 만들면 그림체가 더 잘 맞음. 첫 장 마음에 들면 "같은 스타일로" 라고 덧붙이기
- 배경은 투명이 제일 좋음. 안 되면 **단색 배경**(마젠타 #FF00FF 추천)이면 스크립트가 지움
- 정사각형 1장, 캐릭터 1개만. 글자/테두리/그림자 바닥 없이

---

## 공통 스타일 (매번 앞에 붙이기)

```
Pixel art game sprite, 16-bit style like Enter the Gungeon, top-down 3/4 view,
single character centered, full body, facing right, chunky readable silhouette,
bold dark outline, limited palette, flat shading with one highlight tone,
transparent background (or solid #FF00FF magenta background), no text, no frame,
no ground shadow, square image.
```

---

## 개별 프롬프트

| 파일명 | 프롬프트 |
|---|---|
| `demon_lord.png` | A small cute but menacing demon lord: purple skin, two curved bone horns, red glowing eyes, dark purple cape, tiny clawed hands. |
| `warrior.png` | A human knight hero invader: silver helmet with visor, blue tunic over chainmail, round shield and short sword, brave pose. |
| `archer.png` | A human archer hero: green hood, light leather armor, holding a wooden bow, quiver on back. |
| `boss_hero.png` | The legendary hero boss: large golden armored paladin with red cape, huge glowing greatsword, imposing, heroic. Bigger and more detailed than a normal character. |
| `slime.png` | A round green slime monster with a glossy highlight, two small black eyes, slightly squished blob shape. |
| `goblin.png` | A small green goblin warrior with pointy ears, crude dagger, ragged brown loincloth, mischievous grin. |
| `skeleton.png` | A skeleton archer: bone-white skeleton with a dark tattered hood, holding an old bow. |
| `core.png` | A floating magic dungeon core crystal, deep red gem with glowing pink center, small dark stone pedestal. Object, not a character. |
| `floor.png` | A seamless tileable dark stone dungeon floor tile, grey-blue cobblestones with subtle cracks, viewed from top. Fill the entire square, no background. |
| `wall.png` | A dark stone brick dungeon wall tile seen from top-down 3/4 view: lighter top edge, darker front face of bricks. Fill the entire square, no background. |

선택 (있으면 더 좋음)

| 파일명 | 프롬프트 |
|---|---|
| `spike.png` | A metal floor spike trap: three sharp grey spikes coming out of a dark square metal plate. Object. |
| `fire_trap.png` | A floor fire trap: dark metal grate with orange flames bursting up. Object. |
| `torch.png` | A wall-mounted torch with a bright orange flame. Object. |

---

# 2차: 새 적 4종 (한 장짜리, 위와 같은 방법)

| 파일명 | 프롬프트 |
|---|---|
| `cleric.png` | A human cleric hero healer: white and gold robe, holding a glowing holy staff, kind face, small halo light. |
| `mage.png` | A human battle mage hero: blue pointed hat and robe, holding a staff with a burning orange fireball. |
| `rogue.png` | A sneaky human rogue hero: dark purple hood and mask, two short daggers, light leather armor, crouched pose. |
| `guardian.png` | A heavily armored shield knight hero: full steel plate armor, huge tower shield, bulky and slow-looking. |

---

# 3차: 걷기 애니메이션 (스프라이트 시트)

## 사용법
1. GPT 에 **애니메이션 공통 스타일** + 캐릭터 프롬프트를 이어 붙여 요청
2. 한 장 안에 **같은 캐릭터 4프레임이 가로로 한 줄** 로 나와야 함
3. `art/raw/<캐릭터>_walk.png` 로 저장 (예: `art/raw/warrior_walk.png`)
4. Claude 에게 "걷기 넣었어" → 프레임 자동 분리 + 도트화 + 게임에서 걸을 때 재생

팁
- 첫 시도가 4프레임이 아니면 "exactly 4 frames, one row, evenly spaced" 를 다시 강조
- 프레임마다 캐릭터 모양이 너무 다르면 "same character design in every frame, only legs and arms move" 추가
- 원래 한 장짜리 이미지를 같이 첨부하고 "이 캐릭터로" 라고 하면 일관성이 훨씬 좋아짐 (art/raw/<이름>.png 업로드)

## 애니메이션 공통 스타일 (매번 앞에 붙이기)

```
Pixel art sprite sheet, 16-bit style like Enter the Gungeon, top-down 3/4 view.
Exactly 4 frames in a single horizontal row, evenly spaced, same size,
showing a walk cycle facing right: frame 1 contact (left foot forward),
frame 2 passing, frame 3 contact (right foot forward), frame 4 passing.
Same character design, colors and proportions in every frame; only legs, arms and body bob move.
Bold dark outline, limited palette, transparent background (or solid #FF00FF magenta),
no text, no frame borders, no ground shadow.
```

## 캐릭터 프롬프트

| 파일명 | 프롬프트 |
|---|---|
| `demon_lord_walk.png` | The small purple demon lord with curved bone horns, red eyes and dark purple cape (same character as the attached image). |
| `warrior_walk.png` | The human knight with silver visor helmet, blue tunic, round shield and short sword (same as attached). |
| `archer_walk.png` | The green-hooded human archer holding a bow (same as attached). |
| `goblin_walk.png` | The small green goblin with pointy ears and a crude dagger (same as attached). |
| `skeleton_walk.png` | The skeleton archer with dark tattered hood and old bow (same as attached). |
| `slime_walk.png` | The round green slime: 4-frame bounce instead of walking — squash, rise, stretch, land. |
| `boss_hero_walk.png` | The golden armored paladin boss with red cape and glowing greatsword, heavy stomping walk (same as attached). |

선택: `cleric_walk.png`, `mage_walk.png`, `rogue_walk.png`, `guardian_walk.png` 도 같은 방식.
