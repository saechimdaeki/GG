# 잔류신호 · Residual Signal

폐역에 남은 구조 요청에 응답하는 1인칭 협동 FPS. **1차 개발판**에는 해림역 첫 임무 〈열한 번째 이름〉이 들어 있습니다. 혼자 출격하면 AI 동료 셋과 플레이할 수 있고, 온라인은 최대 4인 코드 참가 방식입니다.

![해림역 광장 진입과 AI 분대 교전](docs/media/approach.gif)

## 플레이

1. Unity Hub에서 이 저장소의 `dead-frequency` 폴더를 프로젝트로 추가합니다.
2. **Unity 6000.6.9f1**으로 열고 패키지와 에셋 임포트가 끝날 때까지 기다립니다.
3. `Assets/Scenes/Platform03.unity`를 열고 **Play**를 누릅니다.
4. **혼자 출격** 또는 **온라인 협동**을 선택합니다. 온라인은 한 사람이 방을 만들고, 다른 플레이어가 **코드로 참가**에서 방 코드를 입력합니다.

온라인 방은 Unity Gaming Services Relay에 연결합니다. 포크에서 자체 서비스 구성을 사용하려면 Unity 프로젝트를 본인의 Cloud 프로젝트에 연결하세요.

### macOS 앱 빌드

저장소 루트에서 실행하세요. Unity Hub를 기본 위치에 설치한 경우 아래 명령으로 로컬 실행 앱을 만들 수 있습니다.

```zsh
cd dead-frequency
UNITY="/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "$PWD" \
  -executeMethod ResidualSignal.StationTrialBuild.Mac \
  -trialOutput "$PWD/Builds/Residual Signal Playtest.app"
open "Builds/Residual Signal Playtest.app"
```

| 조작 | 동작 |
|---|---|
| WASD / 마우스 | 이동 / 시점 |
| 왼쪽 클릭 / 오른쪽 클릭 | 사격 / 밀치기 |
| E / E 길게 | 상호작용 / 쓰러진 동료 일으키기 |
| Q | 조준한 감염체에 분대 집중 사격 지시 (혼자 출격) |
| R / F | 재장전 / 손전등 |
| 1 / 2 / 휠 | 주무기 / 권총 / 무기 전환 |
| Esc / 클릭 | 마우스 잠금 해제 / 조작 재개 |

## 〈열한 번째 이름〉

광장 대피 통제소를 지나 비상전화에 응답합니다. 명부를 확인한 뒤 침수 통로 또는 직원 통로를 열고, 승강장 전력을 복구해 셔터가 열릴 때까지 버팁니다. 서이경을 구조한 뒤 정비동 유압 펌프를 가동하고, 적재장 거대체를 제압해 탈출합니다. 길은 바닥 유도선과 조명, 공간 배치로 안내합니다.

- **분대** — AI 동료 셋, 보급 회수, 엄폐 뒤 접근, 집중 사격, 치료와 구조.
- **전투** — 권총, 산탄총, 자동소총, 저격소총, 반자동 카빈. 부위별 피격 반응, 밀치기와 사망 물리 모션.
- **감염체** — 일반 감염체 외형 6종과 특수 감염체 5종. 덮치개, 끌개, 부풀이, 울음꾼, 적재장 거대체가 다른 방식으로 공격합니다. 울음꾼의 호출은 공격으로 끊을 수 있고, 거대체는 돌진 전에 예고 동작을 합니다.
- **습격 연출** — 증원은 플레이어에게 가려져 있고 분대까지 이동할 수 있는 지점에서 나옵니다. 특수기는 공통 대기 시간을 적용합니다.

![적재장 거대체 접근과 피격](docs/media/loading-bay-boss.gif)

위 GIF는 Unity Play Mode의 자동 플레이 장면을 촬영했습니다. [정비동 화면](docs/loading-bay-workshop.png) · [동료 작업복](docs/loading-bay-team.png)

## 검증과 남은 작업

- **이번 판 검증** — Apple Silicon macOS 실행, 직원·침수 통로 양쪽 완주, AI 동료의 교전·보급·표적 지시, 울음꾼 호출 차단, 적재장 거대체 이동·사망 물리.
- **온라인** — 이전 빌드에서 실제 Relay 방 생성과 코드 참가, 로컬 2인 상태 동기화를 확인했습니다. 확장된 첫 임무의 4인 완주와 밸런스는 미검증입니다.
- 후속 캠페인과 약탈자 교전은 기획 단계입니다.

[게임 기획](design/game-brief.md) · [아트·오디오 출처 및 라이선스](docs/art-sources.md)
