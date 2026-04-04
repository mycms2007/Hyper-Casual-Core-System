using System.Collections;
using UnityEngine;

public class OreManager : MonoBehaviour
{
    public static OreManager Instance { get; private set; }

    [SerializeField] private Transform spawnPointsParent;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private GemCarrier gemCarrier;

    private Ore[] _ores;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Debug.Log($"[OreManager] gemCarrier: {(gemCarrier != null ? "OK" : "NULL")}");
        _ores = spawnPointsParent.GetComponentsInChildren<Ore>();
        Debug.Log($"[OreManager] Ore {_ores.Length}개 초기화");
        foreach (Ore ore in _ores)
            ore.Init(ore.transform, gemCarrier);
    }

    /// <summary>살아있고 점유되지 않은 광석 목록을 반환합니다. MinerWorker가 직접 우선순위를 계산할 때 사용.</summary>
    public Ore[] GetAliveUnclaimedOres()
    {
        if (_ores == null) return System.Array.Empty<Ore>();

        int count = 0;
        foreach (Ore ore in _ores)
            if (ore != null && !ore.IsDead && !ore.IsClaimed) count++;

        Ore[] result = new Ore[count];
        int i = 0;
        foreach (Ore ore in _ores)
            if (ore != null && !ore.IsDead && !ore.IsClaimed) result[i++] = ore;

        return result;
    }

    /// <summary>가장 가까운 살아있는 광석을 반환합니다 (플레이어/DrillCar용). 없으면 null.</summary>
    public Ore GetNearestAliveOre(Vector3 position)
    {
        if (_ores == null) return null;
        Ore nearest = null;
        float minDist = float.MaxValue;
        foreach (Ore ore in _ores)
        {
            if (ore == null || ore.IsDead) continue;
            float dist = Vector3.Distance(position, ore.transform.position);
            if (dist < minDist) { minDist = dist; nearest = ore; }
        }
        return nearest;
    }

    public void ScheduleRespawn(Ore ore, Transform spawnPoint)
    {
        StartCoroutine(RespawnCoroutine(ore, spawnPoint));
    }

    private IEnumerator RespawnCoroutine(Ore ore, Transform spawnPoint)
    {
        yield return new WaitForSeconds(respawnDelay);
        ore.transform.position = spawnPoint.position;
        ore.transform.rotation = spawnPoint.rotation;
        ore.Respawn();
    }
}
