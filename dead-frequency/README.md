# 잔류신호 · Residual Signal

폐역에 남은 구조 요청에 응답하는 1인칭 협동 FPS. **1차 개발판**에는 해림역 첫 임무 〈열한 번째 이름〉이 들어 있습니다. 혼자 출격하면 AI 동료 셋과 플레이할 수 있고, 온라인은 최대 4인 코드 참가 방식입니다.

![밤의 해림역 광장에서 대합실로 진입](docs/media/approach.gif)

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
| 왼쪽 클릭 / 오른쪽 클릭 또는 왼쪽 Alt(길게) | 사격 / 정조준 |
| V 또는 휠 클릭 | 개머리판 밀치기 |
| E / E 길게 | 상호작용 / 쓰러진 동료 일으키기 |
| Q | 조준한 감염체에 분대 집중 사격 지시 (혼자 출격) |
| R / F | 재장전 / 손전등 |
| 1 / 2 / 휠 | 주무기 / 권총 / 무기 전환 |
| Esc / 클릭 | 마우스 잠금 해제 / 조작 재개 |

## 〈열한 번째 이름〉

광장 대피 통제소를 지나 비상전화에 응답합니다. 명부를 확인한 뒤 침수 통로 또는 직원 통로를 열고, 승강장 전력을 복구해 셔터가 열릴 때까지 버팁니다. 서이경을 구조한 뒤 정비동 유압 펌프를 가동하고, 적재장 거대체를 제압한 뒤 화물 연결로에서 배전함과 서비스문 잠금을 조작하고 서이경과 탈출합니다. 길은 바닥 유도선과 조명, 공간 배치로 안내합니다.

- **분대** — Mixamo 캐릭터를 적용한 AI 동료 셋이 보급품을 챙기고, 엄폐하며 접근하고, 지정한 표적에 집중 사격합니다. 쓰러진 아군을 일으키고 붙잡힌 아군을 우선 구조합니다.
- **전투** — 권총, 산탄총, 자동소총, 저격소총, 반자동 카빈. 정조준, 시점 반동, 총구 섬광, 탄흔, 부위별 피격 반응과 사망 물리 모션을 적용했습니다.
- **감염체** — 일반 감염체와 덮치개·끌개·부풀이·울음꾼, 적재장 거대체의 모델과 모션을 교체했습니다. 울음꾼의 호출은 공격으로 끊을 수 있습니다. 거대체는 콘크리트를 던지고, 돌진하거나 가까운 대상을 휩씁니다.
- **습격 연출** — 승강장은 준비 시간이 끝나면 시야 밖 통로에서 감염체 무리와 특수 감염체가 진입합니다. 이동 구간에서는 분대가 받는 압박에 따라 디렉터가 추가 무리와 휴식 간격을 조절합니다.
- **해림역** — 광장의 야간 조명, 대합실과 승강장의 배관·간판·등기구, 역 타일과 젖은 바닥, 정비동의 가구와 소품을 갱신했습니다.

## 플레이 장면

### 승강장 방어

![시야 밖 통로에서 진입하는 감염체 무리와 승강장 교전](docs/media/platform-defense.gif)

### 적재장 거대체

![적재장 거대체와 분대의 교전](docs/media/loading-bay-boss.gif)

### 화물 연결로 탈출

![화물 연결로 서비스문을 열고 서이경을 호송](docs/media/freight-escape.gif)

GIF는 Unity Play Mode에서 자동 조작으로 촬영했습니다.

스크린샷은 [AI 동료](docs/loading-bay-team.png), [해림역 내부](production/qa/evidence/architecture-detail-2026-11-24.png), [승강장](docs/media/platform-defense.png), [정비동](docs/loading-bay-workshop.png), [거대체](docs/loading-bay-boss.png), [화물 연결로](docs/media/freight-escape.png)에서 볼 수 있습니다.

## 검증과 남은 작업

- **이번 판 검증** — Apple Silicon macOS 실행, 직원·침수 통로 양쪽 완주, AI 동료의 교전·보급·표적 지시, 울음꾼 호출 차단, 적재장 거대체 이동·사망 물리.
- **온라인** — 이전 빌드에서 실제 Relay 방 생성과 코드 참가, 로컬 2인 상태 동기화를 확인했습니다. 확장된 첫 임무의 4인 완주와 밸런스는 미검증입니다.
- 후속 캠페인과 약탈자 교전은 기획 단계입니다.

[게임 기획](design/game-brief.md) · [아트·오디오 출처 및 라이선스](docs/art-sources.md)
