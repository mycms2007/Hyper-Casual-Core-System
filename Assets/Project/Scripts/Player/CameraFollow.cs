using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -7f);
    [SerializeField] private float smoothSpeed = 5f;

    private Transform _player;
    private Vector3 _goalPos;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: Target이 연결되지 않았습니다.");
            return;
        }

        _player = target;
        _goalPos = target.position + offset;
    }

    private void LateUpdate()
    {
        if (target != null)
            _goalPos = target.position + offset;

        transform.position = Vector3.Lerp(transform.position, _goalPos, smoothSpeed * Time.deltaTime);
    }

    // 연출용 — 추적 대상을 특정 오브젝트로 교체
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // 연출용 — 대상 없이 특정 월드 좌표 고정
    public void SetFocusPoint(Vector3 worldPos)
    {
        target = null;
        _goalPos = worldPos + offset;
    }

    // 플레이어 추적 복귀
    public void ReturnToPlayer()
    {
        target = _player;
    }
}
