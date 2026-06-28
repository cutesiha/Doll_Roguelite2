using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryStorageDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] int storageIndex;

    RectTransform ghost;
    Canvas rootCanvas;
    ItemData itemData;

    public int StorageIndex => storageIndex;
    public ItemData DraggedItemData => itemData;

    public void SetStorageIndex(int index)
    {
        storageIndex = index;
    }

    public void SetItemData(ItemData item)
    {
        itemData = item;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = InventoryManager.Instance;
        bool hasBodyPart = inv != null && storageIndex >= 0 && storageIndex < inv.storage.Length && inv.storage[storageIndex] != null;

        if (!hasBodyPart && itemData == null)
            return;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return;

        SoundManager.PlayClick();

        Image sourceImage = GetComponent<Image>();

        GameObject go = new GameObject("InventoryDragGhost");
        go.transform.SetParent(rootCanvas.transform, false);
        ghost = go.AddComponent<RectTransform>();
        ghost.sizeDelta = SourceSize();

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, 0.94f);

        if (hasBodyPart)
        {
            image.sprite = sourceImage != null && sourceImage.sprite != null
                ? sourceImage.sprite
                : InventoryUI.FindDisplaySpriteForSlot(inv.storage[storageIndex].slot);
        }
        else
        {
            image.sprite = itemData != null ? itemData.Sprite : null;
        }

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
            return new Vector2(170f, 170f);

        Vector2 size = sourceRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = sourceRect.sizeDelta;
        return new Vector2(Mathf.Max(60f, size.x), Mathf.Max(60f, size.y));
    }
}
