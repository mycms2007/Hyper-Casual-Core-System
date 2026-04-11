# Jailer Life — 프로젝트 개요

> 1인 개발 / Unity 3D / 개발 기간: 10일 (2026.04.01 ~ 2026.04.10)

---

## 게임 소개

광물을 채굴해 수갑을 만들고, 체포자에게 수갑을 채워 죄수로 변환한 뒤 감옥을 채워나가는 **하이퍼캐주얼 방치형 경영 게임**.

플레이어가 직접 조작하면서 루프를 돌리고, 업그레이드를 통해 점점 자동화되는 구조.  
감옥이 가득 차면 확장하고, 최종 확장 완료 시 엔딩.

---

## 핵심 게임루프

```
광물 채굴
  → 보석 획득 → GemDropZone 드랍
  → ProcessingMachine이 수갑 생산
  → HandcuffStackZone에 수갑 쌓임
  → 플레이어(또는 알바)가 수갑 수령
  → OfficeZone에 수갑 전달
  → 대기 중인 체포자에게 수갑 지급
  → 체포자 → 죄수 변환 → 코인 획득
  → 죄수가 감옥으로 이동 → 수감
  → 수감 인원 가득 참 → 감옥 확장 구매
  → 확장 완료 → 엔딩
```

---

## 업그레이드 흐름

| 단계 | 업그레이드 내용 |
|------|--------------|
| 1 | 손 드릴 해금 — 광물 즉사 처리 |
| 2 | 드릴카 해금 — 자동 채굴, 이동 속도 대폭 향상 |
| 3 | 보석 적재량 증가 |
| 4 | 광부 NPC 소환 — 채굴 자동화 |
| 5 | 알바 NPC 소환 — 수갑 수령 자동화 |
| 6 | 감옥 확장 — 수용 인원 +20, 엔딩 진입 |

---

## 사용 기술

| 분류 | 내용 |
|------|------|
| 엔진 | Unity (3D, Built-in RP) |
| 카메라 연출 | Cinemachine VirtualCamera Priority 방식 |
| 이동 | Rigidbody.MovePosition (FixedUpdate 기반) |
| 애니메이션 | Animator + Animation Event, 코루틴 기반 스프링 연출 |
| 오디오 | AudioSource 2채널 구조 (루프 전용 / one-shot 전용), 3D 공간음 |
| UI | Screen Space Overlay (WorldToScreenPoint 추적), World Space Canvas |
| 입력 | 조이스틱 + 키보드 동시 지원 |
| 설계 패턴 | 싱글턴, 이벤트(static Action), OnEnable 실행기, Zone 추상 기반 클래스 |

---

## 구현 포인트

- **코루틴 기반 연출 시스템** — 모든 등장/퇴장 애니메이션을 Unity Animator 없이 코루틴 + 수식으로 직접 구현 (스프링, 바운스, Squash-and-Stretch)
- **Zone 추상화** — 플레이어/알바 NPC 진입을 공통 기반 클래스에서 처리, 각 존은 필요한 메서드만 오버라이드
- **ReservePending 패턴** — 비행 중인 수갑을 예약 카운트로 관리해 이중 차감 방지
- **Cinemachine Priority 전환** — 카메라를 직접 교체하지 않고 우선순위 숫자만 변경해 자연스러운 블렌딩
- **TutorialManager 단계 가드** — `if (_step != N) return` 패턴으로 9단계 튜토리얼 순서 보장
- **_silenced 플래그** — 시네마틱 구간 전체 음소거, EndPanel 사운드만 예외 허용

---

## 개발 중 주요 난관

전체 난관 및 해결 과정 → [Troubleshooting.md](Troubleshooting.md)

대표적인 것들:

- `rb.linearVelocity` API 버전 오류 → `rb.velocity`로 교체
- 시네마틱 중 플레이어 미끄러짐 → 잠금 시점에 velocity 강제 0
- 수갑 3D 사운드 안 들림 → Linear → Logarithmic 감쇠, maxDistance 30
- DrillCar 하위 계층 lossyScale 오염 → 역산으로 worldScale 보정
- EndPanelController 비활성 오브젝트에서 Start() 미호출 → 항상 활성화된 부모로 이전

---

## 문서 목록

| 문서 | 내용 |
|------|------|
| [Player_Systems.md](Player_Systems.md) | 플레이어 이동, DrillCar, 캐리어 시스템, 알바 |
| [Ore_Gem_ProcessingMachine.md](Ore_Gem_ProcessingMachine.md) | 채굴, 보석, 수갑 생산 기계 |
| [Handcuff_To_Money_And_Animation.md](Handcuff_To_Money_And_Animation.md) | 수갑 → 돈 파이프라인, 감옥 애니메이션 |
| [Character_Lifecycle.md](Character_Lifecycle.md) | 체포자 생성 ~ 죄수 수감 전체 흐름 |
| [PurchaseZone_Progression.md](PurchaseZone_Progression.md) | 구매존, 잠금 해제 체인 |
| [SFX_And_Cinematic.md](SFX_And_Cinematic.md) | 효과음 구조, 시네마틱 연출 |
| [UI_HUD_Tutorial.md](UI_HUD_Tutorial.md) | HUD, 튜토리얼, 카메라, 인디케이터 |
| [Troubleshooting.md](Troubleshooting.md) | 전체 난관 & 해결 기록 |
| [Refactoring_Proposal.md](Refactoring_Proposal.md) | 리팩토링 & 최적화 제안서 |


## 영상 링크
