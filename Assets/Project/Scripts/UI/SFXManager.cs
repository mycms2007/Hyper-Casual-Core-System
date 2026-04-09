using System.Collections;
using UnityEngine;

/// <summary>
/// 씬 내 항상 활성화된 오브젝트에 부착.
/// 모든 효과음의 재생 창구.
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("클립")]
    [SerializeField] private AudioClip drillingClip;
    [SerializeField] private AudioClip endPanelClip;
    [SerializeField] private AudioClip gemDropClip;
    [SerializeField] private AudioClip handcuffDropClip;
    [SerializeField] private AudioClip miningClip;
    [SerializeField] private AudioClip moneyDropClip;
    [SerializeField] private AudioClip oreBreakClip;
    [SerializeField] private AudioClip zonePurchaseClip;

    [Header("설정")]
    [SerializeField] private float drillingFadeDuration = 0.5f;
    [SerializeField] private float handcuffDropVolume   = 1f;

    private AudioSource _loopSource;  // drilling 루프 전용
    private AudioSource _sfxSource;   // 일반 one-shot 전용
    private Coroutine   _fadeRoutine;

    private void Awake()
    {
        Instance = this;

        _loopSource            = gameObject.AddComponent<AudioSource>();
        _loopSource.loop       = true;
        _loopSource.playOnAwake = false;

        _sfxSource             = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
    }

    // ── Drilling ─────────────────────────────────────────────────

    public void PlayDrilling()
    {
        if (drillingClip == null) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _loopSource.clip   = drillingClip;
        _loopSource.volume = 1f;
        _loopSource.Play();
    }

    public void FadeDrilling()
    {
        if (!_loopSource.isPlaying) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutLoop());
    }

    private IEnumerator FadeOutLoop()
    {
        float start   = _loopSource.volume;
        float elapsed = 0f;
        while (elapsed < drillingFadeDuration)
        {
            elapsed += Time.deltaTime;
            _loopSource.volume = Mathf.Lerp(start, 0f, elapsed / drillingFadeDuration);
            yield return null;
        }
        _loopSource.Stop();
        _loopSource.volume = 1f;
    }

    // ── EndPanel (0.5초 딜레이) ───────────────────────────────────

    public void PlayEndPanel()
    {
        StartCoroutine(DelayedPlay(endPanelClip, 0.5f));
    }

    // ── 일반 one-shot ─────────────────────────────────────────────

    public void PlayGemDrop()      => _sfxSource.PlayOneShot(gemDropClip);
    public void PlayMining()       => _sfxSource.PlayOneShot(miningClip);
    public void PlayMoneyDrop()    => _sfxSource.PlayOneShot(moneyDropClip);
    public void PlayOreBreak()     => _sfxSource.PlayOneShot(oreBreakClip);
    public void PlayZonePurchase() => _sfxSource.PlayOneShot(zonePurchaseClip);

    // ── HandcuffDrop (3D 위치 기반) ───────────────────────────────

    public void PlayHandcuffDrop(Vector3 position)
    {
        if (handcuffDropClip == null) return;
        AudioSource.PlayClipAtPoint(handcuffDropClip, position, handcuffDropVolume);
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    private IEnumerator DelayedPlay(AudioClip clip, float delay)
    {
        if (clip == null) yield break;
        yield return new WaitForSeconds(delay);
        _sfxSource.PlayOneShot(clip);
    }
}
