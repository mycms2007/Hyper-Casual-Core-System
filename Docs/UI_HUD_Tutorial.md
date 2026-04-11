# UI / HUD / 튜토리얼 시스템 — Jailer Life

> HUDManager, TutorialManager, FlatArrow, MAX 인디케이터,
> CameraFollow, FaceCamera 전체 정리.

---

## 1. HUDManager

역할 최소화. 잔액 텍스트 갱신만 담당.

```csharp
public void SetMoney(int amount)
{
    moneyText.text = $"₩{amount}";
}
```

`PlayerWallet.Add()` / `Spend()` 호출 시 즉시 `hud?.SetMoney()` 로 갱신.  
별도 이벤트 구독 없이 PlayerWallet이 직접 밀어넣는 방식.

---

## 2. TutorialManager

게임 시작 후 처음 플레이하는 플레이어를 핵심 루프로 안내하는 두 트랙 시스템.

### 두 가지 화살표 시스템

| 시스템 | 형태 | 역할 |
|--------|------|------|
| 아래찍기 화살표 | 존 위 고정 오브젝트 | "여기로 가세요" 위치 표시 |
| 납작 화살표 (FlatArrow) | 플레이어 주변 회전 | "이쪽 방향으로" 방향 안내 |

### 아래찍기 화살표 — 9단계 순서

```
Step 0: 광물 화살표 표시
  OnFirstMining()         → Step 1 → 광물 화살표 Hide
Step 1→2: GemDropZone 화살표 표시 (0.2s 딜레이)
  OnGemDropStarted()      → Step 3 → GemDropZone 화살표 Hide (0.1s 딜레이)
Step 3→4: HandcuffStackZone 화살표 표시
  OnFirstHandcuffPickup() → Step 5 → 수갑 화살표 Hide → OfficeZone 화살표 표시
Step 5→6: OfficeZone 화살표 표시
  OnFirstHandcuffDropped()→ Step 7 → OfficeZone 화살표 Hide
Step 7→8: TakeMoneyZone 화살표 표시
  OnFirstCoinLaunched()   → Step 8 → TakeMoneyZone 화살표 표시
  OnFirstMoneyPickup()    → Step 9 → Hide → 튜토리얼 완료
```

단계별로 `if (_step != N) return;` 가드 — 순서가 뒤바뀌거나 중복 실행 방지.

```csharp
public void OnFirstMining()
{
    if (_step != 0) return;
    _step = 1;
    Hide(oreArrow);
    StartCoroutine(Delay(0.1f, () => { _flatPhase = 1; flatArrow?.Show(gemDropZoneTarget); }));
    StartCoroutine(Delay(0.2f, () => { _step = 2; Show(gemDropZoneArrow); }));
}
```

딜레이를 두는 이유 — 화살표가 즉시 꺼지고 켜지면 끊기는 느낌. 짧은 대기로 자연스럽게 전환.

### 납작 화살표 — 4단계 흐름 (FlatPhase)

아래찍기 화살표와 별도로 진행. GemDropZone 왕복 구간만 담당.

```
Phase 0: 대기
  OnFirstMining() → Phase 1: FlatArrow.Show(gemDropZoneTarget)
Phase 1: GemDropZone 방향 표시 중
  OnPlayerEnteredGemDropZone() → Phase 2: FlatArrow.Hide()
Phase 2: 드랍 완료 대기
  OnAllGemsDropped() → Phase 3: FlatArrow.Show(handcuffStackZoneTarget)
Phase 3: HandcuffStackZone 방향 표시 중
  OnPlayerExitedGemDropZone() → Phase 4: FlatArrow.Hide() (0.2s 딜레이)
Phase 4: 완료
```

> **호출 위치 정리**

| 메서드 | 호출 위치 |
|--------|-----------|
| `OnFirstMining()` | `Ore.Die()` — 플레이어 채굴 경로 |
| `OnGemDropStarted()` | `GemDropZone.CollectGems()` 시작 |
| `OnAllGemsDropped()` | `GemDropZone.OnAllGemLanded()` |
| `OnPlayerEnteredGemDropZone()` | `GemDropZone.OnPlayerEnter()` |
| `OnPlayerExitedGemDropZone()` | `GemDropZone.OnPlayerExit()` |
| `OnFirstHandcuffProduced()` | `HandcuffStackZone.SpawnSequence()` 첫 수갑 |
| `OnFirstHandcuffPickup()` | `HandcuffStackZone.TransferToPlayer()` |
| `OnFirstHandcuffDropped()` | `OfficeZone.TryDropHandcuffs()` |
| `OnFirstCoinLaunched()` | `TakeMoneyZone.LaunchCoin()` 최초 |
| `OnFirstMoneyPickup()` | `TakeMoneyZone.CollectCoins()` |

---

## 3. FlatArrow — 방향 안내 화살표

Screen Space Canvas 위에서 플레이어를 중심으로 궤도 위에 위치하며 목표 방향을 가리킴.

### 위치 계산

```csharp
Vector2 playerScreen = _cam.WorldToScreenPoint(player.position);
Vector2 targetScreen = _cam.WorldToScreenPoint(_target.position);
Vector2 dir = (targetScreen - playerScreen).normalized;

// 전진 펄스 — 화살표가 방향으로 살짝 왔다갔다
float pulse = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * pulseAmount;
rectTransform.position = playerScreen + dir * (orbitRadius + pulse);
```

### 회전 계산

```csharp
float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + rotationOffset;
_currentAngle = Mathf.LerpAngle(_currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
rectTransform.rotation = Quaternion.Euler(0f, 0f, _currentAngle);
```

`rotationOffset` — 스프라이트가 기본으로 가리키는 방향 보정값 (↑이면 0, →이면 -90).  
`LerpAngle` — 0°↔360° 경계를 자연스럽게 처리.

### FlatArrow 버그 (미수정, 자가 복구)

**증상**: GemDropZone에서 드랍 중 플레이어가 너무 빠르게 이탈하면 화살표가 사라지지 않고 남음.

**원인**: `OnPlayerExitedGemDropZone()`이 Phase 3에서만 Hide를 실행하는데,  
보석 착지 전에 이탈하면 Phase가 아직 2 → 이탈 이벤트 무시 → 화살표 고착.

**결론**: 두 번째 GemDropZone 이탈 시 자가 복구. 플레이 중 노출 빈도 낮아 미수정.

---

## 4. GemMaxIndicator

보석 적재량이 최대치에 도달하면 플레이어/드릴카 머리 위에 MAX 이미지 반복 블링크.

### 위치 추적

드릴카 운전 중이면 드릴카를 추적, 평상시엔 플레이어 추적.  
카메라 뒤에 있으면(`screenPos.z < 0`) alpha = 0으로 즉시 숨김.

```csharp
bool isDriving = DrillCar.Instance != null && DrillCar.Instance.IsDriving;
Transform tracked = isDriving ? DrillCar.Instance.transform : playerTransform;
float offset      = isDriving ? drillCarHeightOffset : playerHeightOffset;
Vector3 screenPos = _cam.WorldToScreenPoint(tracked.position + Vector3.up * offset);
rectTransform.position = screenPos;
```

### BlinkLoop

fadeIn → visibleDuration 유지 → fadeOut → 반복.  
최대치 해제 시 `quickFadeOutSpeed`로 빠르게 알파 0으로.

```
BlinkLoop:
  fadeIn  (0.3s) : 0 → 1
  visible (0.7s) : 1 유지
  fadeOut (0.5s) : 1 → 0
  반복
```

### HandcuffStackMaxIndicator

GemMaxIndicator와 동일한 BlinkLoop 구조.  
플레이어 대신 HandcuffStackZone 위치를 추적.  
`stackZone.StackCount >= stackZone.MaxCapacity`가 조건.

---

## 5. CameraFollow

플레이어를 부드럽게 추적하는 기본 카메라. Cinemachine 없이 직접 구현.

```csharp
private void LateUpdate()
{
    if (target != null)
        _goalPos = target.position + offset;
    transform.position = Vector3.Lerp(transform.position, _goalPos, smoothSpeed * Time.deltaTime);
}
```

`LateUpdate` 사용 — 플레이어 이동(FixedUpdate)이 완전히 끝난 후 카메라 위치 갱신.

### 외부 제어 메서드

```csharp
SetTarget(Transform)      // 추적 대상 교체 (연출용)
SetFocusPoint(Vector3)    // 대상 없이 특정 좌표 고정
ReturnToPlayer()          // 플레이어 추적 복귀
```

> **실제 게임에서의 카메라 전환**: Cinemachine VirtualCamera Priority 방식 사용.  
> CameraFollow는 기본 추적용, 시네마틱 구간은 Cinemachine이 오버라이드.

---

## 6. FaceCamera

월드 스페이스 오브젝트(말풍선, 표지판 등)가 항상 카메라를 향하도록.

```csharp
private void LateUpdate()
{
    transform.rotation = Quaternion.Euler(0f, _cam.transform.eulerAngles.y + 180f, 0f);
}
```

Y축만 회전 — 오브젝트가 기울지 않고 수직을 유지하면서 카메라 방향만 바라봄.

---

## 7. UI 위치 추적 패턴 정리

프로젝트 전반에서 월드 오브젝트를 Screen Space UI에 연결하는 두 가지 방식 사용.

### WorldToScreenPoint 방식
```csharp
Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
rectTransform.position = screenPos;
```
`screenPos.z < 0` 체크 필수 — 카메라 뒤에 있으면 스크린 좌표가 반전됨.  
사용처: FlatArrow, GemMaxIndicator, HandcuffStackMaxIndicator, HandcuffReceiveZone 말풍선.

### World Space Canvas + FaceCamera 방식
Canvas 자체를 월드에 배치하고 FaceCamera로 항상 카메라 방향.  
사용처: JailCounterUI (감옥 벽면 카운터), 존 위 화살표.

---

## 요약 — UI 컴포넌트 역할 분류

```
HUD (Screen Space Overlay)
  └─ HUDManager — 잔액 텍스트

튜토리얼
  ├─ TutorialManager — 9단계 아래찍기 + 4단계 납작화살표 조율
  └─ FlatArrow — 플레이어 궤도 방향 화살표

상태 인디케이터 (Screen Space Overlay)
  ├─ GemMaxIndicator — 보석 MAX 블링크 (플레이어/드릴카 추적)
  └─ HandcuffStackMaxIndicator — 수갑 스택 MAX 블링크 (Zone 추적)

카메라
  ├─ CameraFollow — 기본 플레이어 추적 (LateUpdate Lerp)
  └─ FaceCamera — 월드 오브젝트 빌보드 (Y축만 회전)
```
