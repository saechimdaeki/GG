# Epic: 유령집 이삿짐센터 (haunted-movers)

**Source:** `haunted-movers/design/game-brief.md` (workflow: minimal — no GDD/ADR)
**Goal:** 친구 2~4명이 날뛰는 저주 가구를 유령집에서 끌어내 트럭에 테트리스처럼 꽉 채워 싣는 엉망진창 물리 협동 게임.

**Scope (Brief MVP):**
1. 1인칭 3D 플레이어 — 이동·줍기·물리 들기
2. 2~4인 온라인 협동 (Steam 로비)
3. 유령집 1채 + 가구 6종 (저주 행동 2종)
4. 트럭 적재 — 칸 격자 + 파손 반영 보수
5. 유령 1종
6. 상점 3개 + 할당량 3일 런

**Order:** Brief Build order — 같이 들기 동기화(가장 위험) 먼저.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 둘이서 같이 들기 (이동·물리 들기·2인 온라인) | Integration | In Review | N/A |
| 002 | 트럭 적재 + 보수 계산 | Logic | In Review | N/A |
| 003 | 유령집 1채 + 가구 6종 | Visual/Feel | In Review | N/A |
| 004 | 유령 1종 | Logic | In Review | N/A |
| 005 | 상점 + 할당량 3일 런 | Logic | In Review | N/A |
