using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }
    public static event System.Action OnFirstMoneyEarned;

    [SerializeField] private HUDManager hud;

    private int _money;
    private bool _firstMoneyFired;
    public int Money => _money;

    private void Awake()
    {
        Instance = this;
    }

    public void Add(int amount)
    {
        _money += amount;
        hud?.SetMoney(_money);
        if (amount > 0)
        {
            MoneyCarrier.Instance?.AddMoney(amount);
            if (!_firstMoneyFired)
            {
                _firstMoneyFired = true;
                OnFirstMoneyEarned?.Invoke();
            }
        }
    }

    public bool Spend(int amount)
    {
        if (_money < amount) return false;
        _money -= amount;
        hud?.SetMoney(_money);
        MoneyCarrier.Instance?.SpendMoney(amount);
        return true;
    }
}
