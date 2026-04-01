using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrestedSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform destination;          // HandcuffReceiveZone 위치
    [SerializeField] private HandcuffReceiveZone receiveZone;
    [SerializeField] private GameObject[] arrestedPrefabs;   // Arrested1, 2, 3
    [SerializeField] private int maxCount = 5;
    [SerializeField] private float gapDistance = 2f;
    [SerializeField] private int initialSpawnCount = 3;      // 게임 시작 시 초기 인원
    [SerializeField] private Transform jailDestination;
    [SerializeField] private TakeMoneyZone takeMoneyZone;

    private readonly List<ArrestedPerson> _queue = new List<ArrestedPerson>();

    [SerializeField] private float initialSpawnInterval = 1f;

    private void Start()
    {
        StartCoroutine(InitialSpawn());
    }

    private IEnumerator InitialSpawn()
    {
        int count = Mathf.Min(initialSpawnCount, maxCount);
        for (int i = 0; i < count; i++)
        {
            SpawnNext();
            yield return new WaitForSeconds(initialSpawnInterval);
        }
    }

    private void SpawnNext()
    {
        if (_queue.Count >= maxCount) return;
        if (arrestedPrefabs == null || arrestedPrefabs.Length == 0) return;

        int index = Random.Range(0, arrestedPrefabs.Length);
        GameObject obj = Instantiate(arrestedPrefabs[index], spawnPoint.position, spawnPoint.rotation);

        ArrestedPerson person = obj.GetComponent<ArrestedPerson>();
        if (person == null) return;

        person.PersonAhead = _queue.Count > 0 ? _queue[_queue.Count - 1] : null;
        person.GapDistance = gapDistance;
        person.Initialize(destination, receiveZone, jailDestination, takeMoneyZone);
        person.OnTransformed += OnPersonTransformed;

        _queue.Add(person);
    }

    private void OnPersonTransformed()
    {
        _queue.RemoveAll(p => p == null || p.CurrentState == ArrestedPerson.State.Done);

        // PersonAhead 재정렬
        for (int i = 0; i < _queue.Count; i++)
            _queue[i].PersonAhead = i > 0 ? _queue[i - 1] : null;

        SpawnNext();
    }
}
