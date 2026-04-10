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

    [SerializeField] private float drillCarCooldown = 1f;

    private List<Ore> _oresInRange = new List<Ore>();
    private bool _isMining;
    private Coroutine _miningOffCoroutine;
    private bool _drillPurchased;
    private bool _drillCarCooldownActive;

    public void UnlockDrill()
    {
        _drillPurchased = true;
        Debug.Log($"[MiningTrigger] UnlockDrill 호출됨 — drillObject={drillObject}");
    }

    private bool IsDrillEquipped => drillObject != null && drillObject.activeSelf;
    private bool IsDrillCarPurchased => DrillCar.Instance != null && DrillCar.Instance.IsPurchased;

    public void StartDrillCarCooldown()
    {
        StartCoroutine(DrillCarCooldownRoutine());
    }

    private IEnumerator DrillCarCooldownRoutine()
    {
        _drillCarCooldownActive = true;
        yield return new WaitForSeconds(drillCarCooldown);
        _drillCarCooldownActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Ore ore = other.GetComponent<Ore>();
        if (ore == null || _oresInRange.Contains(ore)) return;

        _oresInRange.Add(ore);

        // 드릴차 켜져 있거나 쿨다운 중이면 채굴 차단
        if (DrillCar.Instance != null && DrillCar.Instance.IsDriving) return;
        if (_drillCarCooldownActive) return;

        // 드릴차 구매 완료 → 드릴차 발진
        Debug.Log($"[MiningTrigger] 광물 감지 — Instance={DrillCar.Instance != null}, IsPurchased={DrillCar.Instance?.IsPurchased}, _drillPurchased={_drillPurchased}");
        if (IsDrillCarPurchased)
        {
            DrillCar.Instance.StartDrive(player.transform.position, player.gameObject);
            return;
        }

        // 드릴 구매 완료 → 광물 접촉 시 장착
        if (_drillPurchased && drillObject != null)
        {
            Debug.Log($"[MiningTrigger] drillObject.SetActive(true) 호출");
            drillObject.SetActive(true);
            SFXManager.Instance?.PlayDrilling();
        }

        // 손 드릴 장착 → 즉사
        if (IsDrillEquipped)
        {
            while (!ore.IsDead) ore.TakeDamage();
            _isMining = true;
            if (player != null)
            {
                player.SetForceIdle(true);
                player.SetDrillSpeedBoost(true);
            }
        }

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
        if (DrillCar.Instance != null && DrillCar.Instance.IsDriving) return;
        if (_drillCarCooldownActive) return;

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
                if (player != null && !IsDrillEquipped) player.SetMiningActive(true);
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
        if (player != null && !IsDrillEquipped) player.SetMiningActive(false);
        if (drillObject != null)
        {
            drillObject.SetActive(false);
            SFXManager.Instance?.FadeDrilling();
        }
        if (player != null)
        {
            player.SetForceIdle(false);
            player.SetDrillSpeedBoost(false);
        }
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
