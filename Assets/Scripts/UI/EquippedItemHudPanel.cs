using TMPro;
using UnityEngine;
using UnityEngine.UI;

// task8: RunHUD 왼쪽위 HP 아래에 장착된 Q보석(소모품) 표시
public class EquippedItemHudPanel : MonoBehaviour
{
    [SerializeField] Image itemIcon;
    [SerializeField] TextMeshProUGUI itemNameLabel;

    static EquippedItemHudPanel instance;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed -= Refresh;
        if (instance == this) instance = null;
    }

    public static EquippedItemHudPanel Instance => instance;

    public void Refresh()
    {
        ItemData equipped = ItemInventoryManager.Instance != null
            ? ItemInventoryManager.Instance.Consumable
            : null;

        bool hasItem = equipped != null;
        gameObject.SetActive(hasItem);

        if (!hasItem)
            return;

        if (itemIcon != null)
        {
            itemIcon.sprite = equipped.Sprite;
            itemIcon.color = equipped.Sprite != null ? Color.white : new Color(0.88f, 0.48f, 0.24f, 1f);
            itemIcon.preserveAspect = true;
        }

        if (itemNameLabel != null)
            itemNameLabel.text = equipped.ItemName;
    }

    // 에디터에서 아이콘/레이블 레퍼런스 연결용
    public void SetReferences(Image icon, TextMeshProUGUI label)
    {
        itemIcon = icon;
        itemNameLabel = label;
    }
}
