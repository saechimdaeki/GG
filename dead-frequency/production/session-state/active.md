# 잔류신호 작업 인계

- 1차 개발판 범위는 해림역 첫 임무 〈열한 번째 이름〉이다. Unity 6000.6.9f1에서 `Assets/Scenes/Platform03.unity`를 실행한다.
- 흐름은 광장 → 대합실 → 직원 또는 침수 통로 → 승강장 방어 → 구조 → 정비동 펌프 → 적재장 거대체 → 탈출이다.
- 갱신한 씬은 `ResidualSignal.StationSceneBuilder.Build`로 생성한다. 에디터에서 `ResidualSignal.StationPlayCheck.Run`에 `-solo -stationCapture /tmp/residual-check.png`를 넘기면 임무를 검사한다. `-triageLeft -drainRoute`는 반대 경로를 선택한다. `-botCheck`, `-callerCheck`, `-bossModelCheck`로 개별 검사할 수 있다.
- macOS 실행, 두 분기 완주, 분대 지시, 울음꾼 호출 차단, 거대체 동작을 확인했다. 확장 임무 4인 Relay 완주와 사람 플레이에 따른 난이도 조정이 다음 작업이다.
- 보스 개선 브랜치에서 적재장 거대체 체력을 솔로 기준 850 → 2,400으로 조정하고, 추가 플레이어당 400을 더한다. 양손으로 콘크리트를 들어 던지며 조준 고정 후 옆으로 피하거나 엄폐할 수 있다. 돌진 종료에는 반격할 시간이 있다.
- Unity 검사 `-bossThrowCheck`, `-bossDodgeCheck`, `-bossCoverCheck`, `-bossModelCheck` 통과. 다음은 4인 Relay에서 투척 동기화와 실제 분대 화력으로 생존 시간을 확인하는 것이다.
