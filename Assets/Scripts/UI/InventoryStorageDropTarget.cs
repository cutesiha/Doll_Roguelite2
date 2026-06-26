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

        // 보관함 → 보관함 이동(다른 StorageSlot 으로 옮기기 / 교환).
        var storageSource = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (storageSource != null)
        {
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
