using UnityEngine;

/// <summary>
/// 알바 NPC.
/// 상태 흐름:
///   Init(0.2s) → Search → WalkToHandcuff → CollectWait(3s) → WalkToIdle → Search → ...
///
/// 고용되는 순간 IsHired = true → 플레이어는 HandcuffStackZone에서 수갑 수집 불가.
/// </summary>
public class AlbaController : MonoBehaviour
{
    public static bool IsHired { get; private set; }

    private enum State { Init, Search, WalkToHandcuff, CollectWait, WalkToIdle, IdleWait }

    [Header("이동")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float arrivalDistance = 0.5f;

    [Header("타이밍")]
    [SerializeField] private float initDelay = 0.2f;
    [SerializeField] private float collectDuration = 3f;
    [SerializeField] private float searchInterval = 2f;
    [SerializeField] private float idleWaitDuration = 2f;

    [Header("참조 (AlbaSpawner가 주입)")]
    private HandcuffStackZone handcuffStackZone;
    private Transform idlePosition;

    [Header("참조 (프리팹 내부)")]
    [SerializeField] private Transform albaAnchor;

    private Rigidbody _rb;
    private Animator _anim;
    private HandcuffCarrier _carrier;
    private State _state;
    private float _timer;

    public void Initialize(HandcuffStackZone zone, Transform idlePos)
    {
        handcuffStackZone = zone;
        idlePosition      = idlePos;
        Debug.Log($"[AlbaController] Initialize — zone={zone?.name}, idlePos={idlePos?.name}");
    }

    private void Awake()
    {
        _rb      = GetComponent<Rigidbody>();
        _anim    = GetComponentInChildren<Animator>();
        _carrier = GetComponent<HandcuffCarrier>();
        IsHired  = true;
    }

    private void OnEnable()
    {
        _state = State.Init;
        _timer = initDelay;
        SetAnim(false);
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Init:        UpdateInit();        break;
            case State.Search:      UpdateSearch();      break;
            case State.CollectWait: UpdateCollectWait(); break;
            case State.IdleWait:    UpdateIdleWait();    break;
        }
    }

    private void FixedUpdate()
    {
        if (_state == State.WalkToHandcuff)
            MoveTowards(handcuffStackZone.transform.position);
        else if (_state == State.WalkToIdle)
            MoveTowards(idlePosition.position);
    }

    // ── 상태 업데이트 ──────────────────────────────────────────────

    private void UpdateInit()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) EnterSearch();
    }

    private void UpdateSearch()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = searchInterval;

        if (handcuffStackZone == null)
        {
            Debug.LogWarning("[AlbaController] handcuffStackZone이 null — Initialize가 호출되지 않았습니다.");
            return;
        }

        Debug.Log($"[AlbaController] 탐색 중 — StackCount: {handcuffStackZone.StackCount}");

        if (handcuffStackZone.StackCount >= 1)
            EnterWalkToHandcuff();
    }

    private void UpdateCollectWait()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) EnterWalkToIdle();
    }

    private void UpdateIdleWait()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) EnterSearch();
    }

    // ── 상태 전환 ──────────────────────────────────────────────────

    private void EnterSearch()
    {
        _state = State.Search;
        _timer = 0f; // 즉시 첫 탐색
        SetAnim(false);
    }

    private void EnterWalkToHandcuff()
    {
        _state = State.WalkToHandcuff;
        SetAnim(true);
    }

    private void EnterCollectWait()
    {
        _state = State.CollectWait;
        _timer = collectDuration;
        SetAnim(false);
        Debug.Log($"[AlbaController] EnterCollectWait — carrier={(_carrier != null ? "OK" : "NULL")}, albaAnchor={(albaAnchor != null ? "OK" : "NULL")}");
        handcuffStackZone.TryTransferToAlba(_carrier, albaAnchor);
    }

    private void EnterWalkToIdle()
    {
        _state = State.WalkToIdle;
        SetAnim(true);
    }

    private void ArriveAtIdle()
    {
        transform.rotation = Quaternion.LookRotation(Vector3.right);
        _state = State.IdleWait;
        _timer = idleWaitDuration;
        SetAnim(false);
    }

    // ── 이동 ──────────────────────────────────────────────────────

    private void MoveTowards(Vector3 target)
    {
        Vector3 dir = target - _rb.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        _rb.MovePosition(_rb.position + dir.normalized * walkSpeed * Time.fixedDeltaTime);
        _rb.rotation = Quaternion.LookRotation(dir.normalized);

        float dist = new Vector2(
            _rb.position.x - target.x,
            _rb.position.z - target.z).magnitude;

        if (dist <= arrivalDistance)
        {
            if (_state == State.WalkToHandcuff) EnterCollectWait();
            else if (_state == State.WalkToIdle) ArriveAtIdle();
        }
    }

    // ── 애니메이션 ─────────────────────────────────────────────────

    private void SetAnim(bool walking)
    {
        _anim?.SetBool("IsWalking", walking);
    }

    // 클립에 내장된 Animation Event 수신 (알바는 아무것도 하지 않음)
    private void OnMiningHit() { }
}
