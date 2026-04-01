using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiningTrigger : MonoBehaviour
{
    [SerializeField] private float miningOffDelayMove = 0.5f;
    [SerializeField] private float miningOffDelayDead = 0.1f;
    [SerializeField] private float drillOffDelay = 1f;
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject drillObject; // 손 드릴 프리팹

    private List<Ore> _oresInRange = new List<Ore>();
    private bool _isMining;
    private Coroutine _miningOffCoroutine;

    private bool IsDrillEquipped => drillObject != null && drillObject.activeSelf;
    private bool IsDrillCarPurchased => DrillCar.Instance != null && DrillCar.Instance.IsPurchased;

    private void OnTriggerEnter(Collider other)
    {
        Ore ore = other.GetComponent<Ore>();
        if (ore == null || _oresInRange.Contains(ore)) return;

        _oresInRange.Add(ore);

        // 드릴차 구매 완료 → 드릴차 발진
        if (IsDrillCarPurchased)
        {
            DrillCar.Instance.StartDrive(
                transform.position,
                player.transform.forward,
                player.gameObject
            );
            return;
        }

        // 손 드릴 장착 → 즉사
        if (IsDrillEquipped)
            while (!ore.IsDead) ore.TakeDamage();

        UpdateMiningState();
    }

    private void OnTriggerExit(Collider other)
    {
        Ore ore = other.GetComponent<Ore>();
        if (ore != null)
        {
            _oresInRange.Remove(ore);
            UpdateMiningState();
        }
    }

    private void Update()
    {
        UpdateMiningState();
        _oresInRange.RemoveAll(o => o == null || !o.gameObject.activeSelf || o.IsDead);
    }

    /// <summary>PlayerController의 Animation Event에서 호출됩니다.</summary>
    public void DealDamage()
    {
        if (!_isMining || IsDrillEquipped) return;
        Ore target = GetClosestOre();
        if (target != null) target.TakeDamage();
    }

    private void UpdateMiningState()
    {
        bool hasOre = GetClosestOre() != null;

        if (hasOre)
        {
            if (_miningOffCoroutine != null)
            {
                StopCoroutine(_miningOffCoroutine);
                _miningOffCoroutine = null;
            }

            if (!_isMining)
            {
                _isMining = true;
                if (player != null) player.SetMiningActive(true);
            }
        }
        else if (_isMining && _miningOffCoroutine == null)
        {
            float delay;
            if (IsDrillEquipped)
                delay = drillOffDelay;
            else
                delay = _oresInRange.Count > 0 ? miningOffDelayDead : miningOffDelayMove;

            _miningOffCoroutine = StartCoroutine(MiningOffCoroutine(delay));
        }
    }

    private IEnumerator MiningOffCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isMining = false;
        if (player != null) player.SetMiningActive(false);
        _miningOffCoroutine = null;
    }

    private Ore GetClosestOre()
    {
        Ore closest = null;
        float minDist = float.MaxValue;

        foreach (Ore ore in _oresInRange)
        {
            if (ore == null || !ore.gameObject.activeSelf || ore.IsDead) continue;
            float dist = Vector3.Distance(transform.position, ore.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = ore;
            }
        }

        return closest;
    }
}
