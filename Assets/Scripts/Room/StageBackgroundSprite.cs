using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class StageBackgroundSprite : MonoBehaviour
{
    [SerializeField] string spriteResourcePath = "Sprites/stage1_room_floor";
    [SerializeField] Vector2 worldSize = new Vector2(38.4f, 21.6f);
    [SerializeField] int sortingOrder = -100;

    SpriteRenderer spriteRenderer;

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
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = LoadFirstSprite(spriteResourcePath);

        spriteRenderer.sortingOrder = sortingOrder;
        ApplyWorldSize();
    }

    void ApplyWorldSize()
    {
        if (spriteRenderer.sprite == null)
            return;

        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        transform.localScale = new Vector3(worldSize.x / spriteSize.x, worldSize.y / spriteSize.y, 1f);
    }

    Sprite LoadFirstSprite(string resourcePath)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites.Length > 0)
            return sprites[0];

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
    }
}
