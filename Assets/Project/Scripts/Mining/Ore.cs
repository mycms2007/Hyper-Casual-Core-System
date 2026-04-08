using System.Collections;
using UnityEngine;

public class Ore : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    [SerializeField] private GameObject oreBreakEffect;
    [SerializeField] private GameObject gemPrefab;
    [SerializeField] private float spawnDuration = 0.45f;
    private GemCarrier _gemCarrier;

    private int _currentHp;
    private MeshRenderer _mesh;
    private Transform _spawnPoint;
    private bool _isDead;
    private bool _isClaimed;
    private Vector3 _originalScale;

    public bool IsDead => _isDead;
    public bool IsClaimed => _isClaimed;

    public void Claim()   => _isClaimed = true;
    public void Unclaim() => _isClaimed = false;

    public void Init(Transform spawnPoint, GemCarrier gemCarrier)
    {
        _mesh = GetComponentInChildren<MeshRenderer>();
        _originalScale = transform.localScale;
        _spawnPoint = spawnPoint;
        _gemCarrier = gemCarrier;
        _currentHp = maxHp;
        _isDead = false;
        _mesh.enabled = true;
    }

    public void TakeDamage()
    {
        if (_isDead) return;
        _currentHp--;
        if (_currentHp <= 0)
            Die();
    }

    private void Die()
    {
        _isDead = true;
        _mesh.enabled = false;
        if (oreBreakEffect != null)
            Instantiate(oreBreakEffect, transform.position, Quaternion.identity);

        Debug.Log($"[Ore] Die() — gemPrefab: {(gemPrefab != null ? gemPrefab.name : "NULL")}, _gemCarrier: {(_gemCarrier != null ? "OK" : "NULL")}");
        if (gemPrefab != null && _gemCarrier != null)
            _gemCarrier.TryAdd(gemPrefab);

        OreManager.Instance.ScheduleRespawn(this, _spawnPoint);
    }

    public void Respawn()
    {
        _isDead = false;
        _isClaimed = false;
        _currentHp = maxHp;
        transform.localScale = Vector3.zero;
        _mesh.enabled = true;
        StartCoroutine(SpawnAnimation());
    }

    private IEnumerator SpawnAnimation()
    {
        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / spawnDuration);
            float s = 1f - Mathf.Exp(-7f * p) * Mathf.Cos(12f * p);
            transform.localScale = _originalScale * Mathf.Clamp(s, 0f, 1f);
            yield return null;
        }
        transform.localScale = _originalScale;
    }
}
