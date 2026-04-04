using UnityEngine;

/// <summary>
/// 플레이어 등에 쌓이는 돈 코인 스택 관리.
/// StackManager의 체인 추적 + 흔들림 시스템을 그대로 활용.
/// </summary>
public class MoneyCarrier : MonoBehaviour
{
    public static MoneyCarrier Instance { get; private set; }

    [SerializeField] private StackManager stackManager;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int wonPerCoin = 15;

    private void Awake() => Instance = this;

    public void AddMoney(int amount)
    {
        int count = amount / wonPerCoin;
        for (int i = 0; i < count; i++)
            stackManager.TryAdd(coinPrefab);
    }

    public void AttachAnchorTo(Transform newParent) => stackManager.AttachRootTo(newParent);
    public void RestoreAnchor() => stackManager.DetachRoot();

    public void SpendMoney(int remainingMoney)
    {
        int target = remainingMoney / wonPerCoin;
        int toRemove = stackManager.Count - target;
        for (int i = 0; i < toRemove; i++)
        {
            GameObject removed = stackManager.TryRemove();
            if (removed != null) Destroy(removed);
        }
    }
}
