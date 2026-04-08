using UnityEngine;

/// <summary>
/// PurchaseZone의 activateTargets에 연결.
/// 활성화되는 순간 minerPrefab을 spawnPoints 위치/방향으로 일괄 소환.
/// 광부는 스폰포인트가 바라보는 방향 기준으로 즉시 광석 탐색 시작.
/// </summary>
public class MinerSpawner : MonoBehaviour
{
    [Header("광부 프리팹")]
    [SerializeField] private GameObject minerPrefab;

    [Header("소환 지점 (위치 + 방향 모두 적용)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Gem Drop Zone")]
    [SerializeField] private GemDropZone gemDropZone;

    private void OnEnable()
    {
        Debug.Log($"[MinerSpawner] OnEnable 실행 — 오브젝트명: {gameObject.name}");
        if (minerPrefab == null)
        {
            Debug.LogWarning("[MinerSpawner] minerPrefab이 연결되지 않았습니다.");
            return;
        }

        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;
            GameObject miner = Instantiate(minerPrefab, point.position, point.rotation);
            miner.transform.SetParent(null);
            miner.GetComponent<MinerWorker>()?.SetGemDropZone(gemDropZone);
        }
    }
}
