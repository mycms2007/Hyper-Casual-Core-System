# 캐릭터 라이프사이클 — 체포자 → 죄수 → 수감 — Jailer Life

> 체포자가 생성되어 줄을 서고, 수갑을 받아 죄수로 변신하고,  
> 감옥에 수감되기까지의 전체 흐름과 감옥 수용 인원 관리.

---

## 전체 생애 흐름

```
ArrestedSpawner
  │ Instantiate (랜덤 프리팹)
  ▼
ArrestedPerson [State: Walking]
  │ 웨이포인트 이동 + 줄 서기
  ▼
HandcuffReceiveZone 트리거 진입
  │ OnReachedZone()
  ▼
ArrestedPerson [State: Waiting]
  │ 말풍선 표시 / 수갑 수령 대기
  │ OfficeManager가 수갑 하나씩 전송
  ▼
ReceiveHandcuff() × totalNeeded
  │ 모두 수령 완료
  ▼
CompleteQuest()
  │ 파티클 → 렌더러 off → PrisonerController 소환
  │ ThrowCoins() → TakeMoneyZone
  ▼
ArrestedPerson [State: Done] → SetActive(false)

PrisonerController
  │ PrisonerPath 웨이포인트 이동
  │ JailCheckpoint 통과 판정
  ▼
감옥 도착 → JailManager.RegisterArrived()
  │ 자리에 서서 ArrivalFaceTarget 바라봄
  ▼
수감 완료
```

---

## 1. ArrestedSpawner — 체포자 생성

### 초기 스폰

게임 시작 시 `initialSpawnCount`(3명)을 `initialSpawnInterval`(1초) 간격으로 순차 소환.  
이후 한 명이 Done 되면 바로 다음 한 명 소환 — 항상 `maxCount`(5명) 유지.

```csharp
private IEnumerator InitialSpawn()
{
    for (int i = 0; i < count; i++)
    {
        SpawnNext();
        yield return new WaitForSeconds(initialSpawnInterval);
    }
}
```

### 줄 서기 체인 구성

새로 소환할 때 큐의 마지막 사람을 `PersonAhead`로 지정.  
한 명이 Done 되면 큐 재정렬 → PersonAhead 체인 재연결.

```csharp
person.PersonAhead = _queue.Count > 0 ? _queue[_queue.Count - 1] : null;

// OnPersonTransformed 후 재정렬
for (int i = 0; i < _queue.Count; i++)
    _queue[i].PersonAhead = i > 0 ? _queue[i - 1] : null;
```

### 초기화 주입

소환 시 ArrestedPerson에 세 가지를 주입:
- `HandcuffReceiveZone` — 수갑 수령 위치
- `jailDestination` — 변신 후 죄수가 향할 목적지
- `TakeMoneyZone` — 코인 투척 대상

---

## 2. ArrestedPerson — 이동 (Walking 상태)

### 웨이포인트 이동

ArrestedPath 싱글턴에서 공유 웨이포인트 배열을 받아 순서대로 이동.  
Y축은 무시하고 XZ 평면 기준 거리로 도착 판정.

```csharp
Vector3 flatSelf   = new Vector3(transform.position.x, 0f, transform.position.z);
Vector3 flatTarget = new Vector3(target.x,             0f, target.z);
float dist = Vector3.Distance(flatSelf, flatTarget);
```

마지막 웨이포인트 도달 후 트리거 진입 대기 → `IsWalking = false`.

### 줄 서기 (간격 유지)

앞사람과의 거리를 매 프레임 체크. GapDistance 이내이면 이동 정지.  
앞사람이 Done 상태면 무시하고 계속 이동.

```csharp
private bool IsBlockedByPersonAhead()
{
    if (PersonAhead == null || PersonAhead.CurrentState == State.Done) return false;
    float dx = PersonAhead.transform.position.x - transform.position.x;
    float dz = PersonAhead.transform.position.z - transform.position.z;
    return dx * dx + dz * dz <= GapDistance * GapDistance;
}
```

> 제곱 비교로 `Mathf.Sqrt` 호출 없이 거리 판정 — 매 프레임 호출되므로 최적화.

---

## 3. HandcuffReceiveZone — 대기 & 말풍선

체포자가 Trigger에 진입하면 점유 등록 → ArrestedPerson에 통보.

```csharp
private void OnTriggerEnter(Collider other)
{
    if (_currentOccupant != null) return; // 이미 점유 중이면 무시
    ArrestedPerson person = other.GetComponentInParent<ArrestedPerson>();
    _currentOccupant = person;
    person.OnReachedZone(this);
    ShowBubble(person.DisplayCount, person.TotalNeeded);
}
```

### 말풍선 월드→스크린 변환

Canvas가 Screen Space - Overlay이므로 WorldToScreenPoint로 위치를 매 프레임 갱신.

```csharp
Vector3 worldPos  = _currentOccupant.transform.position + Vector3.up * bubbleHeightOffset;
Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
speechBubbleRect.position = screenPos;
```

### 진행 바 (fillImage)

목표치 세팅 후 Lerp로 부드럽게 채움 — 즉각적인 점프 없음.

```csharp
_targetFill = (float)received / totalNeeded;
fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, _targetFill, fillLerpSpeed * Time.deltaTime);
```

---

## 4. ArrestedPerson — 수갑 수령 (Waiting 상태)

### displayCount vs totalNeeded

```
displayCount  = 2  (말풍선에 "x2" 표시)
totalNeeded   = displayCount + 1 = 3  (실제로 3개 필요)
```

항상 1개 더 요구하는 디자인 — 말풍선 숫자가 줄어들다 0이 된 뒤에도 1개 더 받아야 완료.

```csharp
_totalNeeded = displayCount + 1; // Awake에서 설정
public bool NeedsMoreHandcuffs => CurrentState == State.Waiting && _receivedCount < _totalNeeded;
```

### 수령 처리

```csharp
public void ReceiveHandcuff(GameObject handcuff)
{
    if (CurrentState != State.Waiting) return;
    Destroy(handcuff);
    _receivedCount++;
    _zone?.UpdateBubble(_receivedCount, _totalNeeded, displayCount);
    if (_receivedCount >= _totalNeeded)
        StartCoroutine(CompleteQuest());
}
```

---

## 5. CompleteQuest — 변신 & 코인 투척

### 변신 시퀀스

```
1. State = Done
2. 파티클 이펙트 생성 (3초 후 자동 소멸)
3. 0.3s 대기
4. prisonerPrefabs 중 랜덤 하나 소환
   → PrisonerController.SetDestination(jailDestination)
5. 자신의 모든 Renderer 비활성화
6. HandcuffReceiveZone 점유 해제 (_currentOccupant = null)
7. OnTransformed 이벤트 발행 → ArrestedSpawner가 다음 소환
8. ThrowCoins() 코루틴
9. gameObject.SetActive(false)
```

### ThrowCoins

코인을 coinsPerLayer(6)개 coinInterval(0.08s) 간격으로 순차 투척.  
전부 투척 완료 후 `takeMoneyZone.AddMoney(rewardAmount)` — **코인 개수와 금액은 분리**.

```csharp
private IEnumerator ThrowCoins()
{
    Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
    for (int i = 0; i < coinsPerLayer; i++)
    {
        GameObject coin = Instantiate(coinPrefab, spawnPos, Quaternion.identity);
        takeMoneyZone.LaunchCoin(coin, spawnPos, coinFlyDuration);
        yield return new WaitForSeconds(coinInterval);
    }
    takeMoneyZone.AddMoney(rewardAmount); // 보상 금액 등록은 마지막에 한 번
}
```

---

## 6. PrisonerController — 감옥 이동

### SetDestination

목적지를 `JailManager.CurrentJailDestination`으로 오버라이드 — 감옥이 확장되었다면 새 목적지로 자동 향함.  
`scatterRadius` 범위 내 랜덤 오프셋 → 모든 죄수가 정확히 같은 점에 몰리지 않음.

```csharp
public void SetDestination(Transform destination)
{
    Transform effective = JailManager.CurrentJailDestination ?? destination;
    Vector2 rand = Random.insideUnitCircle * scatterRadius;
    _finalTarget = effective.position + new Vector3(rand.x, 0f, rand.y);
    _waypoints = PrisonerPath.Instance?.Waypoints;
    ...
}
```

### 웨이포인트 → 최종 목적지

PrisonerPath 웨이포인트를 순서대로 통과한 뒤 `_finalTarget`으로 이동.  
웨이포인트를 다 소진하면 직선 이동.

### JailCheckpoint 통과

이동 중 JailCheckpoint Trigger에 진입하면:
- 감옥이 가득 참 → `Pause()` → 대기 큐에 추가
- 여유 있음 → `PassThrough()` → `JailManager.RegisterWalking()`

### 도착 처리

```csharp
private void OnArrived()
{
    _arrived = true;
    _anim?.SetBool("IsWalking", false);
    if (!_skipJailRegistration)
        JailManager.Instance?.RegisterArrived();

    // ArrivalFaceTarget 방향 바라보기
    Vector3 dir = faceTarget.position - transform.position;
    dir.y = 0f;
    transform.rotation = Quaternion.LookRotation(dir.normalized);
}
```

**arrivalTimeout(10s)** — 지형에 걸리거나 경로가 막혀도 10초 후 강제 도착 처리.

---

## 7. JailManager — 수용 인원 관리

### 카운팅 구조

```
_walkingCount  — 체크포인트 통과 후 아직 감옥 미도착 (복도 이동 중)
_inJailCount   — 감옥 도착 완료
TotalCount     = _walkingCount + _inJailCount
IsFull         = TotalCount >= capacity
```

**복도 이동 중인 인원까지 포함**해서 IsFull 판정 — 과잉 수용 방지.

```
RegisterWalking()  → _walkingCount++
RegisterArrived()  → _walkingCount--, _inJailCount++
```

### 만석 이벤트

`_inJailCount >= capacity`가 될 때 `OnJailFull` 이벤트를 **단 한 번** 발행.  
`_jailFullFired` 플래그로 중복 발행 방지.

```csharp
if (!_jailFullFired && _inJailCount >= capacity)
{
    _jailFullFired = true;
    OnJailFull?.Invoke();
}
```

---

## 8. JailCheckpoint — 입장 관리

### 대기 큐

감옥이 가득 차면 `prisoner.Pause()` + 큐에 추가.  
`OnCapacityExpanded` 수신 시 빈 자리가 생기는 만큼 순서대로 해제.

```csharp
private void ReleaseWaiting()
{
    if (JailAnimator.IsVideoVersion) return; // 영상버전은 체크포인트 이후 진입 차단
    while (_waiting.Count > 0 && !JailManager.Instance.IsFull)
    {
        PrisonerController p = _waiting[0];
        _waiting.RemoveAt(0);
        PassThrough(p);
        p.Resume();
    }
}
```

### 영상버전 잠금

`LockForVideo()` 호출 후에는 아무도 통과 불가.  
감옥 확장 연출 중 신규 수감 완전 차단용.

---

## 9. JailCounterUI — 카운트 표시

### 3초 지연 (의도된 설계)

`RegisterWalking()`(체크포인트 통과)으로 `OnPrisonerEntered` 이벤트 발행 →  
JailCounterUI가 3초 뒤에 표시 카운트 +1.

죄수가 복도를 실제로 걸어 들어오는 시간과 화면 숫자가 올라가는 타이밍을 맞춘 연출.

```csharp
private IEnumerator DelayedIncrement()
{
    yield return new WaitForSeconds(countDelay); // 3f — 절대 변경 금지
    _displayCount++;
    UpdateText();
}
```

### OnDisplayFull 이벤트

표시 카운트가 용량과 같아질 때 발행.  
영상버전에서 JailAnimator가 구독 → `doorRiseDelay(2s)` 후 Door 상승.

> **주의**: `OnDisplayFull`은 표시 카운트 기준.  
> `JailManager.OnJailFull`은 실제 수감 카운트 기준.  
> 두 이벤트는 발행 시점이 다름 — 혼동 금지.

---

## 10. 감옥 확장 흐름

```
PurchaseZone(확장존) 구매 완료
  → delayedActivateTargets 활성화
  → JailExpander.OnEnable()
       → JailManager.ExpandCapacity(+20)  — capacity 증가, _jailFullFired 리셋
       → JailManager.SetJailDestination()  — 신규 죄수 목적지 = BigPrison
  → JailManager.OnCapacityExpanded 이벤트
       → JailCheckpoint.ReleaseWaiting()  — 대기 죄수 해제
       → JailCounterUI.RefreshDisplay()   — _fullFired 리셋, 텍스트 갱신
       → JailAnimator.OnCapacityExpanded() — 확장 연출 시작
```

---

## 요약 — 캐릭터 상태 전이

```
ArrestedPerson
  Walking  → (HandcuffReceiveZone 진입)
  Waiting  → (totalNeeded 수령 완료)
  Done     → SetActive(false)

PrisonerController
  (생성) → WalkToJail → (JailCheckpoint 통과) → (감옥 도착) → 수감 완료

JailManager
  walkingCount: 체크포인트↑, 도착↓
  inJailCount:  도착↑
  IsFull = (walking + inJail) >= capacity
  OnJailFull → 한 번만 발행 (jailFullFired 플래그)
  ExpandCapacity → jailFullFired 리셋 → 다시 만석 감지 가능
```
