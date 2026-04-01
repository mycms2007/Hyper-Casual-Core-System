using UnityEngine;

public class PrisonerController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float waypointDistance = 0.3f;
    [SerializeField] private float arrivalDistance = 0.3f;
    [SerializeField] private float scatterRadius = 1.5f;

    private Vector3 _finalTarget;
    private int _waypointIndex;
    private bool _paused;
    private bool _arrived;
    private bool _hasDestination;
    private Animator _anim;
    private Rigidbody _rb;
    private Transform[] _waypoints;

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        _rb = GetComponent<Rigidbody>();
    }

    public void SetDestination(Transform destination)
    {
        Vector2 rand = Random.insideUnitCircle * scatterRadius;
        _finalTarget = destination.position + new Vector3(rand.x, 0f, rand.y);
        _waypoints = PrisonerPath.Instance != null ? PrisonerPath.Instance.Waypoints : null;
        _waypointIndex = 0;
        _hasDestination = true;
        _arrived = false;
        _paused = false;
        _anim?.SetBool("IsWalking", true);
    }

    public void Pause()
    {
        _paused = true;
        _anim?.SetBool("IsWalking", false);
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

    private void OnArrived()
    {
        _arrived = true;
        _anim?.SetBool("IsWalking", false);
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
