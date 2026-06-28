using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemConsumableDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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
