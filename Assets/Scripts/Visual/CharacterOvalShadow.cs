using UnityEngine;

[ExecuteAlways]
public class CharacterOvalShadow : MonoBehaviour
{
    const string ShadowName = "Oval Shadow";
    const string ShadowResourcePath = "Sprites/oval_shadow";

    [SerializeField] Vector2 offset = new Vector2(0.08f, -0.42f);
    [SerializeField] Vector2 shadowSize = new Vector2(0.82f, 0.24f);
    [SerializeField] Color shadowColor = new Color(0.08f, 0.045f, 0.03f, 0.48f);
    [SerializeField] int sortingOrderOffset = -1;

    static Sprite shadowSprite;
    SpriteRenderer ownerRenderer;
    SpriteRenderer shadowRenderer;

    void Awake()
    {
        Configure();
    }

    void OnEnable()
    {
        Configure();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Configure();
    }
#endif

    void Configure()
    {
        ownerRenderer = GetComponent<SpriteRenderer>();
        shadowRenderer = EnsureShadowRenderer();
        if (shadowRenderer == null)
            return;

        shadowRenderer.sprite = GetShadowSprite();
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingLayerID = ownerRenderer != null ? ownerRenderer.sortingLayerID : 0;
        shadowRenderer.sortingOrder = ownerRenderer != null ? ownerRenderer.sortingOrder + sortingOrderOffset : sortingOrderOffset;

        Transform shadowTransform = shadowRenderer.transform;
        shadowTransform.localPosition = new Vector3(offset.x, offset.y, 0.02f);
        shadowTransform.localRotation = Quaternion.identity;
        shadowTransform.localScale = new Vector3(Mathf.Max(0.01f, shadowSize.x), Mathf.Max(0.01f, shadowSize.y), 1f);
    }

    SpriteRenderer EnsureShadowRenderer()
    {
        Transform existing = transform.Find(ShadowName);
        if (existing == null)
        {
            GameObject shadowObject = new GameObject(ShadowName);
            shadowObject.transform.SetParent(transform, false);
            existing = shadowObject.transform;
        }

        SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = existing.gameObject.AddComponent<SpriteRenderer>();

        return renderer;
    }

    static Sprite GetShadowSprite()
    {
        if (shadowSprite != null)
            return shadowSprite;

        shadowSprite = Resources.Load<Sprite>(ShadowResourcePath);
        if (shadowSprite != null)
            return shadowSprite;

        const int width = 128;
        const int height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Generated Oval Shadow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f) / width * 2f - 1f;
                float ny = (y + 0.5f) / height * 2f - 1f;
                float distance = nx * nx + ny * ny;
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.35f, 1f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        shadowSprite.name = "Oval Shadow Sprite";
        return shadowSprite;
    }
}
