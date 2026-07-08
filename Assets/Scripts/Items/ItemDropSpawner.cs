using System.Collections.Generic;
using UnityEngine;

public static class ItemDropSpawner
{
    static readonly Dictionary<int, Sprite> placeholderSprites = new();

    public static ItemWorldPickup Spawn(ItemData item, Vector3 position, bool shopItem, int price, ItemWorldPickup template = null, bool storeWithoutEquip = false)
    {
        if (item == null)
            return null;

        GameObject go = template != null
            ? Object.Instantiate(template.gameObject, position, Quaternion.identity)
            : new GameObject((shopItem ? "ShopItem_" : "ItemDrop_") + item.ItemId);
        go.name = (shopItem ? "ShopItem_" : "ItemDrop_") + item.ItemId;
        go.transform.position = position;
        go.SetActive(true);

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = item.Sprite != null ? item.Sprite : PlaceholderSprite(item.PlaceholderShape, item.PlaceholderColor);
        renderer.color = item.Sprite != null ? Color.white : item.PlaceholderColor;
        renderer.sortingOrder = ItemWorldPickup.ItemSortingOrder;

        // 드랍된 아이템이 바닥에서 너무 작아 보인다는 피드백에 따라 스폰 크기를 살짝 키운다.
        const float DropSizeMultiplier = 1.18f;
        float size = item.ResolveWorldScale() * DropSizeMultiplier;
        go.transform.localScale = Vector3.one * size;

        CircleCollider2D collider = go.GetComponent<CircleCollider2D>();
        if (collider == null)
            collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.62f;

        ItemWorldPickup pickup = go.GetComponent<ItemWorldPickup>();
        if (pickup == null)
            pickup = go.AddComponent<ItemWorldPickup>();
        pickup.CopyPresentationFrom(template);
        pickup.Configure(item, shopItem, price, storeWithoutEquip);
        return pickup;
    }

    static Sprite PlaceholderSprite(ItemPlaceholderShape shape, Color color)
    {
        int colorKey = ColorUtility.ToHtmlStringRGBA(color).GetHashCode();
        int key = ((int)shape * 397) ^ colorKey;
        if (placeholderSprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ItemPlaceholder_" + shape;
        texture.filterMode = FilterMode.Point;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center.x) / (size * 0.5f);
                float ny = (y - center.y) / (size * 0.5f);
                bool inside = shape switch
                {
                    ItemPlaceholderShape.Circle => nx * nx + ny * ny <= 0.82f,
                    ItemPlaceholderShape.Diamond => Mathf.Abs(nx) + Mathf.Abs(ny) <= 0.92f,
                    ItemPlaceholderShape.Triangle => ny >= -0.78f && ny <= 0.85f && Mathf.Abs(nx) <= (0.88f - ny) * 0.58f,
                    _ => Mathf.Abs(nx) <= 0.82f && Mathf.Abs(ny) <= 0.82f
                };

                bool border = inside && IsBorder(shape, nx, ny);
                texture.SetPixel(x, y, inside ? (border ? Color.white : color) : clear);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = texture.name;
        placeholderSprites[key] = sprite;
        return sprite;
    }

    static bool IsBorder(ItemPlaceholderShape shape, float x, float y)
    {
        const float border = 0.12f;
        return shape switch
        {
            ItemPlaceholderShape.Circle => x * x + y * y >= 0.82f - border,
            ItemPlaceholderShape.Diamond => Mathf.Abs(x) + Mathf.Abs(y) >= 0.92f - border,
            ItemPlaceholderShape.Triangle => y <= -0.78f + border
                || Mathf.Abs(Mathf.Abs(x) - (0.88f - y) * 0.58f) <= border,
            _ => Mathf.Abs(x) >= 0.82f - border || Mathf.Abs(y) >= 0.82f - border
        };
    }
}
