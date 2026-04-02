using UnityEngine;

/// <summary>
/// 항상 활성화된 오브젝트에 붙여두는 스크립트.
/// 조건 충족 시 지정한 Zone 오브젝트들을 활성화한다.
/// </summary>
public class ZoneUnlocker : MonoBehaviour
{
    [System.Serializable]
    public class MoneyThresholdZone
    {
        public int minAmount;
        public GameObject zone;
    }

    [Header("잔액이 minAmount 이상일 때 활성화 (첫 1회)")]
    [SerializeField] private MoneyThresholdZone[] moneyThresholdZones;

    [Header("감옥 가득 찰 시 활성화")]
    [SerializeField] private GameObject[] jailFullZones;

    private void Awake()
    {
        PlayerWallet.OnBalanceChanged += OnBalanceChanged;
        JailManager.OnJailFull += OnJailFull;
    }

    private void OnDestroy()
    {
        PlayerWallet.OnBalanceChanged -= OnBalanceChanged;
        JailManager.OnJailFull -= OnJailFull;
    }

    private void OnBalanceChanged(int balance)
    {
        foreach (var entry in moneyThresholdZones)
        {
            if (entry.zone != null && !entry.zone.activeSelf && balance >= entry.minAmount)
                entry.zone.SetActive(true);
        }
    }

    private void OnJailFull()
    {
        foreach (var zone in jailFullZones)
            if (zone != null) zone.SetActive(true);
    }
}
