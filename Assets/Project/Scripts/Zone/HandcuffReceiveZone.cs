using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HandcuffReceiveZone : MonoBehaviour
{
    [Header("말풍선 (Screen Space - Overlay)")]
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private RectTransform speechBubbleRect;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image fillImage;
    [SerializeField] private float bubbleHeightOffset = 2.5f;

    [SerializeField] private float fillLerpSpeed = 8f;

    private ArrestedPerson _currentOccupant;
    private Camera _cam;
    private float _targetFill;

    public bool IsOccupied => _currentOccupant != null;
    public ArrestedPerson CurrentOccupant => _currentOccupant;

    private void Awake()
    {
        _cam = Camera.main;
        if (speechBubble != null) speechBubble.SetActive(false);
    }

    private void Update()
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, _targetFill, fillLerpSpeed * Time.deltaTime);

        if (_currentOccupant == null || speechBubble == null || !speechBubble.activeSelf) return;

        Vector3 worldPos = _currentOccupant.transform.position + Vector3.up * bubbleHeightOffset;
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
        speechBubbleRect.position = screenPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_currentOccupant != null) return;

        ArrestedPerson person = other.GetComponentInParent<ArrestedPerson>();
        if (person == null) return;

        _currentOccupant = person;
        person.OnReachedZone(this);
        ShowBubble(person.DisplayCount, person.TotalNeeded);
    }

    public void UpdateBubble(int received, int totalNeeded, int displayCount)
    {
        int remaining = Mathf.Max(0, displayCount - received);
        if (countText != null) countText.text = $"x{remaining}";
        _targetFill = (float)received / totalNeeded;
    }

    public void HideBubble()
    {
        if (speechBubble != null) speechBubble.SetActive(false);
    }

    public void OnOccupantCleared()
    {
        _currentOccupant = null;
        HideBubble();
    }

    private void ShowBubble(int displayCount, int totalNeeded)
    {
        if (speechBubble != null) speechBubble.SetActive(true);
        _targetFill = 0f;
        if (fillImage != null) fillImage.fillAmount = 0f;
        UpdateBubble(0, totalNeeded, displayCount);
    }
}
