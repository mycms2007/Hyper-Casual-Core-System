# 난관 & 트러블슈팅 기록 — Jailer Life

> 전체 스크립트를 직접 검토하며 코드에 남아있는 흔적을 기반으로 작성.  
> 주석, 플래그, 우회 코드, null 체크, 구조적 결정 등 모든 증거를 근거로 함.

---

## 1. PlayerController

### rb.linearVelocity 컴파일 에러
**증거**: `rb.velocity` 사용 (linearVelocity 없음)  
**원인**: 프로젝트 Unity 버전이 `Rigidbody.linearVelocity` 프로퍼티를 지원하지 않음. 신버전 API.  
**해결**: `rb.velocity` / `rb.angularVelocity` 로 교체.

---

### 시네마틱 중 플레이어가 계속 미끄러지는 문제
**증거**:
```csharp
public void SetMovementLocked(bool locked)
{
    _movementLocked = locked;
    if (locked)
    {
        rb.velocity = Vector3.zero;        // ← 이 두 줄이 없으면 미끄러짐
        rb.angularVelocity = Vector3.zero;
        ChangeState(PlayerState.Idle);
        ApplyAnimator();
    }
}
```
**원인**: 이동 중 입력 잠금을 걸어도 Rigidbody에 이미 붙어있던 velocity가 남아 계속 이동.  
**해결**: locked 시점에 velocity/angularVelocity 강제 0 리셋.

---

### 드릴 장착 중 걷기 애니메이션이 켜지는 문제
**증거**:
```csharp
anim.SetBool("IsWalking", !_forceIdle && _currentState == PlayerState.Walk);
```
**원인**: 광석 옆에서 드릴로 제자리 채굴 중에도 이동 입력이 들어오면 Walk 상태 전환.  
**해결**: `_forceIdle` 별도 플래그 추가. MiningTrigger가 드릴 장착 시 `SetForceIdle(true)` 호출.

---

### Update와 FixedUpdate 양쪽에 이동 잠금 체크
**증거**:
```csharp
private void Update()   { if (_movementLocked) return; ... }
private void FixedUpdate() { if (_movementLocked) return; ... }
```
**원인**: 상태 전환 로직은 Update, 물리 이동은 FixedUpdate에 있어 한쪽만 막으면 불완전.  
**해결**: 두 곳 모두 가드 추가.

---

## 2. MiningTrigger

### DrillCar EndDrive 직후 즉시 재발진 문제
**증거**:
```csharp
[SerializeField] private float drillCarCooldown = 1f;
private bool _drillCarCooldownActive;

public void StartDrillCarCooldown() { StartCoroutine(DrillCarCooldownRoutine()); }

// OnTriggerEnter & UpdateMiningState 진입부
if (DrillCar.Instance != null && DrillCar.Instance.IsDriving) return;
if (_drillCarCooldownActive) return;
```
**원인**: EndDrive로 플레이어가 광석 근처에 복귀하면 MiningTrigger가 즉시 재발진 가능.  
**해결**: DrillCar.EndDrive() 시 1초 쿨다운 플래그. 실제론 광석이 이미 죽어서 발생 안 하지만 안전망.

---

### 드릴카 운전 중에도 플레이어 채굴 시도
**증거**: OnTriggerEnter와 UpdateMiningState 양쪽 모두 `IsDriving` 체크 존재.  
**원인**: 드릴카가 이미 운전 중인데 MiningTrigger가 또 채굴 상태를 시작할 수 있었음.  
**해결**: 두 진입점 모두 `DrillCar.Instance.IsDriving` 가드 추가.

---

## 3. DrillCar

### 드릴카 탑승 중 보석/수갑/돈이 안 보이는 문제
**증거**:
```csharp
// StartDrive()에서 SetVisible(false) 전에 AttachAnchorTo 먼저 실행
if (gemCarrier != null && gemAnchor != null) gemCarrier.AttachAnchorTo(gemAnchor);
if (moneyCarrier != null && moneyAnchor != null) moneyCarrier.AttachAnchorTo(moneyAnchor);
if (handcuffCarrier != null && handcuffAnchor != null) handcuffCarrier.AttachAnchorTo(handcuffAnchor);

if (visual != null) visual.SetActive(true);
if (playerController != null)
{
    playerController.enabled = false;
    playerController.SetVisible(false);  // ← 이게 나중
}
```
**원인**: SetVisible(false)가 `GetComponentsInChildren<Renderer>()`로 플레이어 하위 렌더러를 전부 끔. 앵커가 아직 플레이어 하위에 있으면 보석/수갑/돈 렌더러도 같이 꺼짐.  
**해결**: AttachAnchorTo(드릴카 앵커)를 SetVisible(false) 보다 먼저 실행해 앵커를 먼저 드릴카 하위로 이전.

---

### 드릴카 모델 방향이 이동 방향과 반대
**증거**:
```csharp
[SerializeField] private float modelYaw = -90f;
Quaternion targetRot = Quaternion.LookRotation(-dir) * Quaternion.Euler(0f, modelYaw, 0f);
```
**원인**: 모델 자체가 -Z 방향을 앞으로 보는 구조. LookRotation에 음수 방향 + modelYaw 보정 필요.  
**해결**: 이동 방향에 `-` 부호 + modelYaw 오프셋으로 회전 보정.

---

### 시네마틱 중 드릴카 입력 차단 필요
**증거**:
```csharp
private static bool _inputLocked;
public static void SetInputLocked(bool locked) => _inputLocked = locked;

private void Update() { if (!_isDriving) return; if (_inputLocked) return; ... }
```
**원인**: CinematicDirector가 PlayerController는 인스턴스로 막을 수 있지만, DrillCar는 항상 활성화된 별도 오브젝트라 동일 방식 적용 불가.  
**해결**: static 플래그로 CinematicDirector가 외부에서 직접 제어.

---

### 드릴카 첫 소환 위치/방향 이슈
**증거**:
```csharp
// StartDrive에서 플레이어 바라보는 방향으로 초기 회전 설정
Vector3 playerFwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
if (playerFwd.sqrMagnitude > 0.01f)
    transform.rotation = Quaternion.LookRotation(-playerFwd) * Quaternion.Euler(0f, modelYaw, 0f);
```
**원인**: 소환 시 드릴카가 기본 회전(0,0,0)으로 등장해 플레이어 진행 방향과 무관하게 튀어나옴.  
**해결**: 소환 시점에 플레이어 forward 방향을 XZ 투영해 드릴카 초기 회전으로 설정.

---

## 4. HandcuffCarrier

### 드릴카 앵커 하위에서 수갑이 찌그러지는 문제
**증거**:
```csharp
// Add() 내부
Vector3 w = anchor.lossyScale;
Vector3 p = prefab.transform.localScale;
if (w.x != 0f && w.y != 0f && w.z != 0f)
    obj.transform.localScale = new Vector3(p.x / w.x, p.y / w.y, p.z / w.z);
```
**원인**: 드릴카 모델 계층 구조에 non-1 scale이 있어서 앵커의 lossyScale이 1이 아님. 그대로 Instantiate하면 수갑이 찌그러짐.  
**해결**: `anchor.lossyScale`로 역산해 항상 월드 스케일이 프리팹 원본과 같아지도록 localScale 보정.

---

### 날아오는 중인 수갑이 TotalCount에서 누락
**증거**:
```csharp
private int _pendingCount;
public int TotalCount => _handcuffs.Count + _pendingCount;
public void ReservePending() => _pendingCount++;
public void CommitPending() { if (_pendingCount > 0) _pendingCount--; }
```
**원인**: HandcuffStackZone에서 수갑이 날아오는 도중(아직 Add 전)에 OfficeZone이 TotalCount를 확인하면 0으로 보임. 플레이어가 OfficeZone에 진입한 타이밍에 따라 수갑이 무시됨.  
**해결**: 비행 시작 시 ReservePending(), 실제 Add 완료 시 CommitPending()으로 예약 카운트 관리.

---

## 5. StackManager / StackItem

### 아이템이 쌓일 때 흔들림 방향이 일관되지 않는 문제
**증거**:
```csharp
// StackItem.Tick()
Vector3 localDir = Quaternion.Inverse(baseRot) * moveDir;
Quaternion swayRot = Quaternion.Euler(localDir.z * _swayAmount, 0f, -localDir.x * _swayAmount);
```
**원인**: moveDir이 월드 좌표계이므로 플레이어가 다른 방향을 바라볼 때 sway 방향이 달라짐. "앞으로 가면 뒤로 기울기"가 일관되지 않음.  
**해결**: moveDir을 플레이어 로컬 공간으로 변환(Quaternion.Inverse)한 후 sway 적용.

---

### GemDropZone에서 StackItem 제거 후 즉시 참조하면 오류
**증거**:
```csharp
// GemDropZone.CollectGems()
foreach (GameObject gem in toFly)
{
    StackItem si = gem.GetComponent<StackItem>();
    if (si != null) Destroy(si);
}
yield return null; // ← 1프레임 대기
```
**원인**: `Destroy()`는 즉시 실행되지 않고 프레임 말에 처리됨. 대기 없이 바로 gem을 이동시키면 아직 살아있는 StackItem이 위치를 계속 덮어씀.  
**해결**: Destroy 후 `yield return null`로 1프레임 대기해 Destroy 반영 확인.

---

## 6. GemDropZone

### 플레이어가 보석 투척 중 이탈 시 FlatArrow 화살표 고착
**증거**:
```csharp
// TutorialManager
public void OnPlayerExitedGemDropZone()
{
    if (_flatPhase != 3) return; // Phase 2일 때 이탈하면 이 가드에 걸림
    _flatPhase = 4;
    StartCoroutine(Delay(0.2f, () => flatArrow?.Hide()));
}
```
**원인**: 보석 착지 전(`_flatPhase = 2`) 이탈하면 Phase가 3이 아니므로 Hide() 미실행. 화살표가 화면에 남음.  
**현재 상태**: 미수정. 두 번째 이탈 시 자가 복구. 플레이 중 발생 빈도 낮음.

---

### 드릴카 운전 중 GemDropZone 진입 차단 누락
**증거**:
```csharp
protected override void OnPlayerEnter(PlayerController player)
{
    if (_isCollecting) return;
    if (DrillCar.Instance != null && DrillCar.Instance.IsDriving) return; // ← 명시적 차단
    ...
}
```
**원인**: 드릴카 탑승 중 GemDropZone에 진입하면 playerStack(플레이어 스택)이 비어있어도 컬렉션 루틴이 실행될 수 있음.  
**해결**: OnPlayerEnter에서 `IsDriving` 체크로 드릴카 운전 중 진입 차단.

---

## 7. Ore / OreManager

### 광부가 캔 광석의 보석이 플레이어 등에 쌓이는 문제
**증거**:
```csharp
// Ore.Die()
if (_minerDropZone != null)
    _minerDropZone.AddMinerGem(gemPrefab);   // 광부 전용 경로
else if (_gemCarrier != null)
    _gemCarrier.TryAdd(gemPrefab);           // 플레이어 경로
```
**원인**: 광부가 캔 광석도 GemCarrier(플레이어 등)로 보석이 날아가는 문제.  
**해결**: `TakeDamageByMiner(dropZone)` 호출 시 `_minerDropZone` 저장. Die()에서 분기 처리.

---

### 여러 광부가 같은 광석을 동시에 타겟팅
**증거**:
```csharp
public bool IsClaimed => _isClaimed;
public void Claim()   => _isClaimed = true;
public void Unclaim() => _isClaimed = false;

// OreManager.GetAliveUnclaimedOres()
if (ore != null && !ore.IsDead && !ore.IsClaimed) count++;
```
**원인**: 광부가 광석을 향해 이동 중인데 다른 광부가 같은 광석을 선택해 두 광부가 겹쳐서 같은 광석을 채굴.  
**해결**: Ore에 Claim 시스템 추가. 광부가 타겟 설정 시 Claim(), 해제/비활성화 시 Unclaim().

---

## 8. MinerWorker

### 광부들이 항상 같은 광석으로 몰리는 문제
**증거**:
```csharp
[SerializeField] private float forwardThreshold = 0.7f; // 정면 우선 범위
[SerializeField] private float distEpsilon = 0.05f;     // 거리 동률 오차

// IsBetter: 거리 동률이면 오른쪽 광석 우선
if (candidateDist < currentDist - distEpsilon) return true;
if (candidateDist > currentDist + distEpsilon) return false;
float rCandidate = Vector3.Dot(toCandidate.normalized, right);
float rCurrent   = Vector3.Dot(toCurrent.normalized,   right);
return rCandidate > rCurrent;
```
**원인**: "가장 가까운 광석"만 선택하면 모든 광부가 동일 광석으로 집중됨.  
**해결**: 3단계 우선순위 — 정면 콘 안 광석 우선 → 없으면 가장 가까운 것 → 거리 동률이면 오른쪽 우선.

---

### 광부가 광석을 빼앗긴 후 멈추는 문제
**증거**:
```csharp
private void UpdateMoveToOre()
{
    if (_targetOre == null || _targetOre.IsDead)
    {
        ReleaseTarget();
        EnterPause(); // 뺏김 → Pause(1s) → 재탐색
        return;
    }
}
```
**원인**: 이동 중 플레이어/드릴카가 타겟 광석을 먼저 파괴하면 광부가 목적지를 잃고 멈춤.  
**해결**: `UpdateMoveToOre`와 `UpdateMining` 양쪽에서 `IsDead` 체크 → `EnterPause()` → 재탐색.

---

### 애니메이션 이벤트가 MinerWorker에 직접 연결 불가
**증거**: `MinerAnimEvent.cs` 별도 파일 존재.
```csharp
// MinerAnimEvent — 애니메이터가 있는 자식 오브젝트에 부착
public void OnMiningHit()
{
    _miner?.GetComponentInParent<MinerWorker>()... // 아니라 Awake에서 캐싱
    _miner?.OnMiningHit();
}
```
**원인**: 애니메이터가 자식 오브젝트에 있어서 Animation Event가 MinerWorker(부모)를 직접 찾지 못함.  
**해결**: `MinerAnimEvent`를 애니메이터가 있는 자식에 부착하고, Awake에서 `GetComponentInParent<MinerWorker>()`로 참조 캐싱.

---

## 9. ProcessingMachine / ItemTransZone

### 수갑이 도착하자마자 HandcuffStackZone에 개별 통보하면 스폰이 난잡
**증거**:
```csharp
// ItemTransZone — 모든 수갑 소멸 완료 후 한 번에 통보
if (_pendingCount <= 0)
{
    handcuffStackZone.SpawnHandcuffs(_totalCount);
    _totalCount = 0;
}
```
**원인**: ProcessingMachine이 보석 하나씩 순서대로 처리하므로 수갑이 하나씩 산발적으로 도착. 도착할 때마다 SpawnHandcuffs를 호출하면 스폰이 끊겨 보임.  
**해결**: `_pendingCount`로 전체 수갑 소멸 완료를 추적하고, 0이 될 때 `_totalCount`를 한 번에 전달.

---

## 10. HandcuffDropZone

### 여러 배치가 동시에 도착하면 스택 순서 오류
**증거**:
```csharp
private readonly Queue<List<GameObject>> _receiveQueue = new Queue<List<GameObject>>();
private bool _isReceiving;

public void Receive(List<GameObject> handcuffs)
{
    _receiveQueue.Enqueue(new List<GameObject>(handcuffs));
    if (!_isReceiving) StartCoroutine(ProcessQueue());
}
```
**원인**: 플레이어와 알바가 동시에 OfficeZone에 진입하거나 빠르게 연속 전달 시 배치가 겹쳐 스택 위치 계산 오류.  
**해결**: Queue로 배치를 순서대로 쌓고 하나씩 처리.

---

### 날아오는 수갑의 착지 슬롯 중복 예약 문제
**증거**:
```csharp
private int _reservedCount; // 착지 포함 in-flight 슬롯 예약 수

int slot = _reservedCount++;
Vector3 target = GetStackPosition(slot);
StartCoroutine(FlyAndLand(handcuff, target, () => _stack.Add(handcuff)));
```
**원인**: `_stack.Count`로 착지 위치를 계산하면 아직 비행 중인 수갑들이 Count에 반영 안 돼서 같은 위치에 겹침.  
**해결**: `_reservedCount`를 비행 시작 시점에 미리 증가시켜 위치 선점.

---

## 11. OfficeZone

### 플레이어가 OfficeZone 진입 시 아직 날아오는 수갑이 무시되는 문제
**증거**:
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
**원인**: `TakeAll()`을 호출하면 현재 `_handcuffs` 리스트만 꺼냄. PendingCount(비행 중)는 도착하기 전이라 포함되지 않음.  
**해결**: `TotalCount > 0` 동안 루프를 돌며 매 프레임 도착 분을 추가로 흘려보내는 DrainPending 코루틴.

---

## 12. ArrestedPerson

### 말풍선 표시 수(displayCount)와 실제 필요 수가 다른 문제
**증거**:
```csharp
[SerializeField] private int displayCount = 2;
private void Awake() { _totalNeeded = displayCount + 1; }
```
**원인**: 말풍선에 "x2" 표시되어도 실제로는 3개 받아야 완료되는 디자인. 말풍선이 0이 된 후에도 1개 더 받아야 함.  
**해결**: `_totalNeeded = displayCount + 1` 로 항상 1개 더 요구. 의도된 설계.

---

### 앞사람이 Done 상태인데도 줄 서기 블로킹
**증거**:
```csharp
private bool IsBlockedByPersonAhead()
{
    if (PersonAhead == null || PersonAhead.CurrentState == State.Done) return false;
    ...
}
```
**원인**: 앞사람이 변신 완료(Done)되어 SetActive(false) 대기 중인데 거리 체크가 여전히 작동해 뒷사람이 멈춤.  
**해결**: `State.Done`이면 PersonAhead를 무시하고 이동 계속.

---

### 코인 개수와 보상 금액 불일치 설계 혼선
**증거**:
```csharp
private IEnumerator ThrowCoins()
{
    for (int i = 0; i < coinsPerLayer; i++) // 6개 투척
    {
        takeMoneyZone.LaunchCoin(coin, spawnPos, coinFlyDuration);
        yield return new WaitForSeconds(coinInterval);
    }
    takeMoneyZone.AddMoney(rewardAmount); // 마지막에 금액 등록
}
```
**원인**: 코인 오브젝트 수(시각)와 실제 보상 금액(논리)을 분리하지 않으면 코인 6개 = 6×wonPerCoin 원이라는 가정이 생김.  
**해결**: `LaunchCoin()`은 순수 시각 연출, `AddMoney()`로 금액 별도 등록. 완전히 분리.

---

## 13. PrisonerController

### 죄수가 경로에 막혀 영원히 도착하지 못하는 문제
**증거**:
```csharp
private float _arrivalTimer;
[SerializeField] private float arrivalTimeout = 10f;

if (!_arrived && IsOnFinalTarget())
{
    _arrivalTimer += Time.fixedDeltaTime;
    if (_arrivalTimer >= arrivalTimeout)
        OnArrived(); // 강제 도착 처리
}
```
**원인**: 지형이나 다른 죄수에 막혀 arrivalDistance 안으로 진입 못하면 영원히 도착 처리가 안 됨.  
**해결**: 최종 목적지를 향해 이동 중일 때 10초 타임아웃 → 강제 도착 처리.

---

### 감옥 확장 후 죄수를 BigPrison으로 재이동 시 이중 등록
**증거**:
```csharp
private bool _skipJailRegistration;

public void MoveToDestination(Transform destination)
{
    ...
    _skipJailRegistration = true; // 재이동 시 JailManager 등록 건너뜀
}

private void OnArrived()
{
    if (!_skipJailRegistration)
        JailManager.Instance?.RegisterArrived();
}
```
**원인**: 이미 감옥에 수감된 죄수를 BigPrison으로 다시 이동시킬 때 OnArrived()에서 RegisterArrived()가 다시 호출되면 `_inJailCount`가 이중으로 증가.  
**해결**: `_skipJailRegistration` 플래그로 재이동 시 카운트 등록 스킵.

---

### 신규 죄수가 확장 전 소형 감옥으로 계속 이동
**증거**:
```csharp
// PrisonerController.SetDestination()
Transform effective = JailManager.CurrentJailDestination ?? destination;
```
**원인**: 확장 후 새로 변신한 죄수가 여전히 원래 jailDestination(소형 감옥)을 목적지로 삼음.  
**해결**: `JailManager.CurrentJailDestination`이 설정되어 있으면 그것을 우선 사용. JailExpander.OnEnable()에서 `SetJailDestination(newDest)` 호출로 교체.

---

### 모든 죄수가 정확히 같은 지점에 집중되는 문제
**증거**:
```csharp
Vector2 rand = Random.insideUnitCircle * scatterRadius;
_finalTarget = effective.position + new Vector3(rand.x, 0f, rand.y);
```
**원인**: scatterRadius 없으면 모든 죄수의 목적지가 정확히 동일 좌표.  
**해결**: `scatterRadius` 범위 내 랜덤 오프셋으로 자연스럽게 분산.

---

## 14. JailManager

### 복도 이동 중 죄수를 무시한 IsFull 판정으로 초과 수용
**증거**:
```csharp
private int _walkingCount; // 체크포인트 통과 ~ 감옥 도착 전
public bool IsFull => TotalCount >= capacity; // _inJailCount + _walkingCount
```
**원인**: `_inJailCount`만으로 IsFull을 판정하면 복도 이동 중인 죄수가 무시되어 capacity를 초과해서 진입.  
**해결**: `TotalCount = _inJailCount + _walkingCount` 합산으로 IsFull 판정.

---

### OnJailFull 이벤트 중복 발행
**증거**:
```csharp
private bool _jailFullFired;

if (!_jailFullFired && _inJailCount >= capacity)
{
    _jailFullFired = true;
    OnJailFull?.Invoke();
}
```
**원인**: 죄수가 1명씩 도착할 때마다 RegisterArrived()가 호출되므로, capacity 달성 이후에도 계속 이벤트 발행 가능.  
**해결**: `_jailFullFired` 플래그로 최초 1회만 발행. 확장 시 `_jailFullFired = false` 리셋.

---

## 15. JailCounterUI

### 카운트 표시가 실제 입장보다 빠르게 올라가는 문제
**증거**:
```csharp
private IEnumerator DelayedIncrement()
{
    yield return new WaitForSeconds(countDelay); // 3f
    _displayCount++;
    UpdateText();
}
```
**원인**: 체크포인트를 통과하자마자 카운트가 올라가면 죄수가 감옥에 들어가는 걸어가는 연출과 타이밍이 맞지 않음.  
**해결**: 3초 딜레이로 실제 걸어 들어오는 시간과 표시 타이밍 동기화. **의도된 설계, 절대 제거 금지.**

---

### OnDisplayFull vs OnJailFull 이벤트 분리 이유
**증거**: 두 이벤트가 별개로 존재.  
- `JailCounterUI.OnDisplayFull` — 표시 카운트 기준 (3초 딜레이 반영)  
- `JailManager.OnJailFull` — 실제 수감 카운트 기준 (즉시)  

**원인**: 영상 연출(드릴존 카메라)은 표시 카운트 기준으로 타이밍을 잡아야 하고, 게임 로직(확장존 등장)은 실제 수감 기준으로 동작해야 함.  
**해결**: 두 이벤트를 분리해 각자 구독처가 적절한 것을 사용.

---

## 16. JailAnimator

### 확장 전 씬에서 침대를 보이지 않게 숨기는 방법
**증거**:
```csharp
// Start()에서
_bedTargetScales[i] = bedsParent.GetChild(i).localScale; // 원본 스케일 저장
bedsParent.GetChild(i).localScale = Vector3.zero;        // 즉시 숨김
```
**원인**: Inspector에서 침대를 비활성화하면 스프링 애니메이션 타겟 스케일을 Start()에서 읽을 수 없음.  
**해결**: 씬에 active 상태로 두되 Start()에서 원본 스케일 저장 → 즉시 scale 0으로 숨김.

---

### 영상버전에서 확장 연출 중 신규 죄수 진입 차단
**증거**:
```csharp
// JailCheckpoint
private static bool _locked;
public static void LockForVideo() => _locked = true;

// JailAnimator.VideoExpansion()
JailCheckpoint.LockForVideo(); // ← 연출 시작 직후 호출
```
**원인**: 감옥 확장 연출 중 새 죄수가 체크포인트를 통과하면 연출이 깨짐.  
**해결**: 영상버전 연출 시작과 동시에 체크포인트를 정적 플래그로 영구 잠금.

---

## 17. TakeMoneyZone

### 코인이 무한정 쌓여 성능 저하 및 시각적 혼잡
**증거**:
```csharp
[SerializeField] private int maxCapacity = 60; // 최대 60개 = 150원

public void LaunchCoin(GameObject coin, Vector3 from, float flyDuration)
{
    if (_reservedCount >= maxCapacity) { Destroy(coin); return; }
    ...
}
```
**원인**: 체포자가 계속 코인을 던지면 TakeMoneyZone에 코인이 무한 누적. 최대 150원(60코인) 이상 쌓이면 시각적으로도 이상.  
**해결**: maxCapacity = 60 초과 시 즉시 Destroy.

---

### 플레이어가 수거 중 새 코인이 날아와 수거 대상에서 누락
**증거**:
```csharp
private bool _pendingCollect;

// FlyCoin 내부
if (_pendingCollect) { StartCoroutine(ShrinkAndDestroy(coin)); yield break; }

// LandCoin 내부
if (_pendingCollect)
    StartCoroutine(ShrinkAndDestroy(coin));
else
    _stack.Add(coin);
```
**원인**: CollectCoins()가 실행 중인데 새 코인이 날아와 착지하면 이미 처리된 배치에서 누락.  
**해결**: `_pendingCollect` 플래그 — 수거 중에 착지하는 코인은 ShrinkAndDestroy로 즉시 소멸.

---

## 18. SFXManager

### AudioSource 볼륨을 1 이상으로 설정해도 효과 없는 문제
**증거**: 볼륨 필드가 모두 `[Range 없이] private float xxxVolume = 1f`로 선언.  
**원인**: `AudioSource.volume`은 0~1 범위로 클램핑됨. 7, 10으로 설정해도 1과 동일.  
`PlayOneShot(clip, volumeScale)`의 volumeScale도 내부적으로 0~1 적용.  
**해결**: 볼륨은 0~1 범위로만 조정. 각 효과음별 독립 볼륨 필드를 인스펙터에 노출.

---

### 수갑 3D 소리가 전혀 들리지 않는 문제
**증거**:
```csharp
src.rolloffMode = AudioRolloffMode.Logarithmic; // ← Linear에서 변경
src.maxDistance = maxDistance; // handcuffStack: 30f (초기 15f에서 변경)
src.minDistance = 1f;
```
**원인 1**: `AudioRolloffMode.Linear`는 maxDistance 경계에서 볼륨이 정확히 0이 됨. 카메라가 Zone 위 높이에서 내려다보는 구조라 수평 거리 + 높이 합산 시 maxDistance 초과.  
**원인 2**: maxDistance가 15f로 너무 작았음.  
**해결**: Logarithmic 롤오프로 변경 + maxDistance를 30f로 확대.

---

### 시네마틱 중 효과음이 계속 재생되는 문제
**증거**:
```csharp
private bool _silenced;
// 모든 Play 메서드 첫 줄
if (_silenced || drillingClip == null) return;
```
**원인**: 카메라 연출 중에도 광부/드릴링 소리가 계속 재생됨.  
**해결**: `_silenced` 전역 플래그. GlobalFadeOut()에서 `_silenced = true` 설정, GlobalFadeIn()에서 해제.

---

### 마지막 시네마틱 후 EndPanel 소리가 나지 않는 문제
**증거**:
```csharp
public void PlayEndPanel()
{
    StartCoroutine(DelayedPlay(endPanelClip, endPanelDelay));
}

private IEnumerator DelayedPlay(AudioClip clip, float delay)
{
    if (clip == null) yield break;
    yield return new WaitForSeconds(delay);
    _sfxSource.volume = 1f; // ← _silenced 체크 없이 강제 1f 복원
    _sfxSource.PlayOneShot(clip, endPanelVolume);
}
```
**원인**: 마지막 시네마틱 후 GlobalFadeIn을 호출하지 않아 `_sfxSource.volume`이 0인 상태. `_silenced = true`도 유지.  
**해결**: `PlayEndPanel`은 `_silenced` 체크 우회 + `_sfxSource.volume`을 1f로 강제 복원 후 재생.

---

## 19. EndPanelController

### 등장 애니메이션이 전혀 동작하지 않는 문제
**증거**:
```csharp
// Start()에서
_titleOriginal  = title.localScale;  // 비활성 상태의 자식도 접근 가능
_iconOriginal   = appIcon.localScale;
_buttonOriginal = continueButton.localScale;
appIcon.localScale        = Vector3.zero;  // 등장 시작 상태로 세팅
continueButton.localScale = Vector3.zero;
```
**원인**: EndPanelController가 EndPanel 오브젝트 자체에 부착되어 있으면, 런타임 전 비활성화 상태에서 Start()가 호출되지 않음 → 원본 스케일 저장 실패 → OpenPanel() 시 애니메이션 오작동.  
**해결**: EndPanelController를 **항상 활성화된 부모 오브젝트**에 이전. Start()는 항상 실행되고, 비활성인 자식의 Transform에는 접근 가능하므로 스케일 저장 정상 동작.

---

## 20. PurchaseZone

### 구매 완료 효과음이 이미 완료된 후 재생되는 문제
**증거**:
```csharp
[SerializeField] private int soundTriggerOffset = 5;

if (!_soundPlayed && _paidAmount >= price - soundTriggerOffset)
{
    _soundPlayed = true;
    SFXManager.Instance?.PlayZonePurchase();
}
```
**원인**: `_paidAmount >= price`(완료 시점)에 재생하면 구매 완료 → 존 사라짐 → 효과음 재생 순서로 타이밍이 어색.  
**해결**: 완료 5원 전에 미리 재생. 연출과 소리 타이밍이 맞춰짐.

---

### zoneVisual이 없는 존에서 스프링 애니메이션 누락
**증거**:
```csharp
private bool _usingSelf;

if (zoneVisual != null) { _visualTarget = zoneVisual.transform; _usingSelf = false; }
else                    { _visualTarget = transform;            _usingSelf = true;  }
```
**원인**: zoneVisual을 인스펙터에서 연결하지 않으면 AnimationTarget이 null → NullReference.  
**해결**: zoneVisual 없으면 자기 자신(transform)을 타겟으로. `_usingSelf` 플래그로 구매 후 SetActive(false) 대신 scale 0 유지.

---

## 21. GemMaxIndicator

### 드릴카 운전 중 MAX 표시가 플레이어 위치에 남는 문제
**증거**:
```csharp
bool isDriving = DrillCar.Instance != null && DrillCar.Instance.IsDriving;
Transform tracked = isDriving ? DrillCar.Instance.transform : playerTransform;
float offset      = isDriving ? drillCarHeightOffset : playerHeightOffset;
```
**원인**: 드릴카 탑승 시 플레이어가 숨겨지지만 GemMaxIndicator는 여전히 playerTransform 추적 → 플레이어가 있던 자리에 MAX 표시가 떠있음.  
**해결**: `IsDriving` 확인 후 드릴카를 추적 대상으로 교체.

---

### 카메라 뒤에서 WorldToScreenPoint 좌표가 반전되는 문제
**증거**:
```csharp
if (screenPos.z < 0f)
{
    canvasGroup.alpha = 0f;
    return;
}
```
**원인**: 추적 대상이 카메라 뒤에 있으면 `WorldToScreenPoint`의 z가 음수가 되고 x/y 좌표가 반전되어 화면 반대편에 UI가 표시됨.  
**해결**: `screenPos.z < 0` 체크 후 alpha = 0으로 즉시 숨김.

---

## 22. AlbaController

### 알바가 수갑 존에 도착해도 픽업을 안 하는 문제
**증거**:
```csharp
// AlbaController.Initialize() 필수
public void Initialize(HandcuffStackZone zone, Transform idlePos)
{
    handcuffStackZone = zone;
    idlePosition      = idlePos;
}
```
그리고 AlbaSpawner.OnEnable()에서 Initialize를 명시적으로 호출.
```csharp
AlbaController alba = albaObj.GetComponent<AlbaController>();
if (alba != null) alba.Initialize(handcuffStackZone, idlePosition);
```
**원인**: `handcuffStackZone`이 null이면 UpdateSearch()에서 탐색 자체가 작동 안 함. 프리팹에 인스펙터 연결이 불가능한 씬 레퍼런스이므로 반드시 런타임 주입 필요.  
**해결**: AlbaSpawner가 소환 직후 Initialize()로 씬 참조 주입. UpdateSearch()에서도 null 체크 + 경고 로그.

---

### 플레이어와 알바의 수갑 수거 충돌
**증거**:
```csharp
// HandcuffStackZone.TryTransfer()
if (AlbaController.IsHired) return; // 알바 고용 시 플레이어 수거 차단
```
**원인**: 플레이어와 알바가 동시에 HandcuffStackZone에서 수갑을 가져가면 동일 수갑이 두 번 처리됨.  
**해결**: `AlbaController.IsHired` 정적 플래그. 알바가 고용된 상태면 플레이어 경로를 완전 차단.

---

## 23. StackItem (흔들림 방향 버그)

**증거**:
```csharp
// 월드 방향을 플레이어 로컬로 변환 후 sway 적용
Vector3 localDir = Quaternion.Inverse(baseRot) * moveDir;
Quaternion swayRot = Quaternion.Euler(localDir.z * _swayAmount, 0f, -localDir.x * _swayAmount);
```
**원인**: moveDir이 월드 좌표계이면 플레이어가 남쪽을 바라볼 때와 북쪽을 바라볼 때 sway 방향이 반대가 됨. "전진하면 뒤로 기울기"가 방향에 따라 깨짐.  
**해결**: baseRot의 역행렬로 moveDir을 로컬로 변환. 플레이어 방향에 무관하게 항상 일관된 sway 방향.

---

## 24. CameraFollow

### 카메라가 플레이어보다 늦게 반응해서 흔들리는 문제
**증거**:
```csharp
private void LateUpdate() // ← Update 아님
{
    if (target != null) _goalPos = target.position + offset;
    transform.position = Vector3.Lerp(transform.position, _goalPos, smoothSpeed * Time.deltaTime);
}
```
**원인**: 카메라를 Update에서 처리하면 물리 이동(FixedUpdate)이 완료되기 전 또는 후의 위치를 읽을 수 있어 1프레임 지연 발생.  
**해결**: LateUpdate에서 처리 — 해당 프레임의 모든 Update/FixedUpdate가 끝난 후 카메라 위치 계산.

---

## 요약 — 난관 분류

| 분류 | 사례 수 |
|------|---------|
| Unity API 버전/제약 | rb.linearVelocity, AudioSource volume 클램핑 |
| Rigidbody 물리 타이밍 | velocity 잔존 슬라이딩, LateUpdate 카메라 |
| null 참조 / 초기화 순서 | EndPanel Start() 미호출, Alba Initialize() 누락 |
| 동시성 / 레이스 컨디션 | FlatArrow 고착, Pending 수갑 누락, 코인 수거 중 신규 코인 |
| 좌표계 변환 | StackItem sway, DrillCar 방향, FlatArrow 회전 |
| 카운팅 / 상태 중복 | JailFull 중복 발행, 죄수 이중 등록, reservedCount |
| 3D 오디오 설정 | Logarithmic vs Linear, maxDistance, z<0 UI 숨김 |
| 계층 구조 / 스케일 | lossyScale 보정, SetVisible 순서, bedTargetScales |
