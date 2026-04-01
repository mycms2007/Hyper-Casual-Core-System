using System.Collections;
using UnityEngine;

public class ItemTransZone : MonoBehaviour
{
    [SerializeField] private HandcuffStackZone handcuffStackZone;
    [SerializeField] private float shrinkDuration = 0.3f;
    [SerializeField] private float shrinkDelay = 0f;  // 음수: 컨베이어 이동 중 미리 축소 시작

    private int _pendingCount;
    private int _totalCount;

    /// <summary>ProcessingMachine에서 수갑이 도착했을 때 호출됩니다.</summary>
    public void OnHandcuffArrived(GameObject handcuff)
    {
        _pendingCount++;
        _totalCount++;
        StartCoroutine(ShrinkAndTransfer(handcuff));
    }

    private IEnumerator ShrinkAndTransfer(GameObject handcuff)
    {
        if (shrinkDelay > 0f)
            yield return new WaitForSeconds(shrinkDelay);

        Vector3 originalScale = handcuff.transform.localScale;
        float elapsed = shrinkDelay < 0f ? -shrinkDelay : 0f;  // 음수면 이미 진행된 시간만큼 앞에서 시작

        while (elapsed < shrinkDuration)
        {
            if (handcuff == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            handcuff.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(handcuff);
        _pendingCount--;

        if (_pendingCount <= 0)
        {
            if (handcuffStackZone != null)
                handcuffStackZone.SpawnHandcuffs(_totalCount);
            _totalCount = 0;
        }
    }
}
