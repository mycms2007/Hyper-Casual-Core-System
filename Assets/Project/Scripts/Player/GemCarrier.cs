using UnityEngine;

/// <summary>
/// Gem(광물 채굴 결과물) 스택 관리.
/// StackManager를 래핑해 OreManager/Ore가 플레이어 내부 구조에 직접 의존하지 않도록 한다.
/// </summary>
public class GemCarrier : MonoBehaviour
{
    public static GemCarrier Instance { get; private set; }

    [SerializeField] private StackManager stackManager;

    private void Awake() => Instance = this;

    public bool TryAdd(GameObject gemPrefab)
    {
        Debug.Log($"[GemCarrier] TryAdd() — stackManager: {(stackManager != null ? "OK" : "NULL")}, prefab: {(gemPrefab != null ? gemPrefab.name : "NULL")}");
        if (stackManager == null) return false;
        bool result = stackManager.TryAdd(gemPrefab);
        Debug.Log($"[GemCarrier] TryAdd() 결과: {result} (현재 스택 수: {stackManager.Count})");
        return result;
    }

    public int Count => stackManager.Count;

    public void AttachAnchorTo(Transform newParent) => stackManager.AttachRootTo(newParent);
    public void RestoreAnchor() => stackManager.DetachRoot();
}
