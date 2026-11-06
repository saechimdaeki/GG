# Steam 스토어 페이지 초안 — 유령집 이삿짐센터 / Haunted Movers

> 초안. 게임에 실제로 있는 것만 적음 (2026-11-06 MVP 기준). 출시 전 실제 빌드와 대조할 것.
> 아직 없는 것: Steam 로비(현재 IP 직접 접속), 근접 음성, 여러 집, 컨트롤러.

## 짧은 설명 (Short description, 300자 이내)

**KO:** 친구 2~4명과 이삿짐센터 직원이 되어, 도망치고 날뛰는 저주받은 가구를 유령집에서 끌어내 트럭에 꽉꽉 싣는 엉망진창 물리 협동 게임. 소파는 혼자 못 든다. 유령은 가구 든 사람부터 노린다.

**EN:** Grab 1–3 friends and move cursed furniture out of a haunted house. Wardrobes run away, pianos buck you off, the ghost goes for whoever's carrying. Pack the truck tight, hit your quota, don't go bankrupt.

## 긴 설명 (About This Game)

**혼자선 못 든다.** 소파, 냉장고, 피아노 — 무거운 건 둘이 같이 들어야 올라간다. 좁은 문에서는 돌려야 나간다. 계단에서는… 행운을 빈다.

**가구가 살아 있다.** 옷장은 다가가면 깡충깡충 도망가고, 피아노는 3초마다 들썩여 붙잡은 사람을 전부 떨군다.

**유령이 지켜본다.** 벽을 통과해 떠다니다 가구를 든 사람부터 쫓아와 기절시킨다. 다 같이 쓰러지면 그날은 끝. 가까워지면 화면이 지지직대고 엘리베이터 재즈가 뒤틀린다.

**트럭 테트리스.** 빈틈없이 실을수록 보너스. 세게 부딪히면 가구가 상하고 값이 떨어진다.

**3일 안에 회사를 살려라.** 날마다 오르는 할당량. 번 돈으로 수레·밧줄·퇴마 테이프를 사서 다음 날을 버텨라.

## 기능 목록

- 2~4인 온라인 협동 (호스트 / 참가)
- 물리 기반 운반 — 무게가 진짜다
- 저주받은 가구 6종
- 유령 1종, 퇴마 테이프
- 3일 할당량 런 + 상점
- 따뜻한 VHS 로우폴리 화면

## 태그 후보 (Steam 사용자 태그)

Co-op, Online Co-Op, Physics, Funny, Horror, Party Game, Comedy, Simulation, Indie, First-Person

## 스크린샷 후보 (현재 evidence에서)

| 파일 | 쓸 만한가 |
|---|---|
| `production/qa/evidence/story-004-client.png` | ✅ 유령 + 쓰러진 동료 + 화면 노이즈 — 대표 이미지 후보 |
| `production/qa/evidence/story-003-client.png` | ✅ 거실, 동료와 소파 |
| `production/qa/evidence/story-002-result.png` | ✅ 트럭 적재 결과 |
| `production/qa/evidence/story-005-client-shop.png` | ⚠ 상점 UI — 다듬은 뒤 |
| 캡슐 아트 (616×353 등) | ❌ 없음 — 아트 필요 |

## 출시 전 남은 것 (스토어 기준)

- [ ] Steam 로비 (GodotSteam) — IP 입력 없이 친구 초대
- [ ] 캡슐/헤더 아트, 트레일러 30~60초
- [ ] 사람 플레이테스트 3회 이상 (`playtest-checklist.md`)
- [ ] 가격: 프렌드슬롭 대세 가격대 $5~8 (2026 리서치: 싸고 웃긴 협동이 인디 첫 달 판매 46%)
- [ ] Steam 앱 등록비 $100, 스토어 페이지 공개 후 위시리스트 → Next Fest 데모
