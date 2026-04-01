using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakeMoneyZone : MonoBehaviour
{
    [Header("그리드 설정")]
    [SerializeField] private int cols = 2;
    [SerializeField] private int rows = 3;
    [SerializeField] private float xSpacing = 0.2f;
    [SerializeField] private float zSpacing = 0.2f;
    [SerializeField] private float layerHeight = 0.15f;
    [SerializeField] private float stackBaseHeight = 0f;

    [Header("착지 연출")]
    [SerializeField] private AnimationCurve bounceCurve;
    [SerializeField] private float bounceDuration = 0.2f;

    [Header("수령 연출")]
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private float collectFlyDuration = 0.3f;
    [SerializeField] private float collectInterval = 0.05f;

    private readonly List<GameObject> _stack = new List<GameObject>();
    private int _reservedCount;
    private int _totalMoney;
    public int TotalMoney => _totalMoney;

    private void Awake()
    {
        if (bounceCurve.keys.Length == 0)
        {
            bounceCurve = new AnimationCurve(
                new Keyframe(0f,    1f),
                new Keyframe(0.35f, 1.25f),
                new Keyframe(0.65f, 0.9f),
                new Keyframe(1f,    1f)
            );
        }
    }

    /// <summary>ArrestedPerson이 코인을 넘기면 TakeMoneyZone이 비행+착지를 책임진다.</summary>
    public void LaunchCoin(GameObject coin, Vector3 from, float flyDuration)
    {
        Vector3 target = GetGridPosition(_reservedCount);
        _reservedCount++;
        StartCoroutine(FlyCoin(coin, from, target, flyDuration));
    }

    private IEnumerator FlyCoin(GameObject coin, Vector3 start, Vector3 target, float flyDuration)
    {
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            if (coin == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 1.5f;
            coin.transform.position = pos;
            yield return null;
        }
        if (coin != null)
        {
            coin.transform.position = target;
            StartCoroutine(LandCoin(coin));
        }
    }

    /// <summary>코인이 착지했을 때 스택에 추가한다.</summary>
    public void ReceiveCoin(GameObject coin)
    {
        StartCoroutine(LandCoin(coin));
    }

    /// <summary>보상 금액을 누적한다.</summary>
    public void AddMoney(int amount)
    {
        _totalMoney += amount;
    }

    private bool _isCollecting;

    private void OnTriggerEnter(Collider other)
    {
        if (_totalMoney <= 0 || _isCollecting) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        StartCoroutine(CollectCoins());
    }

    private IEnumerator CollectCoins()
    {
        _isCollecting = true;
        int moneyToAdd = _totalMoney;
        _totalMoney = 0;
        _reservedCount = 0;

        List<GameObject> toCollect = new List<GameObject>(_stack);
        _stack.Clear();

        int stackIndex = 0;
        foreach (GameObject coin in toCollect)
        {
            if (coin == null) continue;
            StartCoroutine(FlyToPlayer(coin, stackIndex));
            stackIndex++;
            yield return new WaitForSeconds(collectInterval);
        }

        PlayerWallet.Instance?.Add(moneyToAdd);
        yield return new WaitForSeconds(collectFlyDuration);
        _isCollecting = false;
    }

    [SerializeField] private float collectStackSpacing = 0.15f;

    private IEnumerator FlyToPlayer(GameObject coin, int index)
    {
        Vector3 start = coin.transform.position;
        float elapsed = 0f;

        while (elapsed < collectFlyDuration)
        {
            if (coin == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / collectFlyDuration);
            Vector3 anchor = playerAnchor != null ? playerAnchor.position : transform.position;
            Vector3 target = anchor + Vector3.up * index * collectStackSpacing;
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
            coin.transform.position = pos;
            yield return null;
        }

        if (coin != null) Destroy(coin);
    }

    private IEnumerator LandCoin(GameObject coin)
    {
        if (coin == null) yield break;
        yield return StartCoroutine(BounceScale(coin.transform));
        _stack.Add(coin);
    }

    private IEnumerator BounceScale(Transform t)
    {
        Vector3 baseScale = t.localScale;
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            t.localScale = baseScale * bounceCurve.Evaluate(elapsed / bounceDuration);
            yield return null;
        }
        t.localScale = baseScale;
    }

    private Vector3 GetGridPosition(int index)
    {
        int coinsPerLayer = cols * rows;
        int layer = index / coinsPerLayer;
        int posInLayer = index % coinsPerLayer;
        int col = posInLayer % cols;
        int row = posInLayer / cols;

        float xOffset = (col - (cols - 1) * 0.5f) * xSpacing;
        float zOffset = row * zSpacing;
        float yOffset = stackBaseHeight + layer * layerHeight;

        return transform.position + new Vector3(xOffset, yOffset, zOffset);
    }
}
