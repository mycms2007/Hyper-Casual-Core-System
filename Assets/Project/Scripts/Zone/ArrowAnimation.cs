using UnityEngine;

/// <summary>
/// 화살표 프리팹에 붙이는 애니메이션 스크립트.
/// 위아래 부드러운 반복 이동.
/// </summary>
public class ArrowAnimation : MonoBehaviour
{
    [Header("위아래 이동")]
    [SerializeField] private float bobHeight = 0.3f;      // 위아래 진폭 (단위: 유니티 unit)
    [SerializeField] private float bobSpeed = 2f;          // 위아래 주기 (클수록 빠름)

    private Vector3 _originLocalPos;

    private void Awake()
    {
        _originLocalPos = transform.localPosition;
    }

    private void Update()
    {
        float offsetY = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = _originLocalPos + new Vector3(0f, offsetY, 0f);
    }
}
