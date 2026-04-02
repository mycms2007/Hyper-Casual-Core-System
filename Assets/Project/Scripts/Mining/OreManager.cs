using System.Collections;
using UnityEngine;

public class OreManager : MonoBehaviour
{
    public static OreManager Instance { get; private set; }

    [SerializeField] private Transform spawnPointsParent;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private GemCarrier gemCarrier;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Debug.Log($"[OreManager] gemCarrier: {(gemCarrier != null ? "OK" : "NULL")}");
        Ore[] ores = spawnPointsParent.GetComponentsInChildren<Ore>();
        Debug.Log($"[OreManager] Ore {ores.Length}개 초기화");
        foreach (Ore ore in ores)
            ore.Init(ore.transform, gemCarrier);
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
