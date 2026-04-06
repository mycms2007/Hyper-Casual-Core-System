using System.Collections;
using UnityEngine;

public class AlbaSpawner : MonoBehaviour
{
    [Header("알바 프리팹")]
    [SerializeField] private GameObject albaPrefab;

    [Header("소환 지점")]
    [SerializeField] private Transform spawnPoint;

    [Header("씬 참조 주입")]
    [SerializeField] private HandcuffStackZone handcuffStackZone;
    [SerializeField] private Transform idlePosition;

    private void OnEnable()
    {
        Debug.Log("[AlbaSpawner] OnEnable 호출됨");

        if (albaPrefab == null)
        {
            Debug.LogWarning("[AlbaSpawner] albaPrefab이 연결되지 않았습니다.");
            return;
        }
        if (spawnPoint == null)
        {
            Debug.LogWarning("[AlbaSpawner] spawnPoint가 연결되지 않았습니다.");
            return;
        }

        Debug.Log("[AlbaSpawner] Instantiate 시작");
        GameObject albaObj = Instantiate(albaPrefab, spawnPoint.position, spawnPoint.rotation);
        albaObj.transform.SetParent(null);

        AlbaController alba = albaObj.GetComponent<AlbaController>();
        if (alba != null)
            alba.Initialize(handcuffStackZone, idlePosition);
        else
            Debug.LogWarning("[AlbaSpawner] AlbaController 컴포넌트를 찾을 수 없습니다.");

        StartCoroutine(SpringAppear(albaObj.transform));
        Debug.Log("[AlbaSpawner] 소환 완료");
    }

    private IEnumerator SpringAppear(Transform t)
    {
        Vector3 targetScale = t.localScale;
        t.localScale = Vector3.zero;
        float elapsed = 0f;
        float duration = 0.45f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
            t.localScale = targetScale * Mathf.Clamp(s, 0f, 1f);
            yield return null;
        }

        t.localScale = targetScale;
    }
}
