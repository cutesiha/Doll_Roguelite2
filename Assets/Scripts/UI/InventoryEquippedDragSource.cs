using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryEquippedDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] BodySlot bodySlot;

    RectTransform ghost;
    Canvas rootCanvas;
    Image sourceImage;
    Color sourceColor;

    public BodySlot BodySlot => bodySlot;

    public void SetBodySlot(BodySlot slot)
    {
        bodySlot = slot;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = InventoryManager.Instance;
        int index = (int)bodySlot;
        if (inv == null || index < 0 || index >= inv.equipped.Length || inv.equipped[index] == null)
            return;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return;

        SoundManager.PlayClick();

        sourceImage = GetComponent<Image>();
        if (sourceImage != null)
        {
            sourceColor = sourceImage.color;
            sourceImage.color = new Color(0.04f, 0.035f, 0.03f, 0.48f);
        }

        GameObject go = new GameObject("EquippedPartDragGhost");
        go.transform.SetParent(rootCanvas.transform, false);
        ghost = go.AddComponent<RectTransform>();
        ghost.sizeDelta = SourceSize();

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, 0.94f);
        image.sprite = sourceImage != null && sourceImage.sprite != null
            ? sourceImage.sprite
            : InventoryUI.FindDisplaySpriteForSlot(bodySlot);

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

        var inv = InventoryManager.Instance;
        int index = (int)bodySlot;
        if (sourceImage != null)
            sourceImage.color = inv != null && index >= 0 && index < inv.equipped.Length && inv.equipped[index] == null
                ? new Color(0.04f, 0.035f, 0.03f, 0.48f)
                : sourceColor;
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

    void MoveGhost(PointerEventData eventData)
    {
        if (ghost == null || rootCanvas == null)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            ghost.anchoredPosition = localPoint;
    }
}
