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
    private float _holdProgress;

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
            zoneVisual.transform.localScale = Vector3.zero;
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
        Debug.Log($"[PurchaseZone] {gameObject.name} — OnTriggerConditionMet 호출됨 (ready={_ready}, purchased={_purchased})");
        if (_ready || _purchased) return;

        if (purchaseCooldown > 0f)
            Invoke(nameof(BecomeReady), purchaseCooldown);
        else
            BecomeReady();
    }

    private void BecomeReady()
    {
        _ready = true;
        Debug.Log($"[PurchaseZone] {gameObject.name} — BecomeReady 호출됨 (zoneVisual={zoneVisual})");

        if (zoneVisual != null)
        {
            zoneVisual.SetActive(true);
            StartCoroutine(SpringAppear(zoneVisual.transform));
        }
        else
        {
            Debug.LogWarning($"[PurchaseZone] {gameObject.name} — zoneVisual이 null입니다! 인스펙터에서 연결을 확인하세요.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PurchaseZone] {gameObject.name} OnTriggerEnter — purchased={_purchased}, ready={_ready}, collider={other.name}");
        if (_purchased || !_ready) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        Debug.Log($"[PurchaseZone] {gameObject.name} — 플레이어 감지, 구매 시작");
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

        // 존 오브젝트 및 자식의 렌더러 숨김
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        // Canvas UI 숨김
        foreach (Canvas c in GetComponentsInChildren<Canvas>())
            c.gameObject.SetActive(false);

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
