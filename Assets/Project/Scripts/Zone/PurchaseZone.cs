using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ActivationTrigger { None, Prerequisite, FirstMoney, JailFull }

/// <summary>
/// 플레이어가 서 있는 동안 일정 간격으로 돈을 드레인해서 구매하는 존.
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
    [SerializeField] private float purchaseCooldown = 0f;

    [Header("코인 드레인")]
    [SerializeField] private float drainInterval = 0.12f;
    [SerializeField] private int drainAmountPerTick = 5;
    [SerializeField] private GameObject coinFlyPrefab;
    [SerializeField] private float coinFlyDuration = 0.35f;

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
    private int  _paidAmount;
    private float _drainTimer;
    private Transform _playerTransform;

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

        _originalScale = _visualTarget.localScale;
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

        if (!_isHolding || _purchased || !_ready) return;

        _drainTimer -= Time.deltaTime;
        if (_drainTimer > 0f) return;

        _drainTimer = drainInterval;

        int remaining = price - _paidAmount;
        int toDrain   = Mathf.Min(drainAmountPerTick, remaining);

        if (PlayerWallet.Instance == null || !PlayerWallet.Instance.Spend(toDrain))
            return; // 돈 부족 — 다음 틱에 재시도

        _paidAmount += toDrain;

        if (fillImage != null && price > 0)
            fillImage.fillAmount = (float)_paidAmount / price;

        // 코인 날아가는 연출
        if (coinFlyPrefab != null && _playerTransform != null)
            StartCoroutine(FlyCoinToZone(_playerTransform.position));

        if (_paidAmount >= price)
            Purchase();
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

        _playerTransform = other.GetComponentInParent<PlayerController>().transform;
        _isHolding  = true;
        _drainTimer = 0f; // 진입 즉시 첫 드레인
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _isHolding       = false;
        _playerTransform = null;
    }

    private void Purchase()
    {
        _purchased = true;
        _isHolding = false;
        if (fillImage != null) fillImage.fillAmount = price > 0 ? 1f : 0f;

        foreach (GameObject target in activateTargets)
            if (target != null) target.SetActive(true);

        if (delayedActivateTargets != null && delayedActivateTargets.Length > 0)
            StartCoroutine(DelayedActivate());

        if (!_usingSelf)
        {
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
            StartCoroutine(SpringDisappear(_visualTarget, null));
        }
    }

    private IEnumerator FlyCoinToZone(Vector3 from)
    {
        GameObject coin = Instantiate(coinFlyPrefab, from, Quaternion.identity);
        Vector3 target  = transform.position + Vector3.up * 0.5f;
        float elapsed   = 0f;

        while (elapsed < coinFlyDuration)
        {
            if (coin == null) yield break;
            elapsed += Time.deltaTime;
            float t   = Mathf.Clamp01(elapsed / coinFlyDuration);
            Vector3 p = Vector3.Lerp(from, target, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 1.2f;
            coin.transform.position = p;
            yield return null;
        }

        if (coin != null) StartCoroutine(ShrinkAndDestroy(coin));
    }

    private IEnumerator ShrinkAndDestroy(GameObject coin)
    {
        if (coin == null) yield break;
        float duration = 0.12f;
        float elapsed  = 0f;
        Vector3 start  = coin.transform.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (coin == null) yield break;
            coin.transform.localScale = Vector3.Lerp(start, Vector3.zero, elapsed / duration);
            yield return null;
        }
        if (coin != null) Destroy(coin);
    }

    private IEnumerator DelayedActivate()
    {
        Debug.Log($"[PurchaseZone] DelayedActivate 시작 — {delayedActivateDelay}초 대기");
        yield return new WaitForSeconds(delayedActivateDelay);
        Debug.Log($"[PurchaseZone] DelayedActivate 발동 — 대상 {delayedActivateTargets.Length}개");
        foreach (GameObject target in delayedActivateTargets)
        {
            if (target != null)
            {
                Debug.Log($"[PurchaseZone] SetActive(true) → {target.name}");
                target.SetActive(true);
            }
        }
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
