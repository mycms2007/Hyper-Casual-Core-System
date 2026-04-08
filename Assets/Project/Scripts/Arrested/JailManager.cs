using UnityEngine;

public class JailManager : MonoBehaviour
{
    public static JailManager Instance { get; private set; }
    public static event System.Action OnJailFull;
    public static event System.Action OnPrisonerEntered;
    public static event System.Action<int> OnCountChanged;
    public static event System.Action OnCapacityExpanded;

    [SerializeField] private int capacity = 20;

    private int _inJailCount;
    private int _walkingCount;
    private bool _jailFullFired;

    public int InJailCount => _inJailCount;
    public int Capacity => capacity;
    public int TotalCount => _inJailCount + _walkingCount;
    public bool IsFull => TotalCount >= capacity;
    public static Transform CurrentJailDestination { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>체크포인트 통과 시 호출.</summary>
    public void RegisterWalking()
    {
        _walkingCount++;
        OnPrisonerEntered?.Invoke();
    }

    /// <summary>감옥 도착 시 호출.</summary>
    public void RegisterArrived()
    {
        _walkingCount = Mathf.Max(0, _walkingCount - 1);
        _inJailCount++;
        OnCountChanged?.Invoke(_inJailCount);

        if (!_jailFullFired && _inJailCount >= capacity)
        {
            _jailFullFired = true;
            OnJailFull?.Invoke();
        }
    }

    /// <summary>감옥 확장 시 호출. 수용량 증가 + 대기 죄수 해제 트리거.</summary>
    public void ExpandCapacity(int amount)
    {
        capacity += amount;
        _jailFullFired = false;
        OnCapacityExpanded?.Invoke();
    }

    /// <summary>확장 후 신규 죄수의 목적지를 교체.</summary>
    public void SetJailDestination(Transform destination)
    {
        CurrentJailDestination = destination;
    }
}
