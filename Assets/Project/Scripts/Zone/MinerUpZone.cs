using UnityEngine;

public class MinerUpZone : MonoBehaviour
{
    [SerializeField] private GameObject minerSpawner;

    private void OnEnable()
    {
        if (minerSpawner != null)
            minerSpawner.SetActive(true);
        else
            Debug.LogWarning("[MinerUpZone] minerSpawner가 연결되지 않았습니다.");
    }
}
