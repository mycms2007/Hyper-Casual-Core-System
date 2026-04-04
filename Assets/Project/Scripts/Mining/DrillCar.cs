using UnityEngine;

public class DrillCar : MonoBehaviour
{
    public static DrillCar Instance { get; private set; }

    [Header("이동")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private Joystick joystick;
    [SerializeField] private float idleTimeout = 0.3f;

    [Header("연출")]
    [SerializeField] private GameObject visual;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    [Header("광물 감지")]
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private float drillSideOffset = 0f;

    [Header("모델 보정")]
    [SerializeField] private float modelYaw = -90f;

    [Header("아이템 앵커")]
    [SerializeField] private Transform gemAnchor;
    [SerializeField] private Transform moneyAnchor;
    [SerializeField] private Transform handcuffAnchor;

    [Header("참조")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject drillObject;
    [SerializeField] private Camera cam;
    [SerializeField] private GemCarrier gemCarrier;
    [SerializeField] private MoneyCarrier moneyCarrier;
    [SerializeField] private HandcuffCarrier handcuffCarrier;

    public bool IsPurchased { get; private set; }

    private bool _isDriving;
    private GameObject _player;
    private float _lastMineTime;
    private float _playerOriginalY;
    private Vector3 _camForward;
    private Vector3 _camRight;

    private void Awake()
    {
        Instance = this;
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
        }
    }

    private void OnEnable()
    {
        IsPurchased = true;
        if (visual != null) visual.SetActive(false);
        if (drillObject != null) drillObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void DebugInstantPurchase()
    {
        IsPurchased = true;
        if (visual != null) visual.SetActive(false);
        if (drillObject != null) drillObject.SetActive(false);
        Debug.Log("[DrillCar] 디버그 즉시구매");
    }
#endif

    public void StartDrive(Vector3 position, GameObject player)
    {
        if (_isDriving) return;

        transform.SetParent(null);
        transform.position = position + spawnOffset;

        // 카메라 방향 즉시 갱신 후 초기 회전 세팅 (백무빙 방지)
        if (cam != null)
        {
            _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
        }
        if (_camForward.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(-_camForward) * Quaternion.Euler(0f, modelYaw, 0f);

        Debug.Log($"[DrillCar] StartDrive ▶ cam={(cam != null ? cam.name : "NULL")}" +
                  $"\n  cam.transform.forward={cam?.transform.forward}" +
                  $"\n  _camForward(XZ투영)={_camForward}  _camRight(XZ투영)={_camRight}" +
                  $"\n  DrillCar 초기위치={transform.position}  초기rotation={transform.rotation.eulerAngles}" +
                  $"\n  player위치={player.transform.position}  spawnOffset={spawnOffset}  modelYaw={modelYaw}");

        _player = player;
        _playerOriginalY = player.transform.position.y;
        _lastMineTime = Time.time;
        _debugInputLogged = false;
        _isDriving = true;

        if (visual != null) visual.SetActive(true);
        if (playerController != null)
        {
            playerController.enabled = false;
            playerController.SetVisible(false);
        }
        if (cameraFollow != null) cameraFollow.SetTarget(transform);

        if (gemCarrier != null && gemAnchor != null) gemCarrier.AttachAnchorTo(gemAnchor);
        if (moneyCarrier != null && moneyAnchor != null) moneyCarrier.AttachAnchorTo(moneyAnchor);
        if (handcuffCarrier != null && handcuffAnchor != null) handcuffCarrier.AttachAnchorTo(handcuffAnchor);
    }

    private void Update()
    {
        if (!_isDriving) return;

        if (cam != null)
        {
            _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;
        }

        Vector2 input = GetInput();
        Move(input);
        Rotate(input);

        bool mined = false;
        Vector3 detectionCenter = transform.position + transform.right * drillSideOffset;
        Collider[] hits = Physics.OverlapSphere(detectionCenter, detectionRadius);
        foreach (Collider col in hits)
        {
            Ore ore = col.GetComponent<Ore>();
            if (ore != null && !ore.IsDead)
            {
                while (!ore.IsDead) ore.TakeDamage();
                mined = true;
            }
        }
        if (mined) _lastMineTime = Time.time;

        if (Time.time - _lastMineTime > idleTimeout)
            EndDrive();
    }

    private bool _debugInputLogged;

    private Vector2 GetInput()
    {
        Vector2 keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 stick = joystick != null ? joystick.Direction : Vector2.zero;
        Vector2 result = Vector2.ClampMagnitude(keyboard + stick, 1f);

        // 처음 입력이 들어올 때 한 번만 로그
        if (!_debugInputLogged && result.sqrMagnitude > 0.01f)
        {
            _debugInputLogged = true;
            Vector3 dir = (_camForward * result.y + _camRight * result.x).normalized;
            Debug.Log($"[DrillCar] 첫 입력 감지 ▶" +
                      $"\n  keyboard={keyboard}  stick={stick}  result={result}" +
                      $"\n  최종 이동방향(dir)={dir}" +
                      $"\n  DrillCar 현재위치={transform.position}  현재rotation={transform.rotation.eulerAngles}" +
                      $"\n  경과시간(StartDrive~입력)={Time.time - _lastMineTime:F3}s");
        }

        return result;
    }

    private void Move(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return;
        Vector3 dir = (_camForward * input.y + _camRight * input.x).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    private void Rotate(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return;
        Vector3 dir = (_camForward * input.y + _camRight * input.x).normalized;
        Quaternion targetRot = Quaternion.LookRotation(-dir) * Quaternion.Euler(0f, modelYaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    private void EndDrive()
    {
        _isDriving = false;
        if (visual != null) visual.SetActive(false);
        if (cameraFollow != null) cameraFollow.ReturnToPlayer();

        if (_player != null)
        {
            Vector3 restorePos = transform.position;
            restorePos.y = _playerOriginalY;
            Debug.Log($"[DrillCar] EndDrive ▶ 드라이브 종료" +
                      $"\n  DrillCar 최종위치={transform.position}" +
                      $"\n  플레이어 복귀위치={restorePos}" +
                      $"\n  StartDrive 이후 경과={Time.time - _lastMineTime:F3}s (idleTimeout={idleTimeout})" +
                      $"\n  → 입력 로그가 찍혔나요? [{(_debugInputLogged ? "YES - 실제 이동 문제" : "NO - 너무 빨리 종료됨")}]");
            _player.transform.position = restorePos;
            if (playerController != null)
            {
                playerController.SetVisible(true);
                playerController.enabled = true;
            }
        }

        gemCarrier?.RestoreAnchor();
        moneyCarrier?.RestoreAnchor();
        handcuffCarrier?.RestoreAnchor();
    }
}
