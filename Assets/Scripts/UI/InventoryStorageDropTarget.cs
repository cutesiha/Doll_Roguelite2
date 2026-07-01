using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryStorageDropTarget : MonoBehaviour, IDropHandler
{
    [SerializeField] int storageIndex;

    public void SetStorageIndex(int index) { storageIndex = index; }

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
            // 동전 슬롯 → 다른 슬롯으로 이동/교환
            if (storageSource.IsCoinStack)
            {
                var inv = InventoryManager.Instance;
                if (inv != null && inv.MoveCoinToSlot(storageSource.CoinStackIndex, storageIndex))
                    SoundManager.PlayClick();
                return;
            }

            if (storageSource.DraggedItemData != null)
            {
                // ItemData 아이템 슬롯 이동: 대상이 비어 있으면 그 자리로 옮기고,
                // 다른 아이템이 있으면 서로 자리를 맞바꾼다. (대상이 BodyPart 슬롯이면 미지원)
                if (InventoryManager.Instance.storage[storageIndex] != null)
                    return;

                var itemInv = ItemInventoryManager.Instance;
                if (itemInv == null)
                    return;

                InventoryStorageDragSource targetDragSource = GetComponent<InventoryStorageDragSource>();
                ItemData targetItem = targetDragSource != null ? targetDragSource.DraggedItemData : null;

                bool moved = targetItem != null
                    ? itemInv.SwapStorageItems(storageSource.DraggedItemData, targetItem)
                    : itemInv.MoveStorageItem(storageSource.DraggedItemData, storageIndex);

                if (moved)
                    SoundManager.PlayClick();
                return;
            }

            if (InventoryManager.Instance.MoveStorage(storageSource.StorageIndex, storageIndex))
                SoundManager.PlayClick();
            return;
        }

        // 장착된 부위 → 보관함 내리기.
        var equippedSource = eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>();
        if (equippedSource != null)
        {
            if (InventoryManager.Instance.TryUnequipToStorage(equippedSource.BodySlot, storageIndex))
                SoundManager.PlayClick();
        }
    }
}
