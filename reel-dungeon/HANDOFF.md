# 슬롯 던전 인계 문서 (Claude Code → Codex)

작성 2026-11-18. 이 파일 하나만 읽으면 이어서 작업할 수 있게 쓴다. 게임 소개·규칙은 `README.md`.

## 1. 지금 상태 한눈에

- 게임: 동료 한 명 = 릴 하나인 슬롯 덱빌딩 던전 로그라이크. Unity 6000.6.9f1, C#. 버전 0.1.0.
- 브랜치 `feat/reel-dungeon`, PR https://github.com/saechimdaeki/GG/pull/19 (열림, CI 없음, 아직 머지 안 함).
- 테스트: EditMode 62개 전부 통과.
- 밸런스 봇(2000판): 보통 37.1% · 어려움1 25.1% · 어려움3 16.3% · 어려움5 3.3%.
- 사람이 처음부터 끝까지 플레이한 적은 아직 없다. 숫자는 전부 봇 측정이다.

들어가 있는 것: 동료 9명(기사·엘프·마법사·드워프·천사·도적·연금술사·성기사·드루이드), 심볼 14종, 스킬 9종, 독, 적 행동 5종(공격·방어·저주·분노·흡혈), 3장 갈림길 지도, 정예·영입·상점·휴식·보물(미믹)·이벤트 10종, 장비 14·유물 12, 심볼 강화·릴 다듬기·상점 갱신, 스토리 대화, 어려움 0~5(클리어하면 다음 단계 해금), 이어하기(자동 저장), 설정, 첫 판 안내, 효과음 19·음악 2, 픽셀 UI·갈무리 폰트.

## 2. 작업 폴더 — 꼭 읽기

폴더가 두 개다.

| 폴더 | 용도 |
|---|---|
| `~/Desktop/GG/reel-dungeon` | **실제 작업·빌드하는 곳.** Unity `Library/` 캐시가 여기 있어 빌드가 빠르다. 이 GG 폴더는 다른 세션이 README 작업 브랜치(`docs/readme-gifs` 등)를 쓰고 있어서 **여기서 브랜치를 바꾸거나 커밋하지 않는다.** |
| `~/Desktop/GG-reel-dungeon` | **커밋 전용 git worktree** (브랜치 `feat/reel-dungeon`). 작업 결과를 rsync로 복사해 와서 여기서 커밋·푸시한다. |

커밋 절차:

```bash
cd ~/Desktop/GG/reel-dungeon
rsync -a --delete --exclude Library --exclude Temp --exclude Logs --exclude UserSettings --exclude Builds --exclude obj --exclude '*.csproj' --exclude '*.sln' ./ ~/Desktop/GG-reel-dungeon/reel-dungeon/
cd ~/Desktop/GG-reel-dungeon && git add -A reel-dungeon && git commit && git push
```

커밋 메시지: 한국어 Conventional Commits (`feat: 슬롯 던전 — …`), 본문 첫 줄에 `설계 기록: reel-dungeon/README.md`.

## 3. 자주 쓰는 명령 (reel-dungeon 폴더에서)

```bash
U="/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity"
S=/tmp/rd   # 아무 임시 폴더
# 테스트 — 결과 파일을 먼저 지울 것 (컴파일 실패 시 이전 결과가 남아 "통과"로 착각함)
rm -f $S/results.xml; "$U" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults $S/results.xml -logFile $S/test.log
grep -E "error CS" $S/test.log; grep -o 'result="[^"]*" total="[^"]*" passed="[^"]*" failed="[^"]*"' $S/results.xml
# 맥 빌드 → Builds/Mac/ReelDungeon.app (실행 파일 이름 "Reel Dungeon")
"$U" -batchmode -quit -projectPath "$PWD" -executeMethod BuildScript.BuildMac -logFile $S/build.log
# 밸런스 봇
"$U" -batchmode -quit -projectPath "$PWD" -executeMethod BalanceSim.Run -simRuns 2000 -simAscension 0 -logFile $S/sim.log; grep "\[sim\]" $S/sim.log
# 스크린샷 (예: 3장 전투, 안내·대사 끔)
"Builds/Mac/ReelDungeon.app/Contents/MacOS/Reel Dungeon" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
  --screenshot "$PWD/production/qa/evidence/이름-날짜.png" --at 1.8 --chapter 3 --node Combat --nostory --notips
# 소리·아이콘 다시 만들기
python3 tools/gen_audio.py; python3 tools/make_icon.py
```

스크린샷 옵션 전체: `--title --chapter N --node <Combat|Elite|Shop|Event|Treasure|Recruit|Rest|Boss> --recruit a,b --equip id,… --relic id,… --charge --autocommit --autoturns N --nostory --notips --skiplines N --pause --settings --unlockall --usesave --continue`. 스크린샷 실행은 `--usesave` 없이는 플레이어 저장을 건드리지 않는다.

## 4. 코드 지도

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Core/Run.cs` | 판 전체 규칙: 지도 이동, 전투(Preview→Commit), 스킬, 독, 적 행동, 보상·상점·이벤트, 어려움. Unity 의존 없음 (`noEngineReferences`) |
| `Assets/Scripts/Core/RunSave.cs` | 저장 형식과 `ToSave()` / `FromSave()` |
| `Assets/Scripts/Core/StageMap.cs` | 지도 생성(3줄×7칸, 보스 직전 휴식 1칸)·저장 복원 |
| `Assets/Scripts/Core/SpinResolver.cs` | 릴 면 → 공격·방어·회복·골드·독 (콤보 1.5배/2배) |
| `Assets/Scripts/Core/Config.cs` | `tuning.json` 형식 (주석에 스킬·스탯·이벤트 종류 목록) |
| `Assets/Scripts/Game/GameView.cs` | 화면 전부 (IMGUI, 1280×720 기준 균일 확대), 연출·소리·저장·안내·설정 |
| `Assets/Scripts/Game/UiSkin.cs` | 코드로 만든 픽셀 9-slice 버튼·패널 |
| `Assets/Scripts/Game/Story.cs` + `Resources/story.json` | 대사 |
| `Assets/Scripts/Game/ScreenshotOnArg.cs` | 스크린샷 도구 (실제 시간 기준 대기) |
| `Assets/Editor/BuildScript.cs` | 빌드 + 앱 이름·버전·아이콘 |
| `Assets/Editor/BalanceSim.cs` | 자동 플레이 봇 (관찰만 출력, 판단은 사람이) |
| `Assets/Resources/tuning.json` | **모든 수치·콘텐츠** (심볼·동료·장비·유물·적·이벤트·가격·지도) |
| `Assets/Tests/EditMode/RunTests.cs` | 테스트 62개. 테스트용 설정은 파일 안 `Config(...)` 팩토리 |

새 콘텐츠는 대부분 `tuning.json`만 고치면 된다. 새 **종류**(새 스킬 id, 새 스탯, 새 이벤트 효과)는 `Run.cs`의 해당 switch에 추가하고 `Config.cs` 주석 목록도 갱신.

## 5. 지켜야 할 것

- 사용자에게는 **항상 한국어**로 답한다.
- 수치는 코드에 박지 말고 `tuning.json`. 밸런스를 바꾸면 봇으로 다시 재고 README "밸런스" 숫자를 갱신.
- 규칙(Core)을 바꾸면 테스트를 먼저/같이 추가. 화면을 바꾸면 스크린샷을 `production/qa/evidence/`에 남기고 직접 열어 확인.
- 아트는 0x72 DungeonTileset II(CC0)만 쓴다. 출처는 `Assets/Resources/Art/0x72/LICENSE.txt` (itch.io가 이 맥에서 막혀 GitHub 미러의 고정 커밋에서 받음). 글꼴은 갈무리(OFL).
- 맥에는 `timeout` 명령이 없다. 셸에서 변수 들어간 `rm -rf`는 막힌다 — 절대경로로.
- 실시간 연출 중(`busy`)에는 `run` 상태가 이미 다음 턴이다. 화면은 `shown*` 값으로 그린다.

## 6. 남은 일 (우선순위 순)

2026-11-19 대사 전면 수정(story.json, 이벤트 글 15개 — 사용자가 "어색하다"고 함: 설명 없는 암시 대신 상황을 먼저 말하고, 동료마다 말투를 나눔). README 새로 정리, GIF 7개는 `bash tools/make_gifs.sh`(빌드 후)로 다시 찍는다.

2026-11-19 콘텐츠 5차: 심볼 `neighborBuff`·`twinBonus`(파티 순서 = 슬롯 순서, 이웃은 좌우), 스킬 allIn·boneBurst, 유물 stat lifesteal·curseDamage·comboBonus·rerollGold, 동료 gambler(sprite goblin)·skeleton(sprite skelet, 대표 심볼이 해골 — 기본 슬롯 해골 테스트에서 제외), 도감(PlayerPrefs "seenEnemies"). 달리기 애니메이션이 없는 스프라이트는 idle로 대체. EditMode 82/82, 봇 보통 43.5%.

2026-11-19 이름 변경: 화면·대사·tuning의 "릴" → "슬롯"(`돌릴` 같은 동사는 그대로). 코드 식별자(Reels, ReelDungeon)·폴더·productName "Reel Dungeon"은 저장 경로 때문에 유지. 이벤트 결과 화면(`ShowEventResult`, 스크린샷 `--pick N`).

2026-11-19 UI/UX: `HandleKey`(단축키), `Ghost`(체력바 미리보기), `DrawRewardInfo`(보상·상점 자세히 보기, `focusReward`), 스킬 버튼 진행 줄. 화면만 바뀌고 규칙은 그대로.

2026-11-19 적 스킬(heal·jam·sap·reflect), 보스 `phase2`/`phase2Name`(절반 HP에서 행동 목록 교체), 특별 이벤트 7종(새 효과 mirror·shadow·coinflip·hpFull·goldAll·transform·fuse·relicId, 옵션 `victoryRelic`). 이벤트가 즉석에서 만든 적(거울·그림자)은 저장에 `enemyDef`로 통째로 들어간다. EditMode 78/78.

2026-11-19 콘텐츠·디자인 추가: 적 행동 강탈(steal)·봉인(seal), 장마다 적 2종, 정예 2종(`tint`로 같은 그림 다른 색), 이벤트 3종(새 효과 `trim`), 해금(`unlocks`, PlayerPrefs "unlocks", 스크린샷 실행은 저장 안 함), 지도 장별 바닥·칸 색·그림자·보스 빛, 정예 1.25배. EditMode 72/72, macOS 빌드 성공. 증거 `production/qa/evidence/*-2026-11-19.png`. 리서치와 다음 후보는 [콘텐츠 방향](production/design/content-direction.md) 끝.

1장 콘텐츠 추가 완료. [영입 대표 심볼 보너스](production/qa/evidence/recruit-signature-bonus-2026-11-18.png)와 [왕실 장부 사건](production/qa/evidence/event-royal-ledger-2026-11-18.png)을 macOS 빌드에서 확인했다. 장부 정예전을 이어한 뒤 보유 심볼 중 하나를 강화하는 흐름을 포함해 EditMode 65개 통과. 난이도별 2,000판 재측정 결과는 README의 밸런스 항목에 갱신했다.

2026-11-18 콘텐츠 확장. 왕실 수레에서 감옥 명부와 대악마까지 이어지는 이야기를 추가했다. 새 동료 4명의 합류 대사를 채우고, 각 보스 앞에서 파티에 속한 동료의 반응 한 줄을 선택한다. 상인·예배당·시제품 이벤트의 텍스트에도 같은 세계의 단서를 넣었다. 다음 콘텐츠 작업과 플레이테스트 기준은 [콘텐츠 방향](production/design/content-direction.md)에 있다.

1. **독 수치 화면 확인 완료 (2026-11-18)** — macOS 3장 보스전에서 [체력바 아래 독 1](production/qa/evidence/poison-status-2026-11-18.png)을 확인했다. [일반 피해와 겹치던 독 피해 팝업](production/qa/evidence/poison-popup-before-2026-11-18.png)을 보스 오른쪽 아래로 옮겼고, [새 빌드에서 일반 피해 -2와 독 피해 -1이 따로 읽히는 화면](production/qa/evidence/poison-popup-after-2026-11-18.png)을 확인했다. macOS 빌드 성공, EditMode 62/62 통과.
2. **사람이 한 판 끝까지** — 특히 1장 사망 비중이 높다(봇: 1장 21%, 오우거·오크 전사·미믹). 체감 난이도 확인 후 조정.
3. **어려움 5** — 봇 3.3%. 사람 기준으로 너무 어려운지 확인.
4. **윈도우 빌드** — Unity Hub에서 이 에디터에 Windows Build Support (Mono) 모듈 추가(사용자가 직접) → `BuildScript`에 `BuildWindows` 추가.
5. **아이콘 겹침** — 독사의 송곳니(유물)=단검(심볼), 행운의 동전=코인 등 같은 그림을 쓰는 것들이 있다. 구분되게.
6. **메타 진행 (선택)** — 판을 거듭하며 동료·유물이 해금되는 구조. 지금은 어려움 단계만 해금된다.
7. **소리 다듬기** — 합성 효과음이 단순하다. 장별 음악, 보스별 음악.
8. **장기: IMGUI → UI Toolkit/uGUI** — 지금 화면은 `GameView.cs` 하나(약 1500줄)에 IMGUI로 다 있다. 출시 전 정리하면 좋다.
9. **PR #19 정리** — 리뷰 후 머지. 머지되면 worktree(`~/Desktop/GG-reel-dungeon`)는 `git worktree remove`.

## 7. 최근 판단 기록 (왜 이렇게 됐나)

- 새 동료가 약했던 원인은 화력이 아니라 **회복** 부족이었다 (파티 HP 공유라 긴 전투에서 회복이 승패를 가름). 그래서 새 심볼들에 회복을 섞었다. 측정: 기존 5인 고정 76.6%, 새 4인+기사 고정 82.8%.
- 일반 전투 긴장감: 적 행동 5종 + 보스 직전 휴식 1칸으로, 보스 도착 HP가 99%→80%대로 내려옴.
- 골드: 강화·다듬기·갱신 추가로 남는 골드 145→86.
