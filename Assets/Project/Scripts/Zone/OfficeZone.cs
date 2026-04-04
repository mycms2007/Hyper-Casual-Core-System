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
        if (carrier == null || carrier.TotalCount == 0) return;
        if (dropZone == null) return;

        var list = carrier.TakeAll();
        if (list.Count > 0) dropZone.Receive(list);

        // 아직 날아오는 중인 수갑이 있으면 도착할 때마다 드랍존으로 전달
        if (!_draining && carrier.PendingCount > 0)
            StartCoroutine(DrainPending());
    }

    // carrier.TotalCount > 0인 동안 매 프레임 새로 도착한 수갑을 드랍존으로 넘김
    private IEnumerator DrainPending()
    {
        _draining = true;
        while (carrier.TotalCount > 0)
        {
            yield return null;
            var arrived = carrier.TakeAll();
            if (arrived.Count > 0) dropZone.Receive(arrived);
        }
        _draining = false;
    }
}
