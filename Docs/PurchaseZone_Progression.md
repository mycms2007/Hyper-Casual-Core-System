# PurchaseZone & 게임 진행 잠금 해제 체인 — Jailer Life

> 플레이어가 존에 서서 돈을 내고 기능을 해금하는 핵심 진행 시스템.  
> Zone 기반 구조, 구매 흐름, 활성화 조건, 각 잠금 해제 실행기 전체 정리.

---

## 1. Zone — 추상 기반 클래스

모든 상호작용 존의 공통 기반. Trigger 이벤트를 감지해 플레이어/알바 분기 처리.

```csharp
public abstract class Zone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) { OnPlayerEnter(player); return; }

        AlbaController alba = other.GetComponentInParent<AlbaController>();
        if (alba != null) OnAlbaEnter(alba);
    }

    protected virtual void OnPlayerEnter(PlayerController player) { }
    protected virtual void OnPlayerStay(PlayerController player)  { }
    protected virtual void OnPlayerExit(PlayerController player)  { }
    protected virtual void OnAlbaEnter(AlbaController alba)       { }
    protected virtual void OnAlbaExit(AlbaController alba)        { }
}
```

`GetComponentInParent` 사용 — 콜라이더가 자식 오브젝트에 있어도 정상 감지.  
Zone을 상속하는 클래스: `GemDropZone`, `OfficeZone`, `HandcuffStackZone`.  
PurchaseZone은 Zone을 상속하지 않고 독립적으로 Trigger를 직접 처리.

---

## 2. PurchaseZone — 구매 존 핵심 동작

### 역할
플레이어가 서 있는 동안 일정 간격으로 돈을 드레인하며 구매 진행.  
완료 시 `activateTargets` 활성화 → 잠금 해제 실행기들이 OnEnable에서 동작.

### 활성화 조건 (ActivationTrigger)

| 값 | 등장 조건 |
|----|-----------|
| `None` | 게임 시작 즉시 |
| `Prerequisite` | 지정한 선행 PurchaseZone이 구매 완료 시 |
| `FirstMoney` | 플레이어가 처음 돈을 수령 시 |
| `JailFull` | 감옥이 처음 가득 찰 때 |

```csharp
// Prerequisite: Update에서 매 프레임 체크
if (trigger == ActivationTrigger.Prerequisite &&
    prerequisite != null && prerequisite.IsPurchased)
{
    trigger = ActivationTrigger.None;
    OnTriggerConditionMet();
}
```

### 코인 드레인 & 구매 흐름

```
플레이어 진입 → _isHolding = true, _drainTimer = 0 (즉시 첫 드레인)
Update 매 틱:
  drainInterval마다 drainAmountPerTick 만큼 PlayerWallet.Spend()
  → 코인 비행 연출 (playerPos → zone)
  → price - soundTriggerOffset 도달 시 구매 완료 효과음 미리 재생
  → paidAmount >= price → Purchase()
플레이어 이탈 → _isHolding = false (진행 유지, 재진입 시 이어서)
```

**효과음 미리 재생** — 실제 완료 직전에 먼저 틀어서 완료 타이밍과 자연스럽게 어울리도록.
```csharp
if (!_soundPlayed && _paidAmount >= price - soundTriggerOffset)
{
    _soundPlayed = true;
    SFXManager.Instance?.PlayZonePurchase();
}
```

### activateTargets vs delayedActivateTargets

| 필드 | 활성화 시점 | 용도 |
|------|------------|------|
| `activateTargets` | 구매 완료 즉시 | 잠금 해제 실행기, 새 존 등장 |
| `delayedActivateTargets` | `delayedActivateDelay` 초 후 | 연출 완료 후 활성화 (JailExpander 등) |

```csharp
private void Purchase()
{
    foreach (GameObject target in activateTargets)
        target?.SetActive(true);

    if (delayedActivateTargets.Length > 0)
        StartCoroutine(DelayedActivate());
    // 이후 SpringDisappear → 존 시각적 소멸
}
```

### 존 등장/퇴장 스프링 애니메이션

**등장** (SpringAppear) — 지수 감쇠 × 코사인 오버슈트.
```csharp
float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
t.localScale = _originalScale * Mathf.Clamp(s, 0f, 1f);
```

**퇴장** (SpringDisappear) — 살짝 커졌다가 0으로.
```csharp
float s = p < 0.25f
    ? Mathf.Lerp(1f, 1.12f, p / 0.25f)        // 팽창
    : Mathf.Lerp(1.12f, 0f, (p - 0.25f) / 0.75f); // 소멸
```

**zoneVisual 유무에 따른 분기**:
- `zoneVisual` 연결됨 → zoneVisual에만 애니메이션 적용, 구매 후 zoneVisual SetActive(false)
- `zoneVisual` 없음 → 자기 자신 transform에 적용 (`_usingSelf = true`)

---

## 3. ZoneUnlocker — 조건 기반 존 활성화

항상 활성화된 오브젝트에 부착. 이벤트를 감지해 존을 SetActive(true).

### 잔액 임계값 방식

```csharp
private void OnBalanceChanged(int balance)
{
    foreach (var entry in moneyThresholdZones)
    {
        if (entry.zone != null && !entry.zone.activeSelf && balance >= entry.minAmount)
            entry.zone.SetActive(true); // 조건 달성 시 단 한 번
    }
}
```

이미 활성화된 존은 `!entry.zone.activeSelf` 조건으로 중복 활성화 방지.

### 감옥 만석 방식

```csharp
private void OnJailFull()
{
    foreach (var zone in jailFullZones)
        zone?.SetActive(true);
}
```

`JailManager.OnJailFull` 이벤트 구독 — 감옥이 처음 가득 찰 때 확장 구매존 등장.

---

## 4. 잠금 해제 실행기들

PurchaseZone의 `activateTargets`에 연결되어 구매 완료 시 OnEnable로 실행.

### DrillUnlock

손 드릴 해금. MiningTrigger에 구매 완료 통보.

```csharp
private void OnEnable()
{
    miningTrigger?.UnlockDrill(); // _drillPurchased = true
}
```

이후 MiningTrigger가 광석 감지 시 drillObject 활성화 → 채굴 가능.

### DrillCarUnlock

드릴카 해금. DrillCar.Instance에 구매 완료 통보.

```csharp
private void OnEnable()
{
    DrillCar.Instance?.Purchase(); // IsPurchased = true, gem 최대적재량 증가
}
```

이후 MiningTrigger가 광석 감지 시 DrillCar.StartDrive() 호출.

### GemCapacityExpander

보석 최대 적재량 증가. GemCarrier를 통해 StackManager에 반영.

```csharp
private void OnEnable()
{
    GemCarrier.Instance?.SetCapacity(capacity); // 기본 → 30
}
```

### MinerUpZone

광부 NPC 소환. MinerSpawner 오브젝트를 활성화.

```csharp
private void OnEnable()
{
    minerSpawner?.SetActive(true); // MinerSpawner.OnEnable → 광부 소환
}
```

### JailExpander (delayedActivateTargets 패턴)

감옥 확장. `delayedActivateTargets`에 연결되어 딜레이 후 실행.

```csharp
private void OnEnable()
{
    JailManager.Instance?.ExpandCapacity(expandAmount);     // capacity +20
    JailManager.Instance?.SetJailDestination(newDest);      // 신규 죄수 목적지 변경
}
```

---

## 5. DrillSurface

드릴/드릴카에 붙은 콜라이더 스크립트.  
광석과 충돌하면 즉시 체력을 0으로 만들어 파괴.

```csharp
private void OnTriggerEnter(Collider other)
{
    Ore ore = other.GetComponent<Ore>();
    if (ore == null || ore.IsDead) return;
    while (!ore.IsDead) ore.TakeDamage();
}
```

DrillCar.Update에서도 동일하게 `while (!ore.IsDead) ore.TakeDamage()` 처리.  
DrillSurface는 손 드릴 전용 즉사 콜라이더.

---

## 6. 게임 전체 잠금 해제 흐름

```
게임 시작
  │
  ├─ [즉시 등장, None]
  │    기본 채굴존 / HandcuffStackZone / OfficeZone 등
  │
  ├─ ZoneUnlocker: 잔액 임계값 달성
  │    → DrillUp PurchaseZone 등장
  │         구매 → DrillUnlock.OnEnable() → 손 드릴 해금
  │
  ├─ [Prerequisite: DrillUp 완료 후]
  │    → DrillCarUp PurchaseZone 등장
  │         구매 → DrillCarUnlock.OnEnable() → 드릴카 해금
  │              → GemCapacityExpander.OnEnable() → 적재량 증가
  │
  ├─ [FirstMoney or 잔액 임계값]
  │    → MinerUp PurchaseZone 등장
  │         구매 → MinerUpZone.OnEnable() → MinerSpawner 활성화 → 광부 소환
  │
  ├─ ZoneUnlocker: JailManager.OnJailFull
  │    → 감옥 확장 PurchaseZone 등장
  │         구매 → delayedActivateTargets → JailExpander.OnEnable()
  │              → JailManager.ExpandCapacity(+20)
  │              → JailAnimator 확장 연출
  │
  └─ 확장 완료 → 게임 종료 (EndPanel)
```

---

## 7. 패턴 요약

### activateTargets 패턴
구매 완료 즉시 실행기를 켜야 할 때. 잠금 해제 대부분이 이 방식.

### delayedActivateTargets 패턴
연출(애니메이션 등)이 끝난 후 활성화해야 할 때.  
JailExpander처럼 확장 연출이 선행되어야 할 때 사용.

### ZoneUnlocker 패턴
PurchaseZone 바깥에서 외부 조건(잔액, 이벤트)으로 존을 등장시킬 때.  
PurchaseZone은 구매 조건만, 등장 조건은 ZoneUnlocker가 담당 — 역할 분리.

### OnEnable 실행기 패턴
모든 잠금 해제 실행기(DrillUnlock, DrillCarUnlock, GemCapacityExpander, MinerUpZone, JailExpander)가  
`OnEnable`에서 딱 한 번 실행되도록 설계.  
`activateTargets`의 SetActive(true) 한 번 = 실행기 OnEnable 한 번 = 효과 1회 적용.
