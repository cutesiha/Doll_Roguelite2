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
        var source = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>()
            : null;
        if (source == null || InventoryManager.Instance == null)
            return;

        if (InventoryManager.Instance.TryUnequipToStorage(source.BodySlot, storageIndex))
            SoundManager.PlayClick();
    }
}
