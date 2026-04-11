# SFX & 시네마틱 시스템 — Jailer Life

> SFXManager 효과음 전체 구조, CinematicDirector 카메라 연출 흐름,
> CelebrationEffect 폭죽, EndPanelController 마무리 연출 기록.

---

## 1. SFXManager

항상 활성화된 오브젝트에 부착된 싱글턴. 게임 내 모든 효과음의 단일 창구.

### AudioSource 구조

두 개의 AudioSource를 직접 AddComponent로 생성.

| 소스 | 용도 | 특성 |
|------|------|------|
| `_loopSource` | drilling 루프 전용 | `loop = true` |
| `_sfxSource` | 일반 one-shot 전용 | PlayOneShot 사용 |

3D 공간음은 별도 임시 GameObject를 씬에 생성해 재생 후 자동 소멸.

### 10개 효과음 & 볼륨 인스펙터 관리

모든 클립과 볼륨이 인스펙터에서 개별 조절 가능.

| 효과음 | 재생 방식 | 특이사항 |
|--------|-----------|----------|
| `drilling` | 루프 + 페이드 | DrillCar/손드릴 활성화 중 루프 |
| `miningHit` | one-shot | Animation Event 34프레임 타이밍 |
| `oreBreak` | 3D 공간음 | 광석 위치 기준 |
| `minerMining` | 3D 공간음 | 광부 위치 기준 |
| `gemDrop` | one-shot | GemDropZone 착지 시 |
| `moneyDrop` | one-shot | TakeMoneyZone 착지 시 |
| `handcuffDrop` | 3D 공간음 | HandcuffDropZone 착지 위치 기준 |
| `handcuffStack` | 3D 공간음 | HandcuffStackZone 스폰 위치 기준 |
| `zonePurchase` | one-shot | 구매 완료 직전 미리 재생 |
| `endPanel` | one-shot | `_silenced` 무시, 볼륨 강제 1f |

### 3D 공간음 구현

```csharp
private void Play3D(AudioClip clip, Vector3 position, float volume, float maxDistance)
{
    GameObject go = new GameObject("SFX_3D");
    go.transform.position = position;
    AudioSource src = go.AddComponent<AudioSource>();
    src.clip         = clip;
    src.volume       = volume;
    src.spatialBlend = 1f;                          // 완전 3D
    src.maxDistance  = maxDistance;
    src.minDistance  = 1f;
    src.rolloffMode  = AudioRolloffMode.Logarithmic; // 자연스러운 감쇠
    src.Play();
    Destroy(go, clip.length + 0.1f);                // 재생 완료 후 자동 소멸
}
```

**AudioRolloffMode.Logarithmic** — Linear 대비 가까울수록 훨씬 크게, 멀수록 자연스럽게 작아짐.  
초기에 Linear로 설정했다가 수갑 소리가 안 들리는 문제 발생 → Logarithmic + maxDistance 30으로 수정.

### _silenced 플래그 — 전체 음소거

시네마틱 카메라 작동 중 모든 효과음을 차단하는 전역 플래그.

```csharp
private bool _silenced;

// 모든 Play 메서드 첫 줄
if (_silenced || clip == null) return;
```

`PlayEndPanel`만 예외 — `_silenced` 체크 없이 항상 재생.

### Drilling 루프 페이드

DrillCar/손드릴이 꺼질 때 즉시 끊기지 않고 `drillingFadeDuration` 동안 서서히 소멸.

```csharp
private IEnumerator FadeOutLoop(float duration)
{
    float start = _loopSource.volume;
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        _loopSource.volume = Mathf.Lerp(start, 0f, elapsed / duration);
        yield return null;
    }
    _loopSource.Stop();
    _loopSource.volume = drillingVolume; // 볼륨 원복 (다음 재생 대비)
}
```

### GlobalFadeOut / GlobalFadeIn

CinematicDirector에서 호출. 루프소스와 SFX소스를 동시에 페이드.

```csharp
private IEnumerator FadeOutRoutine()
{
    _silenced = true; // 즉시 신규 재생 차단
    // _loopSource, _sfxSource 볼륨을 globalFadeDuration 동안 0으로
    // 완료 후 둘 다 Stop()
}

private IEnumerator FadeInRoutine()
{
    _silenced = false; // 신규 재생 허용
    // _sfxSource 볼륨을 globalFadeDuration 동안 1로 복원
    // (_loopSource는 복원 안 함 — 드릴링은 다음 PlayDrilling() 호출 시 재개)
}
```

> **마지막 시네마틱 이후**: GlobalFadeIn 호출 없음 → 소리가 복구되지 않은 채로 EndPanel 진입.  
> EndPanel 효과음만 `_sfxSource.volume = 1f` 강제 복원 후 재생.

---

## 2. CinematicDirector

항상 활성화된 오브젝트에 부착. `videoVersion`이 true일 때만 동작.

### 구독 이벤트 → 카메라 연출 매핑

| 이벤트 | 카메라 | 동작 |
|--------|--------|------|
| `PlayerWallet.OnFirstMoneyEarned` | drillZoneCam | ShowCam(delay, hold) |
| `JailManager.OnJailFull` | expansionZoneCam | ShowCam(delay, hold) |
| `JailManager.OnCapacityExpanded` | overviewCam | Priority=11, 입력 잠금, FadeOut |
| `JailAnimator.OnCelebrationPlayed` | — | ReturnFromOverview() |

### ShowCam 코루틴 (drillZone, expansionZone)

```csharp
private IEnumerator ShowCam(CinemachineVirtualCamera cam, float delay, float hold)
{
    yield return new WaitForSeconds(delay);
    SFXManager.Instance?.GlobalFadeOut();   // 소리 페이드아웃
    cam.Priority = 11;                      // Cinemachine 우선순위 상승 → 카메라 전환
    player?.SetMovementLocked(true);        // 플레이어 입력 잠금 + velocity 0
    DrillCar.SetInputLocked(true);          // 드릴카 입력 잠금
    yield return new WaitForSeconds(hold);
    cam.Priority = 0;                       // 카메라 복귀
    player?.SetMovementLocked(false);
    DrillCar.SetInputLocked(false);
    SFXManager.Instance?.GlobalFadeIn();    // 소리 복구
}
```

### OnCapacityExpanded (overviewCam)

감옥 확장 → 축하 이펙트를 보여주는 전체 overview 카메라.

```csharp
private void OnCapacityExpanded()
{
    if (!JailAnimator.IsVideoVersion) return;
    overviewCam.Priority = 11;
    player?.SetMovementLocked(true);
    DrillCar.SetInputLocked(true);
    SFXManager.Instance?.GlobalFadeOut();
    // 복귀는 JailAnimator.OnCelebrationPlayed 이후
}
```

### ReturnFromOverview — 마지막 시네마틱 종료

```csharp
private IEnumerator ReturnFromOverview()
{
    yield return new WaitForSeconds(overviewReturnDelay);
    overviewCam.Priority = 0;
    player?.SetMovementLocked(false);
    DrillCar.SetInputLocked(false);
    // GlobalFadeIn 없음 → 소리 복구 안 됨 (의도적)
    yield return new WaitForSeconds(endPanelDelay);
    endPanel?.OpenPanel();
}
```

### Cinemachine Priority 방식

카메라를 직접 교체하지 않고 Priority 값만 변경.  
기본 카메라 Priority = 10, 연출 카메라 Priority = 11 → Cinemachine이 자동으로 블렌딩.  
복귀 시 Priority = 0으로 내리면 기본 카메라로 자연스럽게 전환.

### 플레이어 이동 잠금 시 슬라이딩 문제 (난관)

**문제**: 이동 중 카메라 연출이 시작되면 입력은 막히지만 Rigidbody의 기존 velocity가 남아 계속 미끄러짐.

**해결**: `SetMovementLocked(true)` 시점에 velocity/angularVelocity를 강제 0으로 리셋.

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

---

## 3. 전체 시네마틱 흐름 (영상버전)

```
게임 시작
  │
  ├─ 첫 돈 수령
  │    → OnFirstMoneyEarned
  │    → ShowCam(drillZoneCam) : FadeOut → 카메라 전환 → hold → FadeIn
  │
  ├─ 감옥 20/20 도달
  │    → OnJailFull
  │    → ShowCam(expansionZoneCam) : FadeOut → 카메라 전환 → hold → FadeIn
  │
  ├─ 감옥 확장 구매 완료 (JailExpander)
  │    → OnCapacityExpanded
  │    → overviewCam Priority=11, 입력 잠금, GlobalFadeOut
  │    → JailAnimator.VideoExpansion() 시퀀스 실행
  │         Door 하강 → Wall 하강 → BigPrison 상승 → 침대 스프링 → 축하 이펙트
  │    → OnCelebrationPlayed
  │    → ReturnFromOverview()
  │         overviewCam Priority=0, 입력 잠금 해제
  │         (GlobalFadeIn 없음 — 소리 복구 안 됨)
  │         endPanelDelay 후 endPanel.OpenPanel()
  │
  └─ EndPanel 표시
```

---

## 4. CelebrationEffect — 폭죽 이펙트

UICanvas 안에서 동작하는 코드 기반 파티클 시스템.  
Unity ParticleSystem 미사용 — UI Image를 직접 생성/이동/소멸.

### 파티클 구성

- **Spark (62%)**: 정사각형에 가까운 불꽃 조각
- **Streamer (38%)**: 길쭉한 종이테이프

두 발 버스트: `burstInterval`(0.32s) 간격, 좌우 다른 위치에서 발사.

### 물리 시뮬레이션

```csharp
// 매 프레임 업데이트
vel -= vel * (drag * Time.deltaTime);   // 공기 저항 감속
vel.y -= gravity * Time.deltaTime;     // 중력 낙하
pos += vel * Time.deltaTime;
```

### Alpha 곡선

```
0 ~ fadeIn(0.06s)   : 0 → 1  (빠르게 등장)
fadeIn ~ (end-0.55s): 1       (유지)
(end-0.55s) ~ end   : 1 → 0  (서서히 소멸)
```

### Emission 펄스

```csharp
float pulse    = Mathf.Sin(elapsed * emissionPulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
float emission = Mathf.Lerp(min, emissionIntensity, pulse);
Color c = baseColor * emission; // HDR-like 반짝임
```

6가지 고정 색상(빨강/노랑/초록/파랑/핑크/주황)에서 랜덤 선택.

---

## 5. EndPanelController — 마무리 화면

### 등장 조건

`CinematicDirector.ReturnFromOverview()` → `endPanel.OpenPanel()` 호출.  
Q키로도 강제 오픈 (에디터 테스트용).

### 등장 시퀀스

```
OpenPanel()
  → endPanel.SetActive(true)
  → SFXManager.PlayEndPanel()  (_silenced 무시, 볼륨 1f 강제)
  → PlaySequence()
       TitleSquashStretch() ─┐ 동시 시작
       SpringFromZero(icon) ─┘
       0.2s 후 SpringFromZero(continueButton)
       → ButtonLoopBounce() 무한 루프
```

### Title — Squash-and-Stretch

납작하게 눌린 상태(가로 1.35배, 세로 0.35배)에서 스프링으로 원래 크기로 복귀.

```csharp
Vector3 squashed = new Vector3(
    _titleOriginal.x * 1.35f,
    _titleOriginal.y * 0.35f,
    _titleOriginal.z);

float spring = 1f - Mathf.Exp(-8f * t) * Mathf.Cos(10f * t);
float sx = Mathf.Lerp(squashed.x, _titleOriginal.x, spring);
float sy = Mathf.Lerp(squashed.y, _titleOriginal.y, spring);
```

### Continue 버튼 — 루프 찌부

1초 간격으로 가로로 살짝 늘어났다가 스프링 복귀. 주의를 끄는 연출.

```csharp
Vector3 stretched = new Vector3(
    _buttonOriginal.x * 1.18f,
    _buttonOriginal.y * 0.88f,
    _buttonOriginal.z);
// 이후 스프링 공식으로 _buttonOriginal로 복귀
```

### EndPanel 부착 위치 (난관)

**문제**: EndPanelController가 EndPanel 자체에 붙어있으면 비활성화 상태에서 Start()가 호출되지 않아 원본 스케일 저장 실패 → 등장 애니메이션 오작동.

**해결**: EndPanelController를 **항상 활성화된 부모 오브젝트**에 부착.  
Start()에서 endPanel이 비활성화 상태여도 자식 Transform에는 접근 가능 → 원본 스케일 저장 정상 동작.

---

## 6. 프로젝트 전반에 사용된 스프링 공식

게임 내 거의 모든 팝업/등장 연출에 동일한 공식 사용.

```csharp
float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
// p: 0~1 진행도
// 결과: 0에서 출발해 1.0을 살짝 오버슈트한 뒤 1.0에 정착
```

적용 위치: PurchaseZone 등장, HandcuffStackZone 스폰, HandcuffCarrier 추가,
GemDropZone 광부 보석 등장, JailAnimator 침대/Canvas, EndPanel 아이콘/버튼.
