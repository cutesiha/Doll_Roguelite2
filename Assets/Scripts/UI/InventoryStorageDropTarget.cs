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
            {
                // ItemInventoryManager(신규 아이템 시스템) 보관함 안에서 이동/교환.
                // 대상 칸이 레거시 BodyPart로 차 있으면 서로 다른 시스템이라 건드리지 않는다.
                if (InventoryManager.Instance.storage[storageIndex] != null)
                    return;

                var itemInv = ItemInventoryManager.Instance;
                if (itemInv == null)
                    return;

                InventoryStorageDragSource targetSource = GetComponent<InventoryStorageDragSource>();
                int toIndex = targetSource != null ? targetSource.ItemStorageIndex : -1;

                if (itemInv.TryMoveOrSwapStorage(storageSource.ItemStorageIndex, toIndex))
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
