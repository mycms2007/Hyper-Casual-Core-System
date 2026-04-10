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
    [SerializeField] private AudioClip handcuffStackClip;
    [SerializeField] private AudioClip miningClip;
    [SerializeField] private AudioClip minerMiningClip;
    [SerializeField] private AudioClip moneyDropClip;
    [SerializeField] private AudioClip oreBreakClip;
    [SerializeField] private AudioClip zonePurchaseClip;

    [Header("볼륨 설정")]
    [SerializeField] private float drillingVolume       = 1f;
    [SerializeField] private float endPanelVolume       = 1f;
    [SerializeField] private float gemDropVolume        = 1f;
    [SerializeField] private float handcuffDropVolume   = 1f;
    [SerializeField] private float handcuffStackVolume  = 1f;
    [SerializeField] private float miningVolume         = 1f;
    [SerializeField] private float minerMiningVolume    = 1f;
    [SerializeField] private float moneyDropVolume      = 1f;
    [SerializeField] private float oreBreakVolume       = 1f;
    [SerializeField] private float zonePurchaseVolume   = 1f;

    [Header("3D 감쇠 설정")]
    [SerializeField] private float minerAudioMaxDistance    = 30f;
    [SerializeField] private float oreBreakMaxDistance      = 30f;
    [SerializeField] private float handcuffDropMaxDistance  = 30f;
    [SerializeField] private float handcuffStackMaxDistance = 30f;

    [Header("타이밍 설정")]
    [SerializeField] private float drillingFadeDuration = 0.5f;
    [SerializeField] private float endPanelDelay         = 0.5f;
    [SerializeField] private float globalFadeDuration    = 1.5f;

    private AudioSource _loopSource;  // drilling 루프 전용
    private AudioSource _sfxSource;   // 일반 one-shot 전용
    private Coroutine   _fadeRoutine;
    private bool        _silenced;    // 전체 음소거 플래그

    private void Awake()
    {
        Instance = this;

        _loopSource             = gameObject.AddComponent<AudioSource>();
        _loopSource.loop        = true;
        _loopSource.playOnAwake = false;

        _sfxSource              = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake  = false;

    }

    // ── Drilling ─────────────────────────────────────────────────

    public void PlayDrilling()
    {
        if (_silenced || drillingClip == null) return;
        if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        _loopSource.clip   = drillingClip;
        _loopSource.volume = drillingVolume;
        if (!_loopSource.isPlaying) _loopSource.Play();
    }

    public void FadeDrilling()
    {
        if (!_loopSource.isPlaying) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutLoop(drillingFadeDuration));
    }

    private IEnumerator FadeOutLoop(float duration)
    {
        float start   = _loopSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _loopSource.volume = Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }
        _loopSource.Stop();
        _loopSource.volume = drillingVolume;
        _fadeRoutine = null;
    }

    // ── EndPanel (딜레이, 전체음소거 이후에도 재생) ───────────────

    public void PlayEndPanel()
    {
        StartCoroutine(DelayedPlay(endPanelClip, endPanelDelay));
    }

    // ── 광부 곡괭이 / OreBreak (3D 위치 기반) ────────────────────

    public void PlayMinerMining(Vector3 position)
    {
        if (_silenced || minerMiningClip == null) return;
        Play3D(minerMiningClip, position, minerMiningVolume, minerAudioMaxDistance);
    }

    public void PlayOreBreak(Vector3 position)
    {
        if (_silenced || oreBreakClip == null) return;
        Play3D(oreBreakClip, position, oreBreakVolume, oreBreakMaxDistance);
    }

    private void Play3D(AudioClip clip, Vector3 position, float volume, float maxDistance)
    {
        GameObject go = new GameObject("SFX_3D");
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip         = clip;
        src.volume       = volume;
        src.spatialBlend = 1f;
        src.maxDistance  = maxDistance;
        src.minDistance  = 1f;
        src.rolloffMode  = AudioRolloffMode.Logarithmic;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // ── 일반 one-shot ─────────────────────────────────────────────

    public void PlayMining()
    {
        if (_silenced) return;
        _sfxSource.PlayOneShot(miningClip, miningVolume);
    }

    public void PlayGemDrop()
    {
        if (_silenced) return;
        _sfxSource.PlayOneShot(gemDropClip, gemDropVolume);
    }

    public void PlayMoneyDrop()
    {
        if (_silenced) return;
        _sfxSource.PlayOneShot(moneyDropClip, moneyDropVolume);
    }


    public void PlayZonePurchase()
    {
        if (_silenced) return;
        _sfxSource.PlayOneShot(zonePurchaseClip, zonePurchaseVolume);
    }

    public void PlayHandcuffDrop(Vector3 position)
    {
        if (_silenced || handcuffDropClip == null) return;
        Play3D(handcuffDropClip, position, handcuffDropVolume, handcuffDropMaxDistance);
    }

    public void PlayHandcuffStack(Vector3 position)
    {
        if (_silenced || handcuffStackClip == null) return;
        Play3D(handcuffStackClip, position, handcuffStackVolume, handcuffStackMaxDistance);
    }

    // ── 전체 페이드아웃 / 페이드인 (CinematicDirector에서 호출) ──

    public void GlobalFadeOut()
    {
        StartCoroutine(FadeOutRoutine());
    }

    public void GlobalFadeIn()
    {
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        _silenced = true;
        float elapsed   = 0f;
        float loopStart = _loopSource.volume;
        float sfxStart  = _sfxSource.volume;

        while (elapsed < globalFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / globalFadeDuration);
            _loopSource.volume = Mathf.Lerp(loopStart, 0f, t);
            _sfxSource.volume  = Mathf.Lerp(sfxStart,  0f, t);
            yield return null;
        }
        _loopSource.Stop();
        _sfxSource.Stop();
    }

    private IEnumerator FadeInRoutine()
    {
        _silenced = false;
        float elapsed = 0f;

        while (elapsed < globalFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / globalFadeDuration);
            _sfxSource.volume  = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }
        _sfxSource.volume = 1f;
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    private IEnumerator DelayedPlay(AudioClip clip, float delay)
    {
        if (clip == null) yield break;
        yield return new WaitForSeconds(delay);
        _sfxSource.volume = 1f;
        _sfxSource.PlayOneShot(clip, endPanelVolume);
    }
}
