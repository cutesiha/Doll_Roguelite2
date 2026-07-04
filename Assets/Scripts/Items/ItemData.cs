using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory
{
    BodyRoom,
    MiddleBossRoom,
    ChallengeRoom,
    Shop,
    ConditionReward
}

public enum ItemType
{
    BodyPart,
    GemConsumable,
    Passive,
    Shield,
    Currency
}

public enum ItemEquipLocation
{
    None,
    Eye,
    Arm,
    Body,
    Leg,
    Consumable,
    Shield
}

[Flags]
public enum ItemAcquisitionLocation
{
    None = 0,
    BodyRoom = 1 << 0,
    MiddleBossRoom = 1 << 1,
    ChallengeRoom = 1 << 2,
    ShopRoom = 1 << 3,
    ConditionRoom = 1 << 4
}

public enum ItemPlaceholderShape
{
    Square,
    Circle,
    Diamond,
    Triangle
}

public enum ItemEffectType
{
    None,
    MoveSpeed,
    LeftArmDamage,
    RightArmDamage,
    BothArmDamage,
    DeprecatedBodyHealthSlot,
    InventoryCapacity,
    VerticalViewFlip,
    PixelatedView,
    InvertedView,
    ButtonView,
    KeyringProjectile,
    AxeAttack,
    NailProjectile,
    NeedleBurstOnHit,
    ThreadTrail,
    Invincibility,
    DamageAllEnemies,
    CoinOnKill,
    RoomMoveSpeed,
    DamageAllParts,
    NextRoomShield,
    StarBurst
}

[Serializable]
public class ItemEffectData
{
    public ItemEffectType effectType;
    public float value;
    public float duration;

    public ItemEffectData(ItemEffectType effectType, float value = 0f, float duration = 0f)
    {
        this.effectType = effectType;
        this.value = value;
        this.duration = duration;
    }
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Doll Roguelite/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] string itemId;
    [SerializeField] string itemName;
    [TextArea(2, 5)]
    [SerializeField] string description;

    [Header("분류")]
    [SerializeField] ItemCategory category;
    [SerializeField] ItemType itemType;
    [SerializeField] ItemEquipLocation equipLocation;
    [SerializeField] ItemAcquisitionLocation acquisitionLocations;
    [SerializeField] bool consumable;

    [Header("수치")]
    [SerializeField] float value;
    [SerializeField] float secondaryValue;
    [SerializeField] float duration;
    [SerializeField, Min(0)] int shopPrice = 10;

    [Header("효과 목록")]
    [SerializeField] List<ItemEffectData> effects = new();

    [Header("표시")]
    [SerializeField] Sprite sprite;
    [Tooltip("투사체 공격(열쇠고리/못 등)일 때 날아가는 오브젝트에 쓸 전용 스프라이트. 비어있으면 색칠된 도형으로 대체.")]
    [SerializeField] Sprite projectileSprite;
    [Tooltip("한 번에 여러 개의 서로 다른 모양 투사체를 쏘는 공격(별 등)의 2번째/3번째 투사체 스프라이트. 비어있으면 projectileSprite를 대신 쓴다.")]
    [SerializeField] Sprite projectileSprite2;
    [SerializeField] Sprite projectileSprite3;
    [Tooltip("Arm/Leg/Eye 부위의 왼쪽 슬롯(ArmLeft/LegLeft/EyeLeft)에 장착됐을 때 캐릭터에 표시할 전용 스프라이트. 비어있으면 sprite를 그대로 사용.")]
    [SerializeField] Sprite equippedSpriteLeft;
    [Tooltip("Arm/Leg/Eye 부위의 오른쪽 슬롯(ArmRight/LegRight/EyeRight)에 장착됐을 때 캐릭터에 표시할 전용 스프라이트. 비어있으면 sprite를 그대로 사용.")]
    [SerializeField] Sprite equippedSpriteRight;
    [SerializeField] ItemPlaceholderShape placeholderShape = ItemPlaceholderShape.Square;
    [SerializeField] Color placeholderColor = Color.white;
    [Tooltip("월드에 스폰될 때 적용할 크기. 0 이하이면 타입별 기본 크기를 사용. (아이템 테스트룸에서 조정한 값과 동기화됨)")]
    [SerializeField] float worldScale = 0f;
    [Tooltip("캐릭터 부위에 장착됐을 때 표시 배율. 특히 Body 부위는 히트박스보다 훨씬 큰 고정 박스(BodyVisual)를 쓰기 때문에, 작은 인벤토리 아이콘을 그대로 쓰면 지나치게 커 보인다. 1 = 원래 크기.")]
    [SerializeField, Min(0.05f)] float equippedScale = 1f;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
    public string ItemName => string.IsNullOrWhiteSpace(itemName) ? name : itemName;
    public string Description => description;
    public ItemCategory Category => category;
    public ItemType Type => itemType;
    public ItemEquipLocation EquipLocation => equipLocation;
    public ItemAcquisitionLocation AcquisitionLocations => acquisitionLocations;
    public bool Consumable => consumable;
    public float Value => value;
    public float SecondaryValue => secondaryValue;
    public float Duration => duration;
    public int ShopPrice => shopPrice;
    public IReadOnlyList<ItemEffectData> Effects => effects;
    public Sprite Sprite => sprite;
    public Sprite ProjectileSprite => projectileSprite;
    public Sprite ProjectileSprite2 => projectileSprite2 != null ? projectileSprite2 : projectileSprite;
    public Sprite ProjectileSprite3 => projectileSprite3 != null ? projectileSprite3 : projectileSprite;
    public Sprite EquippedSpriteLeft => equippedSpriteLeft;
    public Sprite EquippedSpriteRight => equippedSpriteRight;
    public ItemPlaceholderShape PlaceholderShape => placeholderShape;
    public Color PlaceholderColor => placeholderColor;
    public float WorldScale => worldScale;
    public float EquippedScale => equippedScale;

    public float ResolveWorldScale()
    {
        if (worldScale > 0.0001f)
            return worldScale;

        return itemType == ItemType.BodyPart ? 0.95f : 0.72f;
    }

#if UNITY_EDITOR
    public void EditorSetWorldScale(float scale)
    {
        worldScale = Mathf.Max(0f, scale);
    }

    public void EditorSetProjectileSprite(Sprite sprite)
    {
        projectileSprite = sprite;
    }
#endif

    // 캐릭터에 장착 표시할 때 쓸 스프라이트. 왼쪽/오른쪽 부위 전용 그림이 있으면 그걸 쓰고,
    // 없으면 인벤토리 슬롯과 같은 기본 sprite로 대체한다.
    public Sprite GetEquippedSprite(BodySlot slot)
    {
        switch (slot)
        {
            case BodySlot.ArmLeft:
            case BodySlot.LegLeft:
            case BodySlot.EyeLeft:
                return equippedSpriteLeft != null ? equippedSpriteLeft : sprite;
            case BodySlot.ArmRight:
            case BodySlot.LegRight:
            case BodySlot.EyeRight:
                return equippedSpriteRight != null ? equippedSpriteRight : sprite;
            default:
                return sprite;
        }
    }

    public bool HasEffect(ItemEffectType effectType)
    {
        return GetEffect(effectType) != null;
    }

    public ItemEffectData GetEffect(ItemEffectType effectType)
    {
        for (int i = 0; i < effects.Count; i++)
            if (effects[i] != null && effects[i].effectType == effectType)
                return effects[i];

        return null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string id,
        string displayName,
        string itemDescription,
        ItemCategory itemCategory,
        ItemType type,
        ItemEquipLocation location,
        ItemAcquisitionLocation acquisitions,
        bool isConsumable,
        float primaryValue,
        float secondValue,
        float effectDuration,
        int price,
        ItemPlaceholderShape shape,
        Color color,
        params ItemEffectData[] itemEffects)
    {
        itemId = id;
        itemName = displayName;
        description = itemDescription;
        category = itemCategory;
        itemType = type;
        equipLocation = location;
        acquisitionLocations = acquisitions;
        consumable = isConsumable;
        value = primaryValue;
        secondaryValue = secondValue;
        duration = effectDuration;
        shopPrice = Mathf.Max(0, price);
        placeholderShape = shape;
        placeholderColor = color;
        effects = itemEffects != null ? new List<ItemEffectData>(itemEffects) : new List<ItemEffectData>();
    }
#endif
}
