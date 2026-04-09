using System.Collections;
using UnityEngine;

/// <summary>
/// UICanvas 또는 항상 활성화된 오브젝트에 부착.
/// Q키 → EndPanel 활성화 + 등장 애니메이션 시퀀스 실행.
///
/// Title    : 납작 → 탄력있게 늘어났다 원래 크기 (슬라임 squash-and-stretch)
/// AppIcon  : scale 0 → 스프링 등장
/// Continue : 0.2초 후 스프링 등장 → 1초 간격 좌우 찌부 루프
/// </summary>
public class EndPanelController : MonoBehaviour
{
    [Header("오브젝트 참조")]
    [SerializeField] private GameObject endPanel;
    [SerializeField] private Transform  title;
    [SerializeField] private Transform  appIcon;
    [SerializeField] private Transform  continueButton;

    [Header("타이밍")]
    [SerializeField] private float titleDuration      = 0.65f;
    [SerializeField] private float iconDuration       = 0.45f;
    [SerializeField] private float buttonDelay        = 0.2f;
    [SerializeField] private float buttonLoopInterval = 1f;
    [SerializeField] private float buttonLoopDuration = 0.38f;

    private Vector3 _titleOriginal;
    private Vector3 _iconOriginal;
    private Vector3 _buttonOriginal;
    private bool    _opened;

    private void Start()
    {
        // 비활성 상태에서도 Transform 접근 가능 — 원본 스케일 저장
        _titleOriginal  = title.localScale;
        _iconOriginal   = appIcon.localScale;
        _buttonOriginal = continueButton.localScale;

        // 등장 시작 상태로 세팅
        appIcon.localScale        = Vector3.zero;
        continueButton.localScale = Vector3.zero;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && !_opened)
            OpenPanel();
    }

    public void OpenPanel()
    {
        if (_opened) return;
        _opened = true;
        endPanel.SetActive(true);
        SFXManager.Instance?.PlayEndPanel();
        StartCoroutine(PlaySequence());
    }

    // ── 메인 시퀀스 ───────────────────────────────────────────────

    private IEnumerator PlaySequence()
    {
        // Title 슬라임 bounce + AppIcon 동시 시작
        StartCoroutine(TitleSquashStretch());
        StartCoroutine(SpringFromZero(appIcon, _iconOriginal, iconDuration));

        // 0.2초 후 Continue 등장
        yield return new WaitForSeconds(buttonDelay);
        yield return StartCoroutine(SpringFromZero(continueButton, _buttonOriginal, iconDuration));

        // 등장 완료 → 루프 찌부 시작
        StartCoroutine(ButtonLoopBounce());
    }

    // ── Title: squash-and-stretch ─────────────────────────────────

    private IEnumerator TitleSquashStretch()
    {
        // 납작하게 눌린 시작 상태 (가로 넓고 세로 얇음)
        Vector3 squashed = new Vector3(
            _titleOriginal.x * 1.35f,
            _titleOriginal.y * 0.35f,
            _titleOriginal.z);
        title.localScale = squashed;

        float elapsed = 0f;
        while (elapsed < titleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / titleDuration);

            // 스프링 커브 — 위로 탄력있게 늘었다가 정착
            float spring = 1f - Mathf.Exp(-8f * t) * Mathf.Cos(10f * t);

            float sx = Mathf.Lerp(squashed.x, _titleOriginal.x, spring);
            float sy = Mathf.Lerp(squashed.y, _titleOriginal.y, spring);
            title.localScale = new Vector3(sx, sy, _titleOriginal.z);
            yield return null;
        }
        title.localScale = _titleOriginal;
    }

    // ── AppIcon / Continue: scale 0 → 스프링 등장 ────────────────

    private IEnumerator SpringFromZero(Transform t, Vector3 targetScale, float duration)
    {
        t.localScale = Vector3.zero;
        float elapsed = 0f;
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

    // ── Continue: 좌우 살짝 늘어나는 루프 ────────────────────────

    private IEnumerator ButtonLoopBounce()
    {
        while (true)
        {
            yield return new WaitForSeconds(buttonLoopInterval);

            // 좌우로 살짝 늘어났다가 스프링으로 복귀
            Vector3 stretched = new Vector3(
                _buttonOriginal.x * 1.18f,
                _buttonOriginal.y * 0.88f,
                _buttonOriginal.z);

            float elapsed = 0f;
            while (elapsed < buttonLoopDuration)
            {
                elapsed += Time.deltaTime;
                float t      = Mathf.Clamp01(elapsed / buttonLoopDuration);
                float spring = 1f - Mathf.Exp(-5f * t) * Mathf.Cos(8f * t);

                float sx = Mathf.Lerp(stretched.x, _buttonOriginal.x, spring);
                float sy = Mathf.Lerp(stretched.y, _buttonOriginal.y, spring);
                continueButton.localScale = new Vector3(sx, sy, _buttonOriginal.z);
                yield return null;
            }
            continueButton.localScale = _buttonOriginal;
        }
    }
}
