using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// 영상버전(videoVersion) 전용 시네마틱 카메라 연출 총괄.
/// 게임버전에서는 아무것도 실행하지 않는다.
/// </summary>
public class CinematicDirector : MonoBehaviour
{
    [Header("가상 카메라")]
    [SerializeField] private CinemachineVirtualCamera drillZoneCam;
    [SerializeField] private CinemachineVirtualCamera expansionZoneCam;
    [SerializeField] private CinemachineVirtualCamera overviewCam;

    [Header("타이밍")]
    [SerializeField] private float drillCamDelay       = 0.01f;
    [SerializeField] private float drillCamHold        = 3f;
    [SerializeField] private float expansionCamDelay   = 0.01f;
    [SerializeField] private float expansionCamHold    = 3f;
    [SerializeField] private float overviewReturnDelay = 1f;   // 축하이펙트 재생 후 복귀 대기
    [SerializeField] private float endPanelDelay       = 0.2f; // 복귀 후 EndPanel까지 대기

    [Header("참조")]
    [SerializeField] private EndPanelController endPanel;
    [SerializeField] private PlayerController player;

    private void Awake()
    {
        PlayerWallet.OnFirstMoneyEarned    += OnFirstMoneyEarned;
        JailManager.OnJailFull             += OnJailFull;
        JailManager.OnCapacityExpanded     += OnCapacityExpanded;
        JailAnimator.OnCelebrationPlayed   += OnCelebrationPlayed;
    }

    private void OnDestroy()
    {
        PlayerWallet.OnFirstMoneyEarned    -= OnFirstMoneyEarned;
        JailManager.OnJailFull             -= OnJailFull;
        JailManager.OnCapacityExpanded     -= OnCapacityExpanded;
        JailAnimator.OnCelebrationPlayed   -= OnCelebrationPlayed;
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────

    private void OnFirstMoneyEarned()
    {
        if (!JailAnimator.IsVideoVersion) return;
        StartCoroutine(ShowCam(drillZoneCam, drillCamDelay, drillCamHold));
    }

    private void OnJailFull()
    {
        if (!JailAnimator.IsVideoVersion) return;
        StartCoroutine(ShowCam(expansionZoneCam, expansionCamDelay, expansionCamHold));
    }

    private void OnCapacityExpanded()
    {
        if (!JailAnimator.IsVideoVersion) return;
        if (overviewCam != null) overviewCam.Priority = 11;
        player?.SetMovementLocked(true);
    }

    private void OnCelebrationPlayed()
    {
        if (!JailAnimator.IsVideoVersion) return;
        StartCoroutine(ReturnFromOverview());
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    private IEnumerator ShowCam(CinemachineVirtualCamera cam, float delay, float hold)
    {
        if (cam == null) yield break;
        yield return new WaitForSeconds(delay);
        cam.Priority = 11;
        player?.SetMovementLocked(true);
        yield return new WaitForSeconds(hold);
        cam.Priority = 0;
        player?.SetMovementLocked(false);
    }

    private IEnumerator ReturnFromOverview()
    {
        yield return new WaitForSeconds(overviewReturnDelay);
        if (overviewCam != null) overviewCam.Priority = 0;
        player?.SetMovementLocked(false);
        yield return new WaitForSeconds(endPanelDelay);
        endPanel?.OpenPanel();
    }
}
