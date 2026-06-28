using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemConsumableDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    RectTransform ghost;
    Canvas rootCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = ItemInventoryManager.Instance;
        if (inv == null || inv.Consumable == null)
            return;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return;

        SoundManager.PlayClick();

        Image sourceImage = GetComponent<Image>();

        GameObject go = new GameObject("ItemDragGhost");
        go.transform.SetParent(rootCanvas.transform, false);
        ghost = go.AddComponent<RectTransform>();
        ghost.sizeDelta = SourceSize();

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, 0.94f);
        image.sprite = sourceImage != null ? sourceImage.sprite : null;

        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        MoveGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ghost != null)
            Destroy(ghost.gameObject);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
            return;

        var storageSource = eventData.pointerDrag.GetComponent<InventoryStorageDragSource>();
        if (storageSource == null)
            return;

        // ItemInventoryManager에 저장된 보석 (ItemData) 처리.
        ItemData draggedItem = storageSource.DraggedItemData;
        if (draggedItem != null && draggedItem.Type == ItemType.GemConsumable)
        {
            var itemInv = ItemInventoryManager.Instance;
            if (itemInv != null && itemInv.TryEquipConsumableFromStorage(draggedItem))
                SoundManager.PlayClick();
            return;
        }

        // InventoryManager에 저장된 보석 (BodyPart with ItemKind.Gem) 처리.
        var inv = InventoryManager.Instance;
        if (inv == null)
            return;

        int idx = storageSource.StorageIndex;
        if (idx < 0 || idx >= inv.storage.Length)
            return;

        BodyPart part = inv.storage[idx];
        if (part == null || part.kind != ItemKind.Gem)
            return;

        ItemData gemItem = ResolveGemItem(part);
        if (gemItem == null)
            return;

        inv.RemoveStorageAt(idx);

        var itemInventory = ItemInventoryManager.Instance;
        if (itemInventory == null)
            return;

        // 이미 Q 슬롯에 보석이 있으면 보관함으로 되돌린다.
        if (itemInventory.Consumable != null)
        {
            BodyPart oldGem = new BodyPart(ItemKind.Gem);
            oldGem.icon = itemInventory.Consumable.Sprite;
            oldGem.itemId = itemInventory.Consumable.ItemId;
            inv.TryAddPart(oldGem, false);
        }

        itemInventory.ForceSetConsumable(gemItem);
        SoundManager.PlayClick();
    }

    static ItemData ResolveGemItem(BodyPart part)
    {
        if (!string.IsNullOrEmpty(part.itemId))
        {
            ItemData found = ItemCatalog.Find(part.itemId);
            if (found != null)
                return found;
        }

        if (part.icon != null)
        {
            var all = ItemCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                ItemData item = all[i];
                if (item != null && item.Type == ItemType.GemConsumable && item.Sprite == part.icon)
                    return item;
            }
        }

        return null;
    }

    void MoveGhost(PointerEventData eventData)
    {
        if (ghost == null || rootCanvas == null)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            ghost.anchoredPosition = localPoint;
    }

    Vector2 SourceSize()
    {
        RectTransform sourceRect = transform as RectTransform;
        if (sourceRect == null)
            return new Vector2(148f, 92f);

        Vector2 size = sourceRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = sourceRect.sizeDelta;
        return new Vector2(Mathf.Max(60f, size.x), Mathf.Max(60f, size.y));
    }
}
