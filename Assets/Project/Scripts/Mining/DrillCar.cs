using UnityEngine;

/// <summary>
/// 드릴차 운행 관리.
/// - PurchaseZone이 이 GameObject를 활성화하면 구매 완료로 간주.
/// - 플레이어가 광물에 닿으면 MiningTrigger가 StartDrive() 호출.
/// - 0.5초 이상 광물을 못 캐면 자동 종료 후 플레이어 복귀.
/// </summary>
public class DrillCar : MonoBehaviour
{
    public static DrillCar Instance { get; private set; }

    [SerializeField] private float speed = 7f;
    [SerializeField] private float idleTimeout = 0.5f;
    [SerializeField] private GameObject visual; // 드릴차 메시 루트

    public bool IsPurchased { get; private set; }

    private bool _isDriving;
    private Vector3 _direction;
    private GameObject _player;
    private float _lastMineTime;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        IsPurchased = true;
        if (visual != null) visual.SetActive(false);
    }

    public void StartDrive(Vector3 position, Vector3 direction, GameObject player)
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(direction);
        _direction = direction.normalized;
        _player = player;
        _lastMineTime = Time.time;
        _isDriving = true;

        if (visual != null) visual.SetActive(true);
        player.SetActive(false);
    }

    /// <summary>DrillSurface가 광물 파괴 시 호출.</summary>
    public void OnOreMined()
    {
        _lastMineTime = Time.time;
    }

    private void Update()
    {
        if (!_isDriving) return;

        transform.position += _direction * speed * Time.deltaTime;

        if (Time.time - _lastMineTime > idleTimeout)
            EndDrive();
    }

    private void EndDrive()
    {
        _isDriving = false;
        if (visual != null) visual.SetActive(false);

        if (_player != null)
        {
            _player.transform.position = transform.position;
            _player.SetActive(true);
        }
    }
}
