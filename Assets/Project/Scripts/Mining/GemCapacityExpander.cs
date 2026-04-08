using UnityEngine;

public class GemCapacityExpander : MonoBehaviour
{
    [SerializeField] private int capacity = 30;

    private void OnEnable()
    {
        GemCarrier.Instance?.SetCapacity(capacity);
    }
}
