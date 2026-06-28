using UnityEngine;
using UnityEngine.EventSystems;

public class QGemDropTarget : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        var storageSource = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (storageSource == null)
            return;

        // ItemData 보석.
        ItemData draggedItem = storageSource.DraggedItemData;
        if (draggedItem != null && draggedItem.Type == ItemType.GemConsumable)
        {
            var itemInv = ItemInventoryManager.Instance;
            if (itemInv != null && itemInv.TryEquipConsumableFromStorage(draggedItem))
                SoundManager.PlayClick();
            return;
        }

        // BodyPart(ItemKind.Gem) 보석.
        var inv = InventoryManager.Instance;
        if (inv == null)
            return;

        int idx = storageSource.StorageIndex;
        if (idx < 0 || idx >= inv.storage.Length)
            return;

        BodyPart part = inv.storage[idx];
        if (part == null || part.kind != ItemKind.Gem)
            return;

        ItemData gemItem = ResolveGemItem(part);
        if (gemItem == null)
            return;

        inv.RemoveStorageAt(idx);

        var itemInventory = ItemInventoryManager.Instance;
        if (itemInventory == null)
            return;

        if (itemInventory.Consumable != null)
        {
            BodyPart oldGem = new BodyPart(ItemKind.Gem);
            oldGem.icon = itemInventory.Consumable.Sprite;
            oldGem.itemId = itemInventory.Consumable.ItemId;
            inv.TryAddPart(oldGem, false);
        }

        itemInventory.ForceSetConsumable(gemItem);
        SoundManager.PlayClick();
    }

    static ItemData ResolveGemItem(BodyPart part)
    {
        if (!string.IsNullOrEmpty(part.itemId))
        {
            ItemData found = ItemCatalog.Find(part.itemId);
            if (found != null)
                return found;
        }

        if (part.icon != null)
        {
            var all = ItemCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                ItemData item = all[i];
                if (item != null && item.Type == ItemType.GemConsumable && item.Sprite == part.icon)
                    return item;
            }
        }

        return null;
    }
}
