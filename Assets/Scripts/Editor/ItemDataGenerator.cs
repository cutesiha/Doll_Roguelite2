#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ItemDataGenerator
{
    const string Folder = "Assets/Resources/Items";

    [MenuItem("Tools/Items/Create Or Update Default Item Data")]
    public static void CreateOrUpdate()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(Folder);

        CreateSettings();

        Create("glass_eye", "유리눈", "시야가 위아래로 반전되고 오른쪽 팔 데미지 +1.00",
            ItemCategory.BodyRoom, ItemType.BodyPart, ItemEquipLocation.Eye,
            ItemAcquisitionLocation.BodyRoom, false, 1f, 0f, 0f, 18,
            ItemPlaceholderShape.Circle, new Color(0.72f, 0.92f, 1f, 1f),
            E(ItemEffectType.VerticalViewFlip),
            E(ItemEffectType.RightArmDamage, 1f));

        Create("moving_eye", "접착식 움직이는 눈", "화면이 저해상도처럼 보이고 왼쪽 팔 데미지 +2.43",
            ItemCategory.BodyRoom, ItemType.BodyPart, ItemEquipLocation.Eye,
            ItemAcquisitionLocation.BodyRoom, false, 2.43f, 0f, 0f, 20,
            ItemPlaceholderShape.Circle, new Color(0.48f, 0.92f, 0.55f, 1f),
            E(ItemEffectType.PixelatedView),
            E(ItemEffectType.LeftArmDamage, 2.43f));

        Create("bell", "방울", "화면 색상이 반전되고 이동속도 +2.00",
            ItemCategory.BodyRoom, ItemType.BodyPart, ItemEquipLocation.Eye,
            ItemAcquisitionLocation.BodyRoom, false, 2f, 0f, 0f, 20,
            ItemPlaceholderShape.Circle, new Color(1f, 0.78f, 0.18f, 1f),
            E(ItemEffectType.InvertedView),
            E(ItemEffectType.MoveSpeed, 2f));

        Create("keyring", "열쇠 키링", "열쇠 키링을 던져 적을 관통하는 투사체 공격",
            ItemCategory.BodyRoom, ItemType.BodyPart, ItemEquipLocation.Arm,
            ItemAcquisitionLocation.BodyRoom, false, 3f, 0f, 0f, 22,
            ItemPlaceholderShape.Diamond, new Color(0.72f, 0.72f, 0.78f, 1f),
            E(ItemEffectType.KeyringProjectile, 3f));
        SetProjectileSprite("keyring", "Assets/Sprites/Item/Itemeffect/열쇠 나가는거.png");

        Create("knitted_body", "뜨개질 몸", "인벤토리를 4x4(16칸)로 확장하지만 최대 체력 -1",
            ItemCategory.BodyRoom, ItemType.BodyPart, ItemEquipLocation.Body,
            ItemAcquisitionLocation.BodyRoom, false, 16f, -1f, 0f, 24,
            ItemPlaceholderShape.Square, new Color(0.82f, 0.42f, 0.45f, 1f),
            E(ItemEffectType.InventoryCapacity, 16f),
            E(ItemEffectType.MaxHealth, -1f));

        Create("round_pin", "원형 시침핀", "피격 시 사방으로 시침핀이 발사되며 각각 데미지 5.00",
            ItemCategory.MiddleBossRoom, ItemType.BodyPart, ItemEquipLocation.Body,
            ItemAcquisitionLocation.MiddleBossRoom, false, 5f, 0f, 0f, 26,
            ItemPlaceholderShape.Circle, new Color(0.88f, 0.74f, 0.68f, 1f),
            E(ItemEffectType.NeedleBurstOnHit, 5f));

        Create("ribbon", "리본", "이동속도 +1.00",
            ItemCategory.MiddleBossRoom, ItemType.BodyPart, ItemEquipLocation.Leg,
            ItemAcquisitionLocation.MiddleBossRoom, false, 1f, 0f, 0f, 20,
            ItemPlaceholderShape.Diamond, new Color(0.95f, 0.36f, 0.55f, 1f),
            E(ItemEffectType.MoveSpeed, 1f));

        Create("button_eye", "단추 눈", "화면이 단추 구멍으로 바라보는 것처럼 보임",
            ItemCategory.MiddleBossRoom, ItemType.BodyPart, ItemEquipLocation.Eye,
            ItemAcquisitionLocation.MiddleBossRoom, false, 0f, 0f, 0f, 22,
            ItemPlaceholderShape.Circle, new Color(0.32f, 0.18f, 0.10f, 1f),
            E(ItemEffectType.ButtonView));

        Create("spool", "실타래", "지나간 길에 실을 남기고 닿은 적에게 데미지를 준 뒤 사라짐",
            ItemCategory.MiddleBossRoom, ItemType.BodyPart, ItemEquipLocation.Leg,
            ItemAcquisitionLocation.MiddleBossRoom, false, 2f, 0f, 0f, 24,
            ItemPlaceholderShape.Circle, new Color(0.92f, 0.80f, 0.72f, 1f),
            E(ItemEffectType.ThreadTrail, 2f));

        Create("axe", "도끼", "넓은 범위를 휘두르는 강화 근접 공격",
            ItemCategory.MiddleBossRoom, ItemType.BodyPart, ItemEquipLocation.Arm,
            ItemAcquisitionLocation.MiddleBossRoom, false, 4f, 1.55f, 0f, 28,
            ItemPlaceholderShape.Triangle, new Color(0.60f, 0.64f, 0.68f, 1f),
            E(ItemEffectType.AxeAttack, 4f, 1.55f));

        Create("wood_plank", "나무 판자", "나무 판자에서 못을 던져 데미지를 줌",
            ItemCategory.MiddleBossRoom, ItemType.BodyPart, ItemEquipLocation.Arm,
            ItemAcquisitionLocation.MiddleBossRoom, false, 5f, 0f, 0f, 25,
            ItemPlaceholderShape.Square, new Color(0.55f, 0.32f, 0.14f, 1f),
            E(ItemEffectType.NailProjectile, 5f));
        SetProjectileSprite("wood_plank", "Assets/Sprites/Item/Itemeffect/못.png");

        Create("wooden_leg", "목제 다리", "최대 체력 +2, 이동속도 -0.30",
            ItemCategory.ChallengeRoom, ItemType.BodyPart, ItemEquipLocation.Leg,
            ItemAcquisitionLocation.ChallengeRoom, false, 2f, -0.3f, 0f, 25,
            ItemPlaceholderShape.Square, new Color(0.50f, 0.28f, 0.12f, 1f),
            E(ItemEffectType.MaxHealth, 2f),
            E(ItemEffectType.MoveSpeed, -0.3f));

        Create("flag", "깃발", "인벤토리 2x2(4칸), 이동속도 +1.00, 오른팔 공격력 +2.00",
            ItemCategory.ChallengeRoom, ItemType.BodyPart, ItemEquipLocation.Body,
            ItemAcquisitionLocation.ChallengeRoom, false, 4f, 1f, 0f, 26,
            ItemPlaceholderShape.Triangle, new Color(0.85f, 0.18f, 0.20f, 1f),
            E(ItemEffectType.InventoryCapacity, 4f),
            E(ItemEffectType.MoveSpeed, 1f),
            E(ItemEffectType.RightArmDamage, 2f));

        ItemAcquisitionLocation shopCondition = ItemAcquisitionLocation.ShopRoom | ItemAcquisitionLocation.ConditionRoom;

        Create("white_gem", "하얀 보석", "20초 동안 무적",
            ItemCategory.Shop, ItemType.GemConsumable, ItemEquipLocation.Consumable,
            shopCondition, true, 0f, 0f, 20f, 12,
            ItemPlaceholderShape.Diamond, Color.white,
            E(ItemEffectType.Invincibility, 0f, 20f));

        Create("black_gem", "검은 보석", "현재 방의 모든 적에게 데미지 44",
            ItemCategory.Shop, ItemType.GemConsumable, ItemEquipLocation.Consumable,
            shopCondition, true, 44f, 0f, 0f, 12,
            ItemPlaceholderShape.Diamond, new Color(0.08f, 0.08f, 0.10f, 1f),
            E(ItemEffectType.DamageAllEnemies, 44f));

        Create("yellow_gem", "노란 보석", "20초 동안 적 처치 시 동전 드랍",
            ItemCategory.Shop, ItemType.GemConsumable, ItemEquipLocation.Consumable,
            shopCondition, true, 0f, 0f, 20f, 12,
            ItemPlaceholderShape.Diamond, new Color(1f, 0.82f, 0.12f, 1f),
            E(ItemEffectType.CoinOnKill, 0f, 20f));

        Create("blue_gem", "파랑 보석", "현재 방에서 이동속도 +2.00",
            ItemCategory.Shop, ItemType.GemConsumable, ItemEquipLocation.Consumable,
            shopCondition, true, 2f, 0f, 0f, 12,
            ItemPlaceholderShape.Diamond, new Color(0.18f, 0.48f, 1f, 1f),
            E(ItemEffectType.RoomMoveSpeed, 2f));

        Create("pink_gem", "핑크 보석", "모든 기본 부위에 데미지 1, 현재 방에서 양팔 데미지 +2.00",
            ItemCategory.Shop, ItemType.GemConsumable, ItemEquipLocation.Consumable,
            shopCondition, true, 1f, 2f, 0f, 12,
            ItemPlaceholderShape.Diamond, new Color(1f, 0.35f, 0.68f, 1f),
            E(ItemEffectType.DamageAllParts, 1f),
            E(ItemEffectType.BothArmDamage, 2f));

        Create("rag", "누더기", "다음 방에서 피격 1회를 무효화하는 방어막",
            ItemCategory.Shop, ItemType.Shield, ItemEquipLocation.Shield,
            shopCondition, false, 1f, 0f, 0f, 10,
            ItemPlaceholderShape.Square, new Color(0.58f, 0.48f, 0.42f, 1f),
            E(ItemEffectType.NextRoomShield, 1f));

        Create("coin", "동전", "상점에서 사용하는 화폐",
            ItemCategory.ConditionReward, ItemType.Currency, ItemEquipLocation.None,
            ItemAcquisitionLocation.None, false, 1f, 0f, 0f, 0,
            ItemPlaceholderShape.Circle, new Color(1f, 0.74f, 0.12f, 1f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ItemCatalog.Reload();
        Debug.Log("Item data generated: " + Folder);
    }

    static ItemEffectData E(ItemEffectType type, float value = 0f, float duration = 0f)
    {
        return new ItemEffectData(type, value, duration);
    }

    static void Create(
        string id,
        string displayName,
        string description,
        ItemCategory category,
        ItemType type,
        ItemEquipLocation equipLocation,
        ItemAcquisitionLocation acquisitions,
        bool consumable,
        float value,
        float secondaryValue,
        float duration,
        int price,
        ItemPlaceholderShape shape,
        Color color,
        params ItemEffectData[] effects)
    {
        string path = Folder + "/" + id + ".asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.EditorConfigure(
            id, displayName, description, category, type, equipLocation, acquisitions,
            consumable, value, secondaryValue, duration, price, shape, color, effects);
        EditorUtility.SetDirty(item);
    }

    static void SetProjectileSprite(string id, string spritePath)
    {
        string path = Folder + "/" + id + ".asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
            return;

        // Multiple 스프라이트 모드 텍스처는 LoadAssetAtPath<Sprite>가 null을 줄 수 있어 서브 에셋에서 직접 찾는다.
        Sprite sprite = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(spritePath))
        {
            if (asset is Sprite spriteAsset)
            {
                sprite = spriteAsset;
                break;
            }
        }

        if (sprite == null)
        {
            Debug.LogWarning("투사체 스프라이트를 찾을 수 없음: " + spritePath);
            return;
        }

        item.EditorSetProjectileSprite(sprite);
        EditorUtility.SetDirty(item);
    }

    static void CreateSettings()
    {
        string path = Folder + "/ItemSystemSettings.asset";
        ItemSystemSettings settings = AssetDatabase.LoadAssetAtPath<ItemSystemSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ItemSystemSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }
        EditorUtility.SetDirty(settings);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
