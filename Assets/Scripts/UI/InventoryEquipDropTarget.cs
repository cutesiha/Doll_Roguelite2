using UnityEngine;
using UnityEngine.EventSystems;

// task3: 인벤토리 보관함의 신체부위 아이템을 이 슬롯에 드래그하면 장착
public class InventoryEquipDropTarget : MonoBehaviour, IDropHandler
{
    [SerializeField] BodySlot acceptedSlot;

    public void SetAcceptedSlot(BodySlot slot)
    {
        acceptedSlot = slot;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        InventoryStorageDragSource source = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (source == null)
            return;

        ItemData draggedItem = source.DraggedItemData;

        // ItemInventoryManager 보관함의 신체부위 ItemData 드래그
        if (draggedItem != null && draggedItem.Type == ItemType.BodyPart)
        {
            if (!ItemInventoryManager.IsBodyPartCompatibleWithSlot(draggedItem.EquipLocation, acceptedSlot))
                return;

            var itemInv = ItemInventoryManager.Instance;
            if (itemInv != null && itemInv.TryEquipBodyPartFromStorage(draggedItem, acceptedSlot))
                SoundManager.PlayClick();
            return;
        }

        // 기존: InventoryManager 보관함의 BodyPart 드래그
        if (InventoryManager.Instance == null)
            return;

        int storageIndex = source.StorageIndex;
        if (storageIndex < 0 || storageIndex >= InventoryManager.Instance.storage.Length)
            return;

        BodyPart part = InventoryManager.Instance.storage[storageIndex];
        if (part == null || !part.IsEquippable || part.slot != acceptedSlot)
            return;

        // task13: 슬롯에 아이템 부위가 장착돼 있으면 먼저 아이템 보관함으로 내린 뒤 원래 부위를 끼운다.
        // 자리가 없으면 교체를 중단한다(Q2: 교체 차단).
        var bodyItemInv = ItemInventoryManager.Instance;
        if (bodyItemInv != null && bodyItemInv.GetEquippedByBodySlot(acceptedSlot) != null)
        {
            if (!bodyItemInv.TryUnequipBodyPartToStorage(acceptedSlot))
                return;
        }

        if (InventoryManager.Instance.EquipFromStorage(storageIndex))
            SoundManager.PlayClick();
    }
}
