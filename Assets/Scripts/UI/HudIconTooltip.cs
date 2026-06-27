using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HudIconTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] string tooltipText;
    [SerializeField] Vector2 offset = new Vector2(0f, 54f);

    static GameObject tooltipRoot;
    static RectTransform tooltipRect;
    static TextMeshProUGUI tooltipLabel;
    static Canvas ownerCanvas;

    public void SetText(string text)
    {
        tooltipText = text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(tooltipText))
            return;

        EnsureTooltip();
        if (tooltipRoot == null || tooltipLabel == null)
            return;

        tooltipLabel.text = tooltipText;
        PositionTooltip(eventData);
        tooltipRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    void EnsureTooltip()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        if (tooltipRoot != null && ownerCanvas == canvas)
            return;

        if (tooltipRoot != null)
            Destroy(tooltipRoot);

        ownerCanvas = canvas;
        if (TryBindAuthoredTooltip(canvas))
            return;

        tooltipRoot = new GameObject("HudIconTooltip");
        tooltipRoot.transform.SetParent(canvas.transform, false);
        tooltipRoot.transform.SetAsLastSibling();

        tooltipRect = tooltipRoot.AddComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.sizeDelta = new Vector2(150f, 48f);

        Image background = tooltipRoot.AddComponent<Image>();
        background.color = new Color(0.08f, 0.06f, 0.07f, 0.94f);
        background.raycastTarget = false;

        Outline outline = tooltipRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.91f, 0.86f, 0.78f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(tooltipRoot.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 5f);
        labelRect.offsetMax = new Vector2(-12f, -5f);

        tooltipLabel = labelGO.AddComponent<TextMeshProUGUI>();
        tooltipLabel.font = UIThinDungFont.Get();
        tooltipLabel.fontSize = 22f;
        tooltipLabel.alignment = TextAlignmentOptions.Center;
        tooltipLabel.color = Color.white;
        tooltipLabel.raycastTarget = false;
        tooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;

        tooltipRoot.SetActive(false);
    }

    static bool TryBindAuthoredTooltip(Canvas canvas)
    {
        Transform existing = FindChildRecursive(canvas.transform, "HudIconTooltip");
        if (existing == null)
            return false;

        tooltipRoot = existing.gameObject;
        tooltipRoot.transform.SetAsLastSibling();

        tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        if (tooltipRect == null)
            tooltipRect = tooltipRoot.AddComponent<RectTransform>();

        Image background = tooltipRoot.GetComponent<Image>();
        if (background == null)
            background = tooltipRoot.AddComponent<Image>();
        background.raycastTarget = false;

        Transform labelTransform = FindChildRecursive(tooltipRoot.transform, "Label");
        if (labelTransform == null)
        {
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(tooltipRoot.transform, false);
            labelTransform = labelGO.transform;
        }

        RectTransform labelRect = labelTransform.GetComponent<RectTransform>();
        if (labelRect == null)
            labelRect = labelTransform.gameObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 5f);
        labelRect.offsetMax = new Vector2(-12f, -5f);

        tooltipLabel = labelTransform.GetComponent<TextMeshProUGUI>();
        if (tooltipLabel == null)
            tooltipLabel = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
        tooltipLabel.font = UIThinDungFont.Get();
        tooltipLabel.raycastTarget = false;

        tooltipRoot.SetActive(false);
        return true;
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

    void PositionTooltip(PointerEventData eventData)
    {
        if (tooltipRect == null || ownerCanvas == null)
            return;

        RectTransform canvasRect = ownerCanvas.transform as RectTransform;
        RectTransform sourceRect = transform as RectTransform;
        if (canvasRect == null || sourceRect == null)
            return;

        Vector3 worldCenter = sourceRect.TransformPoint(sourceRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventData.pressEventCamera, out Vector2 localPoint);
        tooltipRect.anchoredPosition = localPoint + offset;
        tooltipRoot.transform.SetAsLastSibling();
    }
}
