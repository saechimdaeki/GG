# 잔류신호 작업 인계

- 1차 개발판 범위는 해림역 첫 임무 〈열한 번째 이름〉이다. Unity 6000.6.9f1에서 `Assets/Scenes/Platform03.unity`를 실행한다.
- 흐름은 광장 → 대합실 → 직원 또는 침수 통로 → 승강장 방어 → 구조 → 정비동 펌프 → 적재장 거대체 → 탈출이다.
- 갱신한 씬은 `ResidualSignal.StationSceneBuilder.Build`로 생성한다. 에디터에서 `ResidualSignal.StationPlayCheck.Run`에 `-solo -stationCapture /tmp/residual-check.png`를 넘기면 임무를 검사한다. `-triageLeft -drainRoute`는 반대 경로를 선택한다. `-botCheck`, `-callerCheck`, `-bossModelCheck`로 개별 검사할 수 있다.
- macOS 실행, 두 분기 완주, 분대 지시, 울음꾼 호출 차단, 거대체 동작을 확인했다. 확장 임무 4인 Relay 완주와 사람 플레이에 따른 난이도 조정이 다음 작업이다.
