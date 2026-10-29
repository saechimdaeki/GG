# GPT 이미지 프롬프트 — 저주받은 전당포

세계·인물 설정: [`design/story.md`](../design/story.md)

## 사용법
1. GPT 이미지 생성에 **공통 스타일 + 개별 프롬프트**를 이어 붙여 한 장씩 요청
2. 받은 PNG를 표의 **파일명 그대로** 아래 폴더에 저장
   - 초상화(NPC·손님) → `cursed-pawnshop/art/raw/portraits/`
   - 물건 → `cursed-pawnshop/art/raw/items/`
   - 배경·이벤트 컷 → `cursed-pawnshop/art/raw/scenes/`
3. Claude 에게 "이미지 넣었어" → 도트화해서 게임에 적용 (없는 그림은 코드 그림이 대신함)

팁
- **묶음 하나는 한 대화창에서** 연달아 만들어야 그림체가 맞음. 첫 장이 마음에 들면 이후엔 "같은 스타일로" 덧붙이기
- 배경 투명, 안 되면 **단색 마젠타(#FF00FF)** — 스크립트가 지움
- 글자·테두리·바닥 그림자 없이
- 묶음 순서대로 하면 됨. **1묶음만 있어도 지금 게임 화면이 바뀜**

| 묶음 | 내용 | 장수 | 게임에 들어가는 시점 |
|---|---|---|---|
| 1 | 지금 손님 8종 + 물건 5종 + 가게 배경 | 14 | 바로 |
| 2 | 메인 NPC 10명 (표정 4종 한 장) | 10 | M3~M4 스토리 |
| 3 | 추가 손님 16종 | 16 | 다음 업데이트 (손님 다양화) |
| 4 | 추가 물건 25종 (금속·보석·유기물·문서·조각) | 25 | M3~M5 감정 도구 해금 |
| 5 | 배경·이벤트 컷 9장 | 9 | 아침/밤·이벤트·엔딩 |

---

## 공통 스타일 A — 초상화 (NPC·손님, 묶음 1·2·3)
```
Dark fantasy pixel art character portrait, 32-bit style, eldritch Lovecraftian horror
mood set in a foggy cursed harbor town, muted desaturated palette with sickly greens,
deep purples and bone whites, single character shown from the chest up, facing the
viewer as if standing at a shop counter (counter not shown), dramatic warm candlelight
from the lower left with deep shadows, bold dark outline, detailed but readable at
small size, unsettling but full of personality (not gory), transparent background
(or solid #FF00FF magenta background), no text, no frame, square image.
```

## 공통 스타일 B — 물건 (묶음 1·4)
```
Pixel art game item icon, 32-bit style, single cursed occult artifact centered, front
view, eldritch Lovecraftian design, soft top-left highlight and dark shading, bold dark
outline, transparent background (or solid #FF00FF magenta background), no text, no
frame, no hands, square image.
```
**금속 물건은 프롬프트 끝에 이 줄을 꼭 붙이기** (게임이 금·은·청동 색을 입힘):
```
Made of plain neutral pale silver-grey metal, no gold and no color tint on the metal.
```

## 공통 스타일 C — 배경·이벤트 컷 (묶음 1·5)
```
Dark fantasy pixel art scene, 32-bit style, eldritch Lovecraftian horror in a foggy
cursed harbor town, muted desaturated palette with sickly greens, deep purples and
warm candle oranges, strong atmospheric lighting, detailed environment, no text,
no UI, no frame. Wide 16:9 image.
```

---

## 묶음 1 — 지금 게임에 바로 들어감 (14장)

### 손님 8종 (스타일 A)
| 파일명 | 프롬프트 |
|---|---|
| `deep_one.png` | A Deep One fish-man merchant: grey-green scaly skin, huge bulging glassy fish eyes on the sides of its head, wide lipless mouth, gill slits on the neck, spiny dorsal fin crest, tattered fisherman's coat crusted with barnacles, holding a dripping cloth sack. |
| `faceless.png` | A faceless monk: tall figure in a heavy dark grey hooded robe, inside the hood a smooth pale featureless face with no eyes and no nose, only a thin stitched mouth, long thin pale fingers clasped, a rusty censer hanging from the belt. |
| `tentacle.png` | A tentacle-faced merchant: purple rubbery skin, bulbous bald head, glowing pale green pupil-less eyes, a beard of four writhing tentacles where the mouth should be, ornate but faded purple merchant's robe with gold trim, a coin purse. |
| `eyes.png` | A many-eyed flesh colony: a lumpy pinkish-red mass of flesh vaguely shaped like a person, covered in seven eyes of different sizes all looking in different directions, wrapped in a stained beige shawl, small clawed hands. |
| `ghoul.png` | A ghoul gravedigger: grey corpse-like skin, sunken black eye sockets with tiny yellow glowing pupils, elongated jaw with sharp yellowed teeth, ragged gravedigger clothes, dirt under long claws, a shovel over the shoulder. |
| `drowned.png` | A drowned sailor ghost: pale bloated blue-white skin, empty milky eyes, wet seaweed tangled in long hair, water dripping everywhere, torn navy sailor coat with rusty brass buttons, a small crab on the shoulder. |
| `spider.png` | A spider noblewoman: porcelain-white elegant face with six red eyes (two large, four small), small fangs, black hair in a tall Victorian updo, black lace high-collar gown, eight thin black spider legs spreading from her back. |
| `goblin.png` | A shifty goblin peddler: green warty skin, long droopy ears, yellow eyes, sly grin with crooked teeth, oversized patched brown coat full of pockets with trinkets sticking out. |

### 물건 5종 (스타일 B + 금속 줄)
| 파일명 | 프롬프트 |
|---|---|
| `medal.png` | A round whispering amulet: flat metal disc with engraved spiral tentacles curling toward the center, a small loop on top for a chain, worn edges. Leave a plain smooth area in the middle. |
| `goblet.png` | A ritual chalice: wide metal cup on a thin stem, one real human-like eye set into the stem staring forward, faint dark stains inside the rim. |
| `dagger.png` | A ritual dagger: curved wavy metal blade with faint runes, dark wrapped leather handle, a red gem in the crossguard, dried dark stains on the blade. |
| `idol.png` | A small squatting idol of an octopus-headed winged deity: bulbous head, tentacle beard, folded bat wings, sitting on a square pedestal. |
| `crown.png` | A sunken deep-sea crown: jagged crown with uneven spikes, covered in small barnacles and bits of seaweed, three dull teal gems on the band. |

### 가게 배경 (스타일 C, 단 가로로 아주 긴 그림)
| 파일명 | 프롬프트 |
|---|---|
| `shop_bg.png` | The back wall of a cramped occult pawnshop at night seen from behind the counter: dark wooden walls, crooked shelves full of jars with floating specimens, skulls, dusty books and strange idols, a small rain-streaked window showing a foggy alley, hanging dried herbs, an old ledger nailed to the wall, warm candlelight from the lower left fading into deep purple shadows. No counter, no characters. Very wide 16:5 aspect ratio. |

---

## 묶음 2 — 메인 NPC 10명 (스타일 A, 표정 4종을 한 장에)

각 프롬프트 끝에 이 줄을 붙이기:
```
Show the same character four times side by side in one row, identical pose and framing,
with four expressions from left to right: neutral, smiling, angry, shocked.
```
파일명은 `<id>_faces.png` (예: `baal_faces.png`).

| id | 프롬프트 |
|---|---|
| `baal` | Baal, a demon loan shark: large portly demon with dark red skin, curled ram horns, a gold monocle over one burning yellow eye, impeccable black pinstripe suit with a pocket watch chain, holding a thick leather ledger, polite but terrifying smile with too many sharp teeth. |
| `pip` | Pip, a small ghost child who haunts the pawnshop: pale translucent blue-white glow, oversized old-fashioned newsboy cap, big round dark eyes, a tiny feather duster, faint wispy tail instead of legs, mischievous and kind. |
| `morg` | Old Morg, a swamp witch who runs the rival pawnshop: hunched old woman with mossy green skin, long crooked nose with a wart, one milky eye and one sharp yellow eye, shawl made of fishing nets with shells and bones tied in, rings on every finger. |
| `greta` | Captain Greta, half-werewolf captain of the harbor watch: strong woman with wolf ears, grey fur on her cheeks and neck, amber eyes, a scar across the nose, dented steel breastplate over a dark blue watch uniform, a lantern badge on the chest. |
| `bishop` | The Damp Bishop, leader of the Brotherhood of the Wet Star: gaunt priest with a fish-like face and gills, wearing soaked dark green ceremonial vestments embroidered with a star-and-tentacle symbol, water constantly dripping from the tall mitre, holding a coral staff. |
| `rat_king` | The Rat King, boss of the harbor's stolen goods ring: dozens of rats knotted together by their tails into one humanoid shape, wearing a tiny tattered velvet cape and a bent tin crown, many glinting red eyes, one rat in front holding a gold coin. |
| `amelia` | Amelia, a human scholar of the occult: young woman with messy auburn hair, round spectacles, ink-stained fingers, tweed coat with a satchel full of papers, dark circles under her eyes, a faint strange symbol drawn on her palm. |
| `drachma` | Count Drachma, a vampire collector: tall elegant vampire with slicked silver hair, deathly pale skin, red eyes, high-collared black and crimson coat, white gloves, holding a jeweler's loupe, refined and greedy. |
| `herald` | The Herald of the Abyss: tall shadowy figure made of darkness and drifting fog, no face except for a field of tiny white stars where the face should be, a long ragged cloak like deep water, thin hands of black smoke. |
| `elias` | Elias Moreau, the missing grandfather, as a ghost: kind old man with a white beard and wire spectacles, pawnbroker's apron and sleeve garters, translucent pale green glow, holding an old brass loupe, sad wise smile. |

---

## 묶음 3 — 추가 손님 16종 (스타일 A)
| 파일명 | 프롬프트 |
|---|---|
| `mi_go.png` | A Mi-Go fungal crustacean: pinkish fungus-like body with membranous bat wings, a convoluted ellipsoid head covered in short antennae instead of a face, several jointed pincers, holding a glass brain cylinder. |
| `shadow.png` | A shadow person: a flat human silhouette of pure black darkness with ragged edges, only two small white pinprick eyes, wearing a real old bowler hat that floats on it. |
| `wax_doll.png` | A wax doll: life-sized porcelain-and-wax doll lady with cracked rosy cheeks, glass eyes, one eye slightly melted down the cheek, frilly faded pink dress, stiff jointed hands. |
| `rat_man.png` | A rat man: hunched humanoid rat with patchy brown fur, twitchy whiskers, beady red eyes, yellow buck teeth, wearing a filthy waistcoat stuffed with stolen spoons. |
| `bog_witch.png` | A young bog witch apprentice: green-skinned girl with frog-like wide mouth, lily pads in her wet hair, a pointed hat made of reeds, carrying a jar of glowing tadpoles. |
| `masked_noble.png` | A masked noble: aristocrat in a rich purple coat and lace jabot, face completely hidden behind a smooth white porcelain theater mask with an unnatural smile, something moving behind the mask's eye holes. |
| `moth_man.png` | A moth man: tall gaunt figure with huge dusty grey moth wings folded like a cloak, large glowing red compound eyes, feathery antennae, fuzzy collar of white fur. |
| `bone_peddler.png` | A skeleton peddler: cheerful skeleton in a moth-eaten traveling coat and wide-brimmed hat, a candle burning inside the ribcage, a tray of bones hanging around its neck. |
| `candle_beast.png` | A melted candle creature: a person-shaped heap of melted white wax with several wicks burning on its head, face sagging and dripping, small black eyes sinking into the wax. |
| `stitched.png` | A stitched corpse: patchwork body sewn together from mismatched skin tones with thick black stitches, two different colored eyes, a bolt in the neck, wearing a too-small butler uniform, polite. |
| `plague_doctor.png` | A crow plague doctor: actual crow head with black feathers instead of a mask, round red glass goggles, long black waxed coat, a cane with a bird skull handle. |
| `moss_giant.png` | A moss giant: huge hunched stone-like figure covered in thick moss and tiny mushrooms, only its head and massive shoulders fit in the frame, small kind glowing eyes. |
| `two_headed.png` | A two-headed old man: one body with two bald wrinkled heads that clearly hate each other, one grumpy and one sly, sharing a single shabby brown overcoat. |
| `weeping_statue.png` | A weeping stone angel statue: cracked grey stone angel with chipped wings, hands half covering its face, dark tear stains running from its eyes, moss in the cracks. |
| `mushroom.png` | A mushroom person: small round body with a huge red-capped mushroom head with white spots, gills under the cap, two tiny dot eyes, spores drifting from the cap, wearing a leaf scarf. |
| `salt_mummy.png` | A salt mummy: desiccated sailor's corpse crusted with white salt crystals, empty eye sockets glowing faint blue, wrapped in salt-stiffened sail cloth, a rusty anchor pendant. |

---

## 묶음 4 — 추가 물건 25종 (스타일 B)

### 금속 10종 (+ 금속 줄 붙이기)
| 파일명 | 프롬프트 |
|---|---|
| `ring.png` | A signet ring with a tiny tentacle wrapping around the band and an empty setting where a stone should be. |
| `brooch.png` | An oval brooch shaped like a closed eyelid with long eyelashes, ornate filigree border. |
| `censer.png` | A hanging ritual censer: perforated metal sphere on three chains, thin wisps of greenish smoke leaking out. |
| `bell.png` | A small ritual hand bell with a handle shaped like a screaming face, cracked rim. |
| `candlestick.png` | A candlestick shaped like a withered hand holding up three fingers, each fingertip a candle socket. |
| `pocket_watch.png` | An open pocket watch whose face has thirteen numbers and hands that point outward in different directions, cracked glass. |
| `compass.png` | A ship's compass whose needle points straight down into the sea, engraved waves on the case. |
| `key.png` | A large ornate key with a bit shaped like a tiny ribcage and a bow shaped like an eye. |
| `thorn_crown.png` | A crown of twisted metal thorns with tiny bells hanging from the points. |
| `goat_bust.png` | A small bust of a goat-headed figure with four horns and a serene expression, on a round base. |

### 보석 3종
| 파일명 | 프롬프트 |
|---|---|
| `blood_ruby.png` | A large teardrop-shaped deep red ruby with a darker red liquid swirling inside, set in a simple black claw setting. |
| `star_opal.png` | A round opal with a tiny galaxy of stars trapped inside, one star brighter than the rest, glowing faintly. |
| `pearl_necklace.png` | A mermaid pearl necklace: strand of uneven grey-white pearls, the center pearl is larger and has a faint face inside. |

### 유기물 5종
| 파일명 | 프롬프트 |
|---|---|
| `hand_of_glory.png` | A Hand of Glory: a dried mummified human hand with candles burning on each fingertip, on a small brass stand. |
| `skull_cup.png` | A drinking cup made from the top of a skull, rim lined with tarnished silver, carved runes on the bone. |
| `eye_jar.png` | A glass specimen jar with murky green liquid and a single large eyeball floating inside, looking forward, cork lid with a wax seal. |
| `scale_mirror.png` | A hand mirror with a frame made of iridescent fish scales, the reflection shows dark water instead of a reflection. |
| `bone_flute.png` | A flute carved from a long pale bone with uneven finger holes, a faint shimmering sound-wave drawn coming out. |

### 문서 4종
| 파일명 | 프롬프트 |
|---|---|
| `grimoire.png` | A thick grimoire bound in pale scaly leather with a brass clasp shaped like a mouth, a single eye on the cover. |
| `blood_contract.png` | A rolled parchment contract signed in dripping red ink, a black wax seal with a horned skull, slightly singed edges. |
| `ship_log.png` | A water-damaged ship's logbook with a cracked leather cover, seaweed stuck to the spine, pages swollen. |
| `map_fragment.png` | A torn fragment of an old sea map showing a spiral whirlpool and strange coordinates, edges burnt. |

### 흐르그의 조각 7종 (스토리 전용, 은은하게 빛나는 느낌)
| 파일명 | 프롬프트 |
|---|---|
| `relic_tooth.png` | A giant curved black tooth the size of a dagger, glowing faint violet cracks, ancient and alive-looking. |
| `relic_scale.png` | A single huge iridescent black-green scale the size of a shield, edges glowing faint violet. |
| `relic_eye.png` | A petrified enormous eye the size of a fist, slit pupil, still faintly glowing violet, as if it might blink. |
| `relic_tentacle.png` | A severed petrified tentacle tip coiled in a spiral, suckers glowing faint violet. |
| `relic_heart.png` | A dark stone heart with veins of violet light, slowly beating, wrapped in old chains. |
| `relic_voice.png` | A large spiral conch shell, black with violet glowing ridges, faint sound waves coming out of the opening. |
| `relic_name.png` | A small cracked stone tablet carved with an unreadable alien glyph that glows violet, the glyph hurts to look at. |

---

## 묶음 5 — 배경·이벤트 컷 9장 (스타일 C)
| 파일명 | 프롬프트 |
|---|---|
| `morning_street.png` | The foggy cobblestone alley outside the pawnshop at dawn: crooked leaning buildings, the shop's sign "a hanging brass three-ball pawnbroker sign" creaking, gulls, a lighthouse with a dead light far away, puddles. |
| `night_shop.png` | The inside of the closed pawnshop at night: single candle on the counter, an open ledger, coins being counted, shelves of cursed items glowing faintly, something watching from a jar. |
| `baal_office.png` | A demon loan shark's office: red velvet walls, huge mahogany desk, stacks of contracts, a fireplace with green flames, jars of glowing souls on the shelves. |
| `vault.png` | A secret vault under the pawnshop: round stone room with seven empty pedestals in a circle, chains and seals on the walls, violet light leaking from cracks in the floor. |
| `harbor.png` | The cursed harbor at night: black water, rotting piers, a sunken ship mast sticking out, deep ones' eyes glinting in the water, huge full moon behind fog. |
| `ritual.png` | A cult ritual on a sea cliff: hooded figures in a circle, a stone altar with violet glowing relics, a giant shadow rising from the stormy sea below, stars in a strange alignment. |
| `ending_keeper.png` | Ending scene: the pawnshop at sunrise, fog clearing for the first time, the shopkeeper's silhouette opening the door, a ghostly old man smiling beside them. |
| `ending_awaken.png` | Ending scene: the whole harbor town sinking as a colossal tentacled god rises from the sea, tiny pawnshop sign floating in the water. |
| `ending_seized.png` | Ending scene: the demon loan shark standing in the empty pawnshop holding a glowing soul in a jar, the shop's sign torn down. |
