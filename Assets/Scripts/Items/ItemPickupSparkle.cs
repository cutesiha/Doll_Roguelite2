using UnityEngine;

// 근처에 갔을 때 반짝반짝 뜨는 파티클 효과(문 근접 하이라이트 등에서 사용).
// ParticleSystem 대신 몇 개의 작은 별 스프라이트를 절차적으로 만들어 트윙클시킨다.
[DisallowMultipleComponent]
public class ItemPickupSparkle : MonoBehaviour
{
    const int SparkleCount = 5;

    SpriteRenderer[] renderers;
    Vector2[] basePositions;
    float[] phases;
    float[] speeds;
    int sortingOrder = 60;

    static Sprite sparkleSprite;

    public void Configure(int order)
    {
        sortingOrder = order;
        EnsureSparkles();
    }

    void Awake()
    {
        EnsureSparkles();
    }

    void EnsureSparkles()
    {
        if (renderers != null)
            return;

        renderers = new SpriteRenderer[SparkleCount];
        basePositions = new Vector2[SparkleCount];
        phases = new float[SparkleCount];
        speeds = new float[SparkleCount];

        for (int i = 0; i < SparkleCount; i++)
        {
            GameObject go = new GameObject("Sparkle_" + i);
            go.transform.SetParent(transform, false);

            float angle = (i / (float)SparkleCount) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            float radius = Random.Range(0.35f, 0.62f);
            basePositions[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + 0.15f);
            go.transform.localPosition = basePositions[i];

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SparkleSprite();
            sr.color = SparkleColor(i);
            sr.sortingOrder = sortingOrder;
            renderers[i] = sr;

            phases[i] = Random.Range(0f, Mathf.PI * 2f);
            speeds[i] = Random.Range(2.4f, 4.2f);
        }
    }

    static Color SparkleColor(int i)
    {
        return (i % 2 == 0)
            ? new Color(1f, 0.95f, 0.65f, 1f)
            : new Color(1f, 1f, 1f, 1f);
    }

    void OnEnable()
    {
        if (phases == null)
            return;
        for (int i = 0; i < phases.Length; i++)
            phases[i] = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (renderers == null)
            return;

        float t = Time.unscaledTime;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null)
                continue;

            float tw = (Mathf.Sin(t * speeds[i] + phases[i]) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.05f, 1f, tw * tw);
            float scale = Mathf.Lerp(0.06f, 0.16f, tw);

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
            sr.transform.localScale = new Vector3(scale, scale, 1f);

            Vector2 bob = new Vector2(0f, Mathf.Sin(t * speeds[i] * 0.6f + phases[i]) * 0.05f);
            sr.transform.localPosition = basePositions[i] + bob;
        }
    }

    static Sprite SparkleSprite()
    {
        if (sparkleSprite != null)
            return sparkleSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ItemSparkle_Runtime",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / maxR;
                float dy = (y - center.y) / maxR;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float axis = Mathf.Max(0f, 1f - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) * 6f);
                float ray = axis * Mathf.Max(0f, 1f - r);
                float core = Mathf.Max(0f, 1f - r * 3.2f);
                float a = Mathf.Clamp01(Mathf.Max(core, ray));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        sparkleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sparkleSprite.name = "ItemSparkle_Runtime";
        return sparkleSprite;
    }
}
