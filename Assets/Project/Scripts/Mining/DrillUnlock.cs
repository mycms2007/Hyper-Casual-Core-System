using UnityEngine;

/// <summary>
/// DrillUp PurchaseZone의 activateTargets에 등록.
/// 활성화되는 순간 MiningTrigger에 드릴 구매 완료를 알린다.
/// </summary>
public class DrillUnlock : MonoBehaviour
{
    [SerializeField] private MiningTrigger miningTrigger;

    private void OnEnable()
    {
        Debug.Log($"[DrillUnlock] OnEnable — miningTrigger={miningTrigger}");
        miningTrigger?.UnlockDrill();
    }
}
