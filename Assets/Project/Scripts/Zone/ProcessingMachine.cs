using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcessingMachine : MonoBehaviour
{
    [SerializeField] private Transform inputPoint;
    [SerializeField] private Transform makeItemPoint;
    [SerializeField] private GameObject handcuffPrefab;
    [SerializeField] private float gemFlyDuration = 0.4f;
    [SerializeField] private float shrinkDuration = 0.3f;
    [SerializeField] private float expandDuration = 0.3f;
    [SerializeField] private float conveyorDuration = 1.0f;
    [SerializeField] private Vector3 handcuffSpawnRotation = new Vector3(90f, 0f, 0f);
    [SerializeField] private ItemTransZone itemTransZone;

    /// <summary>GemDropZone에서 모든 Gem 착지 완료 시 호출됩니다.</summary>
    public void ReceiveGems(List<GameObject> gems)
    {
        StartCoroutine(ProcessGems(gems));
    }

    private IEnumerator ProcessGems(List<GameObject> gems)
    {
        Debug.Log($"[ProcessingMachine] ProcessGems 시작 — gem 수: {gems.Count}");
        for (int i = gems.Count - 1; i >= 0; i--)
        {
            yield return StartCoroutine(FlyToInput(gems[i]));
            yield return StartCoroutine(ShrinkAndDestroy(gems[i]));
            yield return StartCoroutine(SpawnHandcuff());
        }
    }

    private IEnumerator FlyToInput(GameObject gem)
    {
        Vector3 start = gem.transform.position;
        Vector3 target = inputPoint.position;
        float elapsed = 0f;

        while (elapsed < gemFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / gemFlyDuration);
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 1.0f;
            gem.transform.position = pos;
            yield return null;
        }
        gem.transform.position = target;
    }

    private IEnumerator ShrinkAndDestroy(GameObject gem)
    {
        Vector3 originalScale = gem.transform.localScale;
        float elapsed = 0f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            gem.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            yield return null;
        }
        Destroy(gem);
    }

    private IEnumerator SpawnHandcuff()
    {
        Debug.Log($"[ProcessingMachine] SpawnHandcuff 호출 — prefab: {handcuffPrefab != null}, makeItemPoint: {makeItemPoint != null}");
        GameObject handcuff = Instantiate(handcuffPrefab, makeItemPoint.position, Quaternion.Euler(handcuffSpawnRotation));
        Debug.Log($"[ProcessingMachine] 수갑 생성됨: {handcuff.name} at {handcuff.transform.position}");
        Vector3 originalScale = handcuff.transform.localScale;
        handcuff.transform.localScale = Vector3.zero;

        // 확장 애니메이션
        float elapsed = 0f;
        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / expandDuration);
            handcuff.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
            yield return null;
        }
        handcuff.transform.localScale = originalScale;

        // 컨베이어 이동
        yield return StartCoroutine(MoveAlongConveyor(handcuff));
    }

    private IEnumerator MoveAlongConveyor(GameObject handcuff)
    {
        if (itemTransZone == null) yield break;

        Vector3 start = handcuff.transform.position;
        Vector3 target = itemTransZone.transform.position;
        float elapsed = 0f;

        while (elapsed < conveyorDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / conveyorDuration);
            handcuff.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
        handcuff.transform.position = target;
        itemTransZone.OnHandcuffArrived(handcuff);
    }
}
