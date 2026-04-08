using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gem 소지량이 한계치에 달했을 때 MAX 이미지를 반복 fade 애니메이션으로 표시.
/// 캐릭터 월드 좌표를 매 프레임 스크린 좌표로 변환해 딱 붙어 따라다님.
/// </summary>
public class GemMaxIndicator : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Transform playerTransform;

    [Header("머리 위 오프셋 (월드)")]
    [SerializeField] private float playerHeightOffset   = 2.5f;
    [SerializeField] private float drillCarHeightOffset = 3.5f;

    [Header("타이밍")]
    [SerializeField] private float fadeInDuration    = 0.3f;
    [SerializeField] private float visibleDuration   = 0.7f;
    [SerializeField] private float fadeOutDuration   = 0.5f;
    [SerializeField] private float quickFadeOutSpeed = 6f;

    private bool _isBlinking;
    private bool _suppressed;
    private Coroutine _blinkCoroutine;
    private Camera _cam;

    private void Awake()
    {
        if (canvasGroup == null)  canvasGroup  = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        _cam = Camera.main;
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        UpdatePosition();

        bool atMax = GemCarrier.Instance != null &&
                     GemCarrier.Instance.Count >= GemCarrier.Instance.Capacity;

        bool shouldBlink = atMax && !_suppressed;

        if (shouldBlink && !_isBlinking)
        {
            _isBlinking = true;
            _blinkCoroutine = StartCoroutine(BlinkLoop());
        }
        else if (!shouldBlink && _isBlinking)
        {
            StopBlink();
        }

        if (!shouldBlink && canvasGroup.alpha > 0f)
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, quickFadeOutSpeed * Time.deltaTime);
    }

    private void UpdatePosition()
    {
        if (_cam == null || playerTransform == null) return;

        bool isDriving = DrillCar.Instance != null && DrillCar.Instance.IsDriving;

        Transform tracked = isDriving ? DrillCar.Instance.transform : playerTransform;
        float offset      = isDriving ? drillCarHeightOffset : playerHeightOffset;

        Vector3 worldPos  = tracked.position + Vector3.up * offset;
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

        // 캐릭터가 카메라 뒤에 있을 경우 숨김
        if (screenPos.z < 0f)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        rectTransform.position = screenPos;
    }

    private void StopBlink()
    {
        _isBlinking = false;
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            while (elapsed < fadeInDuration)
            {
                if (!_isBlinking) yield break;
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            float waited = 0f;
            while (waited < visibleDuration)
            {
                if (!_isBlinking) yield break;
                waited += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                if (!_isBlinking) yield break;
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }
    }

    /// <summary>시네머신 컷씬 등 외부에서 강제 억제할 때 호출.</summary>
    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
    }
}
