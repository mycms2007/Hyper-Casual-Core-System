# Player Systems — Jailer Life

> 플레이어 관련 전체 시스템 기록.  
> 구현 흐름, 난관, 돌파 방법, 핵심 코드 포함.

---

## 1. PlayerController

### 역할
플레이어 이동 / 회전 / 애니메이션 상태 관리. Rigidbody 기반 물리 이동.

### 구조
```
PlayerState { Idle, Walk }
Update()     → 상태 전환 판단 + ApplyAnimator()
FixedUpdate() → Rigidbody 이동/회전
```

### 카메라 상대 이동
고정 카메라 방향을 Awake에서 XZ 평면으로 투영해 저장.  
이후 입력값과 합산해 월드 방향 계산 — 카메라 회전에 종속되지 않음.

```csharp
_camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
_camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;

// Move()
Vector3 dir = (_camForward * input.y + _camRight * input.x).normalized;
rb.MovePosition(rb.position + dir * _currentSpeed * Time.fixedDeltaTime);
```

### 외부 인터페이스 (상태 주입)

| 메서드 | 호출처 | 용도 |
|--------|--------|------|
| `SetMiningActive(bool)` | MiningTrigger | 채굴 애니메이션 on/off |
| `SetForceIdle(bool)` | MiningTrigger | 드릴 장착 중 걷기 애니 강제 중단 |
| `SetDrillSpeedBoost(bool)` | MiningTrigger | 드릴 장착 중 이동속도 1.5배 |
| `SetMovementLocked(bool)` | CinematicDirector | 시네마틱 중 입력 잠금 |
| `SetVisible(bool)` | DrillCar | 드릴카 탑승 중 렌더러 숨김 |
| `OnMiningHit()` | Animation Event | 채굴 타격 시점 SFX + 데미지 |

### 난관 1 — 시네마틱 중 플레이어가 계속 미끄러지는 문제

**원인**: `SetMovementLocked(true)` 호출 시 입력만 막았고 이미 붙어있던 Rigidbody 속도는 그대로였음.

**돌파**: locked 시점에 velocity / angularVelocity를 강제로 0으로 리셋.

```csharp
public void SetMovementLocked(bool locked)
{
    _movementLocked = locked;
    if (locked)
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        ChangeState(PlayerState.Idle);
        ApplyAnimator();
    }
}
```

> **참고**: Unity 버전 이슈로 `rb.linearVelocity`는 존재하지 않음 → `rb.velocity` 사용.

### 난관 2 — 애니메이터 IsWalking이 드릴 장착 중에도 켜지는 문제

**원인**: 드릴 장착(제자리 채굴) 중에도 이동 입력이 들어오면 Walk 상태로 전환됨.

**돌파**: `_forceIdle` 플래그를 별도로 두고 ApplyAnimator에서 AND 조건으로 처리.

```csharp
anim.SetBool("IsWalking", !_forceIdle && _currentState == PlayerState.Walk);
```

### Animation Event 연동

Mining 애니메이션 34프레임에 `OnMiningHit()` 이벤트가 박혀있음.  
PlayerController가 수신 → SFX 재생 + MiningTrigger에 데미지 위임.

```csharp
public void OnMiningHit()
{
    SFXManager.Instance?.PlayMining();
    if (miningTrigger != null) miningTrigger.DealDamage();
}
```

---

## 2. MiningTrigger

### 역할
플레이어 주변 Ore 감지 → 채굴 상태 진입/종료 결정.  
드릴차 구매 여부에 따라 손 드릴 또는 드릴카로 분기.

### 채굴 분기 흐름

```
Ore 감지
├─ DrillCar 구매 완료 → DrillCar.StartDrive()
├─ 드릴 구매 완료     → drillObject 활성화 → 광석 즉사 처리
└─ 맨손              → SetMiningActive(true) → 애니메이션 채굴
```

### 드릴카 중복 발진 방지 (난관)

**원인**: DrillCar가 EndDrive 후 플레이어를 제자리에 복귀시키는데, 근처에 광석이 남아있으면 MiningTrigger가 즉시 다시 발진시킬 수 있음.

**돌파**: EndDrive 시점에 1초 쿨다운 플래그를 세우고, 그 동안 발진 차단.

```csharp
public void StartDrillCarCooldown()
{
    StartCoroutine(DrillCarCooldownRoutine());
}

private IEnumerator DrillCarCooldownRoutine()
{
    _drillCarCooldownActive = true;
    yield return new WaitForSeconds(drillCarCooldown);
    _drillCarCooldownActive = false;
}
```

```csharp
// OnTriggerEnter / UpdateMiningState 진입부
if (DrillCar.Instance != null && DrillCar.Instance.IsDriving) return;
if (_drillCarCooldownActive) return;
```

> **실제로는**: EndDrive가 호출되는 시점엔 주변 광석이 이미 전부 죽어있어서  
> 쿨다운 없이도 루프가 생기지 않음. 안전망 용도.

---

## 3. DrillCar

### 역할
드릴카 이동/채굴 처리. 플레이어를 숨기고 드릴카가 대신 움직임.

### 입력 잠금 (시네마틱 연동)

CinematicDirector가 카메라 연출 중 드릴카 조작도 막아야 했음.  
PlayerController는 인스턴스 참조로 막을 수 있지만,  
DrillCar는 static flag 방식으로 처리.

```csharp
private static bool _inputLocked;
public static void SetInputLocked(bool locked) => _inputLocked = locked;

// Update()
if (_inputLocked) return;
```

### 캐리어 앵커 이전

탑승 시 플레이어 하위의 보석/돈/수갑 앵커를 드릴카 하위로 이전.  
하차 시 원래 위치로 복원.

```csharp
// StartDrive
gemCarrier.AttachAnchorTo(gemAnchor);
moneyCarrier.AttachAnchorTo(moneyAnchor);
handcuffCarrier.AttachAnchorTo(handcuffAnchor);

// EndDrive
gemCarrier?.RestoreAnchor();
moneyCarrier?.RestoreAnchor();
handcuffCarrier?.RestoreAnchor();
```

> **주의**: `SetVisible(false)` 전에 앵커 이전을 먼저 해야 함.  
> 이전 전에 숨기면 앵커가 아직 플레이어 하위라 보석/수갑 렌더러도 같이 꺼짐.

---

## 4. PlayerWallet

### 역할
플레이어 보유 금액 관리. HUD 갱신 및 이벤트 발행.

### 초반 3배 배율 시스템

게임 시작 60초 동안 수익 3배. 이후 자동 해제.

```csharp
private void Update()
{
    if (_earlyGameOver) return;
    _elapsed += Time.deltaTime;
    if (_elapsed >= earlyGameDuration)
        _earlyGameOver = true;
}

public void Add(int amount)
{
    if (amount > 0 && !_earlyGameOver)
        amount *= earlyGameMultiplier;
    _money += amount;
    // ...
}
```

> 인스펙터에서 `earlyGameDuration`(60s), `earlyGameMultiplier`(3) 조정 가능.

### 이벤트
- `OnFirstMoneyEarned` — 첫 수익 발생 시 (CinematicDirector가 구독)
- `OnBalanceChanged` — 잔액 변경 시 (HUD 갱신)

---

## 5. Carrier 시스템

플레이어가 들고 다니는 아이템을 등에 쌓는 시각적/논리적 관리.  
아이템 종류별로 분리된 Carrier가 StackManager를 감싸거나 독립 구현.

### GemCarrier
StackManager를 래핑. Ore → GemCarrier → StackManager로 흐름.  
드릴카 구매 후 최대 적재량 증가.

```csharp
public void SetCapacity(int capacity) => stackManager.SetCapacity(capacity);
```

### MoneyCarrier
수익 발생 시 wonPerCoin(15원)당 코인 1개 생성.  
코인을 즉시 전부 쌓지 않고 0.12초 간격으로 스태거드 추가 — 시각적 효과.

```csharp
public void AddMoney(int amount)
{
    int count = amount / wonPerCoin;
    if (count > 0)
        StartCoroutine(AddCoinsStaggered(count));
}
```

### HandcuffCarrier
StackManager와 독립된 별도 구현.  
수갑이 날아오는 중(Pending)인 수를 예약 카운트로 관리 — 도착 전에도 TotalCount에 포함.

```csharp
private int _pendingCount;
public int TotalCount => _handcuffs.Count + _pendingCount;
public void ReservePending() => _pendingCount++;
public void CommitPending() { if (_pendingCount > 0) _pendingCount--; }
```

**앵커 스케일 보정 (난관)**  
드릴카 모델 계층 하위에 앵커가 붙으면 lossyScale이 1이 아니어서 수갑이 찌그러짐.  
localScale을 역산해 월드 스케일이 항상 프리팹 원본 크기가 되도록 보정.

```csharp
Vector3 w = anchor.lossyScale;
Vector3 p = prefab.transform.localScale;
if (w.x != 0f && w.y != 0f && w.z != 0f)
    obj.transform.localScale = new Vector3(p.x / w.x, p.y / w.y, p.z / w.z);
```

### StackManager
체인 팔로우 + 흔들림 시스템.  
LateUpdate에서 이전 프레임 대비 이동 방향을 계산해 각 아이템에 전달.  
각 StackItem이 앞 아이템을 followSpeed로 추종하면서 swayAmount만큼 기울어짐.

---

## 6. AlbaController (알바 NPC)

### 역할
고용된 알바 NPC. 독립적인 상태머신으로 수갑 수거 → 사무실 복귀 루프를 반복.

### 상태 흐름

```
Init(0.2s)
  → Search (2초 간격으로 HandcuffStackZone 수갑 확인)
    → WalkToHandcuff
      → CollectWait(3s) — 수갑 이전
        → WalkToIdle
          → IdleWait(3s)
            → Search → ...
```

### 플레이어와의 배타적 관계

알바가 고용되면 `IsHired = true` — HandcuffStackZone에서 플레이어 수거 차단.

```csharp
// HandcuffStackZone.TryTransfer()
if (AlbaController.IsHired) return;
```

### 수갑 이전 방식

AlbaController가 직접 수갑을 생성하지 않고,  
HandcuffStackZone에 자신의 Carrier와 Anchor를 넘겨서 이전 요청.

```csharp
handcuffStackZone.TryTransferToAlba(_carrier, albaAnchor);
```

---

## 요약 — 주요 의존 관계

```
PlayerController
  ├─ MiningTrigger      (채굴 감지 → DealDamage / 상태 통보)
  ├─ GemCarrier         (보석 스택)
  ├─ MoneyCarrier       (돈 스택)
  ├─ HandcuffCarrier    (수갑 스택)
  └─ DrillCar           (탑승/하차 시 렌더러·앵커·이동 제어)

PlayerWallet
  └─ MoneyCarrier       (수익 발생 시 코인 추가)

CinematicDirector
  ├─ PlayerController.SetMovementLocked()
  └─ DrillCar.SetInputLocked()
```
