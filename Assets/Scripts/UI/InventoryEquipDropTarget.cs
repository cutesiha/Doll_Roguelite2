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
            if (itemInv != null && itemInv.TryEquipBodyPartFromStorage(source.ItemStorageIndex, acceptedSlot))
                SoundManager.PlayClick();
            return;
        }

        // 누더기 등 Shield 타입 아이템을 이미 장착된 부위 슬롯에 드래그하면 그 부위 전용 방어막을 건다.
        if (draggedItem != null && draggedItem.Type == ItemType.Shield)
        {
            var itemInv = ItemInventoryManager.Instance;
            if (itemInv != null && itemInv.TryShieldBodySlot(source.ItemStorageIndex, acceptedSlot))
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

        if (InventoryManager.Instance.EquipFromStorage(storageIndex))
            SoundManager.PlayClick();
    }
}
