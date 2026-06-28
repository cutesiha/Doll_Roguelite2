using UnityEngine;
using UnityEngine.EventSystems;

// 인벤토리 "버리는 칸". 보관함 또는 장착 부위 아이템을 여기에 드롭하면
// 인벤토리에서 제거하고 플레이어 근처 월드에 다시 떨어뜨린다.
public class InventoryTrashDropTarget : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || eventData.pointerDrag == null)
            return;

        BodyPart removed = null;

        // 보관함 슬롯 → 버리기.
        var storageSource = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (storageSource != null)
            removed = inv.RemoveStorageAt(storageSource.StorageIndex);

        // 장착 부위 → 버리기 (잠긴 슬롯은 RemoveEquipped 에서 막힘).
        if (removed == null)
        {
            var equippedSource = eventData.pointerDrag.GetComponent<InventoryEquippedDragSource>();
            if (equippedSource != null)
                removed = inv.RemoveEquipped(equippedSource.BodySlot);
        }

        if (removed == null)
            return;

        DropToWorld(removed);
        SoundManager.PlayClick();
    }

    static void DropToWorld(BodyPart part)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;
        // 플레이어와 충분히 떨어진 위치에 떨어뜨려 즉시 재획득을 막는다.
        Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), -1.4f, 0f);

        Sprite sprite = part.icon != null
            ? part.icon
            : (part.IsEquippable ? InventoryUI.FindDisplaySpriteForSlot(part.slot) : null);

        BodyPartWorldDrop.Spawn(part, origin + offset, sprite);
    }
}
