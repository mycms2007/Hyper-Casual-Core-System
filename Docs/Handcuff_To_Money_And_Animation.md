# 수갑 → 돈 파이프라인 & 애니메이션 — Jailer Life

> 수갑이 생산된 뒤 OfficeZone을 거쳐 체포자에게 전달되고,  
> 죄수로 변신해 돈을 떨어뜨리고 감옥에 수감되는 전체 흐름.  
> 감옥 확장 애니메이션(영상버전/실제버전) 포함.

---

## 전체 파이프라인 흐름

```
HandcuffStackZone (수갑 적재)
       │ 플레이어/알바 픽업
       ▼
HandcuffCarrier (등에 적재)
       │ OfficeZone 진입
       ▼
HandcuffDropZone (수갑 스택 대기)
       │ OfficeManager가 조건 충족 시 하나씩 전송
       ▼
ArrestedPerson.ReceiveHandcuff()
       │ totalNeeded 충족
       ▼
CompleteQuest() — 변신 파티클 → PrisonerController 소환 → ThrowCoins()
       │                              │
       ▼                              ▼
TakeMoneyZone (코인 쌓임)      JailCheckpoint → 감옥 이동
       │ 플레이어 수령
       ▼
PlayerWallet.Add()
```

---

## 1. HandcuffStackZone → OfficeZone 이동

### OfficeZone

플레이어 또는 알바가 진입하면 HandcuffCarrier의 수갑을 전부 꺼내 HandcuffDropZone으로 전달.

```csharp
protected override void OnPlayerEnter(PlayerController player)
{
    _presenceCount++;
    TryDropHandcuffs(carrier);
}

private void TryDropHandcuffs(HandcuffCarrier c)
{
    var list = c.TakeAll();
    if (list.Count > 0)
        dropZone.Receive(list);

    // 날아오는 중인 수갑(Pending)도 도착하는 대로 추가 전달
    if (!_draining && c.PendingCount > 0)
        StartCoroutine(DrainPending(c));
}
```

**DrainPending** — 수갑이 아직 날아오는 중일 때 존에 진입해도  
도착하는 족족 DropZone으로 흘려보내는 코루틴.

### HandcuffDropZone

수갑을 포물선 비행으로 받아 스택 형태로 쌓음. 최대 용량 초과 시 즉시 Destroy.

```csharp
// 수신 큐 — 여러 배치가 동시에 도착해도 순서대로 처리
private readonly Queue<List<GameObject>> _receiveQueue;
```

착지 후 바운스 스케일 애니메이션 + 3D 공간 SFX.

```csharp
// 착지 시
SFXManager.Instance?.PlayHandcuffDrop(target);
yield return StartCoroutine(BounceScale(handcuff.transform));
onLanded?.Invoke();
```

---

## 2. OfficeManager — 수갑 전달 조건 관리

### 역할
매 Update마다 4가지 조건을 모두 확인하고 충족 시 수갑 1개 전송.

```
① OfficeZone에 누군가 있음 (HasPresence)
② HandcuffReceiveZone에 체포자 대기 중 (IsOccupied)
③ DropZone에 수갑 있음 (StackCount > 0)
④ 체포자가 수갑을 더 필요로 함 (NeedsMoreHandcuffs)
```

```csharp
private IEnumerator SendHandcuff(ArrestedPerson person)
{
    _isSending = true;
    GameObject handcuff = dropZone.TakeOne();

    // 포물선 비행 → person.ReceiveHandcuff()
    ...
    person.ReceiveHandcuff(handcuff);

    yield return new WaitForSeconds(sendInterval); // 간격 조절 (인스펙터)
    _isSending = false;
}
```

> `sendInterval`로 수갑 전달 속도 조절 가능.

---

## 3. ArrestedPerson (체포자)

### 역할
웨이포인트를 따라 걷다가 HandcuffReceiveZone에서 대기 → 수갑 수령 → 변신 → 코인 투척.

### 웨이포인트 이동

ArrestedPath(씬 싱글턴)에서 공유 웨이포인트 배열을 받아 순서대로 이동.  
앞사람이 GapDistance 이내에 있으면 이동 정지 — **줄 서기 구현**.

```csharp
private bool IsBlockedByPersonAhead()
{
    if (PersonAhead == null || PersonAhead.CurrentState == State.Done) return false;
    float dx = PersonAhead.transform.position.x - transform.position.x;
    float dz = PersonAhead.transform.position.z - transform.position.z;
    return dx * dx + dz * dz <= GapDistance * GapDistance;
}
```

### 수갑 퀘스트

`displayCount` — 말풍선에 표시되는 수.  
`_totalNeeded = displayCount + 1` — 실제 필요 수 (항상 1개 더).

```csharp
public void ReceiveHandcuff(GameObject handcuff)
{
    Destroy(handcuff);
    _receivedCount++;
    _zone?.UpdateBubble(_receivedCount, _totalNeeded, displayCount);

    if (_receivedCount >= _totalNeeded)
        StartCoroutine(CompleteQuest());
}
```

### 변신 → 코인 투척 흐름

```
CompleteQuest()
  → 파티클 이펙트 생성
  → 0.3s 대기
  → PrisonerController 소환 (랜덤 프리팹) → SetDestination()
  → 자신 렌더러 비활성화
  → HandcuffReceiveZone 점유 해제
  → ThrowCoins()
  → gameObject.SetActive(false)
```

코인은 하나씩 간격을 두고 투척, 전부 투척 완료 후 `AddMoney(rewardAmount)`.

```csharp
private IEnumerator ThrowCoins()
{
    for (int i = 0; i < coinsPerLayer; i++)
    {
        GameObject coin = Instantiate(coinPrefab, spawnPos, ...);
        takeMoneyZone.LaunchCoin(coin, spawnPos, coinFlyDuration);
        yield return new WaitForSeconds(coinInterval);
    }
    takeMoneyZone.AddMoney(rewardAmount);
}
```

### ArrestedSpawner

최대 `maxCount`명 유지. 한 명이 변신 완료(Done)되면 즉시 다음 소환.  
변신 후 PersonAhead 체인 재정렬.

```csharp
private void OnPersonTransformed()
{
    _queue.RemoveAll(p => p == null || p.CurrentState == ArrestedPerson.State.Done);
    for (int i = 0; i < _queue.Count; i++)
        _queue[i].PersonAhead = i > 0 ? _queue[i - 1] : null;
    SpawnNext();
}
```

---

## 4. HandcuffReceiveZone (말풍선 UI)

체포자가 Zone에 도달하면 말풍선 표시. 수갑 수령마다 진행도 갱신.

```csharp
// 말풍선 위치 — 월드좌표 → 스크린좌표 변환 (Screen Space Overlay)
Vector3 worldPos = _currentOccupant.transform.position + Vector3.up * bubbleHeightOffset;
Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
speechBubbleRect.position = screenPos;
```

fillImage는 Lerp로 부드럽게 채워짐 (`fillLerpSpeed`).

---

## 5. TakeMoneyZone

코인이 날아와 그리드 형태로 쌓임. 플레이어가 진입하면 전부 수거.

**최대 용량**: 60개 코인 = 150원 (6개 × 10층).  
초과 코인은 착지 전에 소멸.

```csharp
public void LaunchCoin(GameObject coin, Vector3 from, float flyDuration)
{
    if (_reservedCount >= maxCapacity) { Destroy(coin); return; }
    ...
}
```

**그리드 위치 계산** — 층별 6개(cols×rows), 층 높이 layerHeight.

```csharp
private Vector3 GetGridPosition(int index)
{
    int layer      = index / coinsPerLayer;
    int posInLayer = index % coinsPerLayer;
    int col = posInLayer % cols;
    int row = posInLayer / cols;
    ...
}
```

코인 착지 시 `PlayMoneyDrop()` (2D one-shot SFX).

---

## 6. PrisonerController (죄수)

ArrestedPerson이 변신 후 소환되는 죄수 NPC.  
PrisonerPath의 웨이포인트를 따라 감옥으로 이동.

목적지 도착 시 `JailManager.RegisterArrived()` 호출.  
ArrivalFaceTarget 방향을 바라보도록 회전.

**감옥 확장 후 재이동** (`MoveToDestination`):  
웨이포인트 없이 BigPrison으로 직행. `_skipJailRegistration = true`로 이중 등록 방지.

`arrivalTimeout` — 도달 판정 타임아웃(10s). 지형 걸림 등으로 멈춰있으면 강제 도착 처리.

---

## 7. JailManager

감옥 인원 상태를 관리하는 싱글턴. 모든 카운팅의 중심.

```
RegisterWalking()  — 체크포인트 통과 시 (복도 이동 중 카운트)
RegisterArrived()  — 감옥 도착 시 (walking→inJail 전환)
ExpandCapacity()   — 확장 시 capacity 증가 + 이벤트 발행
```

**IsFull 조건**: `_inJailCount + _walkingCount >= capacity`  
→ 복도 이동 중인 죄수까지 포함해서 판단.

### 주요 이벤트

| 이벤트 | 발생 시점 | 구독자 |
|--------|-----------|--------|
| `OnPrisonerEntered` | 체크포인트 통과 | JailCounterUI |
| `OnJailFull` | 감옥 가득 참 | CinematicDirector, PurchaseZone |
| `OnCapacityExpanded` | 용량 확장 | JailAnimator, JailCheckpoint, JailCounterUI |
| `OnCountChanged` | 인원 변경 | (HUD 등) |

---

## 8. JailCheckpoint

감옥 입구 게이트. 가득 차면 대기 큐에 쌓고, 확장 시 순서대로 해제.

```csharp
private void OnTriggerEnter(Collider other)
{
    if (JailManager.Instance.IsFull || _locked)
    {
        prisoner.Pause();
        _waiting.Add(prisoner);
    }
    else
        PassThrough(prisoner);
}

// JailManager.OnCapacityExpanded 수신 시
private void ReleaseWaiting()
{
    while (_waiting.Count > 0 && !JailManager.Instance.IsFull)
    {
        _waiting[0].Resume();
        PassThrough(_waiting[0]);
        _waiting.RemoveAt(0);
    }
}
```

영상버전에서는 `LockForVideo()` 호출로 이후 신규 진입을 영구 차단.

---

## 9. JailCounterUI

### 3초 지연 카운트 (의도적 설계)

체크포인트 통과 즉시 +1이 아니라 **3초 후** 표시 증가.  
죄수가 복도를 걸어 감옥에 실제로 들어오는 시간과 싱크를 맞추기 위한 연출.

```csharp
private IEnumerator DelayedIncrement()
{
    yield return new WaitForSeconds(countDelay); // 3f
    _displayCount++;
    UpdateText();
}
```

> **중요**: 이 3초 딜레이는 의도된 것. 절대 제거하지 말 것.

`OnDisplayFull` 이벤트 — 표시 카운트가 용량에 도달했을 때 발행.  
→ JailAnimator(영상버전)가 구독해 Door 상승 연출 트리거.

---

## 10. JailAnimator — 감옥 확장 애니메이션

### 영상버전 vs 실제버전

인스펙터의 `videoVersion` 체크박스로 분기.

### 영상버전 시퀀스

```
OnDisplayFull → doorRiseDelay(2s) 후 Door 상승
OnCapacityExpanded:
  1. JailCheckpoint.LockForVideo() — 이후 죄수 진입 차단
  2. Door 하강 → 비활성화
  3. 0.1s 후 Wall 하강
  4. 0.5s 후 BigPrison 상승
  5. 0.2s 후 침대 스프링 애니메이션 (전체 동시)
  6. 0.65s 후 축하 이펙트 재생 → OnCelebrationPlayed 이벤트
     → CinematicDirector.ReturnFromOverview() → EndPanel
```

### 실제버전 시퀀스

```
OnCapacityExpanded:
  1. Wall 하강 + CountUI 페이드아웃 동시 실행
  2. 0.5s 후 BigPrison 상승
  3. BigPrison 완료 → BPZoneCanvas 스프링 등장
  4. 0.2s 후 침대 스프링
  5. 0.95s 후 기존 죄수들 BigPrison으로 재이동 (가까운 순, stagger)
```

### 공통 애니메이션 헬퍼

**MoveObject** — EaseOut Cubic (감속 이동).
```csharp
float eased = 1f - Mathf.Pow(1f - p, 3f);
t.position  = Vector3.LerpUnclamped(from, to, eased);
```

**SpringBed / SpringAppearCanvas** — 지수 감쇠 × 코사인 오버슈트.
```csharp
float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
```

게임 전반에서 팝업 연출에 반복적으로 사용된 스프링 공식.

### JailExpander

PurchaseZone 구매 완료 → `delayedActivateTargets`에 연결 → OnEnable 발동.

```csharp
private void OnEnable()
{
    JailManager.Instance?.ExpandCapacity(expandAmount);     // 용량 +20
    JailManager.Instance?.SetJailDestination(newDest);      // 신규 죄수 목적지 교체
}
```

---

## 요약 — 주요 의존 관계

```
ArrestedSpawner
  └─ ArrestedPerson × N (웨이포인트 이동 + 줄 서기)
       └─ HandcuffReceiveZone (도착 대기)
            └─ OfficeManager → HandcuffDropZone.TakeOne()
                                    ↑
                              OfficeZone ← HandcuffCarrier (플레이어/알바)

ArrestedPerson.CompleteQuest()
  ├─ PrisonerController → JailCheckpoint → JailManager.RegisterArrived()
  └─ ThrowCoins() → TakeMoneyZone → PlayerWallet.Add()

JailManager
  ├─ OnJailFull        → CinematicDirector (카메라 연출)
  ├─ OnCapacityExpanded → JailAnimator (확장 연출) + JailCheckpoint (대기 해제)
  └─ OnPrisonerEntered → JailCounterUI (3초 딜레이 카운트)
```
