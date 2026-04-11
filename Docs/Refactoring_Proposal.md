# Refactoring & Optimization Proposal — Jailer Life

> 전체 57개 스크립트를 직접 읽고 작성한 제안서.
> Claude Code가 각 항목을 읽고 독립적으로 작업할 수 있도록 작성됨.
> 우선순위: CRITICAL > HIGH > MEDIUM > LOW

---

## 목차

1. [BUG — 잠재적 버그 (즉시 수정 권장)](#1-bug--잠재적-버그)
2. [CLEANUP — 디버그 잔재 제거](#2-cleanup--디버그-잔재-제거)
3. [REFACTOR — 구조 개선](#3-refactor--구조-개선)
4. [OPTIMIZE — 성능 최적화](#4-optimize--성능-최적화)
5. [ROBUSTNESS — 견고성 강화](#5-robustness--견고성-강화)

---

## 1. BUG — 잠재적 버그

### [CRITICAL] DrillCar.OnEnable이 IsPurchased를 강제로 true로 설정

**파일**: `Assets/Project/Scripts/Mining/DrillCar.cs:80`

**문제**:
```csharp
private void OnEnable()
{
    IsPurchased = true;  // ← 이 줄
    if (visual != null) visual.SetActive(false);
    if (drillObject != null) drillObject.SetActive(false);
}
```
OnEnable에서 `IsPurchased = true`로 설정되면, 게임 시작부터 DrillCar가 구매 완료 상태가 된다.
`MiningTrigger.OnTriggerEnter`에서 `IsDrillCarPurchased`가 항상 true이므로, 손 드릴 단계를 건너뛰고 처음부터 DrillCar가 발동된다.
`F5` 디버그 키가 `!DrillCar.Instance.IsPurchased`를 조건으로 거는 것은 IsPurchased가 false로 시작해야 함을 의도한 코드다.

**수정 방향**:
`OnEnable`에서 `IsPurchased = true;` 줄을 제거한다.
visual과 drillObject를 숨기는 코드만 남긴다:
```csharp
private void OnEnable()
{
    if (visual != null) visual.SetActive(false);
    if (drillObject != null) drillObject.SetActive(false);
}
```
단, 현재 씬에서 DrillCar 구매 없이 게임이 정상 동작하고 있었다면, 이 줄이 의도적으로 "항상 구매 완료" 상태를 만든 것일 수 있다. 적용 전 인스펙터에서 DrillCarUnlock PurchaseZone이 씬에 존재하는지 확인할 것.

---

### [CRITICAL] JailZone과 PrisonerController가 RegisterArrived()를 이중 호출할 수 있음

**파일**:
- `Assets/Project/Scripts/Zone/JailZone.cs:12`
- `Assets/Project/Scripts/Arrested/PrisonerController.cs:118`

**문제**:
`JailZone.OnTriggerEnter`는 Prisoner가 Trigger에 닿으면 `JailManager.RegisterArrived()`를 호출한다.
`PrisonerController.OnArrived()`도 목적지 도착 시 `JailManager.RegisterArrived()`를 호출한다.
Prisoner가 감옥 바닥(JailZone 트리거)에 도달하면서 동시에 목적지 거리 판정이 통과되면 두 번 카운트된다.

**증거**: `_skipJailRegistration` 플래그가 있는 것은 RegisterArrived 중복을 이미 의식한 흔적.

**수정 방향**:
두 경로 중 하나만 남긴다. PrisonerController 기반이 더 정밀하므로 JailZone.cs의 RegisterArrived 호출을 제거하거나, JailZone 방식으로 통일한다.
현재 PrisonerController.OnArrived에 `_skipJailRegistration` 가드가 있으므로, JailZone의 RegisterArrived를 제거하는 것이 더 깔끔하다:
```csharp
// JailZone.cs — 이 줄 제거
JailManager.Instance?.RegisterArrived();
```

---

### [HIGH] AlbaController.IsHired가 static으로 선언되어 절대 해제되지 않음

**파일**: `Assets/Project/Scripts/Player/AlbaController.cs:11, 51`

**문제**:
```csharp
public static bool IsHired { get; private set; }
// ...
private void Awake() { IsHired = true; }
```
`IsHired`는 AlbaController.Awake에서 true로 설정된다. 하지만 어디서도 false로 되돌리지 않는다.
HandcuffStackZone의 플레이어 픽업 차단 조건:
```csharp
if (AlbaController.IsHired) return;
```
이 조건은 한번 알바가 소환되면 영구적으로 플레이어의 수갑 픽업을 차단한다. 게임이 싱글 플레이로 한 번만 실행되면 문제 없지만, 에디터에서 재플레이하면 IsHired가 이전 게임 상태를 유지해 버그가 된다.

**수정 방향**:
```csharp
// AlbaController.cs
private void OnDestroy()
{
    IsHired = false;
}
```
또는 PlayerWallet 등에서 게임 초기화 시 `AlbaController.ResetHired()` static 메서드를 제공한다.

---

### [HIGH] GemDropZone._activeMinerGems가 gem이 null일 때 누수

**파일**: `Assets/Project/Scripts/Zone/GemDropZone.cs:165-185`

**문제**:
```csharp
private IEnumerator SpawnMinerGem(GameObject gemPrefab)
{
    // ...
    int index = _activeMinerGems;
    _activeMinerGems++;
    // ...
    yield return StartCoroutine(SpringInGem(gem, originalScale));
    _activeMinerGems--;  // ← SpringInGem에서 gem이 null이면 yield break 후 이 줄이 실행 안 됨
    if (machine != null)
        machine.ReceiveGems(new List<GameObject> { gem });
}

private IEnumerator SpringInGem(GameObject gem, Vector3 originalScale)
{
    while (elapsed < duration)
    {
        if (gem == null) yield break;  // ← 여기서 나가면 _activeMinerGems-- 실행 안 됨
        // ...
    }
}
```
`SpringInGem` 도중 gem이 외부에서 Destroy되면 `_activeMinerGems`가 감소하지 않고 누적된다. 이후 새로운 gem들이 잘못된 인덱스(offset된 위치)에 배치된다.

**수정 방향**:
`try/finally` 패턴을 사용하거나, `_activeMinerGems--`를 `SpawnMinerGem`의 finally 블록으로 이동:
```csharp
private IEnumerator SpawnMinerGem(GameObject gemPrefab)
{
    _activeMinerGems++;
    try
    {
        // ... 기존 코드
    }
    finally { _activeMinerGems--; }
}
```
C# 코루틴에서 try/finally는 yield 앞까지만 보장되므로, 대안으로 gem 완료 후 무조건 카운트 감소:
```csharp
yield return StartCoroutine(SpringInGem(gem, originalScale));
_activeMinerGems = Mathf.Max(0, _activeMinerGems - 1);
// gem이 null이어도 _activeMinerGems 감소 보장
```

---

### [HIGH] MiningTrigger.Update에서 매 프레임 _oresInRange 정리 중 list mutation 위험

**파일**: `Assets/Project/Scripts/Mining/MiningTrigger.cs:97`

**문제**:
```csharp
private void Update()
{
    UpdateMiningState();
    _oresInRange.RemoveAll(o => o == null || !o.gameObject.activeSelf || o.IsDead);
}
```
`UpdateMiningState()`가 `GetClosestOre()`를 통해 `_oresInRange`를 순회하는 중에 Ore가 비활성화될 수 있다. 코루틴으로 광석이 Die하는 시점과 Update 호출 시점이 겹치면 `NullReferenceException` 위험이 있다.

**수정 방향**:
순서를 바꿔 정리 먼저, 상태 업데이트 나중:
```csharp
private void Update()
{
    _oresInRange.RemoveAll(o => o == null || !o.gameObject.activeSelf || o.IsDead);
    UpdateMiningState();
}
```

---

### [MEDIUM] OfficeZone.DrainPending — 무한 루프 잠재적 가능성

**파일**: `Assets/Project/Scripts/Zone/OfficeZone.cs:53-62`

**문제**:
```csharp
private IEnumerator DrainPending(HandcuffCarrier c)
{
    _draining = true;
    while (c.TotalCount > 0)
    {
        yield return null;
        var arrived = c.TakeAll();
        if (arrived.Count > 0) dropZone.Receive(arrived);
    }
    _draining = false;
}
```
`TotalCount = Count + PendingCount`이다. PendingCount는 HandcuffStackZone의 FlyHandcuff 코루틴이 완료되어야 CommitPending으로 감소한다. 만약 FlyHandcuff 코루틴이 어떤 이유로 중단되거나 handcuff가 null이 되면, PendingCount는 영구적으로 > 0이 된다. 이 루프는 매 프레임 실행되며 영원히 종료되지 않는다.

**수정 방향**:
타임아웃 또는 최대 반복 횟수를 추가:
```csharp
private IEnumerator DrainPending(HandcuffCarrier c)
{
    _draining = true;
    float timeout = 5f;
    float elapsed = 0f;
    while (c.TotalCount > 0 && elapsed < timeout)
    {
        yield return null;
        elapsed += Time.deltaTime;
        var arrived = c.TakeAll();
        if (arrived.Count > 0) dropZone.Receive(arrived);
    }
    _draining = false;
}
```

---

### [MEDIUM] ArrestedPerson이 Update에서 transform.position 직접 수정 (물리 비일관성)

**파일**: `Assets/Project/Scripts/Arrested/ArrestedPerson.cs:80-85`

**문제**:
```csharp
// Update에서 직접 position 수정 — 프레임률 의존
transform.position += dir * walkSpeed * Time.deltaTime;
```
다른 NPC(PrisonerController, MinerWorker, AlbaController)는 전부 `Rigidbody.MovePosition`을 `FixedUpdate`에서 호출한다. ArrestedPerson만 Update에서 transform을 직접 수정해 물리 엔진과 충돌할 수 있고, 프레임률에 따라 이동 속도가 달라진다.

**수정 방향**:
`Rigidbody`를 추가하고 `FixedUpdate`로 이동 로직을 이전:
```csharp
private Rigidbody _rb;
private void Awake() { _rb = GetComponent<Rigidbody>(); ... }

private void FixedUpdate()
{
    if (CurrentState != State.Walking) return;
    // 기존 Update 이동 로직을 여기로 이전
}
```
또는 최소한 이동을 FixedUpdate로 옮기고 transform.position 대신 rb.MovePosition 사용.

---

## 2. CLEANUP — 디버그 잔재 제거

### [HIGH] 프로덕션 코드에 남은 Debug.Log 제거

다음 파일들에 디버그용 Debug.Log가 대량으로 남아있다. 이 로그들은 런타임 성능에 영향을 주고 로그 창을 오염시킨다.

| 파일 | 제거할 Debug.Log 수 | 주요 내용 |
|------|---------------------|-----------|
| `DrillCar.cs` | ~8개 | StartDrive, EndDrive, 입력 감지, 구매 확인 |
| `GemCarrier.cs` | 2개 | TryAdd 결과 |
| `OreManager.cs` | 2개 | gemCarrier null 체크, Ore 초기화 수 |
| `MiningTrigger.cs` | 2개 | 광물 감지, UnlockDrill 호출 |
| `DrillUnlock.cs` | 1개 | OnEnable 확인 |
| `DrillCarUnlock.cs` | 2개 | OnEnable, Instance null |
| `PurchaseZone.cs` | 3개 | DelayedActivate 과정 |
| `AlbaController.cs` | 3개 | Initialize, EnterCollectWait |
| `AlbaSpawner.cs` | 4개 | OnEnable, Instantiate, 완료 |
| `ProcessingMachine.cs` | 2개 | ProcessGems 시작, SpawnHandcuff |
| `ArrestedPerson.cs` | 1개 | ThrowCoins 실패 경고 |
| `MinerSpawner.cs` | 1개 | OnEnable 실행 |
| `HandcuffStackZone.cs` 간접 | — | AlbaController Debug.Log |
| `DrillSurface.cs` | 1개 | OnTriggerEnter |
| `JailCheckpoint.cs` | — | — |
| `JailAnimator.cs` | — | — |

**수정 방향**:
1. 조건부 컴파일 방식으로 에디터 전용 로그로 전환:
```csharp
// 변경 전
Debug.Log($"[DrillCar] StartDrive ...");

// 변경 후
#if UNITY_EDITOR
Debug.Log($"[DrillCar] StartDrive ...");
#endif
```
2. 또는 `Debug.Log` 호출을 전부 제거.
3. `Debug.LogWarning`(실제 문제 감지용)은 유지해도 무방.

---

### [MEDIUM] CelebrationEffect에 남은 테스트 입력 코드 제거

**파일**: `Assets/Project/Scripts/UI/CelebrationEffect.cs:43-58`

**문제**:
```csharp
private bool _loaded;

private void Update()
{
    if (Input.GetKeyDown(KeyCode.R))
    {
        _loaded = true;
        Debug.Log("[CelebrationEffect] 장전 완료 — 우클릭으로 발사");
    }
    if (_loaded && Input.GetMouseButtonDown(1))
    {
        _loaded = false;
        Play();
    }
}
```
R키 + 우클릭으로 폭죽을 테스트하는 코드가 그대로 남아있다. 실제 게임에서 우클릭이 다른 용도로 사용되거나 R키가 다른 기능에 할당될 경우 충돌한다.

**수정 방향**:
`#if UNITY_EDITOR` 블록으로 감싸거나 전부 제거:
```csharp
// CelebrationEffect.cs — Update() 전체 제거 또는:
#if UNITY_EDITOR
private bool _loaded;
private void Update()
{
    if (Input.GetKeyDown(KeyCode.R)) { _loaded = true; }
    if (_loaded && Input.GetMouseButtonDown(1)) { _loaded = false; Play(); }
}
#endif
```

---

### [MEDIUM] HUDManager의 logoImage 필드가 사용되지 않음

**파일**: `Assets/Project/Scripts/UI/HUDManager.cs:7`

**문제**:
```csharp
[SerializeField] private Image logoImage;
```
`logoImage`가 선언되어 있지만 HUDManager 어디서도 사용하지 않는다.

**수정 방향**: 해당 필드 제거.

---

## 3. REFACTOR — 구조 개선

### [HIGH] TransferToPlayer와 TransferToAlba가 거의 동일한 코드를 중복

**파일**: `Assets/Project/Scripts/Zone/HandcuffStackZone.cs:120-198`

**문제**:
`TransferToPlayer`(44줄)와 `TransferToAlba`(44줄)가 carrier/anchor 파라미터만 다르고 로직이 완전히 동일하다. 복사-붙여넣기 구조로, 한 쪽에 버그 수정이나 기능 추가 시 다른 쪽도 수동으로 수정해야 한다.

**수정 방향**:
공통 코루틴으로 추출:
```csharp
private IEnumerator TransferHandcuffs(HandcuffCarrier targetCarrier, Transform targetAnchor)
{
    int originalCount = _stackedHandcuffs.Count;
    for (int i = 0; i < originalCount; i++) targetCarrier.ReservePending();

    for (int i = originalCount - 1; i >= 0; i--)
    {
        GameObject handcuff = _stackedHandcuffs[i];
        if (handcuff == null) { targetCarrier.CommitPending(); continue; }
        yield return StartCoroutine(FlyHandcuff(handcuff, targetAnchor));
        Destroy(handcuff);
        targetCarrier.Add(handcuffPrefab);
        targetCarrier.CommitPending();
        yield return new WaitForSeconds(pickupInterval);
    }
    // excess 처리 공통화...
}

private IEnumerator TransferToPlayer() => TransferHandcuffs(carrier, playerAnchor);
private IEnumerator TransferToAlba(HandcuffCarrier albaCarrier, Transform albaAnchor)
    => TransferHandcuffs(albaCarrier, albaAnchor);
```

---

### [HIGH] PurchaseZone의 Prerequisite 조건을 Update에서 매 프레임 폴링

**파일**: `Assets/Project/Scripts/Zone/PurchaseZone.cs:93-100`

**문제**:
```csharp
private void Update()
{
    if (trigger == ActivationTrigger.Prerequisite &&
        !_ready && !_purchased &&
        prerequisite != null && prerequisite.IsPurchased)
    {
        trigger = ActivationTrigger.None;
        OnTriggerConditionMet();
    }
    // ...
}
```
선행 PurchaseZone의 구매 완료를 매 프레임 폴링한다. FirstMoney, JailFull 트리거는 이벤트 구독 방식인데, Prerequisite만 폴링 방식이다. 일관성이 없고 불필요한 매 프레임 체크가 발생한다.

**수정 방향**:
PurchaseZone에 구매 완료 이벤트를 추가하고 구독 방식으로 통일:
```csharp
// PurchaseZone.cs에 추가
public event System.Action OnPurchased;

private void Purchase()
{
    _purchased = true;
    OnPurchased?.Invoke();
    // ...
}

// Prerequisite 구독:
private void Awake()
{
    if (trigger == ActivationTrigger.Prerequisite && prerequisite != null)
        prerequisite.OnPurchased += OnTriggerConditionMet;
    // ...
}
```
Update의 Prerequisite 폴링 블록 제거.

---

### [MEDIUM] GemMaxIndicator와 HandcuffStackMaxIndicator가 BlinkLoop를 완전히 중복

**파일**:
- `Assets/Project/Scripts/UI/GemMaxIndicator.cs:94-127`
- `Assets/Project/Scripts/UI/HandcuffStackMaxIndicator.cs:82-115`

**문제**:
`BlinkLoop` 코루틴이 두 클래스에 일자일획 동일하게 복사되어 있다. 타이밍 파라미터(fadeInDuration, visibleDuration, fadeOutDuration)도 동일한 구조다.

**수정 방향**:
공통 기반 클래스 `MaxIndicatorBase : MonoBehaviour` 추출:
```csharp
public abstract class MaxIndicatorBase : MonoBehaviour
{
    [SerializeField] protected CanvasGroup canvasGroup;
    [SerializeField] protected RectTransform rectTransform;
    [SerializeField] protected float fadeInDuration = 0.3f;
    [SerializeField] protected float visibleDuration = 0.7f;
    [SerializeField] protected float fadeOutDuration = 0.5f;
    [SerializeField] protected float quickFadeOutSpeed = 6f;

    protected bool _isBlinking;
    protected Coroutine _blinkCoroutine;
    protected Camera _cam;

    protected abstract bool IsAtMax();
    protected abstract void UpdatePosition();

    // BlinkLoop, StopBlink, Update 공통 구현
}
```
`GemMaxIndicator`와 `HandcuffStackMaxIndicator`는 `IsAtMax()`와 `UpdatePosition()`만 오버라이드.

---

### [MEDIUM] ArrestedPath와 PrisonerPath가 동일한 구조 중복

**파일**:
- `Assets/Project/Scripts/Arrested/ArrestedPath.cs`
- `Assets/Project/Scripts/Arrested/PrisonerPath.cs`

**문제**:
두 클래스 모두 싱글턴 + 웨이포인트 배열 패턴으로 코드가 거의 동일하다. PrisonerPath에만 ArrivalFaceTarget이 추가된 것 외에 차이가 없다.

**수정 방향**:
`WaypointPath : MonoBehaviour` 공통 기반 클래스 추출:
```csharp
public class WaypointPath : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;
    public Transform[] Waypoints => waypoints;
}
```
PrisonerPath는 이를 상속하고 ArrivalFaceTarget만 추가. ArrestedPath는 WaypointPath를 직접 사용.

---

### [MEDIUM] TutorialManager의 중첩 코루틴 Delay가 가독성을 해침

**파일**: `Assets/Project/Scripts/UI/TutorialManager.cs:96-106`

**문제**:
```csharp
public void OnFirstHandcuffPickup()
{
    if (_step != 4) return;
    _step = 5;
    StartCoroutine(Delay(0.2f, () =>
    {
        Hide(handcuffStackZoneArrow);
        StartCoroutine(Delay(0.1f, () =>    // ← 중첩 코루틴
        {
            _step = 6;
            Show(officeZoneArrow);
        }));
    }));
}
```
중첩 코루틴 람다가 가독성을 떨어뜨린다.

**수정 방향**:
단일 코루틴으로 펼치기:
```csharp
public void OnFirstHandcuffPickup()
{
    if (_step != 4) return;
    _step = 5;
    StartCoroutine(HandcuffPickupSequence());
}

private IEnumerator HandcuffPickupSequence()
{
    yield return new WaitForSeconds(0.2f);
    Hide(handcuffStackZoneArrow);
    yield return new WaitForSeconds(0.1f);
    _step = 6;
    Show(officeZoneArrow);
}
```

---

### [LOW] PlayerWallet이 MoneyCarrier에 직접 의존 (순환 의존 구조)

**파일**: `Assets/Project/Scripts/Player/PlayerWallet.cs:41-42, 55-56`

**문제**:
```csharp
public void Add(int amount)
{
    MoneyCarrier.Instance?.AddMoney(amount);  // ← 직접 의존
    // ...
}

public bool Spend(int amount)
{
    MoneyCarrier.Instance?.SpendMoney(_money);  // ← 직접 의존
    // ...
}
```
PlayerWallet이 MoneyCarrier를 직접 호출하는 것은 역할 경계를 모호하게 만든다. PlayerWallet은 재화 관리, MoneyCarrier는 시각적 표현인데, 재화 관리가 시각 표현을 직접 제어한다.

**수정 방향**:
`OnBalanceChanged` 이벤트에 MoneyCarrier를 구독시키는 방식으로 의존 방향을 역전:
```csharp
// MoneyCarrier.cs
private void Awake()
{
    Instance = this;
    PlayerWallet.OnBalanceChanged += OnBalanceChanged;
}

private void OnBalanceChanged(int newBalance)
{
    // 이전 잔액과 비교해 Add/Spend 판단
}
```
PlayerWallet에서 MoneyCarrier 직접 호출 제거.

---

## 4. OPTIMIZE — 성능 최적화

### [HIGH] MinerWorker.UpdateFindOre가 매 프레임 Ore[] 배열 할당

**파일**: `Assets/Project/Scripts/Mining/MinerWorker.cs:88-94`
**파일**: `Assets/Project/Scripts/Mining/OreManager.cs:28-43`

**문제**:
```csharp
// MinerWorker.Update → UpdateFindOre → SelectTargetOre 에서 매 프레임:
Ore[] ores = OreManager.Instance.GetAliveUnclaimedOres();  // 매 프레임 새 배열 할당
```
`GetAliveUnclaimedOres()`는 매 호출마다 새 `Ore[]` 배열을 생성해 반환한다. 광부 NPC가 여러 명이면 매 프레임 N번 할당이 발생한다.

**수정 방향**:
1. OreManager에 캐시된 배열 제공:
```csharp
// OreManager.cs
private Ore[] _aliveUnclaimedCache;
private int _cacheVersion;

// OreManager.Update 또는 광석 상태 변경 시 캐시 무효화
public Ore[] GetAliveUnclaimedOres()
{
    // 변경이 없으면 캐시 반환, 변경 있으면 재계산
}
```
2. 또는 FindOre 상태에서 매 프레임 대신 간격 탐색(예: 0.2초마다):
```csharp
private float _searchTimer;
private void UpdateFindOre()
{
    _searchTimer -= Time.deltaTime;
    if (_searchTimer > 0f) return;
    _searchTimer = 0.2f;
    // 탐색 실행
}
```

---

### [HIGH] DrillCar.Update에서 매 프레임 Physics.OverlapSphere 호출

**파일**: `Assets/Project/Scripts/Mining/DrillCar.cs:164-175`

**문제**:
```csharp
private void Update()
{
    // ...
    Collider[] hits = Physics.OverlapSphere(detectionCenter, detectionRadius);  // 매 프레임
    foreach (Collider col in hits) { ... }
}
```
DrillCar 운전 중 매 프레임 물리 겹침 검사가 실행된다.

**수정 방향**:
1. OverlapSphere 결과 배열을 재사용하는 `NonAlloc` 버전 사용:
```csharp
private readonly Collider[] _overlapBuffer = new Collider[16];

// Update에서:
int count = Physics.OverlapSphereNonAlloc(detectionCenter, detectionRadius, _overlapBuffer);
for (int i = 0; i < count; i++)
{
    Ore ore = _overlapBuffer[i].GetComponent<Ore>();
    // ...
}
```
2. 검사 간격 줄이기: 매 프레임 대신 FixedUpdate에서 실행.

---

### [MEDIUM] SFXManager.Play3D가 매 호출마다 새 GameObject 생성

**파일**: `Assets/Project/Scripts/UI/SFXManager.cs:119-132`

**문제**:
```csharp
private void Play3D(AudioClip clip, Vector3 position, float volume, float maxDistance)
{
    GameObject go = new GameObject("SFX_3D");  // 매 호출 생성
    go.transform.position = position;
    AudioSource src = go.AddComponent<AudioSource>();
    // ...
    Destroy(go, clip.length + 0.1f);  // 매 호출 소멸
}
```
수갑 낙하, 광부 채굴, 광석 파괴 등 게임 진행 중 빈번하게 호출되어 GC 압박을 준다.

**수정 방향**:
AudioSource 오브젝트 풀 구현:
```csharp
private Queue<AudioSource> _pool3D = new Queue<AudioSource>();

private AudioSource Get3DSource()
{
    if (_pool3D.Count > 0)
    {
        var src = _pool3D.Dequeue();
        src.gameObject.SetActive(true);
        return src;
    }
    var go = new GameObject("SFX_3D_Pooled");
    go.transform.SetParent(transform);
    return go.AddComponent<AudioSource>();
}

private void Return3DSource(AudioSource src)
{
    src.gameObject.SetActive(false);
    _pool3D.Enqueue(src);
}
```

---

### [MEDIUM] PurchaseZone이 매 drainInterval 틱마다 coinFlyPrefab을 Instantiate/Destroy

**파일**: `Assets/Project/Scripts/Zone/PurchaseZone.cs:200-218`

**문제**:
구매 중 `drainInterval`(0.12초)마다 코인 날기 프리팹을 생성하고 0.35초 후 소멸시킨다. 긴 구매 과정에서 반복적인 Instantiate/Destroy가 발생한다.

**수정 방향**:
코인 오브젝트 풀을 SFXManager의 Audio 풀처럼 구현하거나, 최소한 `FlyCoinToZone`에서 완료 시 `SetActive(false)`로 비활성화 후 재사용.

---

### [MEDIUM] JailAnimator.MoveNearbyPrisoners에서 FindObjectsByType 호출

**파일**: `Assets/Project/Scripts/Arrested/JailAnimator.cs:237`

**문제**:
```csharp
PrisonerController[] all = FindObjectsByType<PrisonerController>(FindObjectsSortMode.None);
```
게임 후반부 감옥 확장 시 한 번 호출되므로 큰 문제는 아니지만, `FindObjectsByType`은 씬 전체를 탐색한다.

**수정 방향**:
JailManager에 활성 죄수 목록을 관리하도록 추가:
```csharp
// JailManager.cs
private List<PrisonerController> _prisoners = new List<PrisonerController>();
public void RegisterPrisoner(PrisonerController p) => _prisoners.Add(p);
public void UnregisterPrisoner(PrisonerController p) => _prisoners.Remove(p);
public IReadOnlyList<PrisonerController> Prisoners => _prisoners;
```
JailAnimator는 `JailManager.Instance.Prisoners`를 직접 사용.

---

### [LOW] HandcuffStackZone.GetStackPosition이 매 스폰마다 Vector3 계산 재실행

**파일**: `Assets/Project/Scripts/Zone/HandcuffStackZone.cs:99-102`

사소하지만, `stackBaseOffset`과 `ySpacing` 계산은 스택 크기가 바뀔 때만 의미가 있으므로 캐싱을 고려할 수 있다. 현재 규모에서는 무시해도 무방.

---

## 5. ROBUSTNESS — 견고성 강화

### [HIGH] OfficeZone이 AlbaController의 HandcuffCarrier를 GetComponent로 가져옴

**파일**: `Assets/Project/Scripts/Zone/OfficeZone.cs:26-29`

**문제**:
```csharp
protected override void OnAlbaEnter(AlbaController alba)
{
    _presenceCount++;
    TryDropHandcuffs(alba.GetComponent<HandcuffCarrier>());  // GetComponent 호출
}
```
Alba가 HandcuffCarrier를 가지고 있다는 것을 AlbaController의 public 인터페이스가 노출하지 않는다. GetComponent는 컴포넌트 구조 변경에 취약하다.

**수정 방향**:
AlbaController에 HandcuffCarrier를 노출하는 프로퍼티 추가:
```csharp
// AlbaController.cs
public HandcuffCarrier Carrier => _carrier;
```
OfficeZone에서:
```csharp
TryDropHandcuffs(alba.Carrier);
```

---

### [HIGH] JailCheckpoint._locked가 static으로 한 번 잠기면 영구 해제 불가

**파일**: `Assets/Project/Scripts/Arrested/JailCheckpoint.cs:12-14`

**문제**:
```csharp
private static bool _locked;
public static void LockForVideo() => _locked = true;
```
`_locked`를 해제하는 메서드가 없다. 영상버전에서 한 번 잠기면 에디터 재플레이 시에도 잠긴 상태가 유지된다.

**수정 방향**:
```csharp
public static void LockForVideo() => _locked = true;
public static void UnlockForVideo() => _locked = false;  // 추가

// 또는 OnEnable/OnDestroy에서 리셋
private void OnEnable() => _locked = false;
```

---

### [MEDIUM] PlayerController.Awake에서 카메라 방향 계산 — 카메라 이동 시 오래된 값 사용

**파일**: `Assets/Project/Scripts/Player/PlayerController.cs:33-34`

**문제**:
```csharp
private void Awake()
{
    _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
    _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
}
```
카메라 방향을 Awake에서 한 번만 계산한다. 카메라가 고정된 isometric 게임이라면 문제없지만, CameraFollow가 플레이어를 따라 움직이면 forward/right 방향도 달라질 수 있다.
DrillCar는 이 문제를 인식해 `Update`에서 매 프레임 카메라 방향을 재계산한다.

**수정 방향**:
PlayerController도 Update(또는 FixedUpdate)에서 카메라 방향을 갱신:
```csharp
private void FixedUpdate()
{
    if (_movementLocked) return;
    if (cam != null)
    {
        _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
    }
    Move(GetMoveInput());
    Rotate(GetMoveInput());
}
```
단, 카메라가 완전히 고정(회전 없음)이라면 현재 방식이 더 효율적이다. 실제 카메라 동작 확인 후 결정.

---

### [MEDIUM] HandcuffDropZone._reservedCount와 _stack.Count의 불일치 가능성

**파일**: `Assets/Project/Scripts/Zone/HandcuffDropZone.cs:65-67, 123-127`

**문제**:
`_reservedCount`는 착지 예약(in-flight 포함) 슬롯 수이고, `_stack`은 실제 착지 완료 수다.
`TakeOne()`에서 `_stack`에서 꺼내면서 `_reservedCount--`도 함께 처리하는 것은 맞지만, FlyAndLand 코루틴에서 `handcuff == null`로 yield break 시 `_stack.Add(handcuff)`가 실행되지 않아 `_reservedCount`와 `_stack.Count`가 달라진다.

**수정 방향**:
FlyAndLand에서 실패 시 `_reservedCount`도 감소:
```csharp
private IEnumerator FlyAndLand(GameObject handcuff, Vector3 target, System.Action onLanded = null)
{
    // ...
    while (elapsed < flyDuration)
    {
        if (handcuff == null)
        {
            if (_reservedCount > 0) _reservedCount--;  // 추가
            yield break;
        }
        // ...
    }
}
```

---

### [MEDIUM] MoneyCarrier.SpendMoney가 음수 amount를 처리하지 않음

**파일**: `Assets/Project/Scripts/Player/MoneyCarrier.cs:39-46`

**문제**:
```csharp
public void SpendMoney(int remainingMoney)
{
    int target = remainingMoney / wonPerCoin;
    int toRemove = stackManager.Count - target;
    for (int i = 0; i < toRemove; i++) { ... }
}
```
`remainingMoney`가 음수이면 `target`이 음수 또는 예상치 못한 값이 되어 `toRemove`가 크게 계산될 수 있다. PlayerWallet에서 Spend가 잔액 부족 시 false를 반환하고 호출하지 않으므로 실제로는 발생하지 않지만, 방어 코드 부재.

**수정 방향**:
```csharp
public void SpendMoney(int remainingMoney)
{
    if (remainingMoney < 0) remainingMoney = 0;
    // ...
}
```

---

### [LOW] FlatArrow가 카메라 뒤에 있을 때(z < 0) 위치를 체크하지 않음

**파일**: `Assets/Project/Scripts/UI/FlatArrow.cs:52-68`

**문제**:
GemMaxIndicator는 `screenPos.z < 0` 체크로 카메라 뒤에 있을 때 숨기는 처리를 한다. FlatArrow는 같은 WorldToScreenPoint 방식을 사용하면서 z 체크가 없다. 카메라가 특정 각도에서 플레이어나 타겟이 뒤에 오면 화살표가 반전 위치에 표시될 수 있다.

**수정 방향**:
```csharp
private void Update()
{
    if (_target == null || player == null || _cam == null) return;

    Vector3 playerScreenV3 = _cam.WorldToScreenPoint(player.position);
    Vector3 targetScreenV3 = _cam.WorldToScreenPoint(_target.position);
    if (playerScreenV3.z < 0 || targetScreenV3.z < 0) return;  // 추가

    Vector2 playerScreen = playerScreenV3;
    Vector2 targetScreen = targetScreenV3;
    // 나머지 동일
}
```

---

### [LOW] PurchaseZone의 OnDestroy에서 불필요한 이벤트 해제 발생

**파일**: `Assets/Project/Scripts/Zone/PurchaseZone.cs:66-69`

**문제**:
```csharp
private void OnDestroy()
{
    PlayerWallet.OnFirstMoneyEarned -= OnTriggerConditionMet;
    JailManager.OnJailFull -= OnTriggerConditionMet;
}
```
`FirstMoney` 트리거를 사용하는 PurchaseZone이 `OnJailFull`도 해제하려 한다. 등록하지 않은 이벤트를 해제하는 것은 오류가 나지 않지만 혼란스럽다.

**수정 방향**:
등록 시 기록해 두고 해당 이벤트만 해제:
```csharp
private void Awake()
{
    if (trigger == ActivationTrigger.FirstMoney)
        PlayerWallet.OnFirstMoneyEarned += OnTriggerConditionMet;
    else if (trigger == ActivationTrigger.JailFull)
        JailManager.OnJailFull += OnTriggerConditionMet;
}

private void OnDestroy()
{
    if (trigger == ActivationTrigger.FirstMoney)
        PlayerWallet.OnFirstMoneyEarned -= OnTriggerConditionMet;
    else if (trigger == ActivationTrigger.JailFull)
        JailManager.OnJailFull -= OnTriggerConditionMet;
}
```

---

## 작업 순서 가이드

Claude Code가 이 제안서를 순서대로 작업할 경우 권장 순서:

```
Phase 1 — 버그 수정 (안전하고 독립적인 것 먼저)
  1. [CRITICAL] MiningTrigger Update 순서 수정 (5줄 변경)
  2. [CRITICAL] JailZone double registration 제거 (1줄 제거)
  3. [HIGH] AlbaController.IsHired OnDestroy 추가 (3줄 추가)
  4. [HIGH] GemDropZone._activeMinerGems 누수 수정
  5. [MEDIUM] OfficeZone.DrainPending 타임아웃 추가
  6. [MEDIUM] FlatArrow z < 0 체크 추가
  7. [MEDIUM] HandcuffDropZone FlyAndLand null 처리

Phase 2 — 클린업 (기능 변경 없음)
  8. [HIGH] Debug.Log 조건부 컴파일 또는 제거 (전 파일)
  9. [MEDIUM] CelebrationEffect 테스트 입력 코드 #if EDITOR 처리
  10. [MEDIUM] HUDManager.logoImage 미사용 필드 제거
  11. [LOW] PurchaseZone.OnDestroy 정리

Phase 3 — 리팩토링 (구조 변경, 테스트 필요)
  12. [HIGH] HandcuffStackZone TransferToPlayer/Alba 공통화
  13. [HIGH] PurchaseZone Prerequisite 이벤트 방식으로 전환
  14. [MEDIUM] GemMaxIndicator/HandcuffStackMaxIndicator 기반 클래스 추출
  15. [MEDIUM] TutorialManager 중첩 코루틴 평탄화
  16. [HIGH] OfficeZone AlbaController.Carrier 프로퍼티 사용

Phase 4 — 최적화 (성능 측정 후 필요한 것만)
  17. [HIGH] MinerWorker 탐색 간격 도입 (0.2s)
  18. [HIGH] DrillCar OverlapSphereNonAlloc 전환
  19. [MEDIUM] SFXManager Play3D 오브젝트 풀
  20. [MEDIUM] JailAnimator FindObjectsByType 제거

Phase 5 — CRITICAL 재검토
  21. [CRITICAL] DrillCar.OnEnable IsPurchased = true 제거 여부 확인 후 결정
```

---

## 참고 — 수정하지 말아야 할 것

다음은 코드상 이상해 보이지만 **의도된 설계**이므로 변경하지 말 것:

- `JailCounterUI.countDelay = 3f` — 죄수 보행 애니메이션 타이밍 동기화 의도적 설계
- `TakeMoneyZone._pendingCollect` 플래그 — 비행 중 코인 처리를 위한 필수 플래그
- `HandcuffCarrier.ReservePending/CommitPending` — 이중 차감 방지용 설계
- `SFXManager.PlayEndPanel`의 `_silenced` 무시 — 마지막 시네마틱 후 엔드패널 사운드만 허용하는 의도적 설계
- `GemDropZone.CollectGems`의 1프레임 대기 (`yield return null`) — StackItem Destroy 반영 대기
- `EndPanelController`가 항상 활성화된 부모에 부착 — 비활성 시 Start() 미호출 문제 우회
- `ArrestedPerson.displayCount + 1 = totalNeeded` — 말풍선 UX 설계
