using UnityEngine;

/// <summary>
/// 드릴 표면 콜라이더에 붙이는 스크립트.
/// 광물에 닿으면 즉시 파괴. 드릴차에 붙은 경우 DrillCar에도 알림.
/// </summary>
public class DrillSurface : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[DrillSurface] OnTriggerEnter — {other.name}");
        Ore ore = other.GetComponent<Ore>();
        if (ore == null || ore.IsDead) return;

        while (!ore.IsDead) ore.TakeDamage();
    }
}
