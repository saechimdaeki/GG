# Story 001: 둘이서 같이 들기 — 1인칭 이동, 물리 들기, 2인 온라인

> **Epic**: 유령집 이삿짐센터
> **Status**: In Review — 코드·자동 체크 완료, 사람 2인 플레이테스트 대기
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: L (3~5일)
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-06

## Context

**GDD**: `haunted-movers/design/game-brief.md`
**Requirement**: Brief MVP feature 1 + 2 (같이 들기는 동기화 없이 검증 불가라 묶음)

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Godot 4.7.2 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [ ] 1인칭 WASD 이동 + 마우스 시점 + 점프, 3D 물리(Jolt)
- [ ] 바라보는 가구를 클릭으로 잡기/놓기 — 잡은 지점에 스프링처럼 따라옴 (흔들림 있음)
- [ ] 가구마다 무게. 혼자 들면 느리고 처짐, 둘이 잡으면 무게가 나눠져 들 수 있음
- [ ] 호스트/참가로 2인 접속 (LAN/로컬 먼저, 같은 머신 2창 테스트 가능)
- [ ] 두 사람 위치·시점·잡은 가구 위치가 서로 화면에 보이고 어긋남 없이 따라옴 (호스트 권한 물리)
- [ ] 한 사람이 놓으면 다른 사람 쪽에서도 바로 무게가 바뀜
- [ ] 둘이서 가구 하나를 문 하나 통과시켜 옮길 수 있음 — 그리고 웃김

---

## Implementation Notes

- 호스트가 RigidBody3D 시뮬, 클라이언트는 입력(잡기 대상·목표점)만 보냄 → 상태 동기화
- 전송 레이어는 교체 가능하게: 먼저 ENet, 이후 GodotSteam(로비) — Steam 연동은 이 스토리 범위 밖
- 시작 시 `godot --version` 확인, Jolt/멀티플레이 API는 4.7 레퍼런스 대조

---

## Out of Scope

- Story 003: 실제 집·가구 모델 (여기선 박스 방 + 큐브 가구)
- Steam 로비 연동 (출시 준비 단계)

---

## Files

- haunted-movers/project.godot
- haunted-movers/src/main.gd, main.tscn (방·네트워크·잡기 물리)
- haunted-movers/src/player.gd
- haunted-movers/src/furniture.gd
- haunted-movers/src/tuning.gd, data/tuning.json
- haunted-movers/tests/smoke/run_check.gd

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

*qa.level: minimal — tests waived (advisory).*

**Story Type**: Integration
**Required evidence**: 2창 플레이 스크린샷 `production/qa/evidence/` + 짧은 플레이 메모
**Status**: [x] `production/qa/evidence/story-001-*` (스크린샷 2장 + 메모)

---

## Dependencies

- Depends on: None
- Unlocks: story-002, story-003, story-004
