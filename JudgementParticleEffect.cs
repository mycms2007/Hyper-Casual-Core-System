using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class JudgementParticleEffect : MonoBehaviour
{
    [Header("이펙트 오브젝트")]
    public Image effectImage;

    [Header("위치 지정")]
    public Transform startPointObject;
    public Transform endPointObject;

    [Header("연발 설정")]
    [Range(1, 10)]
    public int burstCount = 3;

    [Range(0f, 0.5f)]
    public float burstInterval = 0.1f;

    [Header("곡선 경로 설정")]
    public CurveDirection curveDirection = CurveDirection.Left;

    [Range(0f, 500f)]
    public float curvature = 100f;

    [Header("애니메이션 설정")]
    [Range(0.2f, 2f)]
    public float totalDuration = 0.8f;

    [Range(0f, 0.3f)]
    public float fadeInDuration = 0.1f;

    [Range(0f, 0.3f)]
    public float fadeOutDuration = 0.15f;

    [Range(100f, 1000f)]
    public float rotationSpeed = 360f;

    [Range(1f, 10f)]
    public float emissionIntensity = 5f;

    [Range(0.5f, 5f)]
    public float emissionPulseSpeed = 2f;

    [Header("크기 애니메이션")]
    [Range(0.5f, 2f)]
    public float startScale = 0.8f;

    [Range(0.5f, 2f)]
    public float endScale = 1.2f;

    [Header("판정별 색상")]
    public Color excellentColor = new Color(0.5f, 1f, 0.3f);
    public Color greatColor = new Color(0.3f, 0.7f, 1f);
    public Color goodColor = new Color(1f, 0.9f, 0.2f);
    public Color bonusColor = new Color(1f, 0.4f, 0.8f);

    // ⭐ enum은 클래스 레벨에서 선언!
    public enum CurveDirection
    {
        Left,
        Right
    }

    public enum JudgementType
    {
        Excellent,
        Great,
        Good,
        Bonus
    }

    private RectTransform effectRect;
    private Vector3 originalScale;

    void Awake()
    {
        if (effectImage != null)
        {
            effectRect = effectImage.GetComponent<RectTransform>();
            originalScale = effectRect.localScale;
            effectImage.gameObject.SetActive(false);
            effectImage.color = Color.white;
        }
    }

    public void PlayEffect(JudgementType type)
    {
        Color selectedColor = Color.white;

        switch (type)
        {
            case JudgementType.Excellent:
                selectedColor = excellentColor;
                break;
            case JudgementType.Great:
                selectedColor = greatColor;
                break;
            case JudgementType.Good:
                selectedColor = goodColor;
                break;
            case JudgementType.Bonus:
                selectedColor = bonusColor;
                break;
        }

        if (effectImage != null && startPointObject != null && endPointObject != null)
        {
            StartCoroutine(BurstEffect(selectedColor));
        }
        else
        {
            Debug.LogWarning("JudgementParticleEffect: 필수 오브젝트가 설정되지 않았습니다!");
        }
    }

    IEnumerator BurstEffect(Color baseColor)
    {
        for (int i = 0; i < burstCount; i++)
        {
            StartCoroutine(SingleEffectAnimation(baseColor));

            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }
    }

    IEnumerator SingleEffectAnimation(Color baseColor)
    {
        GameObject instanceObj = Instantiate(effectImage.gameObject, effectImage.transform.parent);
        Image instance = instanceObj.GetComponent<Image>();
        RectTransform instanceRect = instance.GetComponent<RectTransform>();

        instance.gameObject.SetActive(true);
        instanceRect.position = startPointObject.position;
        instanceRect.localScale = originalScale * startScale;

        Color startColor = baseColor;
        startColor.a = 0f;
        instance.color = startColor;

        float elapsed = 0f;
        float rotation = 0f;

        Vector3 startPos = startPointObject.position;
        Vector3 endPos = endPointObject.position;

        Vector3 midPoint = (startPos + endPos) / 2f;
        Vector3 direction = (endPos - startPos).normalized;

        Vector3 perpendicular;
        if (curveDirection == CurveDirection.Left)
        {
            perpendicular = new Vector3(-direction.y, direction.x, 0);
        }
        else
        {
            perpendicular = new Vector3(direction.y, -direction.x, 0);
        }

        Vector3 controlPoint = midPoint + perpendicular * curvature;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / totalDuration;

            Vector3 pos = CalculateBezierPoint(t, startPos, controlPoint, endPos);
            instanceRect.position = pos;

            rotation += rotationSpeed * Time.deltaTime;
            instanceRect.localEulerAngles = new Vector3(0, 0, rotation);

            float scale = Mathf.Lerp(startScale, endScale, t);
            instanceRect.localScale = originalScale * scale;

            float pulse = Mathf.Sin(elapsed * emissionPulseSpeed * Mathf.PI * 2) * 0.5f + 0.5f;
            float currentEmission = Mathf.Lerp(emissionIntensity * 0.5f, emissionIntensity, pulse);

            float alpha = 1f;

            if (elapsed < fadeInDuration)
            {
                alpha = elapsed / fadeInDuration;
            }
            else if (elapsed > totalDuration - fadeOutDuration)
            {
                float fadeT = (totalDuration - elapsed) / fadeOutDuration;
                alpha = fadeT;
            }

            Color color = baseColor * currentEmission;
            color.a = alpha;
            instance.color = color;

            yield return null;
        }

        Destroy(instanceObj);
    }

    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        Vector3 point = uu * p0;
        point += 2 * u * t * p1;
        point += tt * p2;

        return point;
    }
}