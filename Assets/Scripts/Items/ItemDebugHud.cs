using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ItemDebugHud : MonoBehaviour
{
    Canvas canvas;
    TextMeshProUGUI text;
    bool visible = true;

    void Start()
    {
        Build();
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed -= Refresh;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
        {
            visible = !visible;
            if (canvas != null)
                canvas.gameObject.SetActive(visible);
        }
    }

    void Build()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("ItemDebugCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 850;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panelObject = new GameObject("ItemStatusPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(510f, 260f);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.08f, 0.045f, 0.025f, 0.82f);
        panel.raycastTarget = false;

        GameObject textObject = new GameObject("ItemStatusText");
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);
        text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = UIThinDungFont.Get();
        text.fontSize = 23f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = new Color(1f, 0.9f, 0.7f, 1f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
    }

    void Refresh()
    {
        if (text == null)
            return;

        ItemInventoryManager inventory = ItemInventoryManager.Instance;
        if (inventory == null)
        {
            text.text = "ITEM TEST: 초기화 중";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<b>ITEM TEST</b>  <size=75%>[F8 숨기기]</size>");
        builder.Append("코인 ").Append(inventory.Coins)
            .Append("  |  보관 ").Append(inventory.Storage.Count).Append('/').Append(inventory.Capacity).AppendLine();
        AppendSlot(builder, "왼눈", inventory.GetEquippedByBodySlot(BodySlot.EyeLeft));
        AppendSlot(builder, "오른눈", inventory.GetEquippedByBodySlot(BodySlot.EyeRight));
        AppendSlot(builder, "왼팔", inventory.GetEquippedByBodySlot(BodySlot.ArmLeft));
        AppendSlot(builder, "오른팔", inventory.GetEquippedByBodySlot(BodySlot.ArmRight));
        AppendSlot(builder, "몸", inventory.GetEquipped(ItemEquipLocation.Body));
        AppendSlot(builder, "왼다리", inventory.GetEquippedByBodySlot(BodySlot.LegLeft));
        AppendSlot(builder, "오른다리", inventory.GetEquippedByBodySlot(BodySlot.LegRight));
        builder.Append("Q: ").Append(inventory.Consumable != null ? inventory.Consumable.ItemName : "없음");
        builder.Append("  |  방어막 부위: ");
        builder.Append(ShieldedSlotsLabel(inventory));
        text.text = builder.ToString();
    }

    static void AppendSlot(StringBuilder builder, string label, ItemData item)
    {
        builder.Append(label).Append(": ").Append(item != null ? item.ItemName : "없음").Append("  ");
        if (label == "오른눈" || label == "오른팔" || label == "오른다리" || label == "몸")
            builder.AppendLine();
    }

    static string ShieldedSlotsLabel(ItemInventoryManager inventory)
    {
        (BodySlot slot, string label)[] slots =
        {
            (BodySlot.EyeLeft, "왼눈"), (BodySlot.EyeRight, "오른눈"),
            (BodySlot.ArmLeft, "왼팔"), (BodySlot.ArmRight, "오른팔"),
            (BodySlot.LegLeft, "왼다리"), (BodySlot.LegRight, "오른다리"),
        };

        string result = "";
        for (int i = 0; i < slots.Length; i++)
        {
            if (!inventory.IsBodySlotShielded(slots[i].slot))
                continue;
            if (result.Length > 0)
                result += ", ";
            result += slots[i].label;
        }

        return result.Length > 0 ? result : "없음";
    }
}
