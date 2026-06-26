using UnityEngine;

// World jewel pickup. When the player touches this object, the jewel is added to the first
// free InventoryManager storage slot (StorageSlot_1 ascending) carrying its own sprite, and
// the world object is removed. Works whether the collider is a trigger or solid.
[RequireComponent(typeof(Collider2D))]
public class JewelWorldPickup : MonoBehaviour
{
    [Tooltip("비워두면 이 오브젝트의 SpriteRenderer 스프라이트를 인벤토리 아이콘으로 사용한다.")]
    [SerializeField] Sprite iconOverride;

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

        // 가장 낮은 빈 보관함 칸(StorageSlot_1 부터)에 들어간다. 가득 차 있으면 줍지 않는다.
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
}
