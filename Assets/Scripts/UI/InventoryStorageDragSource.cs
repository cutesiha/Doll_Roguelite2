using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryStorageDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] int storageIndex;

    RectTransform ghost;
    Canvas rootCanvas;
    ItemData itemData;
    int itemStorageIndex = -1;
    int coinStackIndex = -1;
    InventoryUI inventoryUI;
    bool hidSlotContent;

    public int StorageIndex => storageIndex;
    public ItemData DraggedItemData => itemData;
    // ItemInventoryManager.Storage 안에서 이 슬롯이 가리키는 실제 리스트 인덱스.
    // 레거시 BodyPart 칸/동전 더미 칸이면 -1 (이 슬롯 기준 이동/교환 대상 아님).
    public int ItemStorageIndex => itemStorageIndex;
    // ItemInventoryManager.CoinStacks 안에서 이 슬롯이 가리키는 실제 리스트 인덱스. 없으면 -1.
    public int CoinStackIndex => coinStackIndex;

    public void SetStorageIndex(int index)
    {
        storageIndex = index;
    }

    public void SetItemData(ItemData item)
    {
        itemData = item;
    }

    public void SetItemStorageIndex(int index)
    {
        itemStorageIndex = index;
    }

    public void SetCoinStackIndex(int index)
    {
        coinStackIndex = index;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = InventoryManager.Instance;
        bool hasBodyPart = inv != null && storageIndex >= 0 && storageIndex < inv.storage.Length && inv.storage[storageIndex] != null;

        if (!hasBodyPart && itemData == null && coinStackIndex < 0)
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
        else if (itemData != null)
        {
            image.sprite = itemData.Sprite;
        }
        else
        {
            var itemInv = ItemInventoryManager.Instance;
            image.sprite = itemInv != null ? itemInv.CoinItemRef?.Sprite : null;
        }

        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // 몸에서 부위를 떼면 그 자리가 빈 것처럼, 보관 슬롯에서 아이템을 집는 동안에도
        // 원래 슬롯이 빈 것처럼 보이도록 슬롯에 표시된 내용을 숨긴다.
        HideSlotContent();

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

        // 드롭이 성공했든(교환/이동) 실패했든 슬롯 표시를 실제 상태로 되돌린다.
        if (hidSlotContent)
        {
            hidSlotContent = false;
            if (inventoryUI == null)
                inventoryUI = GetComponentInParent<InventoryUI>();
            if (inventoryUI == null)
                inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            inventoryUI?.RefreshUI();
        }
    }

    // 집는 동안 이 슬롯이 비어 보이도록 아이콘/동전더미/그리드/이름을 감춘다.
    // 실제 데이터는 건드리지 않고, OnEndDrag 의 RefreshUI 로 정확히 복원된다.
    void HideSlotContent()
    {
        hidSlotContent = true;

        Image slotBackground = GetComponent<Image>();
        if (slotBackground != null)
            slotBackground.color = new Color(0.17f, 0.15f, 0.13f, 0.20f);

        Transform iconTr = transform.Find("ItemIcon");
        Image iconImage = iconTr != null ? iconTr.GetComponent<Image>() : null;
        if (iconImage != null)
            iconImage.color = new Color(1f, 1f, 1f, 0f);

        Transform pile = transform.Find("CoinPile");
        if (pile != null)
            pile.gameObject.SetActive(false);

        Transform grid = transform.Find("CoinGrid");
        if (grid != null)
            grid.gameObject.SetActive(false);

        Transform nameTr = transform.Find("SlotName");
        if (nameTr != null)
        {
            TMPro.TextMeshProUGUI nameLabel = nameTr.GetComponent<TMPro.TextMeshProUGUI>();
            if (nameLabel != null)
                nameLabel.text = "";
        }

        Transform hpTr = transform.Find("SlotHP");
        if (hpTr != null)
        {
            TMPro.TextMeshProUGUI hpLabel = hpTr.GetComponent<TMPro.TextMeshProUGUI>();
            if (hpLabel != null)
                hpLabel.text = "";
        }
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
