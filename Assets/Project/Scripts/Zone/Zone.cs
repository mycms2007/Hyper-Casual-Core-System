using UnityEngine;

public abstract class Zone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) { OnPlayerEnter(player); return; }

        AlbaController alba = other.GetComponentInParent<AlbaController>();
        if (alba != null) OnAlbaEnter(alba);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null) { OnPlayerExit(player); return; }

        AlbaController alba = other.GetComponentInParent<AlbaController>();
        if (alba != null) OnAlbaExit(alba);
    }

    protected virtual void OnPlayerEnter(PlayerController player) { }
    protected virtual void OnPlayerExit(PlayerController player) { }
    protected virtual void OnAlbaEnter(AlbaController alba) { }
    protected virtual void OnAlbaExit(AlbaController alba) { }
}
