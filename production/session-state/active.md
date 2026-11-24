# Active Session State — [project or branch]

<!--
  HOW THIS FILE WORKS. Read this before editing it.

  This file has TWO regions and they are not interchangeable:

    1. The CHECKPOINT region below (STATUS + CHECKPOINT blocks) is the part
       machines read. session-start.sh shows it verbatim in the context, so it is
       BOUNDED BY CONSTRUCTION: keep it under ~25 lines. It is OVERWRITTEN on
       every update, never appended to.

    2. Everything after `<!-- /CHECKPOINT -->` is free-form narrative for humans
       and for a returning agent that wants detail. No hook ever injects it, so
       it may grow — but see Rotation below.

  Why the split exists: one append-only file doing both jobs grows without
  bound, and every consumer then reads a different slice of it: the top, the
  bottom, or a block nothing writes. They disagree because there is no contract
  to agree on. With the named regions, every consumer reads the same part.

  ROTATION. When the narrative below grows past ~200 lines, move it to
  `production/session-logs/` and start it fresh:
      bash .claude/scripts/rotate-session-state.sh
  Nothing is lost — and the commit history already records most of what the
  narrative repeats.
-->

<!-- STATUS -->
Epic:
Feature:
Task:
<!-- /STATUS -->

<!-- CHECKPOINT -->
**Updated:** 2026-11-17
**Branch:** `feat/haunted-movers-layouts`
**Current task:** 유령집 이삿짐센터 후속 개편 커밋 — Poly Haven/Kenney/OpenGameArt CC0 에셋(가구·텍스처·발소리·무기), 방 종류·몰딩·큰 홀, 집 탑·지붕창, 트럭 탑차, 첫 1분 목표 안내, 상점(삽·섬광탄·무전기), 갈 곳(시골집/저택/폐병원), 함정·금고방, 가구 뒤지기 열린 모양, 쇠지렛대 전투·괴물 체력·처치 드롭, 미믹, 체력 100, 심장 소리·붉은 가장자리, 유령 재디자인.
**Next step:** 사용자 피드백(4분 19초 플레이 후 커밋 요청). 후보: 2~4명 플레이테스트로 난이도, 트럭·미믹·유령은 아직 기본 도형 조합(진짜 모델 필요), 고른 곳마다 집 외관 다르게. 푸시/PR은 사용자 요청 시.
**Blocked on:** 없음
**Files in progress:** 커밋 안 됨 (작업 트리에 변경 있음): haunted-movers/src/*, data/tuning.json, tests, README, design/game-brief.md
**Run result:** smoke PASS(스크립트 에러 0, 손전등 쫓기·가구 안 고물·괴물 우회는 일부러 망가뜨려 FAIL 확인), network integration PASS. 증거: haunted-movers/production/qa/evidence/*-2026-11-17.png.
**Open questions:** 없음. "러셀컴퍼니" = Lethal Company.
<!-- /CHECKPOINT -->

---

## Notes

### 2026-11-19 막차까지 전투 피드백과 약국 비상함

- 완료. 좀비는 공격 방향을 주황색 부채꼴로 예고하고, 예고 범위에서 벗어나거나 먼저 때려 경직시키면 피격되지 않는다. 피해는 공격 시점에 적용하며 피격 방향·붉은 화면 가장자리·피해 수치·몸 반응·짧은 효과음을 보여 준다.
- 공격. 헛스윙·타격·암살·네일건·폭발·피격에 구별된 소리를 합성했다. 실제 맞은 적에게 피해 숫자와 타격 효과가 뜨고, 클릭 공격 애니메이션이 끝나면 이동·대기로 복귀한다.
- 사건. 2일차 약국 비상함은 고철 1개로 조용히 분해하거나 G로 강제 개방한다. 강제 개방은 골목의 좀비를 끌어오며, 함에는 퓨즈와 의료 물자가 남아 있다.
- 검증. macOS 빌드 및 EditMode 21/21 통과. 빌드에서 공격 예고·옆 회피·경직 취소·피격, 약국 양쪽 개방과 퓨즈 수령을 확인했다. 퓨즈를 챙기면 역 전력 복구 때 기관사 응답이 바뀐다. 증거는 `*feedback-2026-11-19.png`.
- 다음. 사람 플레이에서 무리 상대 공격 예고가 읽히는지, 효과음 볼륨과 피격 간격, 약국 경제를 확인한다.

### 2026-11-19 막차까지 상점 실내와 카메라

- 완료. 약국·철물점·편의점 3곳을 열린 입구, 내부 바닥, 충돌 벽과 가구로 제작했다. 수색은 실내에 들어가야 가능하며 약국·철물점 안에는 좀비가 있다. 2일차 비상함은 약국 안으로 이동했다.
- 아트. 카메라를 62도 고정 사선으로 낮추고 낮은 전면 벽으로 실내 단면을 드러냈다. HOLLOWS 약품장과 공구함을 가져왔으며 라이선스 소스가 없는 작업 공간에는 기본 모델을 사용한다.
- 검증. macOS 빌드 성공, EditMode 21/21 통과. 세 상점의 출입·수색·벽 충돌과 기존 카메라, 튜토리얼, 분기 탈출을 빌드에서 확인했다. 캡처는 `*walk-in-2026-11-19.png`.
- 다음. 일반 조작으로 문턱 근접전과 낮은 각도에서의 외부 골목 시인성을 플레이테스트한다.

### 2026-11-19 막차까지 봉쇄 도시 분기 탈출

- 완료. 날짜 기반 자동 탈출을 열차·검문소 운송차·배수로 중 준비한 루트를 실행하는 시스템으로 교체했다. 각 루트는 다른 연락책과 전력 복구, 차량 수리, 수문 밸브 작업을 거친다. J로 추적 대상 전환. 시도를 중단해도 작업은 유지된다.
- 아트. 카메라를 고정 사선 직교 시점으로 바꿨다. 카메라 쪽 쉼터 벽은 높이를 낮추고 기둥으로 단면을 표시한다. 검문소·수문·시설반 연락소를 추가했다.
- 에셋. 승인 후 Unity Package Manager > My Assets로 HOLLOWS FREE 다운로드 완료. 로컬 라이선스 작업 공간에 구급차·침대·라디오와 재질만 넣고 URP로 연결했다. 다른 작업 공간은 기본 그래픽으로 빌드된다. 받을 수 있는 Unity 계정에서 패키지를 내려받은 후 `python3 last-train/tools/import_hollows.py "/path/to/HOLLOWS.unitypackage"`로 재현한다.
- 검증. HOLLOWS가 포함된 macOS 빌드 99MB, EditMode 21/21 통과. 세 루트의 연락·준비·철수·승리와 튜토리얼, 문턱·골목 왕복의 고정 각도/줌을 검사했다. 캡처는 `*hollows-2026-11-19.png`.
- 다음. 사람 플레이로 세 경로의 획득 시간·위험·준비 시간과 사선 시점의 골목 시인성, 야간 밀집 구역 성능을 비교한다.

### 2026-11-19 막차까지 이동 카메라

- 완료. 차폐 판정으로 78–90도를 오가던 카메라를 수직 직교 시점으로 고정했다. 줌을 유지하고 위치만 부드럽게 추적한다. 공격·폭발 시 카메라 위치에 더하던 난수 오프셋도 제거했다.
- 검증. macOS 빌드, EditMode 21/21 및 쉼터 문턱·골목 왕복 519 추적 단계에서 회전·줌·건물 투영 간격 고정 확인.
- 다음. 실제 5일 플레이에서 이동·전투 시인성과 밀집 구역 프레임 타임을 확인한다.

### 2026-11-19 막차까지 첫날 연습, 선택 파밍, 막차 이야기

- 완료. 이동→공구함→등 뒤 암살→무전기 연습과 건너뛰기, Esc 일시 정지·조작법, 암살 가능 표식을 추가했다. 판정은 숙이기, 비경계 상태, 등 뒤, 조준, 거리 및 벽 차폐를 확인한다.
- 파밍. 길게 수색하면 내용만 공개한다. 품목 선택 또는 일괄 담기, 가방 한도에 따른 부분 수령과 재방문을 지원한다. 일괄 수령은 장착 무기 교체가 필요한 물건을 남긴다.
- 이야기. 시설 기사 민서와 박 기관사의 5일 무전, 약국·시장·역 정비 광장 보급, 시장·광장 접근 시 좀비, 역 전력과 생존자 선택에 반응하는 승차 장면.
- 검증. macOS 빌드 성공, EditMode 22/22 통과. 실제 플레이어에서 첫날 이동→개별 수령→재개봉·일괄 수령→암살→무전기까지 자동 검증했다. 주요 화면은 `*playtest-2026-11-19.png`.
- 다음. 사람의 첫 진입과 완주를 관찰하고, 난이도·가방 무게·접근성·성능을 검증한다.

### 2026-11-19 막차까지 생존자와 가방

- 완료. 2일차 물자 요청과 4일차 드문 구조 사건을 추가했다. 말을 걸어 도움을 주면 퓨즈·음식 등 생존과 탈출에 쓰는 물자를 받는다. 구조에는 시간과 NPC 체력이 있으며 좀비 사망은 안정 ID로 추적한다.
- 가방. 소모품에 8kg 제한과 개별 무게를 적용했다. 넘치는 전리품은 수색 장소에 남고, Tab 가방에서 X로 내려놓은 꾸러미는 다시 챙길 수 있다. 역 거래는 교환 후 무게가 한도를 넘으면 취소한다.
- 검증. macOS 빌드 성공, EditMode 17/17 통과. 부탁과 보상, 구조와 보상, 가방과 초과분 장면을 캡처했다.
- 다음. 5일 연속 플레이에서 사건 빈도, 구조 제한 시간, 가방 무게가 파밍과 문 수리 선택에 미치는 영향을 확인한다.

### 2026-11-19 막차까지 넓은 골목과 가변 카메라

- 완료. 건물과 간판을 숨기던 방식을 직교 카메라로 교체했다. 평소 78도에서 내려다보고 근접 전투 범위가 가려질 때만 최대 90도까지 올라간다.
- 구역. 맵 경계를 100×100m에서 112×112m로, 블록 간격을 12m에서 14m로 넓혔다. 세운 골목시장은 통로 양편에 좌판 4개를 배치하고 남쪽 좌판 2개에서 물자를 찾을 수 있도록 했다.
- 검증. macOS 빌드 성공 및 EditMode 15/15 통과. 같은 골목의 기존 카메라와 새 카메라, 시장·야간·역 화면을 `*wide-map-2026-11-19.png`에 캡처했다.
- 다음. 연속 모퉁이 이동에서 카메라 각도 변화와 조준 감각, 확대된 맵의 5일간 이동·수색 시간을 확인한다.

### 2026-11-19 막차까지 골목 시야와 실루엣

- 완료. 카메라가 플레이어와 근접 전투 반경을 가리는 건물·간판을 잠시 숨기고 낮은 기단을 표시한다. 차폐 검사와 건물 충돌 경계는 분리했다.
- 화면. 가로변 건물 높이를 낮추고 시장 마당, 역 앞 정비 광장과 횡단보도를 배치했다. 기존 생존자 리그에 구조 배낭·반사띠와 주황 헬멧을 적용했다.
- 검증. Unity macOS 빌드 성공, EditMode 15/15 통과. 동일 위치 차폐 전후와 시장·야간 쉼터를 `*camera-pass-2026-11-19.png`에 캡처했다.
- 다음. 플레이 중 연속으로 모퉁이를 도는 경로와 밤 밀집 구역 프레임 타임을 확인한다. 캐릭터 모델과 애니메이션 재작업은 후속 범위.

### 2026-11-19 막차까지 골목 사건과 그래픽

- 완료. 낮 보급품을 회수하거나 G로 밤 유인등 예약. 유인등은 밤 첫 30초 동안 주변 좀비의 목표를 바꾼다. 3일차 밤 쉼터 정전은 실내 배전함에서 고철 2개로 복구할 수 있다.
- 화면. 외벽에 생성한 세라믹 타일을 적용하고, 골목 건물 색과 비 입자, 조작·알림·목표 패널을 손봤다.
- 검증. macOS 빌드와 EditMode 13개. 낮·보급·유인등·정전·복구 캡처는 `last-train/production/qa/evidence/`의 `*street*` 파일.
- 다음. 5일 연속 플레이에서 유인등 없이 버티는 난이도와 고철 경제, 밤 시인성 및 밀집 구간 성능을 확인한다. 건물·캐릭터 아트 마감은 후속 범위.

### 2026-11-19 막차까지 그래픽 개편

- 완료. Unity에서 서울 골목 노면과 보도, 차선, 불규칙한 물웅덩이, 한글 간판, 쉼터 입구·실내, 역 계단·바닥과 야간 조명을 갱신했다. 아스팔트는 ImageGen으로 만들고 보도 텍스처는 이를 가공했다. 건물과 캐릭터는 기존 모델을 사용했다.
- 지금 하던 일. Unity macOS 빌드 성공, EditMode 13/13 통과, 낮·밤·역 화면 검토 완료. 캡처는 `last-train/production/qa/evidence/*-art-pass-2026-11-19.png`.
- 다음 할 일. 실제 5일 플레이에서 밤 시인성과 밀집 구간 성능을 확인하고, 건물·캐릭터 아트와 HUD를 마감한다.


### 2026-11-19 슬롯 던전 콘텐츠·디자인

- 6차: 대사·이벤트 글 다시 씀, README(게임·루트) 정리, GIF 7개(tools/make_gifs.sh). 커밋 안 함.

- 5차: 동료 gambler·skeleton, 심볼 banner·twin, 유물 4개, 도감. EditMode 82/82, 봇 보통 43.5%. 커밋 안 함.

- 4차: 이벤트 결과 화면, 이름 릴 던전 → 슬롯 던전(게임 속 릴 → 슬롯). 커밋 안 함.

- 3차 UI/UX: 단축키, 체력바 미리보기, 스킬 준비 표시, 보상·상점 자세히 보기. 커밋 안 함.

- 2차: 적 스킬 heal·jam·sap·reflect, 보스 phase2, 특별 이벤트 7종(거울 파티·보스 그림자 등). EditMode 78/78, 봇 보통 46.1%. 커밋 안 함.

- 리서치(LBaL·Dicey Dungeons·해금 설계) → 적 행동 강탈·봉인, 장별 새 적 2종·정예 2종(tint), 이벤트 3종(trim), 동료4·유물4 해금, 지도 바닥·칸 색 개편, 정예 확대. EditMode 72/72, macOS 빌드, 봇 승률 거의 그대로(보통 45.7%). 커밋 안 함(HANDOFF의 worktree 절차로 커밋).

### 2026-11-18 슬롯 던전 인계
### 2026-11-18 릴 던전 인계

- 전투 HUD와 이벤트 선택 화면을 개편했다. 턴 결과에는 방어 후 받는 피해가 보이고, 릴에는 현재 심볼 이름이 표시된다. Kenney 픽셀 UI 버튼을 타이틀·전투에 사용하고 실제 전투를 README용 GIF로 캡처했다.

- 동료 기본 릴에서 해골을 제거하고 기사의 받아치기와 엘프의 현상금 검을 추가했다. 2장 해골 열쇠는 정화할 때까지 남는 해골을 대가로 유물을 준다. macOS 빌드 성공, EditMode 67/67 통과, 난이도별 봇 재측정. 다음 플레이테스트에서는 초반 공격 빈도와 이벤트의 저주 선택을 확인한다.

- 지도 화면을 확대하고 현재 위치, 지나온 경로, 다음에 갈 수 있는 칸을 표시했다. 16:9 입구와 16:10 이동 후 화면 검증, macOS 빌드 및 EditMode 65/65 통과.

- 보스 전투 개편. 오우거의 분노 후 강타, 거대 좀비의 연속 흡혈, 대악마의 저주·방어 후 강공격을 macOS 빌드에서 확인했다. EditMode 65/65 통과, 난이도별 봇 2,000판 결과는 README에 갱신했다. 다음은 첫 판 플레이테스트.

- PR #20 Godot 통합 테스트는 종료 전에 씬 해제를 기다리도록 수정했다. Godot 4.7.2 로컬 ENet 테스트와 headless 시작이 통과했다. 기존 RID/ObjectDB 경고는 출력됐다.

- 1장 맵 영입에 대표 심볼 1칸 보너스를 추가했다. 새 왕실 장부 사건은 정예 승리 시 보유 심볼 강화 보상을 주며, 승리 보상과 전투 중 저장 복원을 테스트했다. macOS 빌드 성공, EditMode 65/65 통과. 다음 콘텐츠는 보스별 전투 패턴 차별화.

- 콘텐츠 확장. 세 장의 단서와 결말 재작성, 동료별 합류·보스 반응, 장별 패배 대사, 이벤트 3곳 문구 수정 완료. 다음 콘텐츠 작업은 첫 장의 영입·강화 선택 연결. 리서치와 검증 기준은 `reel-dungeon/production/design/content-direction.md`.

- 완료. Unity 6000.6.9f1 macOS 빌드의 3장 보스전에서 체력바 아래 독 수치를 확인하고, 일반 피해 숫자와 겹치는 독 피해 팝업을 보스 오른쪽 아래로 옮겼다. 전후 캡처는 `reel-dungeon/production/qa/evidence/poison-*-2026-11-18.png`.
- 검증. macOS 빌드 성공, EditMode 62/62 통과. 변경은 작업 폴더 `GG/reel-dungeon`에 있다.
- 다음. 사람이 일반 난이도로 한 판 끝까지 플레이하면서 1장 체감 난이도 확인. Windows 빌드는 Unity Hub에서 Windows Build Support (Mono)를 설치한 뒤 진행.

[Free-form. Decisions and their rationale, what was tried and rejected, context
that would be expensive to reconstruct. Not machine-read — write for the person
or agent picking this up cold.]

<!--
  FIELD NOTES

  STATUS block
    Drives the status-line breadcrumb (`Combat System > Melee > Hitboxes`), and
    only at Production / Polish / Release stage. All three fields are optional —
    leave a field blank when it does not apply, and blank all three when there is
    no active focus. Do not delete the markers themselves — `statusline.sh`
    expects them to exist.

  CHECKPOINT block
    The recovery payload. `session-start.sh` shows it when a previous session
    left state, and again after a compaction (SessionStart runs then too).
    It reads THIS region and nothing else, which is what keeps the cost fixed
    no matter how long the narrative gets.

    Keep `Branch` current. This file is not committed, so it stays put when you
    switch branches; session-start.sh compares `Branch` with the branch you are
    on and adds one line when they differ.

    Write it so a cold reader can resume without reading anything else. "Current
    task: fixing the thing" is useless; "Current task: adding wall-jump to
    player_movement.gd" is not.
-->

## 2026-11-02 이미지 원본 준비

내장 image_gen으로 PROMPTS.md 묶음 1·2의 PNG 27장을 art/raw/에 저장. generation-manifest.json에 프롬프트·크기 기록. 게임 코드는 수정하지 않았으며 원본 가공/런타임 연결은 다음 작업. ROADMAP.md는 현재 워크스페이스에 없어 PROMPTS.md 체크리스트로 완료 상태 기록.

### 잠수부 치비 스타일 시안
사용자 신규 프롬프트로 내장 image_gen을 사용하여 sunken-city/art/raw/sprites/diver_chibi.png 생성. 주황 잠수복·노랑 공기통·큰 황동 투구·단순한 큰 픽셀 스타일. 기존 diver.png 보존, 게임 적용 전 시안.

### 치비 잠수부 v2
사용자 수정 반영: 큰 반짝이는 눈·볼터치·웃는 입, 투구보다 작은 콩 모양 몸·짧은 팔다리, 옆창 제거·볼트 단순화·거품 2개. art/raw/sprites/diver_chibi_v2.png 저장, 내장 image_gen 편집, RGBA 투명도 확인. 기존 시안 보존, 런타임 미적용.

## 2026-11-02 묶음 R 완료

PROMPTS.md 지금 할 일 10장 생성·저장, 기준 diver.png 참조. 물방울 전신/초상화 외형 보정. 10장 모두 정사각 RGBA·투명 배경 확인. PROMPTS.md 체크 및 generation-manifest.json 기록 완료. 런타임 가공/연결은 문서 지시대로 Claude 후속 작업. ROADMAP.md는 워크스페이스에 없어 PROMPTS.md 생성 상태로 완료 기록.

### 방향 수정 — Dave the Diver 참고
사용자가 묶음 R의 과한 치비 비율에 수정 요청. 성인 체형·절제된 표정·생활감 있는 장비 방향의 잠수부 시안 art/raw/sprites/diver_grounded_v1.png 생성. 아직 기준 그림/기존 묶음은 교체하지 않음. 사용자 시안 피드백 후 캐릭터 전체 방향 및 PROMPTS.md 갱신 필요.

### 독자적 귀여운 도트 방향
사용자가 Dave와 너무 닮은 체형/얼굴을 거절. 수염 없는 작은 얼굴·둥근 사각 투구·옆 조명·청록 소매/오리발·크림 공기통·주황 멜빵복의 독자적 시안 art/raw/sprites/diver_original_v1.png 생성. 기준 diver.png는 보존. 다음 작업은 사용자 피드백 반영.

### 독자적 도트 스타일 전체 교체 완료
승인 시안 diver_original_v1.png를 diver.png로 반영. 서 있는 잠수부·괴물3·주민2·초상화2·아이템2 총 10장 재생성. 초상화는 주민 전신 추가 참조. 11장 투명 PNG 검증, PROMPTS.md 및 manifest 갱신. 런타임 가공은 후속 작업.

## 2026-11-02 묶음 3D-1 콘셉트 완료

PROMPTS_3D.md의 지금 할 일에 따라 내장 image_gen 사용. S-0 스타일 시트 1장과 이를 참조한 에셋 콘셉트 22장을 raw3d/concepts/에 저장. 23 PNG 디코딩 검증, 전체 콘셉트 축소 미리보기 및 개별 표본·난파선 시각 검토 완료. 체크리스트와 manifest bundle_3d 갱신. 이미지 크레딧 미제공, AI 3D 미실행/플랜 없음/크레딧 0으로 명시. GLB 생성·게임 연결은 미완료이며 문서 대체 절차대로 사람에게 인계. ROADMAP.md는 워크스페이스에 없어 PROMPTS_3D.md에 체크 기록.

Godot 최초 샌드박스 실행은 user 로그 경로·macOS 인증서 접근 ERROR. /tmp 로그 지정 후 허용된 샌드박스 밖 실행으로 exit 0, ERROR 없음. 게임 코드 변경 없음.

이전 게임 작업 이어받기: 넓은 맵·물고기·해초·빛줄기·하늘 반영 상태였음. 마을 물 빠진 화면의 회색 배경·건물 실루엣 개선, 빌딩 외벽을 건물답게 만드는 작업과 옥상↔바닥 도보 동선 플레이 확인이 남아 있음. 기존 진행 중 코드 변경은 보존.

## 2026-11-09 Codex 개발 이어받기

사용자 요청으로 Claude Code Game Studios 저장소의 최신 개발을 이어받음. 루트 ROADMAP.md가 없어 현재 확인한 범위의 진행표를 새로 작성. root README와 sunken-city README의 기존 미커밋 수정은 보존. 최신 체크포인트와 git 이력에 따라 haunted-movers를 작업 대상으로 선택.

기절 직후 지연된 hold 요청이 강제 놓기를 취소하는 문제를 차단. 다음 날 로컬 플레이어 복귀에서 recover()로 기절 타이머·들기 상태 초기화. host와 client 모두 기존 _respawn_local 경로 사용. 회귀 테스트는 기절 중 재잡기 거부, 다음 날 즉시 회복, 회복 후 잡기 허용을 검사. 사람 플레이테스트·아트 사인오프·머지·릴리스 완료를 주장하지 않음.

최종 검증: 전체 smoke PASS(exit 0), 60프레임 시작 exit 0, 두 실행 모두 ERROR 없음. git diff --check 통과. 상세 결과: haunted-movers/production/qa/evidence/stun-recovery-2026-11-09.md.

### 온라인 참가 복구와 실제 네트워크 검증

연결 완료 전 메뉴 유지, 연결 취소, @export 10초 타임아웃, 실패 안내 및 재시도/호스트 전환 구현. 시도 번호로 이전 타임아웃이 새 연결을 취소하지 않게 처리. 메뉴가 없는 headless 실행에서는 취소 버튼을 생성하지 않아 새 리소스 누수 방지.

실제 두 Godot 프로세스 ENet 접속·플레이어 2명 복제·2일차 팀 돈/가구 9개 동기화·클라이언트 로컬 기절 회복 PASS. tests/integration/run_connection_checks.py로 재현 가능하며 smoke.yml에 haunted-movers 전용 단계 추가(원격 CI 미실행). 연결 중/실패 메뉴 PNG를 QA evidence에 저장하고 시각 확인. 전체 기존 smoke PASS, 기본 60프레임 ERROR 없음. 기존 RID/ObjectDB 경고는 남음.

## 2026-11-19 막차까지 캐릭터와 공격 방향

공격 시 판정 방향과 무기를 기록하고 모션이 재생되는 동안 유지. 모델의 정면과 공격 판정을 맞추고 기존 Quaternius 애니메이션 위에 성인 체형과 낮은 채도의 생존자·좀비 외형을 적용. macOS 빌드, Unity EditMode, 동서남북·반대 조준과 첫날 수색·암살·무전 자동 시나리오 통과.

세 팩을 Unity 캐시에 다운로드하고 선택 에셋을 가져옴. Free Scavenger는 기존 이동·무기 모션에 연결, Pxltiger 좀비 2종은 공용 Humanoid Avatar와 전용 모션으로 교체. ZENRIN Japanese Otaku City는 기존 맵 건물의 상층 외벽에 적용. 원본 도심 씬은 사용하지 않음. Unity 캐시에서 재설치: `python3 last-train/tools/import_store_art.py`.

macOS 빌드에서 세 에셋 로딩, 사방 공격·반대 조준, 첫날 수색·암살·무전, 건물 가림 캡처를 확인. EditMode 21/21.

다음 할 일: 야간 좀비 밀집 시 프레임 타임 및 전체 탈출 동선 수동 플레이테스트.

## 2026-11-19 막차까지 이동 모션

Free Scavenger에 프로젝트 내 Humanoid 정지·걷기·달리기·잠행 클립을 연결. 충돌 처리 이후 실제 변위로 보폭과 재생 속도를 고르고 상하체를 나눠 이동에 공격·피격 동작을 섞음. 평상시 팔의 높은 격투 자세를 내려 자연스럽게 흔들도록 수정. Pxltiger 모션을 점진적으로 전환하고 실제 공격 예고와 연결.

macOS 빌드와 이동 영상(정지, 이동, 방향 전환, 잠행, 질주), 사방 공격 방향, 초반 튜토리얼, EditMode 21/21 확인. 다음은 야간 밀집 전투 프레임 타임과 실내 경계에서 이동 모션 수동 확인.

## 2026-11-19 막차까지 WASD 및 근접 공격

플레이어 입력에 벽면까지 이동량을 제한하는 충돌 처리 적용. 코너에서 벽 반대편으로 밀려나거나 클릭 순간 전진하던 동작을 제거하고 카메라를 플레이어 위치만 따라가게 변경. 근접 공격은 110ms 준비 후 첫 조준 방향에 타격하고, 공격 중 이동을 완만하게 감속.

다음 할 일: 실제 키보드 조작감과 야간 밀집 전투를 수동 플레이테스트.

실행 중이던 macOS 앱에서 A 입력을 길게 누르고 피격을 확인. 피격·타격의 전역 시간 감속을 제거하고 Humanoid 모델의 기준 위치를 매 프레임 복원. 새 macOS 빌드에서 연속 이동 프레임별 최대 이동 거리와 시간 배율, 편의점 충돌 및 피격 시나리오 확인. 기존 앱 인스턴스는 새 바이너리로 갱신되지 않으므로 별도 프로세스로 새 빌드 실행.

Humanoid 그래프를 수동 평가해 플레이어 애니메이션과 위치 보정의 순서를 고정. 모델 루트 외에 골반 수평 위치도 이동 좌표에 맞추고 상하 애니메이션은 유지. macOS 실행 빌드에 Input System 키 이벤트를 넣어 W·A·S·D 각각 32프레임과 이동 중 피격을 확인. 표시 골반과 게임 좌표의 수평 오차는 로그 표시 정밀도에서 0.0000m.

플레이어 이동에서 발의 앞으로 뻗는 거리·높이·교차를 제한하고 무릎을 전방으로 굽히도록 보정. 튜토리얼 생략 시에도 쉼터 비상 공구함에 쇠파이프를 배치하고 철물점 무기 공구함에 도끼를 보장. macOS 빌드의 초반 파밍, 철물점 도끼 선택 획득, 이동 프레임 캡처 확인.

### 2026-11-20 막차까지 도시와 실내 탐색 개편

- 지금 하던 일. 시장 뒤 ㄷ자 안뜰·ㄱ자 상가, 도시 보행로, 관리동 1·2층 수색, 문과 계단의 E 출입을 구현했다. 새 보급 구역 세 곳과 감염자·생존자 조우를 추가했다.
- 전투. 질주 게이지, 추격 중 벽 우회, 유형별 공격 예고, 생존자 매복을 도입했다. 근접은 클릭당 한 번에 한 마리가 맞는다.
- 검증. macOS 빌드와 EditMode 27개 통과. 실행 빌드에서 다섯 근접 공격, 네 건물 출입, 층간 이동·수색, 세 방문 분기, 기존 탈출 경로를 확인했다. README에 최신 플레이 GIF와 이미지 7장을 넣었다.
- 다음 할 일. 실제 플레이에서 다층 탐색 안내, 추격과 무리 전투 밸런스를 확인한다.

### 2026-11-24 잔류신호 플레이 미디어

- 지금 하던 일. 해림역 진입과 적재장 거대체 GIF를 최신 씬으로 교체하고, 승강장 방어·화물 연결로 호송 GIF와 새 스크린샷을 프로젝트 및 루트 README에 반영했다.
- 다음 할 일. 확장 임무의 4인 Relay 완주와 실제 플레이 난이도·프레임 타임을 확인한다.
