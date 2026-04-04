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

    public void AttachAnchorTo(Transform newParent)
    {
        _originalAnchorParent = anchor.parent;
        _originalAnchorLocalPos = anchor.localPosition;
        anchor.SetParent(newParent, false);
        anchor.localPosition = Vector3.zero;
    }

    public void RestoreAnchor()
    {
        if (_originalAnchorParent == null) return;
        StartCoroutine(LerpAnchorBack());
    }

    private IEnumerator LerpAnchorBack()
    {
        anchor.SetParent(_originalAnchorParent, true);
        Vector3 startLocal = anchor.localPosition;
        float elapsed = 0f, duration = 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            anchor.localPosition = Vector3.Lerp(startLocal, _originalAnchorLocalPos, elapsed / duration);
            yield return null;
        }
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
