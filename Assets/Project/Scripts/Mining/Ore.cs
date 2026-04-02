using UnityEngine;

public class Ore : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    [SerializeField] private GameObject oreBreakEffect;
    [SerializeField] private GameObject gemPrefab;
    private GemCarrier _gemCarrier;

    private int _currentHp;
    private MeshRenderer _mesh;
    private Transform _spawnPoint;
    private bool _isDead;
    public bool IsDead => _isDead;

    public void Init(Transform spawnPoint, GemCarrier gemCarrier)
    {
        _mesh = GetComponentInChildren<MeshRenderer>();
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
        _currentHp = maxHp;
        _mesh.enabled = true;
    }
}
