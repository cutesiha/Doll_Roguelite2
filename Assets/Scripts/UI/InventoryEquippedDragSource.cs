using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryEquippedDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] BodySlot bodySlot;

    RectTransform ghost;
    Canvas rootCanvas;

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

        GameObject go = new GameObject("EquippedPartDragGhost");
        go.transform.SetParent(rootCanvas.transform, false);
        ghost = go.AddComponent<RectTransform>();
        ghost.sizeDelta = new Vector2(170f, 70f);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.20f, 0.18f, 0.92f);

        CanvasGroup group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = inv.equipped[index].SlotName();
        label.fontSize = 18f;
        label.font = UIThinDungFont.Get();
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

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
}
