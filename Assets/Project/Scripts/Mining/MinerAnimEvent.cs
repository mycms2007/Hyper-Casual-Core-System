using UnityEngine;

public class MinerAnimEvent : MonoBehaviour
{
    private MinerWorker _miner;

    private void Awake()
    {
        _miner = GetComponentInParent<MinerWorker>();
    }

    // 애니메이션 이벤트에서 호출
    public void OnMiningHit()
    {
        _miner?.OnMiningHit();
    }
}
