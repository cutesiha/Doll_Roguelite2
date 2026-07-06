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
        bool hasLegacyPart = inv != null && index >= 0 && index < inv.equipped.Length && inv.equipped[index] != null;
        bool hasItemPart = ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.GetEquippedByBodySlot(bodySlot) != null;
        if (!hasLegacyPart && !hasItemPart)
            return;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return;

        SoundManager.PlayClick();

        // Body 슬롯은 히트박스(이 오브젝트)가 아닌 자식 BodyVisual에 실제 그림이 있으므로
        // 어둡게 칠할 대상도 그쪽을 찾는다. 다른 부위는 BodyVisual이 없으니 자기 자신을 그대로 쓴다.
        Transform bodyVisual = transform.Find("BodyVisual");
        sourceImage = bodyVisual != null ? bodyVisual.GetComponent<Image>() : GetComponent<Image>();
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
        bool stillHasLegacyPart = inv != null && index >= 0 && index < inv.equipped.Length && inv.equipped[index] != null;
        bool stillHasItemPart = ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.GetEquippedByBodySlot(bodySlot) != null;
        if (sourceImage != null)
            sourceImage.color = !stillHasLegacyPart && !stillHasItemPart
                ? new Color(0.04f, 0.035f, 0.03f, 0.48f)
                : sourceColor;
    }

    Vector2 SourceSize()
    {
        // 캐릭터에 부착돼 실제로 보이는 그림(sourceImage)의 크기를 기준으로 고스트를 만든다.
        // 몸통(Body)은 클릭 히트박스(transform)가 작고 실제 그림은 훨씬 큰 자식 BodyVisual에 그려지므로,
        // transform 크기를 쓰면 집었을 때 그림이 작아져 보인다. sourceImage 의 rect × 스케일을 써서
        // 부착 시 크기 그대로 집어지게 한다.
        RectTransform sourceRect = sourceImage != null ? sourceImage.rectTransform : (transform as RectTransform);
        if (sourceRect == null)
            return new Vector2(170f, 170f);

        Vector2 size = sourceRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = sourceRect.sizeDelta;

        Vector3 scale = sourceRect.localScale;
        size = new Vector2(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y));
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
