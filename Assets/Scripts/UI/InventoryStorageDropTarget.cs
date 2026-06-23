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

        // 신체 부위를 보관함으로 내리기.
        var partSource = eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>();
        if (partSource != null)
        {
            if (InventoryManager.Instance.TryUnequipToStorage(partSource.BodySlot, storageIndex))
                SoundManager.PlayClick();
            return;
        }

        // 보석 슬롯에서 보석을 보관함으로 되돌리기.
        var jewelSource = eventData.pointerDrag.GetComponent<InventoryJewelDragSource>();
        if (jewelSource != null)
        {
            if (InventoryManager.Instance.TryUnequipJewelToStorage(storageIndex))
                SoundManager.PlayClick();
        }
    }
}
