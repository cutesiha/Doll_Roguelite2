using UnityEngine;

[CreateAssetMenu(fileName = "ItemSystemSettings", menuName = "Doll Roguelite/Item System Settings")]
public class ItemSystemSettings : ScriptableObject
{
    [Header("획득")]
    [Min(0.1f)] public float pickupRadius = 0.85f;
    [Min(0.1f)] public float tooltipRadius = 2.2f;
    [Min(0f)] public float floatHeight = 0.18f;
    [Min(0f)] public float floatSpeed = 2.2f;

    [Header("조건방")]
    [Range(0f, 1f)] public float conditionRewardChance = 0.18f;

    [Header("상점")]
    [Min(0)] public int startingCoins = 60;
    [Min(0)] public int gemPrice = 12;
    [Min(0)] public int ragPrice = 10;
    [Min(0)] public int bodyPartPrice = 18;

    [Header("인벤토리")]
    [Min(1)] public int defaultInventoryCapacity = 9;
    [Min(1)] public int knittedBodyCapacity = 16;
    [Min(1)] public int flagBodyCapacity = 4;

    public static ItemSystemSettings Load()
    {
        ItemSystemSettings settings = Resources.Load<ItemSystemSettings>("Items/ItemSystemSettings");
        if (settings != null)
            return settings;

        settings = CreateInstance<ItemSystemSettings>();
        settings.name = "RuntimeItemSystemSettings";
        return settings;
    }
}
