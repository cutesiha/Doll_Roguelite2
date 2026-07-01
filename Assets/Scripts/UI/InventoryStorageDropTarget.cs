using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryStorageDropTarget : MonoBehaviour, IDropHandler
{
    [SerializeField] int storageIndex;

    public void SetStorageIndex(int index)
    {
        storageIndex = index;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (InventoryManager.Instance == null || eventData.pointerDrag == null)
            return;

        // Q 보석 슬롯 → 보관함으로 되돌리기.
        var consumableSource = eventData.pointerDrag.GetComponent<ItemConsumableDragSource>();
        if (consumableSource != null)
        {
            var itemInv = ItemInventoryManager.Instance;
            var inv = InventoryManager.Instance;
            if (itemInv == null || inv == null || itemInv.Consumable == null)
                return;

            if (inv.storage[storageIndex] != null)
                return;

            ItemData gem = itemInv.Consumable;
            BodyPart gemPart = new BodyPart(ItemKind.Gem);
            gemPart.icon = gem.Sprite;
            gemPart.itemId = gem.ItemId;

            itemInv.ForceSetConsumable(null);

            if (!inv.TryAddPartToSlot(gemPart, storageIndex))
                inv.TryAddPart(gemPart, false);

            SoundManager.PlayClick();
            return;
        }

        // 보관함 → 보관함 이동(다른 StorageSlot 으로 옮기기 / 교환).
        var storageSource = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (storageSource != null)
        {
            if (storageSource.DraggedItemData != null)
                return;

            if (InventoryManager.Instance.MoveStorage(storageSource.StorageIndex, storageIndex))
                SoundManager.PlayClick();
            return;
        }

        // 장착된 부위 → 보관함 내리기.
        var equippedSource = eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>();
        if (equippedSource != null)
        {
            BodySlot slot = equippedSource.BodySlot;

            // task2/12: 슬롯에 아이템 부위가 장착돼 있으면 그 아이템을 아이템 보관함으로 내린다.
            var itemInv = ItemInventoryManager.Instance;
            if (itemInv != null && itemInv.GetEquippedByBodySlot(slot) != null)
            {
                if (itemInv.TryUnequipBodyPartToStorage(slot))
                    SoundManager.PlayClick();
                return;
            }

            // 원래 부위 내리기.
            if (InventoryManager.Instance.TryUnequipToStorage(slot, storageIndex))
                SoundManager.PlayClick();
        }
    }
}
