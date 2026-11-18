# 릴 던전 (Reel Dungeon)

슬롯을 돌려 나온 심볼이 그 턴의 행동이 되는 턴제 던전 로그라이크. 적을 이길 때마다 릴에 심볼을 넣거나 빼며 나만의 슬롯을 만든다.

![첫 전투](production/qa/evidence/fight-turn2-2026-11-18.png)

- 엔진: Unity 6000.6.9f1 (Unity 6), C#
- 상태: 프로토타입. 화면은 IMGUI 임시 UI, 연출 없음.

## 규칙 (v0)

- 릴 3개가 매 턴 자동으로 돈다. 턴마다 릴 하나를 1번 다시 돌릴 수 있다.
- 칼 = 공격, 방패 = 막기, 하트 = 회복, 불꽃 = 큰 공격 + 자해, 해골 = 저주(자해).
- 같은 심볼 2개면 1.5배, 3개면 2배.
- 적은 다음 공격 수치를 미리 보여준다. 5층 미노타우로스를 잡으면 승리.
- 수치는 전부 `Assets/Resources/tuning.json`.

## 실행

```bash
U="/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity"
"$U" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /tmp/results.xml
"$U" -batchmode -quit -projectPath "$PWD" -executeMethod BuildScript.BuildMac
open Builds/Mac/ReelDungeon.app
```

에디터에서 열 때는 Unity Hub → Add → 이 폴더.
