# 잔류신호 작업 인계

- 1차 개발판 범위는 해림역 첫 임무 〈열한 번째 이름〉이다. Unity 6000.6.9f1에서 `Assets/Scenes/Platform03.unity`를 실행한다.
- 흐름은 광장 → 대합실 → 직원 또는 침수 통로 → 승강장 방어 → 구조 → 정비동 펌프 → 적재장 거대체 → 탈출이다.
- 갱신한 씬은 `ResidualSignal.StationSceneBuilder.Build`로 생성한다. 에디터에서 `ResidualSignal.StationPlayCheck.Run`에 `-solo -stationCapture /tmp/residual-check.png`를 넘기면 임무를 검사한다. `-triageLeft -drainRoute`는 반대 경로를 선택한다. `-botCheck`, `-callerCheck`, `-bossModelCheck`로 개별 검사할 수 있다.
- macOS 실행, 두 분기 완주, 분대 지시, 울음꾼 호출 차단, 거대체 동작을 확인했다. 확장 임무 4인 Relay 완주와 사람 플레이에 따른 난이도 조정이 다음 작업이다.
- 보스 개선 브랜치에서 적재장 거대체 체력을 솔로 기준 850 → 2,400으로 조정하고, 추가 플레이어당 400을 더한다. 양손으로 콘크리트를 들어 던지며 조준 고정 후 옆으로 피하거나 엄폐할 수 있다. 돌진 종료에는 반격할 시간이 있다.
- Unity 검사 `-bossThrowCheck`, `-bossDodgeCheck`, `-bossCoverCheck`, `-bossModelCheck` 통과. 다음은 4인 Relay에서 투척 동기화와 실제 분대 화력으로 생존 시간을 확인하는 것이다.
- 캠페인 개편: 승강장 방어는 준비 후 은폐 통로에서 무리가 진입하고, 후속 파동은 반대편으로 나뉜다. 정비동과 적재장은 분대가 이미 밀리고 있으면 추가 증원을 늦춘다. AI는 붙잡힌 동료를 공격하는 특수 감염체를 우선 사격한다.
- 광장 뒤 주거 건물에 창틀과 발코니를 만들고, 광장·대합실·승강장·정비동·적재장에 구역별 실내등과 반사광을 배치했다. 씬만 갱신하려면 `ResidualSignal.StationSceneBuilder.UpdateNightArt`를 실행한다.
- `-liveDefenseCheck`에서 준비 종료 후 은폐 증원 5체 진입 확인. 이번 변경에서 분대·양쪽 경로 완주·보스 투척 회귀도 확인했다. 다음은 실제 플레이어의 전투 밀도 및 4인 Relay 장시간 플레이 검증이다.
- 적재장 재조정: 거대체는 솔로 체력 4,500, 추가 플레이어당 750. 가림막 옆으로 직접 접근하며 적재장 안에서 싸운다. 처음 8초에는 첫 공격을 실행할 때까지 최대 체력의 절반 아래로 떨어지지 않는다. AI 동료의 보스 피해는 일반 감염체 대비 45%다.
- `-yardBossCheck`는 정비동 펌프부터 실제 적재장 보스를 활성화하고 AI 셋과 카빈으로 싸워 투척과 공격 횟수, 생존 시간을 확인한다.
- 실제 펌프 경로와 적재장 자동 플레이에서 카빈 43발과 AI 동료 셋의 교전 중 투척 포함 공격 5회, 처치까지 11.8초 확인. 온라인 Relay 실기는 미검증.
- 승강장 증원은 앞뒤 진입 요청을 독립적으로 처리하고 은폐가 확인된 위치에서 묶음으로 투입한다. 실제 씬의 첫 방어 테스트에서 17체가 진입했다. 우클릭으로 감염체 세 마리를 밀치고 이어서 사격하는 테스트가 통과했다.
- 보스는 적재장 전투 시작부터 30.5초 동안 받는 누적 피해가 제한된다. `-yardBossCheck`는 네 명의 AK 헤드샷 연사를 장전 간격 없이 주입하면서 실제 적재장 투척과 공격 횟수를 검사한다. Relay 4명 실기와는 별도다.
- `-bossImpactCheck`: 실제 근접 타격 34 피해, 수평 4.26m, 상승 0.94m, 착지 후 사격 복귀 확인. AI 동료는 이동 경로를 유지하며 밀려나는 자세와 착지 동작을 표시한다.
- `-escortCoverCheck`: 호송 대상 근처에 시야가 열린 감염체가 있으면 대기하고, 제거하면 다시 이동한다.
- Claude가 시작한 사격 손맛 1단계 연결을 마무리했다. 반동과 총구 조명, 탄흔·불꽃·먼지, 짧은 예광탄, 헤드샷과 처치 피드백을 적용했다. `-shotFeelCheck`에서 벽 탄흔 7개, 감염체 피해, FOV 반동과 예광탄 생성 확인. `-shoveCheck`에서 3체 밀치고 후속 사격 통과.
- 1인칭 그립과 정조준 적용. 왼쪽 Option(Alt) 또는 휠 버튼을 누르면 중앙 조준 자세로 전환된다. 우클릭 밀치기는 유지했다. 색보정과 손전등 빔을 조정하고, 역·적재장에 타일 보수 패널과 물기·혈흔·설비·잔해를 넣었다. 씬의 해당 부분만 갱신하려면 `ResidualSignal.StationSceneBuilder.UpdateMaterialAndGrip`을 실행한다.
- 다음은 사람 플레이로 전체 임무의 화면 가독성과 기존 사람 모델의 동작, 4인 온라인 프레임 타임을 확인하는 것이다.
- 증원은 불투명한 벽 뒤에서만 생성되며, 경로 초반 0.65m도 가려져야 한다. 선택된 NavMesh 위치의 높이를 유지하고 0.3초 뒤 이동을 시작한다. 은폐 진입 7.4m 이동, 라이브 방어 증원 14체 확인. 원격 4인 시점 검증은 남았다.
- 같은 좌표에서 이전 커밋과 새 빌드의 적재장 화면을 촬영했다. 앞으로 실사감 개선을 위해 장갑, 캐릭터 모델과 큰 벽체의 형상부터 교체를 검토한다.
- 인물 아트 패스: 실제 가죽·천 노멀, 장갑 손목 마감과 왼손 그립, 구조대 무전기 모델, 거대체 피부 광택을 수정했다. 특수 감염체 다섯 유형은 서로 다른 몸체를 사용한다. 일반 감염체의 근접 모션은 몸통 회전과 좌우 스윙을 더했다.
- 특수 감염체 4종에 CC BY 4.0 GLB 메시와 리그·클립을 교체 적용했다. glTFast의 여러 재질 묶음 임포트 충돌을 피하기 위해 원본 피부 가중치와 애니메이션을 유지하며 재질별 메시로 분리했다. `ResidualSignal.StationSceneBuilder.InstallAlternateInfected`로 프리팹과 네트워크 목록을 다시 생성할 수 있다.
- 적재장 이후 화물 연결로 쪽으로 54m 전진하며, 배전함으로 차단문을 열고 반대편 신호함에서 서비스문 잠금을 해제한 뒤 서이경과 탈출한다. `ResidualSignal.StationSceneBuilder.InstallFreightPassage`로 씬과 NavMesh를 갱신한다.
- 거대체 보호는 첫 공격 전에 줄지 않는다. 이후 적재장 안에 표적이 있고 최근 피해를 받은 구간에서만 30.5초 보호가 소모된다.
- 아군 충돌쌍만 제외했다. `-squadCollisionCheck`에서 아군 둘을 지나 3.20m 이동하고 감염체와의 충돌은 유지됐다. 첫 방어부터 특수 감염체를 투입하고 대기 중인 특수 감염체의 마지막 증원 몫을 유지한다. `-liveDefenseCheck`에서 은폐 증원 14체와 특수 감염체 등장 확인.

## 2026-11-24 플레이 미디어

- 지금 하던 일. 최신 Mixamo 모델과 해림역 씬으로 README의 진입·거대체 GIF와 대표 스크린샷을 교체하고, 승강장 방어와 화물 연결로 호송을 별도 GIF로 기록했다.
- 다시 촬영할 때는 `ResidualSignal.StationPlayCheck.Run`에 `-solo -stationCapture <비어 있는 PNG 경로> -demoFrames <프레임 폴더>`를 넘긴다. 진입은 `-botCheck`, 승강장은 `-liveDefenseCheck`, 적재장은 `-yardBossCheck`, 호송은 `-freightDemo -triageLeft -drainRoute`를 사용한다.
- 검증. 분대, 승강장 증원, 적재장 보스 검사와 침수 통로 경유 호송 촬영 통과. 일반 임무 자동 검사는 첫 사격의 피격 반응 확인에서 멈췄다. 호송 촬영 모드는 해당 사격 검사를 건너뛰고 탈출 완료까지 확인한다.
- 다음 할 일. 확장 임무의 4인 Relay 완주, 실플레이 난이도와 프레임 타임을 확인한다.

## 2026-11-24 미션·난이도·L4D2 화면

- 미션 선택(열한 번째 이름 / 정비동 돌파 / 승강장 사수)과 4단계 난이도(`Assets/Scripts/Difficulty.cs`, `StationMission.cs`). 기본 난이도는 보통. 배수는 Difficulty.cs 표에서 조정.
- 화면 등급: `ResidualSignal.StationSceneBuilder.ApplyLook`으로 채도 -18, 대비 32, 그레인 0.32, 비네트 0.38, 안개 62m/실내 21m 적용.
- 검사: `-solo -mission survival|annex -difficulty N -missionCheck [-keepAlive]`. 생존 전문가 17초 만에 쓰러짐(감염체 42), 보통+keepAlive 45초 거대체 등장, 정비동 시작 위치·단계 통과, `-botCheck` 회귀 통과. 증거: production/qa/evidence/*-2026-11-24.png.
- 다음: 사람이 직접 난이도별로 플레이해 배수 조정. 커밋 안 됨.
- 추가: 4번째 미션 〈연료 수집〉(`StationScavenge.cs`, L4D2 스캐빈지). 연료통 8개, 90초+통당 20초, 운반 중 사격 불가. 직원 통로 셔터는 즉시 열림. 손전등 그림자 켬.
- 기존 스크립트 검사는 `-difficulty`가 없으면 쉬움(원래 조정값)으로 고정된다. 보통에서는 감염체 체력이 올라 `-yardBossCheck`의 첫 사격 처치 조건이 맞지 않는다.
- 검사: 연료 수집 고급 60초 8/8 완료(감염체 최대 32), `-botCheck`·`-liveDefenseCheck`·`-yardBossCheck`(쉬움) 통과.
- 추가: `StationHardship.cs` — 흑백 상태(쓰러짐 2회 후 회색 화면, 3회째 사망, 구급상자로 초기화), 오인사격(보통 10/고급 30/전문가 60%), 생존·연료 미션 보급함 25초 재충전. `-hardshipCheck`(survival, 전문가) 통과. 브랜치 feat/residual-signal-missions.
- 2026-11-24 마무리: 흐느끼는 자(L4D2 위치형, `StationInfected.Weeper.cs`; 총소리 무시·근접/손전등/피격에 분노·경직 없음·강타 58), 동료 순간이동 수정(Mixamo 걸음·달리기 루프 이동 1.4/2.4m를 0.4m만 되돌리던 것 → 전부 고정, `-gaitCheck` 1.55m→0.00m; 온라인 아바타·서이경도 동일 처리), STP 동적 해상도(약 2.1M 픽셀 기준), `-perfLog`(무적 포함, 측정 전용). 앱: Builds/Residual Signal Missions.app. GIF: docs/media/survival·scavenge·weeper·approach.gif.
- 다음: 사람 플레이로 난이도·연료 시간 조정, 4인 Relay 검증.

## 2026-11-25 L4D2 추가분 (브랜치 feat/residual-signal-l4d2-next, origin/main 기반)

- 투척 무기(`Throwable.cs`): 파이프 폭탄(6초 유인·폭발)·화염병(불바다 9초). Poly Haven 와인병, 협동은 `MissionSync.ThrowRpc`로 모든 피어가 같은 투척, 피해·유인은 방장 복사본만.
- 특수 3종(`StationInfected.Mutants.cs`, `AcidPool.cs`): 침뱉개(산성 웅덩이, 오래 설수록 피해↑)·돌진개(들고 가 내리찍기, 스스로 못 풂)·올라타개(어깨에 타서 분대 반대로 끌고 감). 디렉터 순서 7종 순환. AI 동료도 붙잡힌 동안 피해, 산성액 회피.
- 장비(`Gear.cs`, `MeleeWeapon.cs`, `StationTrial.Gear.cs`, `StationTrial.Melee.cs`): 근접 4종(Poly Haven CC0 스캔, 2번 칸), 진통제·아드레날린(4번, 임시 체력 0.27/s 감소, 피해 먼저 흡수). `GearImport.cs`로 스캔 메시 읽기 허용.
- 자동차 경보(`CarAlarm.cs`, `StationTrial.Crescendo.cs`): 봉쇄선·화물 연결로 차. 쏘기/근접/폭발로 울림, 무리 16.
- 검사: `-throwCheck -gearCheck -mutantCheck -alarmCheck` 통과. 전체 회귀는 스크래치패드 regress.sh로 실행.
- 남은 것: 세이프룸, 4인 실접속 확인, 사람 플레이 밸런스. 커밋 안 함(사용자 요청 시에만).

## 2026-11-25 PR 문서 및 촬영

- 지금 하던 일. 루트와 프로젝트 README에 출격 화면, 신규 장비·감염체·차량 경보, 난이도 변경을 반영했다. Unity Play Mode GIF 6종과 올라타개 스크린샷을 추가했다.
- 재촬영. `ResidualSignal.StationPlayCheck.Run`에 `-solo -stationCapture <새 PNG 경로> -demoFrames <폴더>`를 넘기고 `-throwCheck`, `-gearCheck`, `-alarmCheck`, `-mutantCheck`를 한 번에 하나씩 실행한다. 프레임은 동작별 접두어로 저장되며, 동일 접두어의 PNG를 사전순으로 모아 10fps로 인코딩한다.
- 검증. Unity 6000.6.9f1에서 네 검사 모두 통과했다. 차량 경보 2.3초 내 8체 진입, 돌진개 1.3m 운반 후 피해, 올라타개 7.1m 이동을 확인했다.
- 다음 할 일. 신규 전투 기능의 Relay 동기화, 확장 캠페인 4인 완주, 실제 플레이 밸런스와 프레임 타임을 확인한다.
- 배포. 잔류신호 변경이 main에 들어오면 전용 워크플로가 macOS 앱을 빌드해 `ResidualSignal-mac.zip`을 GitHub Releases에 게시한다.
