using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryStorageDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] int storageIndex;

    RectTransform ghost;
    Canvas rootCanvas;
    ItemData itemData;
    int coinStackIndex = -1;

    public int StorageIndex => storageIndex;
    public ItemData DraggedItemData => itemData;
    public int CoinStackIndex => coinStackIndex;
    public bool IsCoinStack => coinStackIndex >= 0;

    public void SetStorageIndex(int index)
    {
        storageIndex = index;
    }

    public void SetItemData(ItemData item)
    {
        itemData = item;
    }

    public void SetCoinStackIndex(int index)
    {
        coinStackIndex = index;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = InventoryManager.Instance;
        bool hasBodyPart = inv != null && storageIndex >= 0 && storageIndex < inv.storage.Length && inv.storage[storageIndex] != null;

        if (!hasBodyPart && itemData == null && !IsCoinStack)
            return;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return;

        SoundManager.PlayClick();

        Transform iconTr = transform.Find("ItemIcon");
        Image itemIconImage = iconTr != null ? iconTr.GetComponent<Image>() : null;

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
            BodyPart part = inv.storage[storageIndex];
            image.sprite = itemIconImage != null && itemIconImage.sprite != null
                ? itemIconImage.sprite
                : (part.icon != null ? part.icon : InventoryUI.FindDisplaySpriteForSlot(part.slot));
        }
        else if (IsCoinStack)
        {
            image.sprite = inv != null ? inv.coinIcon : null;
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
        // 슬롯에 표시 중인 ItemIcon 자식 크기와 동일하게 (처음 집을 때와 같은 크기로)
        Transform iconTr = transform.Find("ItemIcon");
        if (iconTr is RectTransform iconRt)
        {
            Vector2 iconSize = iconRt.rect.size;
            if (iconSize.x <= 0f || iconSize.y <= 0f)
                iconSize = iconRt.sizeDelta;
            if (iconSize.x > 1f && iconSize.y > 1f)
                return iconSize;
        }

        RectTransform sourceRect = transform as RectTransform;
        if (sourceRect == null)
            return new Vector2(80f, 80f);

        Vector2 size = sourceRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = sourceRect.sizeDelta;
        return new Vector2(Mathf.Max(60f, size.x), Mathf.Max(60f, size.y));
    }
}
