using UnityEngine;

public class OfficeZone : Zone
{
    [SerializeField] private HandcuffCarrier carrier;
    [SerializeField] private HandcuffDropZone dropZone;

    private int _presenceCount;
    public bool HasPresence => _presenceCount > 0;

    protected override void OnPlayerEnter(PlayerController player)
    {
        _presenceCount++;
        TryDropHandcuffs();
    }

    protected override void OnPlayerExit(PlayerController player)
    {
        _presenceCount = Mathf.Max(0, _presenceCount - 1);
    }

    protected override void OnAlbaEnter(AlbaController alba)
    {
        _presenceCount++;
    }

    protected override void OnAlbaExit(AlbaController alba)
    {
        _presenceCount = Mathf.Max(0, _presenceCount - 1);
    }

    private void TryDropHandcuffs()
    {
        if (carrier == null || carrier.Count == 0) return;
        if (dropZone == null) return;
        dropZone.Receive(carrier.TakeAll());
    }
}
