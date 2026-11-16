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
**Current task:** 유령집 이삿짐센터에 리썰 컴퍼니 규칙 적용 — 사망+관전, 트럭 자동 출발(못 탄 사람 사망·벌금), 전원 사망 시 할당량 기간 수익 손실, 3일 마감 끝없는 할당량(×1.5, 미달=해고).
**Next step:** 2명 이상 실제 플레이로 관전 카메라 확인. 이후 후보: 트럭에 남는 사람 역할(지도·문·무전), 어두운 실내·배터리·무게 감속.
**Blocked on:** 없음
**Files in progress:** haunted-movers/src/{main,player,ghost,run_state}.gd, data/tuning.json, tests/smoke/run_check.gd, tests/integration/connection_check.gd, README, design/game-brief.md + 11-09 작업분 (모두 미커밋)
**Run result:** smoke PASS, network integration PASS, 실제 실행 스크린샷 2장(production/qa/evidence/lethal-*-2026-11-17.png). 관전 카메라는 실제 2인 플레이 미검증.
**Open questions:** 없음. "러셀컴퍼니" = Lethal Company (메모리 저장).
<!-- /CHECKPOINT -->

---

## Notes

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
