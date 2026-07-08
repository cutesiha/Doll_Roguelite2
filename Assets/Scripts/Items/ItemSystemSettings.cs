using UnityEngine;

[CreateAssetMenu(fileName = "ItemSystemSettings", menuName = "Doll Roguelite/Item System Settings")]
public class ItemSystemSettings : ScriptableObject
{
    [Header("획득")]
    [Min(0.1f)] public float pickupRadius = 0.85f;
    [Min(0.1f)] public float tooltipRadius = 2.2f;
    [Min(0f)] public float floatHeight = 0.18f;
    [Min(0f)] public float floatSpeed = 2.2f;

    [Header("동전 드랍")]
    [Range(0f, 1f)] public float coinDropChance = 0.30f;
    [Min(0)] public int waveClearCoinBonus = 1;
    [Min(0)] public int challengeClearCoinBonus = 2;
    [Min(0)] public int normalRoomClearCoinDrop = 5;

    [Header("조건방 추가 보상 확률")]
    [Range(0f, 1f)] public float conditionGemChance = 0.10f;
    [Range(0f, 1f)] public float conditionRagChance = 0.05f;

    [Header("상점")]
    [Min(0)] public int startingCoins = 0;
    [Min(0)] public int gemPrice = 8;
    [Min(0)] public int ragPrice = 8;
    [Min(0)] public int bodyPartPrice = 17;

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
