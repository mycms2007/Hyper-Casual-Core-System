using System.Collections.Generic;
using UnityEngine;

public class StackManager : MonoBehaviour
{
    [SerializeField] private Transform stackRoot;
    [SerializeField] private int maxCapacity = 10;
    [SerializeField] private float itemSpacing = 0.5f;
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float swayAmount = 20f;

    private List<StackItem> _stack = new List<StackItem>();
    private Vector3 _prevPos;

    public int Count => _stack.Count;

    private Transform _originalRootParent;
    private Vector3 _originalRootLocalPos;
    private Vector3 _originalRootLocalScale;

    public void AttachRootTo(Transform newParent)
    {
        _originalRootParent     = stackRoot.parent;
        _originalRootLocalPos   = stackRoot.localPosition;
        _originalRootLocalScale = stackRoot.localScale;
        stackRoot.SetParent(newParent, false);
        stackRoot.localPosition = Vector3.zero;
    }

    public void DetachRoot()
    {
        if (_originalRootParent == null) return;
        stackRoot.SetParent(_originalRootParent, false);
        stackRoot.localPosition = _originalRootLocalPos;
        stackRoot.localScale    = _originalRootLocalScale;
        _originalRootParent = null;
    }

    private void Awake()
    {
        if (stackRoot == null)
            Debug.LogWarning("StackManager: Stack Root가 연결되지 않았습니다.");

        _prevPos = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - _prevPos;
        delta.y = 0f;
        Vector3 moveDir = delta.magnitude > 0.001f ? delta.normalized : Vector3.zero;

        foreach (StackItem item in _stack)
            item.Tick(moveDir);

        _prevPos = transform.position;
    }

    public bool TryAdd(GameObject itemPrefab, Vector3 rotationOffset = default)
    {
        if (stackRoot == null)
        {
            Debug.LogWarning("[StackManager] TryAdd 실패 — stackRoot가 null");
            return false;
        }
        if (_stack.Count >= maxCapacity)
        {
            Debug.LogWarning($"[StackManager] TryAdd 실패 — 최대치 도달 ({_stack.Count}/{maxCapacity})");
            return false;
        }

        Transform target = _stack.Count == 0 ? stackRoot : _stack[_stack.Count - 1].transform;
        Vector3 spawnPos = target.position + Vector3.up * itemSpacing;

        GameObject obj = Instantiate(itemPrefab, spawnPos, Quaternion.identity);
        obj.SetActive(true);
        StackItem item = obj.AddComponent<StackItem>();

        item.Initialize(target, Vector3.up * itemSpacing, followSpeed, rotationSpeed, swayAmount, rotationOffset);

        _stack.Add(item);
        return true;
    }

    public GameObject TryRemove()
    {
        if (_stack.Count == 0) return null;

        StackItem last = _stack[_stack.Count - 1];
        _stack.RemoveAt(_stack.Count - 1);
        return last.gameObject;
    }

    /// <summary>스택 전체를 한번에 비웁니다. index 0 → last 순서로 반환.</summary>
    public List<GameObject> TakeAll()
    {
        var result = new List<GameObject>();
        for (int i = 0; i < _stack.Count; i++)
            result.Add(_stack[i].gameObject);
        _stack.Clear();
        return result;
    }

    /// <summary>이미 존재하는 GameObject를 스택에 추가합니다 (Instantiate 없이).</summary>
    public bool TryAddExisting(GameObject obj, Vector3 rotationOffset = default)
    {
        if (stackRoot == null || _stack.Count >= maxCapacity) return false;

        // 물리 시뮬레이션이 체인 이동을 방해하지 않도록
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        StackItem item = obj.AddComponent<StackItem>();
        Transform target = _stack.Count == 0 ? stackRoot : _stack[_stack.Count - 1].transform;
        item.Initialize(target, Vector3.up * itemSpacing, followSpeed, rotationSpeed, swayAmount, rotationOffset);

        _stack.Add(item);
        return true;
    }
}
