using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ActivationTrigger { None, Prerequisite, FirstMoney, JailFull }

/// <summary>
/// 플레이어가 holdDuration 동안 서있으면 구매되는 존.
/// 활성화 조건: None(즉시), Prerequisite(선행존), FirstMoney(첫 돈 수령), JailFull(감옥 20명)
///
/// zoneVisual이 연결되어 있으면 해당 오브젝트를 스프링 애니메이션 타겟으로 사용.
/// 연결되어 있지 않으면 이 GameObject 자신의 transform을 타겟으로 자동 사용.
/// → 어떤 존이든 인스펙터 추가 연결 없이 스프링 등장/퇴장 동작.
/// </summary>
public class PurchaseZone : MonoBehaviour
{
    [Header("구매 설정")]
    [SerializeField] private int price;
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float purchaseCooldown = 0f;

    [Header("활성화 조건")]
    [SerializeField] private ActivationTrigger trigger = ActivationTrigger.None;
    [SerializeField] private PurchaseZone prerequisite;

    [Header("연출 (선택)")]
    [SerializeField] private GameObject zoneVisual; // null이면 self 애니메이션
    [SerializeField] private GameObject[] activateTargets;
    [SerializeField] private Image fillImage;

    [Header("구매 후 지연 활성화 (선택)")]
    [SerializeField] private GameObject[] delayedActivateTargets;
    [SerializeField] private float delayedActivateDelay = 0f;

    private bool _purchased;
    private bool _ready;
    private bool _isHolding;
    private float _holdProgress;

    private Transform _visualTarget;  // 애니메이션 타겟 (zoneVisual 또는 self)
    private bool _usingSelf;          // true면 self 애니메이션 — SetActive 대신 scale 0 유지
    private Vector3 _originalScale;   // 씬에서 설정된 원래 localScale

    public bool IsPurchased => _purchased;

    private void Awake()
    {
        if (trigger == ActivationTrigger.FirstMoney)
            PlayerWallet.OnFirstMoneyEarned += OnTriggerConditionMet;
        else if (trigger == ActivationTrigger.JailFull)
            JailManager.OnJailFull += OnTriggerConditionMet;
    }

    private void OnDestroy()
    {
        PlayerWallet.OnFirstMoneyEarned -= OnTriggerConditionMet;
        JailManager.OnJailFull -= OnTriggerConditionMet;
    }

    private void Start()
    {
        if (zoneVisual != null)
        {
            _visualTarget = zoneVisual.transform;
            _usingSelf = false;
        }
        else
        {
            _visualTarget = transform;
            _usingSelf = true;
        }

        _originalScale = _visualTarget.localScale;  // 제로 세팅 전에 원래 크기 저장
        _visualTarget.localScale = Vector3.zero;
        if (fillImage != null) fillImage.fillAmount = 0f;

        if (trigger == ActivationTrigger.None)
            OnTriggerConditionMet();
    }

    private void Update()
    {
        if (trigger == ActivationTrigger.Prerequisite &&
            !_ready && !_purchased &&
            prerequisite != null && prerequisite.IsPurchased)
        {
            trigger = ActivationTrigger.None;
            OnTriggerConditionMet();
        }

        if (_isHolding && !_purchased && _ready)
        {
            _holdProgress -= Time.deltaTime / holdDuration;
            if (fillImage != null) fillImage.fillAmount = _holdProgress;
            if (_holdProgress <= 0f)
            {
                _holdProgress = 0f;
                if (fillImage != null) fillImage.fillAmount = 0f;
                if (PlayerWallet.Instance != null && PlayerWallet.Instance.Spend(price))
                    Purchase();
                else
                    _isHolding = false;
            }
        }
    }

    private void OnTriggerConditionMet()
    {
        if (_ready || _purchased) return;

        if (purchaseCooldown > 0f)
            Invoke(nameof(BecomeReady), purchaseCooldown);
        else
            BecomeReady();
    }

    private void BecomeReady()
    {
        _ready = true;

        if (!_usingSelf)
            _visualTarget.gameObject.SetActive(true);

        StartCoroutine(SpringAppear(_visualTarget));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_purchased || !_ready) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _isHolding = true;
        _holdProgress = 1f;
        if (fillImage != null) fillImage.fillAmount = 1f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _isHolding = false;
        _holdProgress = 0f;
        if (fillImage != null) fillImage.fillAmount = 0f;
    }

    private void Purchase()
    {
        _purchased = true;
        _isHolding = false;
        if (fillImage != null) fillImage.fillAmount = 0f;

        foreach (GameObject target in activateTargets)
            if (target != null) target.SetActive(true);

        if (delayedActivateTargets != null && delayedActivateTargets.Length > 0)
            StartCoroutine(DelayedActivate());

        // 퇴장 애니메이션 후 시각 요소 정리
        if (!_usingSelf)
        {
            // zoneVisual을 제외한 나머지(존 플랫폼/UI)만 즉시 숨기고, visual은 애니메이션 후 숨김
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (!r.transform.IsChildOf(_visualTarget) && r.transform != _visualTarget)
                    r.enabled = false;
            foreach (Canvas c in GetComponentsInChildren<Canvas>())
                if (!c.transform.IsChildOf(_visualTarget) && c.transform != _visualTarget)
                    c.gameObject.SetActive(false);
            StartCoroutine(SpringDisappear(_visualTarget, () => _visualTarget.gameObject.SetActive(false)));
        }
        else
        {
            // self 모드: 애니메이션 자체가 시각 정리 — scale 0이 되면 충분
            StartCoroutine(SpringDisappear(_visualTarget, null));
        }
    }

    private IEnumerator DelayedActivate()
    {
        yield return new WaitForSeconds(delayedActivateDelay);
        foreach (GameObject target in delayedActivateTargets)
            if (target != null) target.SetActive(true);
    }

    private IEnumerator SpringAppear(Transform t)
    {
        float elapsed = 0f;
        float duration = 0.45f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            t.localScale = _originalScale * Mathf.Clamp(SpringEaseOut(p), 0f, 1f);
            yield return null;
        }
        t.localScale = _originalScale;
    }

    private IEnumerator SpringDisappear(Transform t, System.Action onDone)
    {
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float s = p < 0.25f
                ? Mathf.Lerp(1f, 1.12f, p / 0.25f)
                : Mathf.Lerp(1.12f, 0f, (p - 0.25f) / 0.75f);
            t.localScale = _originalScale * s;
            yield return null;
        }
        t.localScale = Vector3.zero;
        onDone?.Invoke();
    }

    private float SpringEaseOut(float t)
    {
        return 1f - Mathf.Exp(-7f * t) * Mathf.Cos(12f * t);
    }
}
