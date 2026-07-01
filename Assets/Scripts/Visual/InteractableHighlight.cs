using System.Collections.Generic;
using UnityEngine;

// 상호작용 가능한 오브젝트 뒤쪽에 부드러운 백라이트(글로우)와 반짝이는 스파클을 표시한다.
// 플레이어가 activateDistance 안으로 들어오면 서서히 밝아지고, 멀어지면 사라진다.
// SpriteRenderer 기반이라 URP 2D에서 별도 머티리얼 설정 없이 안전하게 동작한다.
public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] Transform player;
    [Tooltip("이 거리 안으로 플레이어가 들어오면 반짝임이 켜진다.")]
    [SerializeField] float activateDistance = 2.0f;
    [Tooltip("반짝임/글로우 색 (살구색 계열)")]
    [SerializeField] Color glowColor = new Color(1f, 0.78f, 0.62f, 0.85f);
    [Tooltip("오브젝트 크기 대비 백라이트 글로우의 배율")]
    [SerializeField] float glowSizeMultiplier = 1.6f;
    [SerializeField, Min(0)] int sparkCount = 6;

    SpriteRenderer targetRenderer;
    SpriteRenderer glowRenderer;
    readonly List<SpriteRenderer> sparks = new();
    readonly List<Vector3> sparkBaseLocalPositions = new();
    readonly List<float> sparkPhases = new();
    readonly List<float> sparkSpeeds = new();
    float currentAlpha;
    float pulseTime;
    bool built;

    static Sprite softGlowSprite;

    public void Configure(Transform playerTransform, float distance)
    {
        player = playerTransform;
        if (distance > 0f)
            activateDistance = distance;
    }

    void Start()
    {
        Build();
        ResolvePlayer();
    }

    void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
            player = found.transform;
    }

    void Build()
    {
        if (built)
            return;
        built = true;

        targetRenderer = GetComponentInChildren<SpriteRenderer>();

        Vector3 center = targetRenderer != null ? targetRenderer.bounds.center : transform.position;
        float objectSize = targetRenderer != null
            ? Mathf.Max(targetRenderer.bounds.size.x, targetRenderer.bounds.size.y)
            : 1.6f;
        if (objectSize <= 0.01f)
            objectSize = 1.6f;

        int baseSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 10;
        int baseSortingLayer = targetRenderer != null ? targetRenderer.sortingLayerID : 0;

        BuildGlow(center, objectSize * glowSizeMultiplier, baseSortingOrder, baseSortingLayer);
        BuildSparks(center, objectSize, baseSortingOrder, baseSortingLayer);
    }

    void BuildGlow(Vector3 worldCenter, float worldDiameter, int baseSortingOrder, int baseSortingLayer)
    {
        GameObject go = new GameObject("InteractGlow");
        go.transform.SetParent(transform, false);
        go.transform.position = worldCenter;
        SetWorldDiameter(go.transform, worldDiameter);

        glowRenderer = go.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = SoftGlowSprite();
        glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        glowRenderer.sortingLayerID = baseSortingLayer;
        glowRenderer.sortingOrder = baseSortingOrder - 1;
        glowRenderer.enabled = false;
    }

    void BuildSparks(Vector3 worldCenter, float objectSize, int baseSortingOrder, int baseSortingLayer)
    {
        float radius = objectSize * 0.5f;
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject go = new GameObject("InteractSpark_" + i);
            go.transform.SetParent(transform, false);

            float angle = (i / Mathf.Max(1f, sparkCount)) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            float dist = radius * Random.Range(0.55f, 1.05f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist * 0.85f, 0f);
            go.transform.position = worldCenter + offset;
            SetWorldDiameter(go.transform, objectSize * Random.Range(0.10f, 0.18f));

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SoftGlowSprite();
            sr.color = new Color(1f, 0.82f, 0.66f, 0f);
            sr.sortingLayerID = baseSortingLayer;
            sr.sortingOrder = baseSortingOrder - 1;
            sr.enabled = false;

            sparks.Add(sr);
            sparkBaseLocalPositions.Add(go.transform.localPosition);
            sparkPhases.Add(Random.Range(0f, Mathf.PI * 2f));
            sparkSpeeds.Add(Random.Range(4.5f, 7.5f));
        }
    }

    void SetWorldDiameter(Transform t, float worldDiameter)
    {
        Vector3 parentScale = transform.lossyScale;
        t.localScale = new Vector3(
            worldDiameter / Mathf.Max(0.0001f, parentScale.x),
            worldDiameter / Mathf.Max(0.0001f, parentScale.y),
            1f);
    }

    void Update()
    {
        ResolvePlayer();
        pulseTime += Time.deltaTime;

        // 콜라이더 접촉이 아니라 오브젝트의 시각적 중심 기준 '거리'로 판정한다.
        Vector3 refCenter = targetRenderer != null ? targetRenderer.bounds.center : transform.position;
        float distance = player != null
            ? Vector2.Distance(player.position, refCenter)
            : float.PositiveInfinity;
        bool near = distance <= activateDistance;

        float targetAlpha = near ? 1f : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * 4.5f);
        bool visible = currentAlpha > 0.01f;

        if (glowRenderer != null)
        {
            float pulse = 0.70f + Mathf.Sin(pulseTime * 4.6f) * 0.30f;
            Color c = glowColor;
            c.a = glowColor.a * currentAlpha * pulse;
            glowRenderer.color = c;
            glowRenderer.enabled = visible;
        }

        for (int i = 0; i < sparks.Count; i++)
        {
            SpriteRenderer sr = sparks[i];
            if (sr == null)
                continue;

            sr.enabled = visible;
            if (!visible)
                continue;

            float twinkle = Mathf.Clamp01(Mathf.Sin(pulseTime * sparkSpeeds[i] + sparkPhases[i]) * 0.5f + 0.5f);
            twinkle = twinkle * twinkle;
            Color c = sr.color;
            c.a = currentAlpha * twinkle;
            sr.color = c;

            float bob = Mathf.Sin(pulseTime * (sparkSpeeds[i] * 0.5f) + sparkPhases[i]) * 0.06f;
            sr.transform.localPosition = sparkBaseLocalPositions[i] + new Vector3(0f, bob, 0f);
        }
    }

    static Sprite SoftGlowSprite()
    {
        if (softGlowSprite != null)
            return softGlowSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "InteractSoftGlow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(1f - d);
                alpha = alpha * alpha; // 가장자리로 갈수록 부드럽게 감쇠
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        softGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        softGlowSprite.name = texture.name;
        return softGlowSprite;
    }
}
