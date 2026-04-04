using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Gem의 StackManager/StackItem과 완전히 독립된 수갑 전용 적재 시스템.
/// 수갑을 HandcuffAnchor 하위에 부모-자식 구조로 쌓는다.
/// </summary>
public class HandcuffCarrier : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private float itemSpacing = 0.3f;
    [SerializeField] private Vector3 itemRotation = new Vector3(90f, 0f, 0f);
    [SerializeField] private float expandDuration = 0.25f;

    private readonly List<GameObject> _handcuffs = new List<GameObject>();
    private Transform _originalAnchorParent;
    private Vector3 _originalAnchorLocalPos;
    private Vector3 _originalAnchorLocalScale;
    private bool _isAttached;

    // HandcuffStackZone에서 날아오는 중(아직 Add 전)인 수갑 수
    private int _pendingCount;
    public int TotalCount => _handcuffs.Count + _pendingCount;
    public int PendingCount => _pendingCount;
    public void ReservePending() => _pendingCount++;
    public void CommitPending() { if (_pendingCount > 0) _pendingCount--; }

    public void AttachAnchorTo(Transform newParent)
    {
        _originalAnchorParent   = anchor.parent;
        _originalAnchorLocalPos = anchor.localPosition;
        _originalAnchorLocalScale = anchor.localScale;
        _isAttached = true;
        anchor.SetParent(newParent, false);
        anchor.localPosition = Vector3.zero;
    }

    public void RestoreAnchor()
    {
        if (!_isAttached) return;
        _isAttached = false;
        anchor.SetParent(_originalAnchorParent, false);
        anchor.localScale    = _originalAnchorLocalScale;
        anchor.localPosition = _originalAnchorLocalPos;
        _originalAnchorParent = null;
    }

    public int Count => _handcuffs.Count;

    /// <summary>수갑 프리팹을 새로 생성해 스택에 추가한다.</summary>
    public void Add(GameObject prefab)
    {
        if (anchor == null) return;

        GameObject obj = Instantiate(prefab, anchor);
        obj.transform.localPosition = Vector3.up * (_handcuffs.Count * itemSpacing);
        obj.transform.localRotation = Quaternion.Euler(itemRotation);

        // anchor의 worldScale이 1이 아닐 경우(DrillCar 모델 계층 오염 등)
        // localScale을 역산해 worldScale이 항상 prefab 원본 크기가 되도록 보정.
        // worldScale = anchor.lossyScale × localScale → localScale = prefabScale / anchor.lossyScale
        Vector3 w = anchor.lossyScale;
        Vector3 p = prefab.transform.localScale;
        if (w.x != 0f && w.y != 0f && w.z != 0f)
            obj.transform.localScale = new Vector3(p.x / w.x, p.y / w.y, p.z / w.z);

        _handcuffs.Add(obj);
        StartCoroutine(ExpandScale(obj.transform));
    }

    private IEnumerator ExpandScale(Transform t)
    {
        if (t == null) yield break;
        Vector3 targetScale = t.localScale;
        t.localScale = Vector3.zero;
        float elapsed = 0f;
        while (elapsed < expandDuration)
        {
            if (t == null) yield break;
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / expandDuration);
            float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
            t.localScale = targetScale * s;
            yield return null;
        }
        if (t != null) t.localScale = targetScale;
    }

    /// <summary>전체 수갑을 꺼내 반환하고 스택을 비운다. 언패런트 후 반환.</summary>
    public List<GameObject> TakeAll()
    {
        var result = new List<GameObject>(_handcuffs);
        foreach (var obj in result)
            if (obj != null) obj.transform.SetParent(null);
        _handcuffs.Clear();
        return result;
    }
}
