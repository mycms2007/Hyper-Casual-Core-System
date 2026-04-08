using UnityEngine;

/// <summary>
/// 감옥 확장 실행기.
/// PurchaseZone의 delayedActivateTargets에 연결된 GameObject에 부착.
/// OnEnable 시 JailManager 수용량을 늘리고 대기 죄수를 해제.
/// 확장에 따른 오브젝트 연출은 이후 단계에서 구현.
/// </summary>
public class JailExpander : MonoBehaviour
{
    [SerializeField] private int expandAmount = 20;
    [SerializeField] private Transform newJailDestination;

    private void OnEnable()
    {
        JailManager.Instance?.ExpandCapacity(expandAmount);
        if (newJailDestination != null)
            JailManager.Instance?.SetJailDestination(newJailDestination);
    }
}
