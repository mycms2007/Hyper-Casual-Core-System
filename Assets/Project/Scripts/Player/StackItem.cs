using UnityEngine;

public class StackItem : MonoBehaviour
{
    private Transform _target;
    private Vector3 _offset;
    private float _followSpeed;
    private float _rotationSpeed;
    private float _swayAmount;
    private Vector3 _rotationOffset;

    public void Initialize(Transform target, Vector3 offset, float followSpeed, float rotationSpeed, float swayAmount, Vector3 rotationOffset = default)
    {
        _target = target;
        _offset = offset;
        _followSpeed = followSpeed;
        _rotationSpeed = rotationSpeed;
        _swayAmount = swayAmount;
        _rotationOffset = rotationOffset;
    }

    // StackManager의 LateUpdate에서 호출
    public void Tick(Vector3 moveDir)
    {
        // 위치 — 타겟 + 오프셋으로 Lerp
        Vector3 goalPos = _target.position + _offset;
        transform.position = Vector3.Lerp(transform.position, goalPos, _followSpeed * Time.deltaTime);

        // 회전 — 타겟의 Y방향 기준 + sway + 아이템 고유 오프셋
        Quaternion baseRot = Quaternion.Euler(0f, _target.eulerAngles.y, 0f);
        Quaternion swayRot = Quaternion.Euler(moveDir.z * _swayAmount, 0f, -moveDir.x * _swayAmount);
        Quaternion goalRot = baseRot * swayRot * Quaternion.Euler(_rotationOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, goalRot, _rotationSpeed * Time.deltaTime);
    }
}
