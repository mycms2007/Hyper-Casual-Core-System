# Ore / Gem / ProcessingMachine — Jailer Life

> 광석 채굴부터 수갑 생산까지의 전체 파이프라인 기록.  
> 구현 흐름, 난관, 돌파 방법, 핵심 코드 포함.

---

## 전체 파이프라인 흐름

```
[플레이어/드릴카/광부]
       │ 채굴
       ▼
     Ore.Die()
       │
       ├─ 플레이어/드릴카 → GemCarrier (등에 적재)
       │                        │ GemDropZone 진입
       │                        ▼
       └─ 광부(MinerWorker) → GemDropZone.AddMinerGem()
                                    │
                              [GemDropZone]
                              보석 착지 완료 → OnAllGemLanded()
                                    │
                          ProcessingMachine.ReceiveGems()
                                    │
                          gem 한 개씩: FlyToInput → ShrinkAndDestroy → SpawnHandcuff
                                    │
                          MoveAlongConveyor → ItemTransZone.OnHandcuffArrived()
                                    │
                          모든 수갑 소멸 완료 → HandcuffStackZone.SpawnHandcuffs()
```

---

## 1. Ore

### 역할
광석 HP 관리 + 소멸 시 보석 생성 / 리스폰 요청.

### HP & 데미지

최대 HP 3. 타격원에 따라 두 가지 경로로 분기.

```csharp
public void TakeDamage()                          // 플레이어/드릴카
public void TakeDamageByMiner(GemDropZone dropZone) // 광부 NPC
```

내부는 동일한 `TakeDamageInternal()` 호출. 차이는 `_minerDropZone` 저장 여부.

### 소멸 처리

```csharp
private void Die()
{
    _isDead = true;
    _mesh.enabled = false;
    SFXManager.Instance?.PlayOreBreak(transform.position);
    Instantiate(oreBreakEffect, ...);

    if (_minerDropZone != null)
        _minerDropZone.AddMinerGem(gemPrefab);   // 광부가 캔 경우
    else if (_gemCarrier != null)
        _gemCarrier.TryAdd(gemPrefab);           // 플레이어/드릴카가 캔 경우

    OreManager.Instance.ScheduleRespawn(this, _spawnPoint);
}
```

### 리스폰

OreManager가 `respawnDelay`(5s) 후 `ore.Respawn()` 호출.  
리스폰 시 스케일 0에서 스프링 애니메이션으로 등장.

```csharp
// SpawnAnimation: 지수 감쇠 × 코사인 오버슈트
float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
```

### Claim 시스템

광부 NPC가 특정 광석을 타겟으로 삼으면 `Claim()` 호출.  
다른 광부가 이미 점유한 광석은 `IsClaimed`로 건너뜀.  
광부가 비활성화되거나 광석이 소멸하면 자동으로 `Unclaim()`.

---

## 2. OreManager

### 역할
씬 내 모든 Ore의 초기화 / 리스폰 스케줄링 / 쿼리 제공.

```csharp
// 광부용 — 살아있고 점유되지 않은 광석 목록
public Ore[] GetAliveUnclaimedOres()

// 플레이어/드릴카용 — 가장 가까운 살아있는 광석
public Ore GetNearestAliveOre(Vector3 position)
```

모든 Ore는 `spawnPointsParent` 하위 자식으로 씬에 배치.  
Start에서 `GetComponentsInChildren<Ore>()`로 일괄 수집 후 Init.

---

## 3. MinerWorker (광부 NPC)

### 역할
고용된 광부 NPC. 독립 상태머신으로 광석 탐색 → 이동 → 채굴 루프.

### 상태 흐름

```
FindOre → MoveToOre → Mining → Pause(1s) → FindOre → ...

광석 뺏기거나 소멸 시 → 즉시 Pause → 재탐색
```

### 광석 선택 우선순위 (난관)

여러 광부가 동시에 같은 광석으로 몰리는 문제.  
단순 "가장 가까운 것"만으론 동선이 비효율적.

**돌파**: 3단계 우선순위 적용.
1. 정면 콘(forwardThreshold, cos값) 안의 광석 중 가장 가까운 것
2. 없으면 전방향 중 가장 가까운 것
3. 거리가 같으면(distEpsilon 이내) 오른쪽 방향 광석 우선

```csharp
bool isForward = Vector3.Dot(dir, fwd) >= forwardThreshold;

// IsBetter — 거리 동률 시 오른쪽 우선
if (candidateDist < currentDist - distEpsilon) return true;
if (candidateDist > currentDist + distEpsilon) return false;
float rCandidate = Vector3.Dot(toCandidate.normalized, right);
float rCurrent   = Vector3.Dot(toCurrent.normalized,   right);
return rCandidate > rCurrent;
```

### 채굴 SFX — 3D 공간음

광부 애니메이션 타격 이벤트에서 호출.  
광부 위치 기반 3D 오디오 — 멀수록 조용해짐.

```csharp
public void OnMiningHit()
{
    SFXManager.Instance?.PlayMinerMining(transform.position);
    if (_targetOre != null)
        _targetOre.TakeDamageByMiner(gemDropZone);
}
```

### MinerSpawner

PurchaseZone 구매 완료 → activateTargets에 연결된 MinerSpawner 활성화.  
OnEnable에서 spawnPoints 수만큼 광부 소환 + GemDropZone 주입.

---

## 4. GemDropZone

### 역할
플레이어가 보석을 내려놓는 존. 착지 연출 후 ProcessingMachine에 전달.

### 플레이어 보석 처리 흐름

```
OnPlayerEnter
  → CollectGems()
    → playerStack.TakeAll()       // 등에서 전부 꺼냄
    → StackItem 컴포넌트 제거      // 체인 추적 해제
    → 1프레임 대기                 // Destroy 반영
    → 맨 위 gem부터 역순으로 FlyGem() 병렬 실행
    → 전부 착지 → OnAllGemLanded()
    → machine.ReceiveGems()
```

**StackItem 제거 후 1프레임 대기**가 중요.  
Destroy는 즉시 실행되지 않아서 대기 없이 진행하면 이미 파괴된 컴포넌트 참조 오류 발생.

### 착지 위치 계산 — 지그재그 배열

```csharp
private Vector3 GetStackPosition(int index)
{
    float side = (index % 2 == 0) ? -xSpacing : xSpacing;
    float y = (index / 2) * ySpacing;
    Vector3 sideOffset = _stackDir * side;
    return _zoneBasePos + stackBaseOffset + Vector3.up * y + sideOffset;
}
```

홀짝 인덱스로 좌우 교대 → 높이 올라가는 지그재그 스택 형태.

### 광부 보석 직접 전달 (AddMinerGem)

광부가 캔 보석은 플레이어 등을 거치지 않고 GemDropZone에 직접 도착.  
스프링 애니메이션으로 등장 후 즉시 ProcessingMachine으로 전달.

```csharp
public void AddMinerGem(GameObject gemPrefab)
{
    StartCoroutine(SpawnMinerGem(gemPrefab));
}
// 0.2s 딜레이 → 스택 위치 계산 → Instantiate → SpringIn → machine.ReceiveGems()
```

---

## 5. ProcessingMachine (수갑 생산 기계)

### 역할
GemDropZone에서 보석을 받아 수갑으로 변환. 보석 1개 → 수갑 1개.

### 처리 순서 (직렬)

보석을 동시에 처리하지 않고 하나씩 순서대로 처리.

```csharp
private IEnumerator ProcessGems(List<GameObject> gems)
{
    for (int i = gems.Count - 1; i >= 0; i--)
    {
        yield return StartCoroutine(FlyToInput(gems[i]));    // inputPoint로 포물선 이동
        yield return StartCoroutine(ShrinkAndDestroy(gems[i])); // 축소 후 소멸
        yield return StartCoroutine(SpawnHandcuff());        // 수갑 생성 + 컨베이어 이동
    }
}
```

### 수갑 생성 연출

```
스케일 0에서 expandDuration 동안 선형 확장
  → MoveAlongConveyor() — conveyorDuration 동안 ItemTransZone으로 이동
  → ItemTransZone.OnHandcuffArrived() 호출
```

---

## 6. ItemTransZone

### 역할
컨베이어 끝에서 수갑을 받아 소멸시키고, **전부 소멸 완료 후** HandcuffStackZone에 일괄 통보.

### 왜 일괄 통보인가?

ProcessingMachine이 보석을 하나씩 직렬로 처리하므로,  
수갑이 하나씩 도착하는 동안 누적 카운트를 추적.  
마지막 수갑이 소멸할 때 `totalCount`를 한 번에 전달 → 수갑 스택이 몰아서 생성됨.

```csharp
public void OnHandcuffArrived(GameObject handcuff)
{
    _pendingCount++;
    _totalCount++;
    StartCoroutine(ShrinkAndTransfer(handcuff));
}

// ShrinkAndTransfer 끝에서
_pendingCount--;
if (_pendingCount <= 0)
{
    handcuffStackZone.SpawnHandcuffs(_totalCount);
    _totalCount = 0;
}
```

---

## 7. 연결 구조 요약

```
OreManager
  └─ Ore[] (씬 배치)
       └─ Die()
            ├─ GemCarrier.TryAdd()          ← 플레이어/드릴카
            └─ GemDropZone.AddMinerGem()    ← 광부(MinerWorker)

GemDropZone
  └─ OnAllGemLanded() → ProcessingMachine.ReceiveGems()

ProcessingMachine
  └─ SpawnHandcuff() → MoveAlongConveyor() → ItemTransZone.OnHandcuffArrived()

ItemTransZone
  └─ 전체 소멸 완료 → HandcuffStackZone.SpawnHandcuffs(count)

MinerSpawner
  └─ PurchaseZone 구매 완료 → OnEnable → MinerWorker 소환 × N
```

---

## 참고 — FlatArrow 버그 (미수정, 자연 해소)

**증상**: 보석 투척 후 GemDropZone 화살표가 사라지지 않고 남는 경우 발생.

**원인 분석**: 플레이어가 보석이 착지하기 전에 GemDropZone을 이탈하면,  
`_isCollecting = true` 상태에서 OnPlayerExit 이벤트가 잘못 처리됨.  
두 번째 존 이탈 시 자연히 해소됨.

**결론**: 플레이어가 투척 완료 전에 존을 나가는 경우에만 발생.  
게임 플로우상 노출 빈도 낮고, 다음 진입 시 자가 복구되어 미수정으로 마무리.
