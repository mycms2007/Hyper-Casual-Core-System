using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }
    public static event System.Action OnFirstMoneyEarned;
    public static event System.Action<int> OnBalanceChanged;

    [SerializeField] private HUDManager hud;
    [Header("초반 배율")]
    [SerializeField] private float earlyGameDuration = 60f;
    [SerializeField] private int   earlyGameMultiplier = 3;

    private int  _money;
    private bool _firstMoneyFired;
    private bool _earlyGameOver;
    private float _elapsed;
    public int Money => _money;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (_earlyGameOver) return;
        _elapsed += Time.deltaTime;
        if (_elapsed >= earlyGameDuration)
            _earlyGameOver = true;
    }

    public void Add(int amount)
    {
        if (amount > 0 && !_earlyGameOver)
            amount *= earlyGameMultiplier;
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
            OnBalanceChanged?.Invoke(_money);
        }
    }

    public bool Spend(int amount)
    {
        if (_money < amount) return false;
        _money -= amount;
        hud?.SetMoney(_money);
        MoneyCarrier.Instance?.SpendMoney(_money);
        return true;
    }
}
