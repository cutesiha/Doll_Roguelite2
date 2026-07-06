using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ItemTestRoomSpawner : MonoBehaviour
{
    [SerializeField] Vector2 startPosition = new Vector2(-12f, 5f);
    [SerializeField] Vector2 spacing = new Vector2(3.7f, 2.35f);
    [SerializeField, Min(1)] int columns = 7;
    [SerializeField] bool clearExistingItems = true;
    [SerializeField] bool includeCurrency = true;
    [SerializeField] float labelFontSize = 0.34f;
    [Tooltip("에디터 툴로 하이어라키에 직접 배치된 경우 true. 런타임 스폰을 건너뜀.")]
    [SerializeField] bool authoredInHierarchy;

    bool spawned;

    void Start()
    {
        if (authoredInHierarchy)
        {
            RegisterAuthoredTooltipTemplate();
            return;
        }
        SpawnAllItems();
    }

    [ContextMenu("Spawn All Items")]
    public void SpawnAllItems()
    {
        if (spawned && Application.isPlaying)
            return;

        spawned = true;
        if (clearExistingItems)
            ClearExistingRoot();

        GameObject root = new GameObject("ItemTestRoom_AllItems");
        root.transform.SetParent(transform, false);

        IReadOnlyList<ItemData> items = ItemCatalog.All;
        int visibleIndex = 0;
        ItemWorldPickup tooltipTemplate = null;
        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;
            if (!includeCurrency && item.Type == ItemType.Currency)
                continue;

            int row = visibleIndex / Mathf.Max(1, columns);
            int col = visibleIndex % Mathf.Max(1, columns);
            Vector3 position = new Vector3(
                startPosition.x + col * spacing.x,
                startPosition.y - row * spacing.y,
                0f);

            ItemWorldPickup pickup = ItemDropSpawner.Spawn(item, position, false, 0, tooltipTemplate, false);
            if (pickup != null)
            {
                pickup.name = "ItemTest_" + item.ItemId;
                pickup.transform.SetParent(root.transform, true);
                if (tooltipTemplate == null)
                {
                    tooltipTemplate = pickup;
                    tooltipTemplate.UseAsGlobalTooltipTemplate();
                }
            }

            CreateLabel(root.transform, item, position + new Vector3(0f, -0.95f, 0f));
            visibleIndex++;
        }
    }

    void ClearExistingRoot()
    {
        Transform oldRoot = transform.Find("ItemTestRoom_AllItems");
        if (oldRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(oldRoot.gameObject);
        else
            DestroyImmediate(oldRoot.gameObject);
    }

    void CreateLabel(Transform parent, ItemData item, Vector3 position)
    {
        GameObject labelObject = new GameObject("Label_" + item.ItemId);
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.position = position;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.font = UIThinDungFont.Get();
        label.fontSize = labelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.sortingOrder = 80;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.rectTransform.sizeDelta = new Vector2(3.2f, 0.85f);
        label.text = item.ItemId + "\n" + item.ItemName;
    }

    void RegisterAuthoredTooltipTemplate()
    {
        ItemWorldPickup[] pickups = GetComponentsInChildren<ItemWorldPickup>(true);
        if (pickups == null || pickups.Length == 0)
            return;

        pickups[0].UseAsGlobalTooltipTemplate();
    }
}
