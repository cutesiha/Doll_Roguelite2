using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class JewelWorldPickup : MonoBehaviour
{
    [Tooltip("비워두면 이 오브젝트의 SpriteRenderer 스프라이트를 인벤토리 아이콘으로 사용한다.")]
    [SerializeField] Sprite iconOverride;

    [Tooltip("이 보석에 대응하는 ItemData ID (예: black_gem, white_gem). 비워두면 스프라이트로 매칭 시도.")]
    [SerializeField] string gemItemId;

    bool collected;

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.collider);
    }

    void TryCollect(Collider2D other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        BodyPart gem = new BodyPart(ItemKind.Gem);
        gem.icon = ResolveIcon();
        gem.itemId = ResolveItemId();

        if (!inventory.TryAddPart(gem, false))
            return;

        collected = true;
        SoundManager.PlayClick();
        Destroy(gameObject);
    }

    Sprite ResolveIcon()
    {
        if (iconOverride != null)
            return iconOverride;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }

    string ResolveItemId()
    {
        if (!string.IsNullOrEmpty(gemItemId))
            return gemItemId;

        Sprite icon = ResolveIcon();
        if (icon == null)
            return "";

        var all = ItemCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            ItemData item = all[i];
            if (item != null && item.Type == ItemType.GemConsumable && item.Sprite == icon)
                return item.ItemId;
        }
        return "";
    }
}
