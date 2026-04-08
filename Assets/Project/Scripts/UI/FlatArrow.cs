using UnityEngine;

/// <summary>
/// Screen Space Canvas 위에서 플레이어 근처에 위치하며 목표 방향을 가리키는 납작화살표.
/// 플레이어 스크린 좌표 기준으로 orbitRadius만큼 떨어진 위치에 배치되고,
/// 목표를 향해 부드럽게 회전한다.
/// </summary>
public class FlatArrow : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Transform player;

    [Header("궤도 설정 (스크린 픽셀 단위)")]
    [SerializeField] private float orbitRadius = 80f;

    [Header("회전 설정")]
    [SerializeField] private float rotateSpeed = 8f;

    [Tooltip("화살표 스프라이트가 기본으로 가리키는 방향 보정. 위쪽(↑)이면 0, 오른쪽(→)이면 -90")]
    [SerializeField] private float rotationOffset = 0f;

    [Header("전진 펄스")]
    [SerializeField] private float pulseAmount = 12f;
    [SerializeField] private float pulseSpeed  = 3f;

    private Transform _target;
    private Camera _cam;
    private float _currentAngle;

    private void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        _cam = Camera.main;
        gameObject.SetActive(false);
    }

    public void Show(Transform target)
    {
        _target = target;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _target = null;
    }

    private void Update()
    {
        if (_target == null || player == null || _cam == null) return;

        // 플레이어와 목표의 스크린 좌표
        Vector2 playerScreen = _cam.WorldToScreenPoint(player.position);
        Vector2 targetScreen = _cam.WorldToScreenPoint(_target.position);

        // 스크린상 방향
        Vector2 dir = (targetScreen - playerScreen).normalized;

        // 화살표 위치: 플레이어 스크린 좌표에서 orbitRadius + 펄스만큼 목표 방향으로
        float pulse = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * pulseAmount;
        rectTransform.position = playerScreen + dir * (orbitRadius + pulse);

        // 회전: 방향 각도 계산 후 부드럽게 보간
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + rotationOffset;
        _currentAngle = Mathf.LerpAngle(_currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
        rectTransform.rotation = Quaternion.Euler(0f, 0f, _currentAngle);
    }
}
