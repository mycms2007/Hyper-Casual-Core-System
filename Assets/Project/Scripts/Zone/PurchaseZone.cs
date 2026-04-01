using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ActivationTrigger { None, Prerequisite, FirstMoney, JailFull }

/// <summary>
/// 플레이어가 holdDuration 동안 서있으면 구매되는 존.
/// 활성화 조건: None(즉시), Prerequisite(선행존), FirstMoney(첫 돈 수령), JailFull(감옥 20명)
/// 활성화 시 스프링 등장, 구매 시 역스프링 퇴장.
/// </summary>
public class PurchaseZone : MonoBehaviour
{
    [Header("구매 설정")]
    [SerializeField] private int price;
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float purchaseCooldown = 0f;

    [Header("활성화 조건")]
    [SerializeField] private ActivationTrigger trigger = ActivationTrigger.None;
    [SerializeField] private PurchaseZone prerequisite; // Prerequisite일 때만 사용

    [Header("연출")]
    [SerializeField] private GameObject zoneVisual;
    [SerializeField] private GameObject[] activateTargets;
    [SerializeField] private Image fillImage;

    private bool _purchased;
    private bool _ready;
    private bool _isHolding;

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
            zoneVisual.SetActive(false);
            zoneVisual.transform.localScale = Vector3.zero;
        }
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

        if (_isHolding && !_purchased && _ready && fillImage != null)
        {
            fillImage.fillAmount -= Time.deltaTime / holdDuration;
            if (fillImage.fillAmount <= 0f)
            {
                fillImage.fillAmount = 0f;
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

        if (zoneVisual != null)
        {
            zoneVisual.SetActive(true);
            StartCoroutine(SpringAppear(zoneVisual.transform));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_purchased || !_ready) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _isHolding = true;
        if (fillImage != null) fillImage.fillAmount = 1f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _isHolding = false;
        if (fillImage != null) fillImage.fillAmount = 0f;
    }

    private void Purchase()
    {
        _purchased = true;
        _isHolding = false;
        if (fillImage != null) fillImage.fillAmount = 0f;

        foreach (GameObject target in activateTargets)
            if (target != null) target.SetActive(true);

        if (zoneVisual != null)
            StartCoroutine(SpringDisappear(zoneVisual.transform, () => zoneVisual.SetActive(false)));
    }

    private IEnumerator SpringAppear(Transform t)
    {
        float elapsed = 0f;
        float duration = 0.45f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            t.localScale = Vector3.one * SpringEaseOut(p);
            yield return null;
        }
        t.localScale = Vector3.one;
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
            t.localScale = Vector3.one * s;
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
