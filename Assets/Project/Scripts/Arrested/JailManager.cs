using UnityEngine;

public class JailManager : MonoBehaviour
{
    public static JailManager Instance { get; private set; }
    public static event System.Action OnJailFull;

    [SerializeField] private int capacity = 20;

    private int _inJailCount;
    private int _walkingCount;
    private bool _jailFullFired;

    public int TotalCount => _inJailCount + _walkingCount;
    public bool IsFull => TotalCount >= capacity;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>체크포인트 통과 시 호출.</summary>
    public void RegisterWalking() => _walkingCount++;

    /// <summary>감옥 도착 시 호출.</summary>
    public void RegisterArrived()
    {
        _walkingCount = Mathf.Max(0, _walkingCount - 1);
        _inJailCount++;

        if (!_jailFullFired && _inJailCount >= capacity)
        {
            _jailFullFired = true;
            OnJailFull?.Invoke();
        }
    }
}
