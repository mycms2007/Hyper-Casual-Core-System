using System.Collections;
using UnityEngine;

public class OreManager : MonoBehaviour
{
    public static OreManager Instance { get; private set; }

    [SerializeField] private Transform spawnPointsParent;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private StackManager oreStackManager;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Ore[] ores = spawnPointsParent.GetComponentsInChildren<Ore>();
        foreach (Ore ore in ores)
            ore.Init(ore.transform, oreStackManager);
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
