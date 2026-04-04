using UnityEngine;

/// <summary>
/// 고용된 삼광부 NPC.
///
/// 광석 선택 우선순위:
///   1) 정면 콘(forwardThreshold) 안에 있는 광석 중 가장 가까운 것
///   2) 없으면 전방향 중 가장 가까운 것
///   3) 거리 동률(distEpsilon 이내)이면 오른쪽(transform.right) 방향 우선
///   ※ 다른 광부가 Claim한 광석은 항상 건너뜀
///
/// 상태 흐름:
///   FindOre → MoveToOre → Mining → Pause(1s 서있기) → FindOre → ...
///
/// 광석이 소멸하면(플레이어에게 뺏기든, 내가 캐든) 항상 Pause를 거쳐 재탐색.
/// </summary>
public class MinerWorker : MonoBehaviour
{
    private enum State { FindOre, MoveToOre, Mining, Pause }

    [Header("이동")]
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float arrivalDistance = 0.8f;

    [Header("채굴")]
    [SerializeField] private float mineInterval = 0.5f;
    [SerializeField] private float mineRange = 1.2f;

    [Header("광석 선택")]
    [Tooltip("정면 우선 범위. 1=정면만, 0=전방향. cos(45°)≈0.7 권장")]
    [SerializeField] private float forwardThreshold = 0.7f;
    [Tooltip("거리 동률 판정 오차 (이 값 이내면 오른쪽 우선 처리)")]
    [SerializeField] private float distEpsilon = 0.05f;

    [Header("광석 뺏김 후 대기")]
    [SerializeField] private float pauseDuration = 1f;

    private Rigidbody _rb;
    private Animator _anim;
    private State _state;
    private Ore _targetOre;
    private float _nextMineTime;
    private float _pauseEndTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anim = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        _state = State.FindOre;
        _targetOre = null;
    }

    private void OnDisable()
    {
        ReleaseTarget();
    }

    private void Update()
    {
        switch (_state)
        {
            case State.FindOre:   UpdateFindOre();   break;
            case State.MoveToOre: UpdateMoveToOre(); break;
            case State.Mining:    UpdateMining();    break;
            case State.Pause:     UpdatePause();     break;
        }
    }

    private void FixedUpdate()
    {
        if (_state == State.MoveToOre && _targetOre != null)
            MoveTo(_targetOre.transform.position);
    }

    // ──────────────────────────────────────────────────────────────
    // 상태별 업데이트
    // ──────────────────────────────────────────────────────────────

    private void UpdateFindOre()
    {
        if (OreManager.Instance == null) return;

        Ore ore = SelectTargetOre();
        if (ore == null) return; // 광석 없음 — 다음 프레임에 재시도

        SetTarget(ore);
        EnterMoveToOre();
    }

    private void UpdateMoveToOre()
    {
        if (_targetOre == null || _targetOre.IsDead)
        {
            ReleaseTarget();
            EnterPause(); // 뺏김 처리
            return;
        }

        float dist = Vector3.Distance(transform.position, _targetOre.transform.position);
        if (dist <= arrivalDistance)
            EnterMining();
    }

    private void UpdateMining()
    {
        if (_targetOre == null || _targetOre.IsDead)
        {
            ReleaseTarget();
            EnterPause(); // 뺏김 처리
            return;
        }

        float dist = Vector3.Distance(transform.position, _targetOre.transform.position);
        if (dist > mineRange)
        {
            EnterMoveToOre();
            return;
        }

        if (Time.time >= _nextMineTime)
        {
            _nextMineTime = Time.time + mineInterval;
            _targetOre.TakeDamage();
        }
    }

    private void UpdatePause()
    {
        if (Time.time >= _pauseEndTime)
            EnterFindOre();
    }

    // ──────────────────────────────────────────────────────────────
    // 광석 선택 — 정면 우선, 거리 동률 시 오른쪽 우선
    // ──────────────────────────────────────────────────────────────

    private Ore SelectTargetOre()
    {
        Ore[] ores = OreManager.Instance.GetAliveUnclaimedOres();
        if (ores.Length == 0) return null;

        Vector3 fwd   = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x,   0f, transform.right.z).normalized;

        Ore bestForward = null;
        float bestForwardDist = float.MaxValue;

        Ore bestAny = null;
        float bestAnyDist = float.MaxValue;

        foreach (Ore ore in ores)
        {
            Vector3 toOre = ore.transform.position - transform.position;
            toOre.y = 0f;
            float dist = toOre.magnitude;
            Vector3 dir = dist > 0.001f ? toOre / dist : fwd;

            bool isForward = Vector3.Dot(dir, fwd) >= forwardThreshold;

            if (isForward && IsBetter(ore, dist, bestForward, bestForwardDist, right))
            {
                bestForward = ore;
                bestForwardDist = dist;
            }

            if (IsBetter(ore, dist, bestAny, bestAnyDist, right))
            {
                bestAny = ore;
                bestAnyDist = dist;
            }
        }

        return bestForward != null ? bestForward : bestAny;
    }

    /// <summary>candidate가 current보다 더 좋은 광석이면 true.</summary>
    private bool IsBetter(Ore candidate, float candidateDist,
                          Ore current,   float currentDist, Vector3 right)
    {
        if (current == null) return true;
        if (candidateDist < currentDist - distEpsilon) return true;
        if (candidateDist > currentDist + distEpsilon) return false;

        // 거리 동률 → 오른쪽 우선
        Vector3 toCandidate = (candidate.transform.position - transform.position);
        toCandidate.y = 0f;
        Vector3 toCurrent = (current.transform.position - transform.position);
        toCurrent.y = 0f;

        float rCandidate = Vector3.Dot(toCandidate.normalized, right);
        float rCurrent   = Vector3.Dot(toCurrent.normalized,   right);
        return rCandidate > rCurrent;
    }

    // ──────────────────────────────────────────────────────────────
    // 상태 전환
    // ──────────────────────────────────────────────────────────────

    private void EnterFindOre()
    {
        _state = State.FindOre;
        SetAnimation(walking: false, mining: false);
    }

    private void EnterMoveToOre()
    {
        _state = State.MoveToOre;
        SetAnimation(walking: true, mining: false);
    }

    private void EnterMining()
    {
        _state = State.Mining;
        _nextMineTime = Time.time + mineInterval;
        SetAnimation(walking: false, mining: true);
        FaceTarget(_targetOre.transform.position);
    }

    private void EnterPause()
    {
        _state = State.Pause;
        _pauseEndTime = Time.time + pauseDuration;
        SetAnimation(walking: false, mining: false);
    }

    // ──────────────────────────────────────────────────────────────
    // Claim 관리
    // ──────────────────────────────────────────────────────────────

    private void SetTarget(Ore ore)
    {
        ReleaseTarget();
        _targetOre = ore;
        _targetOre.Claim();
    }

    private void ReleaseTarget()
    {
        if (_targetOre != null)
        {
            _targetOre.Unclaim();
            _targetOre = null;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 이동 / 회전
    // ──────────────────────────────────────────────────────────────

    private void MoveTo(Vector3 target)
    {
        Vector3 dir = target - _rb.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        _rb.MovePosition(_rb.position + dir.normalized * walkSpeed * Time.fixedDeltaTime);
        _rb.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ──────────────────────────────────────────────────────────────
    // 애니메이션
    // ──────────────────────────────────────────────────────────────

    private void SetAnimation(bool walking, bool mining)
    {
        _anim?.SetBool("IsWalking", walking);
        _anim?.SetBool("IsMining",  mining);
    }
}
