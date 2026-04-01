# Jailer Life 개발 로그

---

## 세션 1 — 2026-03-30

---

### [유저] 프로젝트 초기 파악 요청

> `C:\Users\CMS\Desktop\Jailer Life` 폴더에 접근해서 md파일과 텍스트파일을 확인해줘.
> 그 다음 전체 프로젝트 구조를 확인해줘.
> 프로젝트쪽의 폴더 구조는 클로드웹을 통해 확보해서 만들어놓았고, 프로젝트에 필요한 프리팹과 스프라이트를 거의 다 구해놓았어.
> 그때는 확인하는대로 구해서 작업을 속행할 생각이야. 하이어라키도 대부분은 맞게 꾸려놓았어.
> 하지만 작업 와중에 맞지않는 구조라고 생각되거나 변경점이 있으면 변경함에 주저할 생각은 없어.

### [Claude] 프로젝트 파악 결과

**파일 2개 내용 요약**
- **CLAUDE.md** : 한국어 응답, 단계별 지시, plan.md 기반 워크플로우 규정
- **영상흐름.txt** : Prison Life 광고영상 전체 흐름 분석 (맵 구성, 인물, UI, 이벤트 순서 상세 기술)

#### 현재 프로젝트 상태

| 구분 | 상태 |
|---|---|
| 씬 | `GameScene.unity` 1개 |
| 폴더 구조 | 설계 완료 |
| 스크립트 (.cs) | **전무** — 7개 폴더 모두 비어있음 |
| 애니메이션 | **전무** — Player/, Prisoner/ 폴더 비어있음 |
| 머티리얼 | 20개 완비 |
| 프리팹/모델 | 대부분 준비됨 |

#### 폴더 구조 요약

```
Assets/Project/
├── Animations/Player/, Prisoner/     ← 비어있음
├── Data/                              ← 비어있음
├── Materials/ (20개 .mat)
├── Prefabs/
│   ├── Effects/     ← 비어있음
│   ├── Handcuff/    ← handcuffs원본.fbx
│   ├── Ore/         ← BlueMarlin Gem.prefab, BlueMarlin.prefab
│   ├── Part time/   ← Alba.fbx
│   ├── Player/      ← Alba.fbx, Player.fbx
│   ├── Prisoner/    ← Arrested1~3.fbx, Priosoner2.fbx, Prisoner1.fbx
│   ├── UI&2D/       ← 15개 PNG
│   └── Zone/        ← ProcessingMachine.prefab
├── Scenes/          ← GameScene.unity
└── Scripts/
    ├── Machine/, Manager/, Mining/, Player/, Prisoner/, UI/, Zone/  ← 모두 비어있음
```

#### Third Party Assets

| 패키지 | 용도 |
|---|---|
| Joystick Pack | 모바일 조이스틱 입력 |
| Kabungus | 3D 소품 모델 90+종 |
| LowPolyBarriersPackFree | 담장/울타리 |
| TMI - Dungeon | 감옥 관련 에셋 |

#### 눈에 띄는 빈 곳
1. `Effects/` — 이펙트 프리팹 없음 (아우라, 빵빠레, MAX 텍스트 등 영상에 필요)
2. `Data/` — 데이터 ScriptableObject 없음
3. 스크립트 전체 미작성
4. 애니메이션 클립 없음
5. 드릴차 모델/프리팹 없음

---

### [유저] 폴더 이동 + PlayerController 구현 요청

> 체포자 3종은 다른 폴더를 만들어서 그쪽으로 이동시켜줘. 체포자가 수갑받고 죄수되거든.
> 이팩트는 후순위. 프로젝트 끝나가면 손대보자.
> 처음으로 구현할 것을 알려줄게. 플레이어 이동
>
> Unity 2022.3 모바일 캐주얼 게임용 플레이어 이동 시스템을 만들어줘.
> - 리지드바디 기반 3D 이동 (물리 회전은 Freeze)
> - 이동 방향으로 캐릭터가 부드럽게 회전 (Quaternion.Slerp)
> - 입력은 일단 키보드(WASD)로 받고, 나중에 조이스틱 입력으로 교체할 수 있게 입력부를 메서드로 분리해줘
> - 스크립트명: PlayerController

### [Claude] 폴더 이동 처리

`Prefabs/Prisoner/` 내 Arrested1~3.fbx를 `Prefabs/Arrested/`로 분리 (`.meta` 파일 포함 이동하여 GUID 보존)

| 폴더 | 파일 |
|---|---|
| `Prefabs/Arrested/` | Arrested1.fbx, Arrested2.fbx, Arrested3.fbx (체포자 — 수갑 받기 전) |
| `Prefabs/Prisoner/` | Prisoner1.fbx, Priosoner2.fbx (수감자) |

> Unity 에디터가 열려 있다면 Assets 우클릭 → Reimport All 권장

---

### [Claude] plan.md 작성 (PlayerController)

#### 구현 방식

- **이동**: `Rigidbody.MovePosition()` — 충돌 유지, 미끄러짐 없음
- **회전**: `Quaternion.Slerp` — Rigidbody Rotation Freeze All 후 코드로 직접 제어
- **카메라 기준**: 탑뷰 고정이므로 월드 XZ 평면 기준 이동

#### 입력 교체 구조

```
GetMoveInput() → Vector2   // 이 메서드만 교체하면 WASD → 조이스틱 전환 완료
```

#### 파일 목록

| 파일 | 경로 | 동작 |
|---|---|---|
| PlayerController.cs | Assets/Project/Scripts/Player/ | 신규 생성 |

#### 트레이드오프

| 선택지 | 결정 | 이유 |
|---|---|---|
| MovePosition vs velocity | MovePosition | 충돌 유지, 미끄러짐 없음 |
| 카메라 기준 vs 월드 기준 이동 | 월드 기준 | 탑뷰 고정, 나중에 GetMoveInput() 안에서만 수정하면 됨 |

#### Inspector 노출 변수

| 변수명 | 타입 | 설명 |
|---|---|---|
| moveSpeed | float | 이동 속도 |
| rotationSpeed | float | 회전 보간 속도 |

---

### [유저] 승인

> 확인했어. 진행해

---

### [Claude] TODO 주석 작성 (3단계)

```csharp
public class PlayerController : MonoBehaviour
{
    // TODO: Inspector 노출 변수 선언 (moveSpeed, rotationSpeed)
    // TODO: Rigidbody 캐싱용 private 변수 선언
    // TODO: Awake() — Rigidbody 컴포넌트 가져오기
    // TODO: FixedUpdate() — GetMoveInput() 호출 → 이동 처리 → 회전 처리
    // TODO: GetMoveInput() → Vector2 반환 (조이스틱 교체 포인트)
    // TODO: Move(Vector2 input) — Rigidbody.MovePosition()으로 XZ 평면 이동
    // TODO: Rotate(Vector2 input) — 이동 방향이 있을 때만 Quaternion.Slerp 회전
}
```

---

### [유저] 승인

> 진행해.

---

### [Claude] PlayerController.cs 구현 완료

```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector2 input = GetMoveInput();
        Move(input);
        Rotate(input);
    }

    // 입력 교체 포인트 — 조이스틱으로 전환 시 이 메서드만 수정
    private Vector2 GetMoveInput()
    {
        return new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    private void Move(Vector2 input)
    {
        Vector3 dir = new Vector3(input.x, 0f, input.y).normalized;
        Vector3 nextPos = rb.position + dir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPos);
    }

    private void Rotate(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return;

        Vector3 dir = new Vector3(input.x, 0f, input.y).normalized;
        Quaternion targetRot = Quaternion.LookRotation(dir);
        rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
    }
}
```

#### 씬별 필요 오브젝트 목록

| 오브젝트 | 필요 컴포넌트 |
|---|---|
| Player | `PlayerController`, `Rigidbody`, `CapsuleCollider` |

#### Inspector 연결 목록

| 슬롯명 | 값 |
|---|---|
| Move Speed | 5 (조절 필요) |
| Rotation Speed | 10 (조절 필요) |

#### Rigidbody Constraints 세팅

| Freeze Position | Freeze Rotation |
|---|---|
| Y 체크 | X, Y, Z 모두 체크 |

---

### [유저] 오브젝트 세팅 완료 + 로그 파일 요청

> 플레이어 오브젝트 세팅이 완료됐어.
> 다음단계로 넘어가기전에 요청하나 할게.
> 나의 프롬프트, 너의 응답 전체를 md파일로 만들어줘.

### [Claude] devlog.md 생성

현재 파일이 그 결과물입니다.

---

### [유저] CameraFollow 요청

> PlayerController 오브젝트를 따라가는 CameraFollow 스크립트를 만들어줘.
> - 카메라는 Orthographic 고정. 카메라 설정 자체는 건드리지 마.
> - offset은 Inspector에서 조정 가능하게
> - LateUpdate, Vector3.Lerp로 부드럽게
> - 스크립트명: CameraFollow
> 후반부 카메라 이동 구간이 있기 때문에 부드러운 이동이 필요함

### [Claude] CameraFollow.cs 구현 완료

```csharp
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -7f);
    [SerializeField] private float smoothSpeed = 5f;

    private Transform _player;
    private Vector3 _goalPos;

    // SetTarget(Transform), SetFocusPoint(Vector3), ReturnToPlayer() — 연출용 메서드
}
```

#### Inspector 연결 목록

| 슬롯명 | 연결 |
|---|---|
| Target | Player 오브젝트 |
| Offset | (0, 10, -7) 기본값 |
| Smooth Speed | 5 |

---

### [유저] 조이스틱 + UI 요청

> - 입력 없을 때 alpha 0, 입력 시 나타나는 Dynamic 방식
> - 키보드 WASD / 모바일 터치 드래그 병행
> - 두 입력을 Vector2로 통합해 PlayerController에 전달
> - 우측 상단 소지금 TMP ("₩0" 형식), 좌측 상단 로고 이미지 슬롯
> - 스크립트명: VirtualJoystick

### [Claude] A안 확정 — Joystick Pack DynamicJoystick 활용

Joystick Pack에 이미 완성된 `DynamicJoystick.cs`가 있음을 확인.
VirtualJoystick.cs 대신 기존 에셋 활용 결정.

- `PlayerController.cs` 수정: `Joystick` 타입 참조 추가, `GetMoveInput()`에 `Vector2.ClampMagnitude` 합산
- `HUDManager.cs` 신규: `SetMoney(int)` public 메서드, logoImage 슬롯

#### Canvas 구조 확정

```
UICanvas
├── HUD (빈 오브젝트 — HUDManager 부착)
│   ├── MoneyText (TMP)
│   └── LogoImage (Image)
└── DynamicJoystick (Joystick Pack 프리팹)
```

---

### [유저] UI 구조 논의 — 추천 구조 채택

> 캔버스나 패널이 다수 운영되어서 곤란했던 기억이 있어. 방지하고 싶어.

### [Claude] 최종 Canvas 계층 구조 제안 및 확정

```
UICanvas
├── HUD              ← 항상 표시 (MoneyText, LogoImage, MuteButton)
├── ArrowLayer       ← 방향 화살표 전용
├── PopupLayer       ← MAX텍스트, 말풍선, 구역 연출
├── ScreenLayer      ← 페이드패널, 타이틀, 컨티뉴버튼
└── DynamicJoystick
```

- Canvas 1개 고정, 레이어는 빈 오브젝트(RectTransform만)로 구성
- HUD = 빈 오브젝트 (Panel X — Raycast Target 문제 방지)
- World Space Canvas(Zone UI): 이벤트 카메라 제거, Raycast Target 전부 OFF

---

### [유저] 런타임 버그 3가지 보고

1. 캐릭터가 키입력 방향의 거꾸로 이동
2. 키보드 입력 시 조이스틱 미표시
3. 클릭 드래그로 조이스틱 미작동

### [Claude] 버그 수정

**버그 1 — 이동 방향 반전**: `Move()`, `Rotate()`에서 `-input.x, 0f, -input.y` 부호 반전 (임시 처치)

**버그 2 — 조이스틱 미표시**: 정상 동작. 키보드는 포인터 이벤트 아님 → 조이스틱 UI 미표시가 맞음. 키보드 전용 시각 피드백 별도 구현으로 해결

**버그 3 — 클릭 드래그 미작동 체크리스트**:
- EventSystem 씬 존재 여부
- Canvas GraphicRaycaster 존재 여부
- DynamicJoystick RectTransform stretch 여부
- Background/Handle 슬롯 연결 여부 (씬 인스턴스로 연결해야 함, Project 에셋 X)

---

### [유저] 키보드 조이스틱 연동 요청

> 키보드 조작에 맞춰 조이스틱이 보이길 원해.

### [Claude] KeyboardJoystickVisualizer.cs 구현

- 별도 키보드 전용 시각 오브젝트 (DynamicJoystick과 독립)
- 키 입력 시 배경 활성화, 핸들이 방향 표시
- `_isActive` 플래그로 SetActive 매 프레임 호출 방지

#### Canvas 추가 구조

```
UICanvas
├── KeyboardJoystick (키보드 전용 시각)
│   ├── Background
│   │   └── Handle
```

---

### [유저/Claude] 결함 점검 및 수정

**발견된 결함 4가지:**

| 순위 | 결함 | 처리 |
|---|---|---|
| 즉시 | CameraFollow null 체크 없음 | Start()에 null 가드 추가 |
| 즉시 | 이동 방향 부호 반전 — 임시 처치 | 카메라 기준 방향 계산으로 근본 수정 |
| 낮음 | KeyboardJoystickVisualizer SetActive 매 프레임 | _isActive 플래그로 수정 |
| 예고 | PlayerController 상태 머신 부재 | 채굴 구현 전 도입 예정 |

**카메라 방향 근본 수정:**
> 영상에서 카메라 회전 없음 확인 → Awake에서 한 번만 계산 후 저장이 최적

```csharp
// Awake에서 한 번만
_camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
_camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;

// Move / Rotate에서 사용
Vector3 dir = (_camForward * input.y + _camRight * input.x).normalized;
```

#### 최종 PlayerController.cs

```csharp
[SerializeField] private Camera cam;
private Vector3 _camForward;
private Vector3 _camRight;

private void Awake()
{
    rb = GetComponent<Rigidbody>();
    if (cam == null) cam = Camera.main;
    _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
    _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
}
```

---

### [유저] Ore 시스템 요청

> - 플레이어 자식 Sphere Trigger로 광물 감지
> - 광물 HP, 감지마다 1 감소, 0이면 파괴
> - 파괴 시 파티클 후 재생성
> - 파티클: Burst, Mesh 렌더러로 돌 느낌
> - 오브젝트 풀링으로 관리
> - 스크립트명: Ore, OreManager

### [Claude] Ore 시스템 구현

**생성 파일:**

| 파일 | 역할 |
|---|---|
| `Ore.cs` | HP, TakeDamage, Die, Respawn |
| `OreManager.cs` | 씬 배치 Ore 자동 수집, 리스폰 스케줄 |
| `MiningTrigger.cs` | 플레이어 자식 Sphere Trigger, 채굴 타이머 |

**주요 결정:**
- Ore 136개 씬에 배치된 상태 → Instantiate 불필요, `GetComponentsInChildren<Ore>()` 자동 수집
- SpawnPoints 배열(136개 수동 등록) → `spawnPointsParent` 단일 참조로 변경 (자동 수집)
- 오브젝트 풀링 → 필요 없음 (고정 위치 hide/show 방식이 적합)
- 파티클 → `Instantiate` 방식으로 변경 (자식 오브젝트 활성화 문제 근본 해결)

**Ore.cs 최종:**

```csharp
public class Ore : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    [SerializeField] private GameObject oreBreakEffect;

    private bool _isDead;  // Die() 후 중복 호출 방지

    public void TakeDamage() { if (_isDead) return; ... }
    private void Die() { _isDead = true; Instantiate(oreBreakEffect, ...); ... }
    public void Respawn() { _isDead = false; ... }
}
```

**버그 수정 이력:**
- Play on Awake로 런타임 시작과 동시에 파티클 발생 → `_isDead` 플래그 + Instantiate 방식으로 해결
- 광물 사망 후 콜라이더 유지로 Die() 반복 호출 → `_isDead` 플래그로 차단
- 파티클 미표시 → GameObject Instantiate 방식으로 전환하여 해결

#### OreManager Inspector 연결

| 슬롯명 | 연결 |
|---|---|
| Spawn Points Parent | OreSpawnPoints 부모 오브젝트 |
| Respawn Delay | 5 (Inspector 조절) |

---

### [유저] StackManager 요청

> 플레이어 등 뒤에 아이템을 쌓는 StackManager를 만들어줘.
> 꼬리물기 방식: 1번은 플레이어를, 2번은 1번을, 3번은 2번을 따라감
> Inspector 조정 항목: maxCapacity, stackOriginOffset, itemSpacing, followSpeed, rotationSpeed, swayAmount
> 위치: Vector3.Lerp / 회전: Quaternion.Slerp
> 스크립트명: StackManager, StackItem

### [Claude] plan.md 작성 완료 — 승인 대기 중
