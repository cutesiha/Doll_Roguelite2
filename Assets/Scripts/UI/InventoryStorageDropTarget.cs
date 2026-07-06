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
                HandleEntryDrop(ItemInventoryManager.StorageEntryKind.Item, storageSource.ItemStorageIndex, storageSource.StorageIndex);
                return;
            }

            if (storageSource.CoinStackIndex >= 0)
            {
                HandleEntryDrop(ItemInventoryManager.StorageEntryKind.CoinStack, storageSource.CoinStackIndex, storageSource.StorageIndex);
                return;
            }

            if (InventoryManager.Instance.MoveStorage(storageSource.StorageIndex, storageIndex))
                SoundManager.PlayClick();
            return;
        }

        // 장착된 부위 → 보관함 내리기 / 교체.
        var equippedSource = eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>();
        if (equippedSource != null)
        {
            var itemInv = ItemInventoryManager.Instance;

            // 이 보관슬롯에 이 부위와 호환되는 신규 아이템이 표시 중이면, 그냥 떼는 대신 "교체"한다.
            // (장착된 부위를 아이템 위로 끌어다 놓으면: 그 아이템이 장착되고 원래 부위는 보관함으로 이동.)
            // 예전에는 무조건 떼기만 해서, 교체하려던 아이템과 뗀 부위가 둘 다 보관함에 남는 버그가 있었다.
            InventoryStorageDragSource targetSlot = GetComponent<InventoryStorageDragSource>();
            ItemData targetItem = targetSlot != null ? targetSlot.DraggedItemData : null;
            int targetItemIndex = targetSlot != null ? targetSlot.ItemStorageIndex : -1;
            if (itemInv != null && targetItem != null && targetItemIndex >= 0
                && targetItem.Type == ItemType.BodyPart
                && ItemInventoryManager.IsBodyPartCompatibleWithSlot(targetItem.EquipLocation, equippedSource.BodySlot)
                && itemInv.TryEquipBodyPartFromStorage(targetItemIndex, equippedSource.BodySlot))
            {
                SoundManager.PlayClick();
                return;
            }

            if (InventoryManager.Instance.TryUnequipToStorage(equippedSource.BodySlot, storageIndex))
            {
                SoundManager.PlayClick();
                return;
            }

            // 신규 아이템 시스템(ItemInventoryManager)으로 장착된 신체부위 아이템 내리기.
            if (itemInv != null && itemInv.TryUnequipBodyPartToStorage(equippedSource.BodySlot))
                SoundManager.PlayClick();
        }
    }

    // 신규 아이템 시스템(ItemInventoryManager) 아이템/동전더미를 이 물리 슬롯에 고정시킨다.
    // 이 슬롯에 이미 다른 아이템/동전더미가 표시 중이면 서로 자리를 맞바꾼다.
    void HandleEntryDrop(ItemInventoryManager.StorageEntryKind kind, int index, int fromPhysicalSlot)
    {
        // 대상 칸이 레거시 BodyPart로 차 있으면 서로 다른 시스템이라 건드리지 않는다.
        if (InventoryManager.Instance.storage[storageIndex] != null)
            return;

        var itemInv = ItemInventoryManager.Instance;
        if (itemInv == null)
            return;

        InventoryStorageDragSource targetSource = GetComponent<InventoryStorageDragSource>();
        int occupantItemIndex = targetSource != null ? targetSource.ItemStorageIndex : -1;
        int occupantCoinIndex = targetSource != null ? targetSource.CoinStackIndex : -1;
        bool hasOccupant = occupantItemIndex >= 0 || occupantCoinIndex >= 0;
        ItemInventoryManager.StorageEntryKind occupantKind = occupantItemIndex >= 0
            ? ItemInventoryManager.StorageEntryKind.Item
            : ItemInventoryManager.StorageEntryKind.CoinStack;
        int occupantIndex = occupantItemIndex >= 0 ? occupantItemIndex : occupantCoinIndex;

        if (hasOccupant && occupantKind == kind && occupantIndex == index)
            return; // 자기 자신 위에 드롭

        if (itemInv.PinEntryToPhysicalSlot(kind, index, fromPhysicalSlot, storageIndex, occupantKind, occupantIndex, hasOccupant))
            SoundManager.PlayClick();
    }
}
