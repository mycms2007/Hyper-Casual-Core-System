using UnityEngine;

/// <summary>
/// 감옥 타일에 부착. 죄수가 진입하면 JailManager에 도착 카운트.
/// </summary>
public class JailZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PrisonerController>() != null)
            JailManager.Instance?.RegisterArrived();
    }
}
