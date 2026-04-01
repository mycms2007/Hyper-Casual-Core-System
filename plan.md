# StackManager / StackItem 구현 계획

## 생성 파일 목록

| 파일 | 경로 | 역할 |
|---|---|---|
| StackItem.cs | Scripts/Player/ | 개별 아이템 — 타겟 추적, sway 회전 |
| StackManager.cs | Scripts/Player/ | 플레이어에 부착 — 스택 관리, 아이템 추가/제거 |

---

## 체인 구조

```
Player (StackRoot 기준점)
└── Item[0] → StackRoot를 따라감
    └── Item[1] → Item[0]을 따라감
        └── Item[2] → Item[1]을 따라감
            └── ...
```

- StackRoot: Player 자식 빈 오브젝트, `stackOriginOffset` 위치에 고정
- Item[N]의 target = Item[N-1].transform (Item[0]은 StackRoot)
- 각 아이템은 target으로부터 `itemSpacing` 만큼 위에 위치

---

## 1. StackItem.cs

### 역할
- `Initialize(Transform target)` — 추적 대상 설정
- `LateUpdate()` — Vector3.Lerp 위치 추적 + Quaternion.Slerp 회전 + sway 적용
- sway: 플레이어 이동 방향 반대로 기울어짐

### 외부에서 주입받는 값 (StackManager가 전달)
```
target        Transform   — 따라갈 대상
offset        Vector3     — target으로부터의 거리 (itemSpacing * up)
followSpeed   float
rotationSpeed float
swayAmount    float
moveDir       Vector3     — 플레이어 현재 이동 방향
```

---

## 2. StackManager.cs

### Inspector 노출
| 변수명 | 타입 | 설명 |
|---|---|---|
| maxCapacity | int | 최대 소지 개수 |
| stackOriginOffset | Vector3 | 플레이어 기준 첫 아이템 위치 |
| itemSpacing | float | 아이템 간 간격 |
| followSpeed | float | 위치 Lerp 속도 |
| rotationSpeed | float | 회전 Slerp 속도 |
| swayAmount | float | 기울기 강도 |

### public 메서드
```
bool TryAdd(GameObject itemPrefab)   // 최대치 미만이면 추가, 초과면 false 반환
GameObject TryRemove()               // 마지막 아이템 제거 후 반환 (없으면 null)
int Count { get; }                   // 현재 스택 수
```

### 내부 동작
- Awake(): StackRoot 빈 오브젝트 생성 (stackOriginOffset 위치)
- LateUpdate(): 플레이어 이동 방향 계산 → 각 StackItem에 전달
- 이동 방향: 현재 프레임 position - 이전 프레임 position

---

## 트레이드오프

| 항목 | 결정 | 이유 |
|---|---|---|
| StackItem 업데이트 주체 | StackManager가 LateUpdate에서 일괄 호출 | 실행 순서 보장, PlayerController 이후 확실히 실행 |
| moveDir 계산 | position delta (프레임 차이) | Rigidbody 참조 없이 독립적으로 작동 |
| StackRoot | 코드로 생성 | Inspector에서 직접 만들 필요 없음 |

---

## 리뷰
✅ 구현 완료 (2026-03-31)

---
---

# Zone 시스템 + ProcessingMachine 구현 계획

## 전체 파이프라인

```
플레이어 스택 (Gem)
  ↓ GemDropZone 진입
GemDropZone — 좌우 교번 쌓임 (스프링 바운스)
  ↓ 전부 착지 완료
ProcessingMachine.InputGem — Gem 날아와 축소 소멸
  ↓ 1개 소멸마다
ProcessingMachine.MakeItemZone — 수갑 확장 생성
  ↓
HandcuffConveyor — 수갑 이동 (씬 오브젝트)
  ↓
ItemTransZone — 수갑 축소 소멸
  ↓ 전부 소멸
HandcuffStackZone — 수갑 스프링 바운스로 쌓임
```

---

## 생성할 스크립트

| 파일 | 위치 | 역할 |
|------|------|------|
| `Zone.cs` | Scripts/Zone/ | 베이스 클래스 (트리거 감지, 플레이어 참조) |
| `GemDropZone.cs` | Scripts/Zone/ | Gem 수거 → InputGem으로 전달 |
| `ProcessingMachine.cs` | Scripts/Zone/ | InputGem 소멸 → 수갑 생성 |
| `ItemTransZone.cs` | Scripts/Zone/ | 수갑 소멸 → HandcuffStackZone 전달 |
| `HandcuffStackZone.cs` | Scripts/Zone/ | 수갑 스프링 스택 |

---

## Zone 베이스

```
Zone (abstract MonoBehaviour)
├── OnTriggerEnter/Exit → PlayerController 감지
├── OnPlayerEnter(PlayerController) virtual
└── OnPlayerExit(PlayerController) virtual
```

---

## GemDropZone

### Inspector 슬롯
| 슬롯 | 설명 |
|------|------|
| `StackManager playerStack` | 플레이어 Gem 스택 |
| `float gemFlyDuration` | 날아가는 시간 |
| `float gemSpawnInterval` | Gem 순차 발사 간격 |
| `float xSpacing` | 좌우 간격 |
| `float ySpacing` | 상하 간격 |
| `AnimationCurve bounceCurve` | 착지 바운스 커브 |
| `ProcessingMachine machine` | 다음 단계 참조 |

### 동작 흐름
1. 플레이어 진입 → 수거 중이면 무시
2. playerStack에서 맨 위 Gem부터 TryRemove()
3. 각 Gem을 코루틴으로 목표 위치(좌우 교번)로 날림
4. 스프링 바운스 착지
5. 모든 Gem 착지 완료 → machine.ReceiveGems() 호출
6. 플레이어 Zone 이탈 시 리셋 허용

### 착지 위치
```
xOffset = (index % 2 == 0) ? -xSpacing : +xSpacing
yOffset = index * ySpacing
position = zoneBasePos(스냅샷) + Vector3(xOffset, yOffset, 0)
```

---

## 스프링 바운스

AnimationCurve 사용 (외부 패키지 불필요)
- 날아가는 포물선: Lerp + 높이 오프셋 (arc)
- 착지 바운스: bounceCurve로 scale overshoot → settle

---

## ProcessingMachine

| 슬롯 | 설명 |
|------|------|
| `Transform inputPoint` | InputGem 실린더 중앙 |
| `Transform makeItemPoint` | 수갑 생성 위치 |
| `GameObject handcuffPrefab` | 수갑 프리팹 |
| `float shrinkDuration` | 축소 시간 |
| `float expandDuration` | 확장 시간 |
| `ItemTransZone itemTransZone` | 다음 단계 참조 |

흐름: Gem 날아옴 → 축소 소멸 → 수갑 확장 생성 → ItemTransZone으로 넘김

---

## ItemTransZone

- 수갑 Trigger 감지 (Conveyor 끝)
- 수갑 축소 소멸
- 전부 소멸 시 HandcuffStackZone.AddHandcuff() 호출

---

## HandcuffStackZone

- GemDropZone과 동일한 스프링 스택 구조
- 플레이어 진입 시 수갑을 플레이어 스택으로 이동 (다음 단계)

---

## 주의사항

1. GemDropZone 착지 위치는 진입 시점 스냅샷 (플레이어 이동 무관)
2. 수거 중 재진입 무시 → 전부 소진 후 리셋
3. 각 단계는 이전 단계 완료 콜백으로 연결 (폴링 없음)
