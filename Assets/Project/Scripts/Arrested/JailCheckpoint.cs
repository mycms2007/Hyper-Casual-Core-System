using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 감옥 입구 체크포인트.
/// (감옥 인원 + 복도 이동 중 인원) < capacity 이면 통과, 가득 찼으면 Idle 대기.
/// 감옥 확장 시 OnCapacityExpanded 이벤트를 받아 대기 죄수를 순서대로 해제.
/// </summary>
public class JailCheckpoint : MonoBehaviour
{
    private readonly List<PrisonerController> _waiting = new List<PrisonerController>();
    private static bool _locked;

    public static void LockForVideo() => _locked = true;

    private void Awake()
    {
        JailManager.OnCapacityExpanded += ReleaseWaiting;
    }

    private void OnDestroy()
    {
        JailManager.OnCapacityExpanded -= ReleaseWaiting;
    }

    private void OnTriggerEnter(Collider other)
    {
        PrisonerController prisoner = other.GetComponentInParent<PrisonerController>();
        if (prisoner == null) return;
        if (JailManager.Instance == null) return;

        if (JailManager.Instance.IsFull || _locked)
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

    private void ReleaseWaiting()
    {
        if (JailAnimator.IsVideoVersion) return;

        while (_waiting.Count > 0 && !JailManager.Instance.IsFull)
        {
            PrisonerController p = _waiting[0];
            _waiting.RemoveAt(0);
            PassThrough(p);
            p.Resume();
        }
    }
}
