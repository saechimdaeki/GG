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
**Updated:** 2026-11-02
**Branch:** `main`
**Current task:** 가라앉은 도시 A안 2차 — 넓은 맵(128x64, tools/gen_zone1.py: 가라앉은 빌딩 5채·옥상 마을·깊은 동굴), 캐릭터 2x 디테일, 물고기 떼 6, 해초, 빛줄기, 하늘
**Next step:** 마을(물 빠진 뒤) 화면 — 회색 배경·건물 실루엣 단조로움. 빌딩 외벽이 바위처럼 보임 → 건물다운 모양(창문 테두리 등). 도보 동선(옥상↔바닥) 실제 플레이 확인
**Blocked on:** 유저 화면 평가
**Files in progress:** src/view3d/*
**Run result:** OBSERVED — 넓은 바다·물고기 떼·해초·탑 내부·깊은 폐허·마을 (sunken-city/production/qa/evidence/view3d-wide-sea/), smoke PASS
**Open questions:** 커밋·푸시 여부, 낮 잠수/밤 고물상 루프 도입 여부
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
