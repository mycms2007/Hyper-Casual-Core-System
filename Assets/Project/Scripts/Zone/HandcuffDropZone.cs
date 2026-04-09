using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandcuffDropZone : MonoBehaviour
{
    [SerializeField] private float flyDuration = 0.5f;
    [SerializeField] private float arcHeight = 1.5f;
    [SerializeField] private float dropInterval = 0.1f;
    [SerializeField] private float ySpacing = 0.25f;
    [SerializeField] private float stackBaseHeight = 0f;  // 첫 수갑 착지 높이 오프셋
    [SerializeField] private Vector3 landingRotation = new Vector3(90f, 0f, 0f);
    [SerializeField] private AnimationCurve bounceCurve;
    [SerializeField] private float bounceDuration = 0.25f;

    [Header("용량 설정")]
    [SerializeField] private int maxCapacity = 40;

    private readonly List<GameObject> _stack = new List<GameObject>();
    private readonly Queue<List<GameObject>> _receiveQueue = new Queue<List<GameObject>>();
    private bool _isReceiving;
    private int _reservedCount;  // 착지 포함 in-flight 슬롯 예약 수

    private void Awake()
    {
        if (bounceCurve.keys.Length == 0)
        {
            bounceCurve = new AnimationCurve(
                new Keyframe(0f,    1f),
                new Keyframe(0.35f, 1.2f),
                new Keyframe(0.65f, 0.9f),
                new Keyframe(1f,    1f)
            );
        }
    }

    public void Receive(List<GameObject> handcuffs)
    {
        if (handcuffs == null || handcuffs.Count == 0) return;
        _receiveQueue.Enqueue(new List<GameObject>(handcuffs));
        if (!_isReceiving) StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        _isReceiving = true;
        while (_receiveQueue.Count > 0)
            yield return StartCoroutine(ReceiveSequence(_receiveQueue.Dequeue()));
        _isReceiving = false;
    }

    private IEnumerator ReceiveSequence(List<GameObject> handcuffs)
    {
        for (int i = handcuffs.Count - 1; i >= 0; i--)
        {
            GameObject handcuff = handcuffs[i];
            if (handcuff == null) continue;

            if (_reservedCount >= maxCapacity)
            {
                Destroy(handcuff);
                continue;
            }

            int slot = _reservedCount++;
            Vector3 target = GetStackPosition(slot);
            StartCoroutine(FlyAndLand(handcuff, target, () => _stack.Add(handcuff)));

            yield return new WaitForSeconds(dropInterval);
        }
    }

    private IEnumerator FlyAndLand(GameObject handcuff, Vector3 target, System.Action onLanded = null)
    {
        handcuff.transform.rotation = Quaternion.Euler(landingRotation);
        Vector3 start = handcuff.transform.position;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            if (handcuff == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            handcuff.transform.position = pos;
            yield return null;
        }

        if (handcuff == null) yield break;
        handcuff.transform.position = target;
        handcuff.transform.rotation = Quaternion.Euler(landingRotation);

        yield return StartCoroutine(BounceScale(handcuff.transform));
        onLanded?.Invoke();
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

    private Vector3 GetStackPosition(int index)
    {
        return transform.position + Vector3.up * (stackBaseHeight + index * ySpacing);
    }

    public int StackCount => _stack.Count;

    /// <summary>맨 위 수갑 하나를 꺼내 반환한다.</summary>
    public GameObject TakeOne()
    {
        if (_stack.Count == 0) return null;
        int last = _stack.Count - 1;
        GameObject top = _stack[last];
        _stack.RemoveAt(last);
        if (_reservedCount > 0) _reservedCount--;
        return top;
    }
}
