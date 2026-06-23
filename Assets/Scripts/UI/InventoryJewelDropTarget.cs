using UnityEngine;
using UnityEngine.EventSystems;

// Drop target on the dedicated jewel slot. Accepts only Gem items dragged from storage.
public class InventoryJewelDropTarget : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var source = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<InventoryStorageDragSource>()
            : null;
        if (source == null || InventoryManager.Instance == null)
            return;

        int storageIndex = source.StorageIndex;
        if (storageIndex < 0 || storageIndex >= InventoryManager.Instance.storage.Length)
            return;

        BodyPart part = InventoryManager.Instance.storage[storageIndex];
        if (part == null || !part.IsJewel)
            return;

        if (InventoryManager.Instance.EquipJewelFromStorage(storageIndex))
            SoundManager.PlayClick();
    }
}
