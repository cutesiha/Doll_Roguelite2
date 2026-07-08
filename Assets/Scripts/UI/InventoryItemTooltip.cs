using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// task6: 인벤토리 슬롯에 마우스를 올리면 아이템 이름 + 설명 툴팁 표시
public class InventoryItemTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 툴팁 패널은 InventoryUI에서 공유 (싱글턴 패턴)
    static GameObject tooltipPanel;
    static TextMeshProUGUI tooltipText;
    static Canvas tooltipCanvas;

    [SerializeField] int storageIndex = -1;
    ItemData itemData;
    BodySlot? bodySlot;

    public void SetStorageIndex(int index) => storageIndex = index;
    public void SetItemData(ItemData data) => itemData = data;
    public void SetBodySlot(BodySlot slot) { bodySlot = slot; itemData = null; }
    public void ClearBodySlot() { bodySlot = null; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        string content = BuildTooltipText();
        if (string.IsNullOrEmpty(content))
            return;

        EnsureTooltip();
        tooltipText.text = content;
        tooltipPanel.SetActive(true);
        PositionTooltip(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    void OnDisable()
    {
        HideTooltip();
    }

    void Update()
    {
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 mousePos = Input.mousePosition;
            PositionTooltip(mousePos);
        }
    }

    static void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    string BuildTooltipText()
    {
        ItemData item = itemData;

        // BodySlot 장착 아이템 확인
        if (item == null && bodySlot.HasValue)
            item = ItemInventoryManager.Instance?.GetEquippedByBodySlot(bodySlot.Value);

        // InventoryManager.storage BodyPart 확인
        if (item == null && storageIndex >= 0)
        {
            var inv = InventoryManager.Instance;
            if (inv != null && storageIndex < inv.storage.Length)
            {
                BodyPart part = inv.storage[storageIndex];
                if (part != null)
                    return "<b>" + part.DisplayName() + "</b>\n<size=85%>HP: " + part.currentHp + "/" + part.maxHp + "</size>";
            }
        }

        if (item == null)
            return "";

        return ItemTooltipTextFormatter.Build(item);
    }

    public static void EnsureTooltip()
    {
        if (tooltipPanel != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;
        tooltipCanvas = canvas;

        if (TryBindAuthoredTooltip(canvas))
            return;

        GameObject go = new GameObject("InventoryTooltipPanel");
        go.transform.SetParent(canvas.transform, false);
        go.layer = canvas.gameObject.layer;

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(370f, 160f);
        rect.pivot = new Vector2(0f, 1f);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.06f, 0.04f, 0.93f);
        bg.raycastTarget = false;

        GameObject textGo = new GameObject("TooltipText");
        textGo.transform.SetParent(go.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 8f);
        textRect.offsetMax = new Vector2(-10f, -8f);

        tooltipText = textGo.AddComponent<TextMeshProUGUI>();
        tooltipText.font = UIThinDungFont.Get();
        tooltipText.fontSize = 36f;
        tooltipText.color = Color.white;
        tooltipText.raycastTarget = false;
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        tooltipText.enableAutoSizing = true;
        tooltipText.fontSizeMin = 24f;
        tooltipText.fontSizeMax = 36f;

        Canvas tooltipCanvasComp = go.AddComponent<Canvas>();
        tooltipCanvasComp.overrideSorting = true;
        tooltipCanvasComp.sortingOrder = 600;

        tooltipPanel = go;
        tooltipPanel.SetActive(false);
    }

    static bool TryBindAuthoredTooltip(Canvas preferredCanvas)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i] == preferredCanvas ? preferredCanvas : canvases[i];
            if (canvas == null)
                continue;

            Transform existing = FindChildRecursive(canvas.transform, "InventoryTooltipPanel");
            if (existing == null)
                continue;

            tooltipPanel = existing.gameObject;
            tooltipCanvas = canvas;

            RectTransform rect = tooltipPanel.GetComponent<RectTransform>();
            if (rect == null)
                rect = tooltipPanel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 370f), Mathf.Max(rect.sizeDelta.y, 160f));

            Image background = tooltipPanel.GetComponent<Image>();
            if (background == null)
                background = tooltipPanel.AddComponent<Image>();
            background.raycastTarget = false;

            Transform textTransform = FindChildRecursive(tooltipPanel.transform, "TooltipText");
            if (textTransform == null)
                textTransform = FindChildRecursive(tooltipPanel.transform, "Label");
            if (textTransform == null)
            {
                GameObject textGo = new GameObject("TooltipText");
                textGo.transform.SetParent(tooltipPanel.transform, false);
                RectTransform textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10f, 8f);
                textRect.offsetMax = new Vector2(-10f, -8f);
                textTransform = textGo.transform;
            }

            tooltipText = textTransform.GetComponent<TextMeshProUGUI>();
            if (tooltipText == null)
                tooltipText = textTransform.gameObject.AddComponent<TextMeshProUGUI>();
            tooltipText.font = UIThinDungFont.Get();
            tooltipText.raycastTarget = false;
            tooltipText.fontSize = 36f;
            tooltipText.enableAutoSizing = true;
            tooltipText.fontSizeMin = 24f;
            tooltipText.fontSizeMax = 36f;
            tooltipText.textWrappingMode = TextWrappingModes.Normal;

            Canvas tooltipCanvasComp = tooltipPanel.GetComponent<Canvas>();
            if (tooltipCanvasComp == null)
                tooltipCanvasComp = tooltipPanel.AddComponent<Canvas>();
            tooltipCanvasComp.overrideSorting = true;
            tooltipCanvasComp.sortingOrder = Mathf.Max(tooltipCanvasComp.sortingOrder, 600);

            tooltipPanel.SetActive(false);
            return true;
        }

        return false;
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static void PositionTooltip(Vector2 screenPos)
    {
        if (tooltipPanel == null || tooltipCanvas == null)
            return;

        RectTransform canvasRect = tooltipCanvas.transform as RectTransform;
        RectTransform tooltipRect = tooltipPanel.transform as RectTransform;
        if (canvasRect == null || tooltipRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, tooltipCanvas.worldCamera, out Vector2 localPoint))
        {
            // 오른쪽 및 위 오버플로우 방지
            float tooltipW = tooltipRect.sizeDelta.x;
            float tooltipH = tooltipRect.sizeDelta.y;
            float canvasW = canvasRect.rect.width;
            float canvasH = canvasRect.rect.height;

            float x = localPoint.x + 12f;
            float y = localPoint.y + 12f;

            if (x + tooltipW > canvasW * 0.5f)
                x = localPoint.x - tooltipW - 8f;
            if (y > canvasH * 0.5f - tooltipH)
                y = localPoint.y - tooltipH - 4f;

            tooltipRect.anchoredPosition = new Vector2(x, y);
        }
    }
}
