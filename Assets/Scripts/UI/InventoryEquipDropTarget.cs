using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryEquipDropTarget : MonoBehaviour, IDropHandler
{
    [SerializeField] BodySlot acceptedSlot;

    public void SetAcceptedSlot(BodySlot slot)
    {
        acceptedSlot = slot;
    }

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
        if (part == null || part.slot != acceptedSlot)
            return;

        if (InventoryManager.Instance.EquipFromStorage(storageIndex))
            SoundManager.PlayClick();
    }
}
