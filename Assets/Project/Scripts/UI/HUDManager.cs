using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private Image logoImage;

    private void Start()
    {
        SetMoney(0);
    }

    public void SetMoney(int amount)
    {
        moneyText.text = $"₩{amount}";
    }
}
