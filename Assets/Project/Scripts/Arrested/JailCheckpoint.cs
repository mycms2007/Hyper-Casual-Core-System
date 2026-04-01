using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 감옥 입구 체크포인트.
/// (감옥 인원 + 복도 이동 중 인원) < 20 이면 통과, 20이면 Idle 대기.
/// </summary>
public class JailCheckpoint : MonoBehaviour
{
    private readonly List<PrisonerController> _waiting = new List<PrisonerController>();

    private void OnTriggerEnter(Collider other)
    {
        PrisonerController prisoner = other.GetComponentInParent<PrisonerController>();
        if (prisoner == null) return;

        if (JailManager.Instance.IsFull)
        {
            prisoner.Pause();
            _waiting.Add(prisoner);
        }
        else
        {
            PassThrough(prisoner);
        }
    }

    private void PassThrough(PrisonerController prisoner)
    {
        JailManager.Instance.RegisterWalking();
    }
}
