using System.Collections;
using UnityEngine;

public class ArrestedPerson : MonoBehaviour
{
    public enum State { Walking, Waiting, Done }

    [Header("이동")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float stopDistance = 0.15f;

    [Header("수갑 퀘스트")]
    [SerializeField] private int displayCount = 2;  // 말풍선에 표시되는 수 (실제 필요 = +1)

    [Header("변신")]
    [SerializeField] private GameObject[] prisonerPrefabs;
    [SerializeField] private GameObject transformParticlePrefab;
    [SerializeField] private Transform jailDestination;

    [Header("보상")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private TakeMoneyZone takeMoneyZone;
    [SerializeField] private int rewardAmount = 15;
    [SerializeField] private float coinFlyDuration = 0.6f;

    // 런타임
    public State CurrentState { get; private set; } = State.Walking;
    public ArrestedPerson PersonAhead { get; set; }
    public float GapDistance { get; set; } = 2f;
    public bool NeedsMoreHandcuffs => CurrentState == State.Waiting && _receivedCount < _totalNeeded;

    public System.Action OnTransformed;

    private HandcuffReceiveZone _zone;
    private int _totalNeeded;
    private int _receivedCount;
    private Animator _anim;

    // 웨이포인트
    private Transform[] _waypoints;
    private int _waypointIndex;

    public int DisplayCount => displayCount;
    public int TotalNeeded => _totalNeeded;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _totalNeeded = displayCount + 1;
    }

    public void Initialize(HandcuffReceiveZone zone, Transform jailDest, TakeMoneyZone moneyZone)
    {
        _zone         = zone;
        jailDestination = jailDest;
        takeMoneyZone = moneyZone;
    }

    public void SetWaypoints(Transform[] waypoints)
    {
        _waypoints     = waypoints;
        _waypointIndex = 0;
    }

    private void Update()
    {
        if (CurrentState != State.Walking) return;
        if (_waypoints == null || _waypoints.Length == 0) return;

        // 앞 사람이 GapDistance 이내에 있으면 대기
        if (IsBlockedByPersonAhead())
        {
            _anim?.SetBool("IsWalking", false);
            return;
        }

        Vector3 target     = _waypoints[_waypointIndex].position;
        Vector3 flatSelf   = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(target.x,             0f, target.z);
        float dist = Vector3.Distance(flatSelf, flatTarget);

        if (dist > stopDistance)
        {
            Vector3 dir = (flatTarget - flatSelf).normalized;
            transform.position += dir * walkSpeed * Time.deltaTime;
            transform.rotation  = Quaternion.LookRotation(dir);
            _anim?.SetBool("IsWalking", true);
        }
        else
        {
            // 현재 웨이포인트 도달 — 다음으로 전진
            if (_waypointIndex < _waypoints.Length - 1)
                _waypointIndex++;
            else
                _anim?.SetBool("IsWalking", false); // 마지막 웨이포인트: 트리거 대기
        }
    }

    private bool IsBlockedByPersonAhead()
    {
        if (PersonAhead == null || PersonAhead.CurrentState == State.Done) return false;
        float dx = PersonAhead.transform.position.x - transform.position.x;
        float dz = PersonAhead.transform.position.z - transform.position.z;
        return dx * dx + dz * dz <= GapDistance * GapDistance;
    }

    /// <summary>HandcuffReceiveZone에 도달했을 때 호출.</summary>
    public void OnReachedZone(HandcuffReceiveZone zone)
    {
        _zone       = zone;
        CurrentState = State.Waiting;
        _anim?.SetBool("IsWalking", false);
    }

    /// <summary>OfficeManager에서 수갑 하나 착지 시 호출.</summary>
    public void ReceiveHandcuff(GameObject handcuff)
    {
        if (CurrentState != State.Waiting) return;

        Destroy(handcuff);
        _receivedCount++;
        _zone?.UpdateBubble(_receivedCount, _totalNeeded, displayCount);

        if (_receivedCount >= _totalNeeded)
            StartCoroutine(CompleteQuest());
    }

    private IEnumerator CompleteQuest()
    {
        CurrentState = State.Done;

        if (transformParticlePrefab != null)
        {
            GameObject fx = Instantiate(transformParticlePrefab, transform.position, Quaternion.identity);
            Destroy(fx, 3f);
        }

        yield return new WaitForSeconds(0.3f);

        if (prisonerPrefabs != null && prisonerPrefabs.Length > 0)
        {
            int idx = Random.Range(0, prisonerPrefabs.Length);
            GameObject prisoner = Instantiate(prisonerPrefabs[idx], transform.position, transform.rotation);
            PrisonerController pc = prisoner.GetComponent<PrisonerController>();
            if (pc != null && jailDestination != null)
                pc.SetDestination(jailDestination);
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        _zone?.OnOccupantCleared();
        OnTransformed?.Invoke();

        yield return StartCoroutine(ThrowCoins());

        gameObject.SetActive(false);
    }

    [Header("코인 연출")]
    [SerializeField] private int coinsPerLayer = 6;
    [SerializeField] private float coinInterval = 0.08f;

    private IEnumerator ThrowCoins()
    {
        if (coinPrefab == null || takeMoneyZone == null)
        {
            Debug.LogWarning($"ThrowCoins 실패 - coinPrefab: {coinPrefab}, takeMoneyZone: {takeMoneyZone}");
            yield break;
        }

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        for (int i = 0; i < coinsPerLayer; i++)
        {
            GameObject coin = Instantiate(coinPrefab, spawnPos, Quaternion.identity);
            takeMoneyZone.LaunchCoin(coin, spawnPos, coinFlyDuration);
            yield return new WaitForSeconds(coinInterval);
        }

        takeMoneyZone.AddMoney(rewardAmount);
    }
}
