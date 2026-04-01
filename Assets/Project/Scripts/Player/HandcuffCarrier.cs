using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gem의 StackManager/StackItem과 완전히 독립된 수갑 전용 적재 시스템.
/// 수갑을 HandcuffAnchor 하위에 부모-자식 구조로 쌓는다.
/// </summary>
public class HandcuffCarrier : MonoBehaviour
{
    [SerializeField] private Transform anchor;       // HandcuffAnchor
    [SerializeField] private float itemSpacing = 0.3f;
    [SerializeField] private Vector3 itemRotation = new Vector3(90f, 0f, 0f);

    private readonly List<GameObject> _handcuffs = new List<GameObject>();

    public int Count => _handcuffs.Count;

    /// <summary>수갑 프리팹을 새로 생성해 스택에 추가한다.</summary>
    public void Add(GameObject prefab)
    {
        if (anchor == null) return;

        GameObject obj = Instantiate(prefab, anchor);
        obj.transform.localPosition = Vector3.up * (_handcuffs.Count * itemSpacing);
        obj.transform.localRotation = Quaternion.Euler(itemRotation);
        _handcuffs.Add(obj);
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
