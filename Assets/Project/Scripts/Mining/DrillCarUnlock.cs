using UnityEngine;

/// <summary>
/// DrillCarUp PurchaseZone의 activateTargets에 등록.
/// 활성화되는 순간 DrillCar.Instance에 구매 완료를 알린다.
/// </summary>
public class DrillCarUnlock : MonoBehaviour
{
    private void OnEnable()
    {
        Debug.Log($"[DrillCarUnlock] OnEnable 호출됨 — DrillCar.Instance: {(DrillCar.Instance != null ? "OK" : "NULL")}");
        if (DrillCar.Instance != null)
            DrillCar.Instance.Purchase();
        else
            Debug.LogWarning("[DrillCarUnlock] DrillCar.Instance가 null — DrillCar 오브젝트가 활성화 상태인지 확인하세요.");
    }
}
