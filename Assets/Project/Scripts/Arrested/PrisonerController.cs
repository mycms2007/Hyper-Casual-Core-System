using UnityEngine;

public class PrisonerController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float waypointDistance = 0.3f;
    [SerializeField] private float arrivalDistance = 0.3f;
    [SerializeField] private float scatterRadius = 1.5f;
    [SerializeField] private float arrivalTimeout = 10f;

    private Vector3 _finalTarget;
    private int _waypointIndex;
    private bool _paused;
    private bool _arrived;
    private bool _hasDestination;
    private float _arrivalTimer;
    private bool _skipJailRegistration;
    private Animator _anim;
    private Rigidbody _rb;
    private Transform[] _waypoints;

    public bool HasArrived => _arrived;

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        _rb = GetComponent<Rigidbody>();
    }

    public void SetDestination(Transform destination)
    {
        Transform effective = JailManager.CurrentJailDestination ?? destination;
        Vector2 rand = Random.insideUnitCircle * scatterRadius;
        _finalTarget = effective.position + new Vector3(rand.x, 0f, rand.y);
        _waypoints = PrisonerPath.Instance != null ? PrisonerPath.Instance.Waypoints : null;
        _waypointIndex = 0;
        _hasDestination = true;
        _arrived = false;
        _paused = false;
        _arrivalTimer = 0f;
        _anim?.SetBool("IsWalking", true);
    }

    public void Pause()
    {
        _paused = true;
        _anim?.SetBool("IsWalking", false);
    }

    public void Resume()
    {
        _paused = false;
        _anim?.SetBool("IsWalking", true);
    }

    private void FixedUpdate()
    {
        if (_paused || _arrived || !_hasDestination) return;

        Vector3 target = GetCurrentTarget();
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        float threshold = IsOnFinalTarget() ? arrivalDistance : waypointDistance;

        if (dist > threshold)
        {
            _rb.MovePosition(_rb.position + dir.normalized * walkSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
        else
        {
            if (!IsOnFinalTarget())
                _waypointIndex++;
            else
                OnArrived();
        }

        if (!_arrived && IsOnFinalTarget())
        {
            _arrivalTimer += Time.fixedDeltaTime;
            if (_arrivalTimer >= arrivalTimeout)
                OnArrived();
        }
    }

    private Vector3 GetCurrentTarget()
    {
        if (_waypoints != null && _waypointIndex < _waypoints.Length)
            return _waypoints[_waypointIndex].position;
        return _finalTarget;
    }

    private bool IsOnFinalTarget()
    {
        return _waypoints == null || _waypointIndex >= _waypoints.Length;
    }

    /// <summary>감옥 확장 후 BigPrison으로 재이동. 경로 없이 직행, JailManager 재등록 안 함.</summary>
    public void MoveToDestination(Transform destination)
    {
        Vector2 rand = Random.insideUnitCircle * scatterRadius;
        _finalTarget = destination.position + new Vector3(rand.x, 0f, rand.y);
        _waypoints = null;
        _waypointIndex = 0;
        _hasDestination = true;
        _arrived = false;
        _paused = false;
        _arrivalTimer = 0f;
        _skipJailRegistration = true;
        _anim?.SetBool("IsWalking", true);
    }

    private void OnArrived()
    {
        _arrived = true;
        _anim?.SetBool("IsWalking", false);
        if (!_skipJailRegistration)
            JailManager.Instance?.RegisterArrived();

        Transform faceTarget = PrisonerPath.Instance?.ArrivalFaceTarget;
        if (faceTarget != null)
        {
            Vector3 dir = faceTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }
}
