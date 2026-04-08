using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GemDropZone : Zone
{
    [SerializeField] private StackManager playerStack;
    [SerializeField] private float gemFlyDuration = 0.5f;
    [SerializeField] private float gemSpawnInterval = 0.1f;
    [SerializeField] private float xSpacing = 0.3f;
    [SerializeField] private float ySpacing = 0.3f;
    [SerializeField] private Vector3 stackBaseOffset = Vector3.zero;
    [SerializeField] [Range(0f, 360f)] private float stackAngleDegrees = 0f;
    [SerializeField] private AnimationCurve bounceCurve;
    [SerializeField] private ProcessingMachine machine;

    private bool _isCollecting;
    private List<GameObject> _stackedGems = new List<GameObject>();
    private Vector3 _zoneBasePos;
    private Vector3 _stackDir;

    private void Awake()
    {
        // Inspector에서 커브 미설정 시 기본 바운스 커브 적용
        // 1.0 → 1.3 → 0.85 → 1.0 (overshoot → settle)
        if (bounceCurve.keys.Length == 0)
        {
            bounceCurve = new AnimationCurve(
                new Keyframe(0f,    1f),
                new Keyframe(0.35f, 1.3f),
                new Keyframe(0.65f, 0.85f),
                new Keyframe(1f,    1f)
            );
        }
    }

    protected override void OnPlayerEnter(PlayerController player)
    {
        if (_isCollecting) return;
        if (DrillCar.Instance != null && DrillCar.Instance.IsDriving) return;
        if (playerStack == null || playerStack.Count == 0) return;

        _isCollecting = true;
        _stackedGems.Clear();
        _zoneBasePos = transform.position;
        _stackDir = Quaternion.Euler(0f, stackAngleDegrees, 0f) * Vector3.forward;
        StartCoroutine(CollectGems());
    }

    private IEnumerator CollectGems()
    {
        // TakeAll: index 0(바닥) → last(맨 위) 순서
        List<GameObject> toFly = playerStack.TakeAll();

        // StackItem 컴포넌트 전부 제거 — 체인 추적 해제
        foreach (GameObject gem in toFly)
        {
            StackItem si = gem.GetComponent<StackItem>();
            if (si != null) Destroy(si);
        }

        // Destroy 반영 대기 (1프레임)
        yield return null;

        int total = toFly.Count;
        int landed = 0;
        int index = 0;

        for (int i = toFly.Count - 1; i >= 0; i--)
        {
            GameObject gem = toFly[i];
Vector3 targetPos = GetStackPosition(index);
            StartCoroutine(FlyGem(gem, targetPos, () =>
            {
                _stackedGems.Add(gem);
                landed++;
                if (landed >= total)
                    OnAllGemLanded();
            }));

            index++;
            yield return new WaitForSeconds(gemSpawnInterval);
        }
    }

    private void OnAllGemLanded()
    {
        _isCollecting = false;
        if (machine != null)
            machine.ReceiveGems(new List<GameObject>(_stackedGems));
        _stackedGems.Clear();
    }

    [SerializeField] private Vector3 landingRotation = Vector3.zero;

    private IEnumerator FlyGem(GameObject gem, Vector3 target, System.Action onLanded)
    {
        gem.transform.rotation = Quaternion.Euler(landingRotation);
        Vector3 start = gem.transform.position;
        float elapsed = 0f;
        float arcHeight = 1.5f;

        // 포물선 이동
        while (elapsed < gemFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / gemFlyDuration);
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            gem.transform.position = pos;
            yield return null;
        }

        gem.transform.position = target;
        gem.transform.rotation = Quaternion.Euler(landingRotation);

        // 착지 바운스
        yield return StartCoroutine(BounceScale(gem.transform));
        onLanded?.Invoke();
    }

    private IEnumerator BounceScale(Transform t)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 baseScale = t.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float s = bounceCurve.Evaluate(elapsed / duration);
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }

    private Vector3 GetStackPosition(int index)
    {
        float side = (index % 2 == 0) ? -xSpacing : xSpacing;
        float y = (index / 2) * ySpacing;
        Vector3 sideOffset = _stackDir * side;
        return _zoneBasePos + stackBaseOffset + Vector3.up * y + sideOffset;
    }
}
