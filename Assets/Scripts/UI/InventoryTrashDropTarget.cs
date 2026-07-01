using UnityEngine;
using UnityEngine.EventSystems;

// 인벤토리 "버리는 칸". 보관함 또는 장착 부위 아이템을 여기에 드롭하면
// 인벤토리에서 제거하고 플레이어 근처 월드에 다시 떨어뜨린다.
public class InventoryTrashDropTarget : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        // Q 보석(소모품) 슬롯 → 버리기.
        var consumableSource = eventData.pointerDrag.GetComponent<ItemConsumableDragSource>();
        if (consumableSource != null)
        {
            var itemInv = ItemInventoryManager.Instance;
            if (itemInv != null && itemInv.Consumable != null)
            {
                ItemData removedItem = itemInv.RemoveConsumable();
                if (removedItem != null)
                {
                    DropItemToWorld(removedItem);
                    SoundManager.PlayClick();
                }
            }
            return;
        }

        var inv = InventoryManager.Instance;
        if (inv == null)
            return;

        BodyPart removed = null;

        // 보관함 슬롯 → 버리기.
        var storageSource = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (storageSource != null)
        {
            // 동전 스택 버리기 → 월드에 개별 동전으로 떨어뜨림
            if (storageSource.IsCoinStack)
            {
                int count = inv.RemoveCoinFromSlot(storageSource.CoinStackIndex);
                if (count > 0)
                {
                    DropCoinsToWorld(count, inv.coinIcon);
                    SoundManager.PlayClick();
                }
                return;
            }

            // 아이템(보석 등)이 표시된 슬롯이면 ItemInventoryManager에서 제거.
            ItemData draggedItem = storageSource.DraggedItemData;
            if (draggedItem != null && inv.storage[storageSource.StorageIndex] == null)
            {
                var itemInv = ItemInventoryManager.Instance;
                if (itemInv != null && itemInv.RemoveItemFromStorage(draggedItem))
                {
                    DropItemToWorld(draggedItem);
                    SoundManager.PlayClick();
                }
                return;
            }

            removed = inv.RemoveStorageAt(storageSource.StorageIndex);
        }

        // 장착 부위 → 버리기 (잠긴 슬롯은 RemoveEquipped 에서 막힘).
        if (removed == null)
        {
            var equippedSource = eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>();
            if (equippedSource != null)
                removed = inv.RemoveEquipped(equippedSource.BodySlot);
        }

        if (removed == null)
            return;

        DropToWorld(removed);
        SoundManager.PlayClick();
    }

    static void DropToWorld(BodyPart part)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        if (part.kind == ItemKind.Coin && part.count > 1)
        {
            DropCoinStackToWorld(part, origin);
            return;
        }

        Sprite sprite = part.icon != null
            ? part.icon
            : (part.IsEquippable ? InventoryUI.FindDisplaySpriteForSlot(part.slot) : null);

        BodyPartWorldDrop drop = BodyPartWorldDrop.Spawn(part, origin, sprite);
        if (drop != null)
            drop.Toss(origin);
    }

<<<<<<< Updated upstream
    // 동전 더미를 버리면 개수만큼 낱개 동전으로 나뉘어 사방으로 흩어진다.
    static void DropCoinStackToWorld(BodyPart stack, Vector3 origin)
    {
        int count = stack.count;
        float angleStep = 360f / count;
        float angleJitter = angleStep * 0.35f;

        for (int i = 0; i < count; i++)
        {
            BodyPart single = new BodyPart(ItemKind.Coin) { icon = stack.icon, itemId = stack.itemId, count = 1 };

            float angle = angleStep * i + Random.Range(-angleJitter, angleJitter);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float distance = Random.Range(1.6f, 2.8f);

            BodyPartWorldDrop drop = BodyPartWorldDrop.Spawn(single, origin, single.icon);
            if (drop != null)
                drop.Toss(origin, dir, distance);
=======
    static void DropCoinsToWorld(int count, Sprite coinSprite)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        GameObject coinPrefab = Resources.Load<GameObject>("Drops/동전");
        for (int i = 0; i < count; i++)
        {
            if (coinPrefab != null)
            {
                GameObject go = UnityEngine.Object.Instantiate(coinPrefab, origin, Quaternion.identity);
                go.GetComponent<CoinWorldPickup>()?.Toss(origin);
            }
>>>>>>> Stashed changes
        }
    }

    static void DropItemToWorld(ItemData item)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;

        ItemWorldPickup pickup = ItemDropSpawner.Spawn(item, origin, false, 0);
        if (pickup != null)
            pickup.Toss(origin);
    }
}
