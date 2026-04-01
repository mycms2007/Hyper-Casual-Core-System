using UnityEngine;

public class KeyboardJoystickVisualizer : MonoBehaviour
{
    [SerializeField] private GameObject background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRadius = 50f;

    private bool _isActive;

    private void Update()
    {
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        if (input.sqrMagnitude > 0.01f)
        {
            if (!_isActive)
            {
                background.SetActive(true);
                _isActive = true;
            }
            handle.anchoredPosition = input.normalized * handleRadius;
        }
        else
        {
            if (_isActive)
            {
                background.SetActive(false);
                handle.anchoredPosition = Vector2.zero;
                _isActive = false;
            }
        }
    }
}
