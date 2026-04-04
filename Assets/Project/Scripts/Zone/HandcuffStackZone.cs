using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandcuffStackZone : Zone
{
    [Header("생산 설정")]
    [SerializeField] private GameObject handcuffPrefab;
    [SerializeField] private float ySpacing = 0.3f;
    [SerializeField] private AnimationCurve bounceCurve;
    [SerializeField] private float spawnInterval = 0.1f;
    [SerializeField] private float expandDuration = 0.4f;
    [SerializeField] private Vector3 stackBaseOffset = Vector3.zero;
    [SerializeField] private Vector3 handcuffSpawnRotation = new Vector3(90f, 0f, 0f);

    [Header("픽업 설정")]
    [SerializeField] private HandcuffCarrier carrier;   // Gem StackManager와 완전 분리
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private float flyDuration = 0.4f;
    [SerializeField] private float pickupInterval = 0.08f;

    private List<GameObject> _stackedHandcuffs = new List<GameObject>();
    private bool _isTransferring;

    [Header("낙하 연출")]
    [SerializeField] private float dropDuration = 0.35f;
    [SerializeField] private AnimationCurve landBounceCurve;

    private void Awake()
    {
        if (bounceCurve.keys.Length == 0)
        {
            bounceCurve = new AnimationCurve(
                new Keyframe(0f,    0f),
                new Keyframe(0.5f,  1.3f),
                new Keyframe(0.75f, 0.85f),
                new Keyframe(1f,    1f)
            );
        }

        if (landBounceCurve.keys.Length == 0)
        {
            landBounceCurve = new AnimationCurve(
                new Keyframe(0f,    1f),
                new Keyframe(0.35f, 1.3f),
                new Keyframe(0.65f, 0.85f),
                new Keyframe(1f,    1f)
            );
        }
    }

    /// <summary>ItemTransZone에서 전체 소멸 완료 시 호출됩니다.</summary>
    public void SpawnHandcuffs(int count)
    {
        StartCoroutine(SpawnSequence(count));
    }

    private IEnumerator SpawnSequence(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int index = _stackedHandcuffs.Count;
            Vector3 pos = GetStackPosition(index);
            GameObject handcuff = Instantiate(handcuffPrefab, pos, Quaternion.Euler(handcuffSpawnRotation));
            Vector3 originalScale = handcuff.transform.localScale;
            handcuff.transform.localScale = Vector3.zero;
            _stackedHandcuffs.Add(handcuff);

            StartCoroutine(ExpandScale(handcuff.transform, originalScale));
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private IEnumerator ExpandScale(Transform t, Vector3 targetScale)
    {
        float elapsed = 0f;
        while (elapsed < expandDuration)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float s = bounceCurve.Evaluate(elapsed / expandDuration);
            t.localScale = targetScale * s;
            yield return null;
        }
        if (t != null) t.localScale = targetScale;
    }

    private Vector3 GetStackPosition(int index)
    {
        return transform.position + stackBaseOffset + new Vector3(0f, index * ySpacing, 0f);
    }

    protected override void OnPlayerEnter(PlayerController player) => TryTransfer();
    protected override void OnPlayerStay(PlayerController player) => TryTransfer();

    private void TryTransfer()
    {
        if (_isTransferring) return;
        if (_stackedHandcuffs.Count == 0) return;
        if (carrier == null || playerAnchor == null) return;

        _isTransferring = true;
        StartCoroutine(TransferToPlayer());
    }

    private IEnumerator TransferToPlayer()
    {
        int originalCount = _stackedHandcuffs.Count;

        // 전체를 미리 예약 — OfficeZone이 TotalCount로 in-flight 수갑까지 파악 가능
        for (int i = 0; i < originalCount; i++) carrier.ReservePending();

        for (int i = originalCount - 1; i >= 0; i--)
        {
            GameObject handcuff = _stackedHandcuffs[i];
            if (handcuff == null) { carrier.CommitPending(); continue; }

            yield return StartCoroutine(FlyHandcuff(handcuff));
            Destroy(handcuff);
            carrier.Add(handcuffPrefab);
            carrier.CommitPending();

            yield return new WaitForSeconds(pickupInterval);
        }

        // 루프 도중 SpawnSequence가 추가한 수갑들 → 1층으로 낙하
        List<GameObject> excess = new List<GameObject>();
        for (int i = originalCount; i < _stackedHandcuffs.Count; i++)
            if (_stackedHandcuffs[i] != null) excess.Add(_stackedHandcuffs[i]);

        _stackedHandcuffs.Clear();

        for (int i = 0; i < excess.Count; i++)
        {
            _stackedHandcuffs.Add(excess[i]);
            StartCoroutine(DropToFloor(excess[i].transform, GetStackPosition(i)));
        }

        _isTransferring = false;
    }

    private IEnumerator DropToFloor(Transform t, Vector3 target)
    {
        if (t == null) yield break;
        Vector3 start = t.position;
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / dropDuration);
            Vector3 pos = Vector3.Lerp(start, target, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * 0.4f;
            t.position = pos;
            yield return null;
        }

        if (t == null) yield break;
        t.position = target;
        yield return StartCoroutine(LandBounce(t));
    }

    private IEnumerator LandBounce(Transform t)
    {
        if (t == null) yield break;
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 baseScale = t.localScale;

        while (elapsed < duration)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            t.localScale = baseScale * landBounceCurve.Evaluate(elapsed / duration);
            yield return null;
        }

        if (t != null) t.localScale = baseScale;
    }

    private IEnumerator FlyHandcuff(GameObject handcuff)
    {
        Vector3 start = handcuff.transform.position;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            if (handcuff == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            Vector3 pos = Vector3.Lerp(start, playerAnchor.position, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 1.0f;
            handcuff.transform.position = pos;
            yield return null;
        }

        if (handcuff == null) yield break;
        handcuff.transform.position = playerAnchor.position;
    }
}
