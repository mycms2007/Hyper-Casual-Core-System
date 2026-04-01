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

    protected override void OnPlayerEnter(PlayerController player)
    {
        if (_isTransferring) return;
        if (_stackedHandcuffs.Count == 0) return;
        if (carrier == null || playerAnchor == null) return;

        _isTransferring = true;
        StartCoroutine(TransferToPlayer());
    }

    private IEnumerator TransferToPlayer()
    {
        for (int i = _stackedHandcuffs.Count - 1; i >= 0; i--)
        {
            GameObject handcuff = _stackedHandcuffs[i];
            if (handcuff == null) continue;

            yield return StartCoroutine(FlyHandcuff(handcuff));
            Destroy(handcuff);
            carrier.Add(handcuffPrefab);

            yield return new WaitForSeconds(pickupInterval);
        }

        _stackedHandcuffs.Clear();
        _isTransferring = false;
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
