using UnityEngine;

public enum PlayerState { Idle, Walk }

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private Joystick joystick;
    [SerializeField] private Camera cam;
    [SerializeField] private Animator anim;
    [SerializeField] private MiningTrigger miningTrigger;

    private Rigidbody rb;
    private Vector3 _camForward;
    private Vector3 _camRight;

    private PlayerState _currentState;
    private bool _isMining;
    private bool _forceIdle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (cam == null) cam = Camera.main;
        _camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        _camRight   = Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized;

        ChangeState(PlayerState.Idle);
    }

    private void Update()
    {
        switch (_currentState)
        {
            case PlayerState.Idle: UpdateIdle(); break;
            case PlayerState.Walk: UpdateWalk(); break;
        }

        ApplyAnimator();
    }

    private void FixedUpdate()
    {
        Vector2 input = GetMoveInput();
        Move(input);
        Rotate(input);
    }

    // ── 상태 전환 ──────────────────────────────────────────

    private void ChangeState(PlayerState next)
    {
        if (_currentState == next) return;
        _currentState = next;
    }

    // ── Idle ───────────────────────────────────────────────

    private void UpdateIdle()
    {
        if (HasMoveInput()) ChangeState(PlayerState.Walk);
    }

    // ── Walk ───────────────────────────────────────────────

    private void UpdateWalk()
    {
        if (!HasMoveInput()) ChangeState(PlayerState.Idle);
    }

    // ── 외부 인터페이스 ────────────────────────────────────

    /// <summary>MiningTrigger에서 채굴 가능 여부를 통보합니다.</summary>
    public void SetMiningActive(bool active)
    {
        _isMining = active;
    }

    /// <summary>드릴 장착 시 IsWalking을 강제로 false로 유지합니다.</summary>
    public void SetForceIdle(bool force)
    {
        _forceIdle = force;
    }

    /// <summary>Mining 애니메이션 34프레임 Animation Event에서 호출됩니다.</summary>
    public void OnMiningHit()
    {
        if (miningTrigger != null) miningTrigger.DealDamage();
    }

    public PlayerState CurrentState => _currentState;

    // ── 애니메이터 ─────────────────────────────────────────

    private void ApplyAnimator()
    {
        if (anim == null) return;
        anim.SetBool("IsWalking", !_forceIdle && _currentState == PlayerState.Walk);
        anim.SetBool("IsMining",  _isMining);
    }

    // ── 입력 / 이동 ────────────────────────────────────────

    private bool HasMoveInput() => GetMoveInput().sqrMagnitude > 0.01f;

    private Vector2 GetMoveInput()
    {
        Vector2 keyboard = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        Vector2 stick = joystick != null ? joystick.Direction : Vector2.zero;
        return Vector2.ClampMagnitude(keyboard + stick, 1f);
    }

    private void Move(Vector2 input)
    {
        Vector3 dir = (_camForward * input.y + _camRight * input.x).normalized;
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
    }

    private void Rotate(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return;
        Vector3 dir = (_camForward * input.y + _camRight * input.x).normalized;
        rb.rotation = Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.fixedDeltaTime);
    }
}
