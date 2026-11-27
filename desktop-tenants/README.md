# 데스크톱 세입자 (Desktop Tenants)

열려 있는 macOS 앱 창이 픽셀 세입자의 놀이터가 됩니다. 세입자들이 창 위를 걷고, 사다리를 타고, 상점에 들르는 동안 월세와 상점 수익을 모아 동네를 키웁니다.

![10층 아파트와 상점가에서 생활하는 세입자들](docs/media/desktop-life.gif)

## 다운로드와 실행

[macOS Apple Silicon용 v0.2.0 미리보기](https://github.com/saechimdaeki/GG/releases/tag/desktop-tenants-v0.2.0)에서 `DesktopTenants-macos-arm64.zip`을 받아 압축을 풀고 `DesktopTenants.app`을 실행하세요. macOS 12 이상이 필요하며 앱은 공증 없이 배포됩니다.

아래 GIF는 v0.2.0 미리보기에서 볼 수 있는 기능을 담았습니다. [플레이테스트와 버그 제보](https://github.com/saechimdaeki/GG/issues/42)

## 최신 소스에서 플레이하기

- 세입자를 드래그해서 옮기거나 움직이면서 놓아 던집니다. 잠시 마우스를 올려 두면 쓰다듬을 수 있습니다.
- 동전에 마우스를 가까이 가져가면 월세를 모읍니다. 집을 클릭하면 16층까지 올릴 수 있고, 빈 부지나 상점을 클릭하면 건설 또는 업그레이드합니다.
- 11층부터는 증축비가 층마다 약 12%씩 오릅니다. 16층까지 완공하면 세입자 32명이 살 수 있습니다.
- 편의점, 카페, 공원 등 10종의 장소에 세입자가 찾아갑니다. 옥상 상태판에서 날씨, 시각, CPU 사용률과 메모리 사용량을 볼 수 있습니다.
- 동네 벼룩시장, 즉석 사진관, 소원 등불이 열리면 화면 아래 소품에 커서를 잠깐 올려 참여합니다. 보물 동전을 찾거나, 주민들과 사진을 찍거나, 등불을 띄울 수 있습니다.
- 화가가 둔 그림에 커서를 잠시 올리면 주변 주민이 감상에 동참합니다. 10층에서는 소금빵을 나누는 제빵사 소금이와 이웃의 단체 사진을 찍는 사진사 찰칵이 입주할 수 있습니다.
- 빈 영역의 클릭은 아래 앱으로 전달됩니다. Dock의 앱 아이콘에서 종료할 수 있습니다.
- 다른 모니터에서 실행하려면 `open -a /path/to/DesktopTenants.app --args --screen 2`를 사용하세요. `--screen 1`은 주 모니터입니다.

## 플레이 GIF

### 아파트와 상점가

**옥상 상태판**

![옥상 상태판 플레이 장면](docs/media/status-display.gif)

**사다리와 옥상 이동**

![사다리와 옥상 이동 플레이 장면](docs/media/ladder-climb.gif)

**이사 트럭**

![이사 트럭 플레이 장면](docs/media/moving-truck.gif)

### 날씨

**비**

![비 플레이 장면](docs/media/rain.gif)

**뇌우**

![뇌우 플레이 장면](docs/media/thunderstorm.gif)

**눈**

![눈 플레이 장면](docs/media/snow.gif)

**강풍**

![강풍 플레이 장면](docs/media/wind.gif)

**무지개**

![무지개 플레이 장면](docs/media/rainbow.gif)

### 주민 이벤트

**생일**

![생일 플레이 장면](docs/media/birthday.gif)

**연예인 방문**

![연예인 방문 플레이 장면](docs/media/celebrity-visit.gif)

**아이스크림 트럭**

![아이스크림 트럭 플레이 장면](docs/media/ice-cream-truck.gif)

**마라톤**

![마라톤 플레이 장면](docs/media/marathon.gif)

**길거리 공연**

![길거리 공연 플레이 장면](docs/media/busking.gif)

**고백**

![고백 플레이 장면](docs/media/confession.gif)

**반상회**

![반상회 플레이 장면](docs/media/residents-meeting.gif)

### 하늘과 돌발 이벤트

**헬기 택시**

![헬기 택시 플레이 장면](docs/media/helicopter-taxi.gif)

**UFO**

![UFO 플레이 장면](docs/media/ufo.gif)

**동전 소나기**

![동전 소나기 플레이 장면](docs/media/coin-shower.gif)

**화재 대피 훈련**

![화재 대피 훈련 플레이 장면](docs/media/fire-drill.gif)

**불꽃놀이**

![불꽃놀이 플레이 장면](docs/media/fireworks.gif)

**정전**

![정전 플레이 장면](docs/media/blackout.gif)

**지진**

![지진 플레이 장면](docs/media/earthquake.gif)

**새 떼**

![새 떼 플레이 장면](docs/media/birds.gif)

**선물 비행기**

![선물 비행기 플레이 장면](docs/media/gift-plane.gif)

**유성우**

![유성우 플레이 장면](docs/media/meteor-shower.gif)

## 소스 빌드

Unity 6000.6.9f1과 Xcode Command Line Tools가 필요합니다. Unity 에디터에서는 세입자 시뮬레이션을, macOS 플레이어에서는 데스크톱 오버레이와 앱 창 연동을 실행합니다.

```bash
cd desktop-tenants
bash tools/build-plugin.sh
"/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit -projectPath "$PWD" -executeMethod Setup.BuildMac -logFile -
open Builds/Mac/DesktopTenants.app
```

행사만 빠르게 확인하려면 앱 실행 인수에 `--floors 10 --event flea`를 붙이세요. 즉석 사진관은 `photo`, 소원 등불은 `lantern`을 사용합니다.
