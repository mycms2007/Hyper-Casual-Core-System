using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OfficeZone : Zone
{
    [SerializeField] private HandcuffCarrier carrier;
    [SerializeField] private HandcuffDropZone dropZone;

    private int _presenceCount;
    private bool _draining;
    public bool HasPresence => _presenceCount > 0;

    protected override void OnPlayerEnter(PlayerController player)
    {
        _presenceCount++;
        TryDropHandcuffs(carrier);
    }

    protected override void OnPlayerExit(PlayerController player)
    {
        _presenceCount = Mathf.Max(0, _presenceCount - 1);
    }

    protected override void OnAlbaEnter(AlbaController alba)
    {
        _presenceCount++;
        TryDropHandcuffs(alba.GetComponent<HandcuffCarrier>());
    }

    protected override void OnAlbaExit(AlbaController alba)
    {
        _presenceCount = Mathf.Max(0, _presenceCount - 1);
    }

    private void TryDropHandcuffs(HandcuffCarrier c)
    {
        if (c == null || c.TotalCount == 0) return;
        if (dropZone == null) return;

        var list = c.TakeAll();
        if (list.Count > 0)
        {
            dropZone.Receive(list);
            TutorialManager.Instance?.OnFirstHandcuffDropped();
        }

        if (!_draining && c.PendingCount > 0)
            StartCoroutine(DrainPending(c));
    }

    private IEnumerator DrainPending(HandcuffCarrier c)
    {
        _draining = true;
        while (c.TotalCount > 0)
        {
            yield return null;
            var arrived = c.TakeAll();
            if (arrived.Count > 0) dropZone.Receive(arrived);
        }
        _draining = false;
    }
}
