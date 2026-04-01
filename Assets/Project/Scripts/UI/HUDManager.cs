using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private Image logoImage;

    public void SetMoney(int amount)
    {
        moneyText.text = $"₩{amount}";
    }
}
