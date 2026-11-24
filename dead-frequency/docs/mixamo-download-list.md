# Mixamo 다운로드 목록

감염체가 맞고 휘청이고, 쓰러지고, 기어 오는 동작을 Mixamo 모션으로 바꾸기 위한 목록이다.
Adobe 계정 로그인이 필요해서 **다운로드는 직접** 한다. 받은 뒤 알려 주면 연결 작업을 이어서 한다.

## 먼저 알아둘 것

- **리그를 하나로 맞춘다.** 지금 일반 감염체는 Rocketbox 모델이고 Legacy 리그다. Mixamo 모션은 Mixamo 뼈대에서만 그대로 재생된다.
  그래서 **캐릭터도 Mixamo 좀비로 받는다.** 모델과 모션이 같은 뼈대라 변환 작업이 필요 없다.
- **라이선스:** Mixamo 캐릭터와 모션은 게임에 넣어 배포하는 건 무료다(상업용 포함). 원본 FBX를 따로 재배포하는 건 안 된다.
  이 저장소는 지금 **비공개**라 괜찮다. 나중에 공개로 바꾸면 `Assets/Art/Models/Mixamo/`를 저장소에서 빼야 한다.
- 이름은 Mixamo 검색창에 `zombie`로 검색해서 고른다. 아래 이름은 검색 결과에서 찾을 이름이고, 철자가 조금 다를 수 있다.

## 1. 캐릭터 (3~4개)

| 고를 것 | 용도 |
|---|---|
| 좀비 캐릭터 3~4종 (예: `Zombiegirl`, `Parasite`, `Warzombie`, `Copzombie`) | 일반 감염체 외형. 서로 다른 옷과 체형으로 고른다 |
| `Mutant` | 덩치 큰 특수 감염체 후보 |

**캐릭터 다운로드 설정**

| 항목 | 값 |
|---|---|
| Format | FBX for Unity (.fbx) |
| Pose | T-pose |

## 2. 모션 (우선순위 순)

| 순위 | 검색어 | 용도 | In Place |
|---|---|---|---|
| 1 | `Zombie Reaction Hit` | 맞고 휘청 (손맛에 가장 중요) | - |
| 2 | `Zombie Death` / `Zombie Dying` | 쓰러짐 (현재 물리 래그돌과 섞어 쓴다) | - |
| 3 | `Zombie Running` | 달려옴 (호드 러시) | ✅ 체크 |
| 4 | `Zombie Attack` / `Zombie Punching` | 근접 공격 (지금은 코드로 팔만 휘두름) | - |
| 5 | `Zombie Walk` | 배회 | ✅ 체크 |
| 6 | `Zombie Idle` | 대기 | - |
| 7 | `Zombie Scream` | 울음꾼 호출, 발견 반응 | - |
| 8 | `Zombie Biting` / `Zombie Neck Bite` | 덮치개가 붙잡았을 때 | - |
| 9 | `Zombie Crawl` | 다리 잃은 감염체, 바닥 기기 | ✅ 체크 |
| 10 | `Zombie Stand Up` | 바닥에서 일어남 (밀치기 뒤) | - |

**모션 다운로드 설정**

| 항목 | 값 |
|---|---|
| Format | FBX for Unity (.fbx) |
| Skin | **Without Skin** (용량 절약, 캐릭터는 1번에서 따로 받음) |
| Frames per Second | 30 |
| Keyframe Reduction | none |
| In Place | 걷기, 달리기, 기기만 체크 (이동은 게임 코드가 한다) |

모션은 **1번에서 받은 캐릭터 중 하나를 선택한 상태로** 다운로드한다. 그래야 같은 뼈대로 받아진다.

## 받은 것 (2026-11-24)

- 캐릭터 4종: `Characters/Zombiegirl`, `Copzombie`, `Warzombie`, `Parasite` (FBX for Unity, T-pose)
- 모션 36개: Mixamo의 `Scary Zombie Pack`(12개) → `Animations/Scary`, `Not So Scary Zombie Pack`(24개) → `Animations/NotSoScary`.
  Parasite 뼈대로 받았고 스킨이 포함돼 있다. 팩 다운로드에는 In Place 옵션이 없어서 루트 모션이 들어 있다. Unity 임포트에서 루트 위치를 고정해야 한다.
- 위 우선순위 목록의 1~10번이 모두 들어 있다.

## 3. 넣을 위치

```
dead-frequency/Assets/Art/Models/Mixamo/
  Characters/   ← 1번 캐릭터 FBX
  Animations/   ← 2번 모션 FBX (파일 이름은 받은 그대로)
```

최소 세트는 캐릭터 2개 + 모션 1~5번이다. 받는 데 20분 정도 걸린다.
