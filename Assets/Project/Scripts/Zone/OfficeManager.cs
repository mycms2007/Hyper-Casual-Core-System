using System.Collections;
using UnityEngine;

/// <summary>
/// 조건이 충족될 때 HandcuffDropZone → 체포자에게 수갑을 자동 전달한다.
/// 조건: OfficeZone 내 누군가 있음 + HandcuffDropZone에 수갑 있음 + 체포자 대기 중
/// </summary>
public class OfficeManager : MonoBehaviour
{
    [SerializeField] private OfficeZone officeZone;
    [SerializeField] private HandcuffDropZone dropZone;
    [SerializeField] private HandcuffReceiveZone receiveZone;
    [SerializeField] private float sendInterval = 0.4f;    // 수갑 하나 보내는 간격
    [SerializeField] private float flyDuration = 0.4f;
    [SerializeField] private float arcHeight = 1.5f;

    private bool _isSending;

    private void Update()
    {
        if (_isSending) return;
        if (officeZone == null || receiveZone == null || dropZone == null) return;
        if (!officeZone.HasPresence) return;
        if (!receiveZone.IsOccupied) return;
        if (dropZone.StackCount == 0) return;

        ArrestedPerson person = receiveZone.CurrentOccupant;
        if (person == null || !person.NeedsMoreHandcuffs) return;

        StartCoroutine(SendHandcuff(person));
    }

    private IEnumerator SendHandcuff(ArrestedPerson person)
    {
        _isSending = true;

        GameObject handcuff = dropZone.TakeOne();
        if (handcuff == null) { _isSending = false; yield break; }

        // 포물선 비행
        Vector3 start = handcuff.transform.position;
        Vector3 target = person.transform.position + Vector3.up * 0.5f;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            if (handcuff == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            handcuff.transform.position = pos;
            yield return null;
        }

        if (handcuff != null)
            person.ReceiveHandcuff(handcuff);

        yield return new WaitForSeconds(sendInterval);
        _isSending = false;
    }
}
