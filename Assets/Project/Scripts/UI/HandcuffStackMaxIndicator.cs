using System.Collections;
using UnityEngine;

/// <summary>
/// HandcuffStackZone이 최대 수량에 도달하면 MAX 이미지를 zone 위에 반복 fade 애니메이션으로 표시.
/// GemMaxIndicator와 동일한 블링크 구조, HandcuffReceiveZone과 동일한 위치 추적 방식.
/// </summary>
public class HandcuffStackMaxIndicator : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private HandcuffStackZone stackZone;

    [Header("Zone 위 오프셋 (월드)")]
    [SerializeField] private float heightOffset = 2.0f;

    [Header("타이밍")]
    [SerializeField] private float fadeInDuration    = 0.3f;
    [SerializeField] private float visibleDuration   = 0.7f;
    [SerializeField] private float fadeOutDuration   = 0.5f;
    [SerializeField] private float quickFadeOutSpeed = 6f;

    private bool _isBlinking;
    private Coroutine _blinkCoroutine;
    private Camera _cam;

    private void Awake()
    {
        if (canvasGroup == null)   canvasGroup   = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        _cam = Camera.main;
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        UpdatePosition();

        bool atMax = stackZone != null && stackZone.StackCount >= stackZone.MaxCapacity;

        if (atMax && !_isBlinking)
        {
            _isBlinking = true;
            _blinkCoroutine = StartCoroutine(BlinkLoop());
        }
        else if (!atMax && _isBlinking)
        {
            StopBlink();
        }

        if (!atMax && canvasGroup.alpha > 0f)
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, quickFadeOutSpeed * Time.deltaTime);
    }

    private void UpdatePosition()
    {
        if (_cam == null || stackZone == null) return;

        Vector3 worldPos  = stackZone.transform.position + Vector3.up * heightOffset;
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

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
}
