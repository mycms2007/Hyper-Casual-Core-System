# Jailer Life

> Unity 3D · Hyper-Casual · Solo Dev · 10 Days (2026.04.01 ~ 2026.04.10)

하이퍼캐주얼 방치형 경영 게임. 광물을 채굴해 수갑을 만들고, 체포자를 죄수로 변환해 감옥을 채워나가는 루프를 가진다. 업그레이드를 통해 자동화가 진행되며, 감옥 확장 완료 시 엔딩.

본 프로젝트는 S사의 과제 수행물이기도 합니다.

---

## Core Loop

```
광물 채굴 → 보석 획득 → 수갑 생산 → 체포자에게 수갑 지급
→ 죄수 변환 → 감옥 수감 → 확장 구매 → 엔딩
```

---

## Key Implementation

- **Zone 추상화** — 플레이어/알바 NPC의 존 진입·퇴장을 공통 기반 클래스로 처리, 각 존은 필요한 메서드만 오버라이드
- **코루틴 기반 연출** — Unity Animator 없이 수식(스프링, Squash-and-Stretch)으로 모든 등장/퇴장 애니메이션 직접 구현
- **Cinemachine Priority 전환** — 카메라를 직접 교체하지 않고 우선순위 숫자만 변경해 자연스러운 블렌딩
- **ReservePending 패턴** — 비행 중인 수갑을 예약 카운트로 관리해 이중 차감 방지
- **3D 공간음** — 임시 GameObject 생성 방식으로 Logarithmic 감쇠 3D 사운드 구현
- **TutorialManager 단계 가드** — `if (_step != N) return` 패턴으로 9단계 튜토리얼 순서 보장
- **조이스틱 + 키보드 동시 지원** — Joystick Pack 기반, 동일한 입력 인터페이스로 모바일/PC 대응

---

## Tech Stack

| | |
|---|---|
| Engine | Unity 3D (Built-in RP) |
| Camera | Cinemachine VirtualCamera |
| Movement | Rigidbody.MovePosition (FixedUpdate) |
| Animation | Animator + Coroutine-based spring |
| Audio | AudioSource 2채널 + 3D Spatial Sound |
| UI | Screen Space Overlay + World Space Canvas |
| Input | Joystick Pack + Keyboard |

---

## Gameplay Video

> 영상 링크 준비 중

<!-- [YouTube](링크) -->
