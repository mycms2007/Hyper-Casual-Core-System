using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 감옥 벽면 World Space Canvas에 부착.
/// 죄수가 체크포인트 통과 후 3초 뒤에 카운트 +1.
/// 수용 인원이 가득 차면 빨간색으로 전환.
/// </summary>
public class JailCounterUI : MonoBehaviour
{
    public static event System.Action OnDisplayFull;

    [SerializeField] private TMP_Text counterText;
    [SerializeField] private TMP_Text bpCounterText;  // BPZoneCanvas 텍스트 (실제버전)
    [SerializeField] private float countDelay = 3f;

    private int _displayCount;
    private bool _fullFired;

    private void Awake()
    {
        JailManager.OnPrisonerEntered += OnPrisonerEntered;
        JailManager.OnCapacityExpanded += RefreshDisplay;
    }

    private void OnDestroy()
    {
        JailManager.OnPrisonerEntered -= OnPrisonerEntered;
        JailManager.OnCapacityExpanded -= RefreshDisplay;
    }

    private void Start()
    {
        _displayCount = 0;
        RefreshDisplay();
    }

    private void OnPrisonerEntered()
    {
        StartCoroutine(DelayedIncrement());
    }

    private IEnumerator DelayedIncrement()
    {
        yield return new WaitForSeconds(countDelay);
        _displayCount++;
        UpdateText();
    }

    private void RefreshDisplay()
    {
        _fullFired = false; // 용량 확장 시 다음 만석에 다시 발동 가능하도록 리셋
        UpdateText();
    }

    private void UpdateText()
    {
        int cap = JailManager.Instance != null ? JailManager.Instance.Capacity : 20;
        string text = $"{_displayCount}/{cap}";
        Color color = _displayCount >= cap ? Color.red : Color.white;

        counterText.text  = text;
        counterText.color = color;

        if (bpCounterText != null)
        {
            bpCounterText.text  = text;
            bpCounterText.color = color;
        }

        if (!_fullFired && _displayCount >= cap)
        {
            _fullFired = true;
            OnDisplayFull?.Invoke();
        }
    }
}
