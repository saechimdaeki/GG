# 이미지 프롬프트 (Gemini) — 가라앉은 도시

기획: [`design/game-brief.md`](../design/game-brief.md)

## 사용법
1. Gemini에 **공통 스타일 + 개별 프롬프트**를 이어 붙여 한 장씩 요청
2. 받은 PNG를 표의 **파일명 그대로** 아래 폴더에 저장
   - 캐릭터·괴물·주민 → `sunken-city/art/raw/sprites/`
   - 타일·지형 → `sunken-city/art/raw/tiles/`
   - 물건·부품·건물 → `sunken-city/art/raw/props/`
   - 배경·컷신 → `sunken-city/art/raw/scenes/`
3. Claude에게 "이미지 넣었어" → 도트화·배경 제거해서 게임에 적용 (없는 그림은 코드 그림이 대신함)

팁
- **묶음 하나는 한 대화창에서** 연달아 만들어야 그림체가 맞음. 첫 장이 마음에 들면 이후엔 "같은 스타일로" 덧붙이기
- 다른 도구(GPT 등)를 쓰게 되면 **한 묶음 안에서는 섞지 말기** — 그림체가 달라짐. 쓴 도구는 표 "도구" 칸에 적기
- 배경 투명, 안 되면 **단색 마젠타(#FF00FF)** — 스크립트가 지움
- 글자·테두리·바닥 그림자 없이
- **걷기·헤엄 애니메이션 시트는 요청하지 않기** — AI가 프레임을 못 맞춤. 정지 포즈 한 장만 받고 움직임은 코드가 만듦
- 타일은 "seamless tileable"이 핵심. 이음매가 보이면 Claude가 잘라 맞춤

| 묶음 | 내용 | 장수 | 게임에 들어가는 시점 | 도구 |
|---|---|---|---|---|
| 1 | 잠수부 + 첫 구역 타일 + 펌프 + 괴물 3 + 재료 4 + 배경 | 16 | M1 | |
| 2 | 마을 건물 3 + 주민 2 (초상화) + 마른 땅 타일 | 9 | M2 | |
| 3 | 수문·물 생태(해초 숲·산호) + 괴물 4 + 구역 2개 배경 | 12 | M3 | |
| 4 | 구역 보스 3 + 깊은 층 시대별 유물 6 | 9 | M4 | |
| 5 | 타이틀·오프닝·엔딩 컷 | 6 | M5~M6 | |

---

## 공통 스타일 A — 스프라이트 (잠수부·괴물·주민 전신)
```
Pixel art 2D side-view game sprite, 32-bit style, single character in full body,
facing right, neutral standing or swimming pose, mysterious flooded ancient city
setting, deep teal and navy palette with soft cyan rim light from above, bold dark
outline, readable at small size, charming not gory, transparent background (or solid
#FF00FF magenta background), no text, no frame, no shadow, square image.
```

## 공통 스타일 B — 타일 (지형)
```
Pixel art 2D side-view platformer terrain tile, 32-bit style, seamless tileable
square texture, flooded ancient stone city, deep teal and navy palette, subtle
algae and barnacles, viewed straight from the side, no perspective, no text,
no frame, square image.
```

## 공통 스타일 C — 물건·부품·건물
```
Pixel art game object, 32-bit style, single object centered, side view, ancient
waterlogged machinery and ruins from a sunken city, brass and green-stained copper,
soft top-left highlight, bold dark outline, transparent background (or solid #FF00FF
magenta background), no text, no frame, no hands, square image.
```

## 공통 스타일 D — 배경·컷신
```
Pixel art 2D side-view game background, 32-bit style, vast flooded ancient city
underwater, layers of sunken towers fading into darkness, god rays from the surface,
deep teal and navy with glowing cyan accents, atmospheric and lonely, no characters,
no text, no UI, no frame. Wide 16:9 image.
```

---

## 묶음 1 — M1 (16장)

| 파일명 | 스타일 | 개별 프롬프트 |
|---|---|---|
| `sprites/diver.png` | A | A lone diver in an old brass diving helmet with a round glass window and a small lamp on top, patched canvas suit, air tank on the back, swimming pose |
| `sprites/eel_lantern.png` | A | A long eel monster with a glowing lantern lure on its head, sharp tiny teeth, swimming pose |
| `sprites/crab_rubble.png` | A | A big crab whose shell is a broken chunk of carved stone wall, angry eyes |
| `sprites/jelly_ghost.png` | A | A pale translucent jellyfish shaped like a drifting ghost with sad eyes, long trailing tentacles |
| `tiles/stone_wall.png` | B | Carved ancient stone bricks, sturdy |
| `tiles/stone_cracked.png` | B | The same carved stone bricks but heavily cracked, looks breakable |
| `tiles/sand_floor.png` | B | Seabed sand with small pebbles and shells, top edge is the surface of the ground |
| `tiles/coral_rubble.png` | B | Loose rubble mixed with small coral, diggable |
| `props/pump_broken.png` | C | A large ancient water pump machine with a big wheel and pipes, rusted, missing parts, broken and dark |
| `props/pump_fixed.png` | C | The same large ancient water pump machine repaired, polished brass, glowing cyan gauge, water gushing from the pipe |
| `props/scrap_brass.png` | C | A handful of brass scrap pieces |
| `props/pipe_part.png` | C | A short bent copper pipe fitting |
| `props/gear.png` | C | A green-stained bronze gear |
| `props/air_bubble_plant.png` | C | An underwater plant with glowing bulbs that release air bubbles (oxygen refill) |
| `scenes/bg_zone1_far.png` | D | Far layer only: silhouettes of sunken towers and domes, very dark, light from above |
| `scenes/bg_zone1_drained.png` | D | The same sunken street after the water was drained: wet stones, puddles, warm sunlight through a hole far above, moss, hopeful mood, no water |

묶음 2~5의 개별 프롬프트는 해당 마일스톤을 시작할 때 이 파일에 추가함.
