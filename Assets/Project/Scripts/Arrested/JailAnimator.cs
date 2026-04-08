using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 항상 활성화. videoVersion 체크 여부에 따라 분기.
///
/// [영상버전] OnDisplayFull → doorRiseDelay 후 Door 상승
///            OnCapacityExpanded → Door↓ → Wall↓ → BigPrison↑ → 침대 스프링 → 축하이펙트
///
/// [실제버전] OnCapacityExpanded → Wall↓(+CountUI 페이드) → BigPrison↑ → BPZoneCanvas 등장
///            → 침대 스프링 → 0.5s 후 죄수들 BigPrison으로 이동
/// </summary>
public class JailAnimator : MonoBehaviour
{
    [SerializeField] private bool videoVersion;

    [Header("공통 오브젝트")]
    [SerializeField] private Transform jailWall;
    [SerializeField] private Transform bigPrison;
    [SerializeField] private Transform bedsParent;

    [Header("영상버전 전용")]
    [SerializeField] private Transform jailDoor;
    [SerializeField] private CelebrationEffect celebrationEffect;

    [Header("실제버전 전용")]
    [SerializeField] private GameObject countUI;
    [SerializeField] private float countUIFadeDuration  = 0.8f;
    [SerializeField] private GameObject bpZoneCanvas;
    [SerializeField] private Transform bigPrisonDestination;
    [SerializeField] private float prisonerMoveStagger  = 0.12f;

    [Header("이동 설정")]
    [SerializeField] private float doorHideOffset       = 2.5f;
    [SerializeField] private float bigPrisonHideOffset  = 3f;
    [SerializeField] private float wallDescendOffset    = 3f;
    [SerializeField] private float moveDuration         = 1f;
    [SerializeField] private float doorRiseDelay        = 2f;

    private Vector3   _doorTarget;
    private Vector3   _wallStartPos;
    private Vector3   _bigPrisonTarget;
    private Vector3[] _bedTargetScales;
    private Vector3   _bpZoneCanvasScale;

    private void Awake()
    {
        JailCounterUI.OnDisplayFull    += OnDisplayFull;
        JailManager.OnCapacityExpanded += OnCapacityExpanded;
    }

    private void OnDestroy()
    {
        JailCounterUI.OnDisplayFull    -= OnDisplayFull;
        JailManager.OnCapacityExpanded -= OnCapacityExpanded;
    }

    private void Start()
    {
        // 두 버전 공통 초기화
        _wallStartPos    = jailWall.position;
        _bigPrisonTarget = bigPrison.position;

        _bedTargetScales = new Vector3[bedsParent.childCount];
        for (int i = 0; i < bedsParent.childCount; i++)
        {
            _bedTargetScales[i] = bedsParent.GetChild(i).localScale;
            bedsParent.GetChild(i).localScale = Vector3.zero;
        }

        // 영상버전 전용 초기화
        if (videoVersion && jailDoor != null)
            _doorTarget = jailDoor.position;

        // 실제버전 전용 초기화
        if (!videoVersion && bpZoneCanvas != null)
        {
            _bpZoneCanvasScale = bpZoneCanvas.transform.localScale;
            bpZoneCanvas.transform.localScale = Vector3.zero;
        }
    }

    // ── 이벤트 수신 ───────────────────────────────────────────────

    private void OnDisplayFull()
    {
        if (!videoVersion) return;
        StartCoroutine(DoorRiseSequence());
    }

    private void OnCapacityExpanded()
    {
        StartCoroutine(ExpansionSequence());
    }

    // ── 메인 시퀀스 ───────────────────────────────────────────────

    private IEnumerator DoorRiseSequence()
    {
        yield return new WaitForSeconds(doorRiseDelay);
        jailDoor.position = _doorTarget - Vector3.up * doorHideOffset;
        jailDoor.gameObject.SetActive(true);
        yield return StartCoroutine(MoveObject(jailDoor, jailDoor.position, _doorTarget, moveDuration));
    }

    private IEnumerator ExpansionSequence()
    {
        if (videoVersion)
            yield return StartCoroutine(VideoExpansion());
        else
            yield return StartCoroutine(RealExpansion());
    }

    // ── 영상버전 시퀀스 ───────────────────────────────────────────

    private IEnumerator VideoExpansion()
    {
        // 1. Door 하강
        Vector3 doorHidden = _doorTarget - Vector3.up * doorHideOffset;
        yield return StartCoroutine(MoveObject(jailDoor, jailDoor.position, doorHidden, moveDuration));
        jailDoor.gameObject.SetActive(false);

        // 2. 0.1초 후 Wall 하강
        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(DescendWall());

        // 3. 0.5초 후 BigPrison 상승
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(RaiseBigPrison());

        // 4. 0.2초 후 침대 스프링
        yield return new WaitForSeconds(0.2f);
        StartBedSprings();

        // 5. 침대 완료(0.45s) + 0.2초 후 축하 이펙트
        yield return new WaitForSeconds(0.45f + 0.2f);
        celebrationEffect?.Play();
    }

    // ── 실제버전 시퀀스 ───────────────────────────────────────────

    private IEnumerator RealExpansion()
    {
        // 1. Wall 하강 + CountUI 페이드 동시 시작
        StartCoroutine(FadeOutCountUI());
        yield return StartCoroutine(DescendWall());

        // 2. 0.5초 후 BigPrison 상승
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(RaiseBigPrison());

        // 3. BigPrison 완료 → BPZoneCanvas 스프링 등장
        if (bpZoneCanvas != null)
            StartCoroutine(SpringAppearCanvas(bpZoneCanvas.transform, _bpZoneCanvasScale));

        // 4. 0.2초 후 침대 스프링
        yield return new WaitForSeconds(0.2f);
        StartBedSprings();

        // 5. 침대 완료(0.45s) + 0.5초 후 죄수 이동
        yield return new WaitForSeconds(0.45f + 0.5f);
        StartCoroutine(MoveNearbyPrisoners());
    }

    // ── 공통 서브루틴 ─────────────────────────────────────────────

    private IEnumerator DescendWall()
    {
        Vector3 wallEnd = _wallStartPos - Vector3.up * wallDescendOffset;
        yield return StartCoroutine(MoveObject(jailWall, _wallStartPos, wallEnd, moveDuration));
        jailWall.gameObject.SetActive(false);
    }

    private IEnumerator RaiseBigPrison()
    {
        bigPrison.position = _bigPrisonTarget - Vector3.up * bigPrisonHideOffset;
        bigPrison.gameObject.SetActive(true);
        yield return StartCoroutine(MoveObject(bigPrison, bigPrison.position, _bigPrisonTarget, moveDuration));
    }

    private void StartBedSprings()
    {
        for (int i = 0; i < bedsParent.childCount; i++)
        {
            int idx = i;
            StartCoroutine(SpringBed(bedsParent.GetChild(idx), _bedTargetScales[idx]));
        }
    }

    // ── 실제버전 전용 코루틴 ─────────────────────────────────────

    private IEnumerator FadeOutCountUI()
    {
        if (countUI == null) yield break;
        CanvasGroup cg = countUI.GetComponent<CanvasGroup>();
        if (cg == null) cg = countUI.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        while (elapsed < countUIFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = 1f - Mathf.Clamp01(elapsed / countUIFadeDuration);
            yield return null;
        }
        cg.alpha = 0f;
        countUI.SetActive(false);
    }

    private IEnumerator SpringAppearCanvas(Transform t, Vector3 targetScale)
    {
        t.localScale = Vector3.zero;
        t.gameObject.SetActive(true);
        float elapsed = 0f;
        float dur = 0.45f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / dur);
            float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
            t.localScale = targetScale * Mathf.Clamp(s, 0f, 1f);
            yield return null;
        }
        t.localScale = targetScale;
    }

    private IEnumerator MoveNearbyPrisoners()
    {
        if (bigPrisonDestination == null) yield break;

        PrisonerController[] all = FindObjectsByType<PrisonerController>(FindObjectsSortMode.None);
        List<PrisonerController> prisoners = new List<PrisonerController>();
        foreach (var p in all)
            if (p != null) prisoners.Add(p);

        // 가까운 순서로 정렬
        Vector3 center = bigPrison.position;
        prisoners.Sort((a, b) =>
            Vector3.Distance(a.transform.position, center)
            .CompareTo(Vector3.Distance(b.transform.position, center)));

        foreach (var p in prisoners)
        {
            if (p == null) continue;
            p.MoveToDestination(bigPrisonDestination);
            yield return new WaitForSeconds(prisonerMoveStagger);
        }
    }

    // ── 이동/애니메이션 헬퍼 ─────────────────────────────────────

    private IEnumerator MoveObject(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p     = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            t.position  = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        t.position = to;
    }

    private IEnumerator SpringBed(Transform t, Vector3 targetScale)
    {
        float elapsed  = 0f;
        float duration = 0.45f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
            t.localScale = targetScale * Mathf.Clamp(s, 0f, 1f);
            yield return null;
        }
        t.localScale = targetScale;
    }
}
