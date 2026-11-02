# 리서치: 데이브 더 다이버 같은 게임으로 가려면 (2026-11-02)

목표: 캐릭터를 따라 하는 게 아니라 **"데이브 더 다이버 같은 게임"** — 바다가 예쁘고 살아 있고, 나갔다 돌아오는 루프가 중독적인 게임.

---

## 1. 데이브 더 다이버는 어떻게 만들었나 (확인된 사실)

### 그림
- **캐릭터는 2D 도트, 바다(지형·물고기·보스·배)는 3D 모델에 도트 느낌 텍스처.** 처음엔 배경 물체만 3D로 넣었고, 나중에 물고기까지 3D로 바꿈. 2D 데이브 vs 3D 상어가 가장 어려웠다고 함 ([Game Developer](https://www.gamedeveloper.com/design/dave-the-diver), [The Indie Arcade](https://theindiearcadecom.wordpress.com/2025/05/27/my-time-with-dave-the-diver-went-swimmingly/))
- 이유: 평평한 2D보다 깊이감이 훨씬 좋았고, 모든 물고기를 손 도트 애니메이션으로 만드는 작업량을 줄임
- 감독 황재호: 바다는 무섭고 진지할 수 있는 곳이라 **도트의 엉뚱함으로 "재미와 모험"** 쪽으로 틀었다 ([GameRant](https://gamerant.com/dave-the-diver-interview/))
- Unity + URP(조명), Cinemachine(카메라를 "촬영장"처럼 조정), Shader Graph(아티스트가 직접 셰이더 실험) ([Game World Observer](https://gameworldobserver.com/2023/12/04/dave-the-diver-unity-2d-3d-graphics-optimization-mintrocket))
- 저지대 3D + 매끈한 텍스처와 전통 도트를 섞음 ([Software Engineering Daily](https://softwareengineeringdaily.com/2024/04/09/developing-dave-the-diver-with-jaeho-hwang-and-minsuk-cha/))

### 게임
- 시작은 "깊은 바다 = 던전, 물고기 = 전리품" ([Inven Global, GDC24](https://www.invenglobal.com/articles/18786/gdc24-blending-humor-and-gameplay-insights-from-dave-the-divers-session))
- 처음엔 초밥집 운영 + 채집을 한 화면에서 했더니 어느 쪽에도 집중을 못 함 → 실제 초밥집(아침에 낚시, 밤에 요리)을 보고 **낮 잠수 / 밤 초밥집으로 분리**한 게 결정적 ([Game Developer](https://www.gamedeveloper.com/design/dave-the-diver))
- 두 루프가 서로 먹여 살림: 새 물고기 → 새 메뉴, 가게 수익 → 장비 업그레이드 ([LinkedIn 분석](https://www.linkedin.com/pulse/genius-goal-loops-dave-diver-dan-butchko-g2zwe))
- 반복을 깨려고 VIP 손님·파티 주문(특정 재료 시간제한), 미니게임을 계속 추가. 단, 메인 루프를 끊지 않게
- 바다는 넓은 오픈월드가 아니라 **"닫혀 있지만 매번 바뀌는"** 곳: 해류·물고기 위치가 잠수마다 달라짐 (팀이 작아서 택한 방식)
- 분위기의 오르내림: 거북이 사진 찍는 평화로운 순간 ↔ 위험 감수하고 전리품 챙기는 긴장
- 작살은 일반 속도보다 느리게, 대신 거리·각도를 정확히 맞춰야 → 맞혔을 때 손맛

---

## 2. 지금 우리 화면이 별로인 이유 (스크린샷 진단)

`production/qa/evidence/art-bundle-r/`의 4장을 기준으로.

| # | 문제 | 왜 별로인가 | 데이브는 |
|---|---|---|---|
| 1 | **세상이 16px 네모 블록 격자** | 같은 벽돌 무늬가 반복되는 정사각형 → 마인크래프트 블록처럼 보이고 "바위·동굴" 모양이 없음 | 지형이 둥글고 울퉁불퉁한 덩어리(바위, 산호초, 해초 숲) |
| 2 | **캐릭터가 너무 작음** | 640×360 화면에 잠수부 24~32px → 화면의 1/12. 표정·동작이 안 보임 | 데이브가 화면에서 훨씬 크고, 카메라가 상황 따라 당기고 밂 |
| 3 | **픽셀 크기가 제각각 (mixel)** | 타일은 128px 텍스처의 일부, 캐릭터는 1254px 그림을 24px로, 배경은 640px 그림 → 같은 화면에 서로 다른 "픽셀 크기"가 섞임. AI 도트는 원래 격자가 26~32px로 들쭉날쭉하고 색이 수만 개 ([분석](https://rangy.ai/blog/ai-pixel-art)) | 캐릭터 도트는 한 격자, 3D 배경은 도트가 아니라서 충돌 안 함 |
| 4 | **배경이 앞을 잡아먹음** | 배경 그림이 전경과 밝기·채도가 비슷하고 디테일이 많아서 어디가 벽인지 헷갈림 | 먼 곳일수록 흐리고 푸르게(대기 원근), 앞은 선명 |
| 5 | **빛·분위기가 없음** | 깊이별 색 변화, 빛줄기, 물결 빛무늬(커스틱), 떠다니는 부유물, 물고기 떼가 전부 없음 → 정지 화면 같음 | 수면 빛, 깊어질수록 어두워짐, 물고기가 늘 돌아다님 |
| 6 | **바다에 생명이 없음** | 장어 몇 마리뿐. 지나가는 물고기·게·해초 흔들림 없음 | 바다 자체가 구경거리 |

**결론:** 그림 한 장 한 장의 문제가 아니라 **화면을 만드는 방식(격자 타일 + 작은 카메라 + 연출 없음)** 의 문제. GPT 그림을 더 뽑아도 이 구조에선 안 좋아짐.

---

## 3. 어떻게 바꿀까 — 선택지

### A. 데이브식 2.5D (3D 바다 + 2D 도트 캐릭터) — 가장 데이브와 비슷
- Godot에서 3D 씬(바위·산호 메시, 안개, 빛)을 깔고 캐릭터·물고기는 `Sprite3D`(빌보드)로
- 장점: 깊이감·조명이 공짜로 좋아짐, 데이브와 같은 느낌
- 단점: **3D 모델이 필요** (AI 3D 생성 or 에셋 팩), 지금 코드(2D 격자 충돌)를 거의 다시 짬. 작업량 가장 큼

### B. 레이어드 2D (그린 지형 + 연출 스택) — ⭐ 추천
- **지형을 격자 타일 대신 "그림 조각"으로**: 큰 바위·산호초·해초·기둥을 GPT로 투명 PNG 조각으로 받아서 배치하고, 충돌은 보이지 않는 다각형(Polygon)으로
- **카메라 확대**: 화면에 보이는 세계를 지금의 절반 정도로 → 잠수부가 화면의 1/6~1/5. 캐릭터 그림은 지금보다 2배 크게 써서 표정이 보이게
- **깊이 레이어 3~5장 (Parallax2D)**: 먼 실루엣(흐리고 푸름) → 중간 바위 → 플레이 층 → 앞을 스치는 해초(살짝 어둡게)
- **연출 스택 (Godot 내장 기능만으로 가능)**
  - 깊이별 색 그라데이션 + 안개 (얕은 곳 청록 → 깊은 곳 남색·검정)
  - 수면 빛줄기(god rays), 바닥의 물결 빛무늬(caustics) 셰이더
  - 잠수부 랜턴은 `PointLight2D`, 2D HDR 글로우로 발광체(장어 랜턴, 공기 식물) 빛나게
  - 부유물(마린 스노우)·거품 파티클
  - 지나가는 물고기 떼(전리품 아님, 구경용) — 바다가 살아 있게
- **픽셀 크기 통일**: AI 그림을 "진짜 격자"로 맞추고(격자 크기 찾기 → 칸마다 다수결 색 → 팔레트 축소) 모든 그림을 같은 배율로 ([방법](https://dev.to/jenissimo/how-to-tame-your-ai-pixel-art-3pk5), [ai2pixelart](https://pypi.org/project/ai2pixelart/))
- 장점: 지금 엔진·코드 구조 유지(충돌만 격자→다각형), GPT 그림 파이프라인과 잘 맞음, 데이브의 "깊이감·생동감" 대부분을 얻음
- 단점: 레벨을 그림 조각 배치로 만드는 툴이 필요 (Godot 에디터에서 직접 배치하면 됨)

### C. 지금 구조 유지 + 연출만 추가
- 타일은 그대로, B의 연출 스택·카메라 확대만
- 장점: 가장 빠름 / 단점: **네모 블록 느낌(문제 1)이 그대로** 남음

---

## 4. 게임 쪽도 데이브에서 배울 것

지금 루프: 잠수 → 재료 → 펌프 → 물 빼기 → 마을 짓기. 데이브와 비교하면 **"매일 돌아오는 두 번째 루프"가 약함** (마을은 한 번 짓고 끝).

| 데이브 | 우리 게임에 넣는다면 |
|---|---|
| 낮 잠수 / 밤 초밥집 | **낮 잠수 / 밤 "마을 고물상(또는 수리점)"**: 건져온 유물·부품을 주민·떠돌이 손님에게 고쳐 팔기. 새 유물 → 새 상품 |
| 물고기 = 전리품, 도감 | 물고기·유물 도감. 잡은 물고기는 마을 식당 재료로 |
| 작살 손맛 | 작살 또는 집게총: 느리지만 정확히 맞히면 손맛 |
| 잠수마다 바뀌는 바다 | 매일 해류·물고기 배치·잔해 위치가 바뀜 (구역은 손으로 만들되 내용물만 무작위) |
| VIP 손님·파티 주문 | 주민 부탁을 "기한 있는 주문"으로 (오늘 밤까지 은빛 물고기 3마리) |
| 평화 ↔ 긴장 | 얕은 곳은 구경·사진, 깊은 곳은 수압·괴물·보물 |
| **이 게임만의 것** | 물을 빼면 그 구역이 마을이 됨 — 데이브에 없는 우리 무기. 유지 |

---

## 5. 추천 순서

1. **B로 간다.** 먼저 "보는 맛" 한 구역 시연: 카메라 확대 + 깊이 레이어 + 빛줄기·커스틱·부유물 + 물고기 떼 + 그림 조각 지형 한 화면 → 스크린샷으로 데이브 느낌이 나는지 판단
2. 괜찮으면 지금 구역(Zone1)을 그림 조각 지형으로 옮김 (게임 로직은 그대로, 충돌만 다각형)
3. 게임 쪽: 낮 잠수 / 밤 고물상 루프를 M3 앞에 넣을지 결정
4. GPT 그림은 그다음에 "조각 단위"로 다시 요청 (바위 덩어리, 산호초, 해초, 먼 실루엣 레이어, 물고기 10종)

## 출처
- [Game Developer — Into the depths of Dave the Diver](https://www.gamedeveloper.com/design/dave-the-diver)
- [Inven Global — GDC24 Dave the Diver session](https://www.invenglobal.com/articles/18786/gdc24-blending-humor-and-gameplay-insights-from-dave-the-divers-session)
- [GameRant — Dave the Diver director interview](https://gamerant.com/dave-the-diver-interview/)
- [Software Engineering Daily — Developing Dave the Diver](https://softwareengineeringdaily.com/2024/04/09/developing-dave-the-diver-with-jaeho-hwang-and-minsuk-cha/)
- [Game World Observer — Unity 2D/3D in Dave the Diver](https://gameworldobserver.com/2023/12/04/dave-the-diver-unity-2d-3d-graphics-optimization-mintrocket)
- [The Indie Arcade — review (3D environments, 2D sprites)](https://theindiearcadecom.wordpress.com/2025/05/27/my-time-with-dave-the-diver-went-swimmingly/)
- [LinkedIn — The Genius Goal Loops of Dave the Diver](https://www.linkedin.com/pulse/genius-goal-loops-dave-diver-dan-butchko-g2zwe)
- [Rangy — AI pixel art isn't really pixel art](https://rangy.ai/blog/ai-pixel-art)
- [DEV — How to tame your AI pixel art](https://dev.to/jenissimo/how-to-tame-your-ai-pixel-art-3pk5)
- [ai2pixelart (PyPI)](https://pypi.org/project/ai2pixelart/)
- [godot-realtimecaustics](https://github.com/paddy-exe/godot-realtimecaustics), [godotshaders — water](https://godotshaders.com/shader-tag/water)
