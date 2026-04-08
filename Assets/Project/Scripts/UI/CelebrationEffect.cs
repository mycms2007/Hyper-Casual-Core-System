using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UICanvas 안 빈 오브젝트에 부착.
/// Play() 호출 시 폭죽+빵빠레 파티클 2발 버스트.
///
/// 360° 전방향 발사 + 감속(drag) + 약한 중력으로 폭죽이 타들어가는 느낌.
/// Spark(사각형)와 Streamer(직사각형)를 혼합, Emission 펄스로 반짝임 연출.
/// 2발은 살짝 다른 위치에서 터져 두 발의 폭죽처럼 보임.
/// </summary>
public class CelebrationEffect : MonoBehaviour
{
    [Header("버스트 설정")]
    [SerializeField] private int   particlesPerBurst = 45;
    [SerializeField] private float burstInterval     = 0.32f;
    [SerializeField] private Vector2 burst1Offset    = new Vector2(-75f, 0f);
    [SerializeField] private Vector2 burst2Offset    = new Vector2( 75f, 0f);

    [Header("파티클 물리")]
    [SerializeField] private float launchSpeed = 500f;
    [SerializeField] private float drag        = 2.6f;   // 감속 계수 — 클수록 빨리 멈춤
    [SerializeField] private float gravity     = 230f;   // 약한 중력으로 자연스러운 낙하
    [SerializeField] private float duration    = 1.7f;

    [Header("Emission")]
    [SerializeField] private float emissionIntensity  = 4.5f;
    [SerializeField] private float emissionPulseSpeed = 6f;

    // 6가지 색상
    private static readonly Color[] ParticleColors =
    {
        new Color(1f,    0.18f, 0.18f),  // 빨강
        new Color(1f,    0.85f, 0.08f),  // 노랑
        new Color(0.15f, 0.88f, 0.28f),  // 초록
        new Color(0.18f, 0.52f, 1f   ),  // 파랑
        new Color(1f,    0.22f, 0.88f),  // 핑크
        new Color(1f,    0.52f, 0.06f),  // 주황
    };

    // ── 테스트 입력 (R 장전 → 우클릭 발사) ──────────────────────
    private bool _loaded;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            _loaded = true;
            Debug.Log("[CelebrationEffect] 장전 완료 — 우클릭으로 발사");
        }

        if (_loaded && Input.GetMouseButtonDown(1))
        {
            _loaded = false;
            Play();
        }
    }
    // ────────────────────────────────────────────────────────────

    public void Play()
    {
        StartCoroutine(BurstSequence());
    }

    // ── 버스트 시퀀스 ─────────────────────────────────────────────

    private IEnumerator BurstSequence()
    {
        SpawnBurst(burst1Offset);
        yield return new WaitForSeconds(burstInterval);
        SpawnBurst(burst2Offset);
    }

    private void SpawnBurst(Vector2 origin)
    {
        for (int i = 0; i < particlesPerBurst; i++)
        {
            Color c      = ParticleColors[Random.Range(0, ParticleColors.Length)];
            bool isSpark = Random.value > 0.38f; // 62% spark / 38% streamer
            StartCoroutine(AnimateParticle(c, origin, isSpark));
        }
    }

    // ── 파티클 애니메이션 ─────────────────────────────────────────

    private IEnumerator AnimateParticle(Color baseColor, Vector2 startPos, bool isSpark)
    {
        GameObject go = new GameObject("FxParticle");
        go.transform.SetParent(transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        Image img        = go.AddComponent<Image>();

        // Spark: 정사각형에 가까운 불꽃  /  Streamer: 길쭉한 종이테이프
        rt.sizeDelta = isSpark
            ? new Vector2(Random.Range(5f,  13f), Random.Range(5f,  13f))
            : new Vector2(Random.Range(11f, 23f), Random.Range(4f,   9f));

        rt.anchoredPosition = startPos;
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

        // 360° 전방향 발사
        float angle   = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float spd     = Random.Range(launchSpeed * 0.45f, launchSpeed * 1.4f);
        Vector2 vel   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spd;
        float rotSpd  = Random.Range(-700f, 700f);

        float   elapsed = 0f;
        Vector2 pos     = startPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // 감속(drag) + 중력
            vel   -= vel * (drag * Time.deltaTime);
            vel.y -= gravity * Time.deltaTime;
            pos   += vel * Time.deltaTime;

            rt.anchoredPosition = pos;
            rt.localEulerAngles = new Vector3(0f, 0f, rotSpd * elapsed);

            // Emission 펄스 (참조 코드 방식 동일)
            float pulse    = Mathf.Sin(elapsed * emissionPulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            float emission = Mathf.Lerp(emissionIntensity * 0.35f, emissionIntensity, pulse);

            // Alpha — 빠른 페이드인 / 후반 페이드아웃
            float fadeIn  = 0.06f;
            float fadeOut = 0.55f;
            float alpha;
            if (elapsed < fadeIn)
                alpha = elapsed / fadeIn;
            else if (elapsed > duration - fadeOut)
                alpha = (duration - elapsed) / fadeOut;
            else
                alpha = 1f;

            Color c = baseColor * emission;
            c.a     = alpha;
            img.color = c;

            yield return null;
        }

        Destroy(go);
    }
}
