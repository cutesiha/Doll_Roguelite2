using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerItemEffects : MonoBehaviour
{
    PlayerController controller;
    PlayerAttack attack;
    PlayerDamageReceiver damageReceiver;
    ItemVisionEffect visionEffect;
    Vector3 lastThreadPosition;
    float nextThreadTime;

    float moveSpeedBonus;
    float leftArmDamageBonus;
    float rightArmDamageBonus;
    int maxHealthModifier;
    ItemData armItem;
    ItemData legItem;
    ItemData bodyItem;
    ItemData eyeItem;

    public float MoveSpeedBonus => moveSpeedBonus;
    public float LeftArmDamageBonus => leftArmDamageBonus;
    public float RightArmDamageBonus => rightArmDamageBonus;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        attack = GetComponent<PlayerAttack>();
        damageReceiver = GetComponent<PlayerDamageReceiver>();
        lastThreadPosition = transform.position;
    }

    void OnEnable()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed += Refresh;
        if (damageReceiver != null)
            damageReceiver.Damaged += OnPlayerDamaged;
        Refresh();
    }

    void OnDisable()
    {
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed -= Refresh;
        if (damageReceiver != null)
            damageReceiver.Damaged -= OnPlayerDamaged;
    }

    void Update()
    {
        if (legItem == null || !legItem.HasEffect(ItemEffectType.ThreadTrail))
            return;

        float distance = Vector2.Distance(lastThreadPosition, transform.position);
        if (Time.time < nextThreadTime || distance < 0.5f)
            return;

        ItemEffectData effect = legItem.GetEffect(ItemEffectType.ThreadTrail);
        PlayerThreadTrail.Spawn(transform.position, Mathf.Max(1, Mathf.RoundToInt(effect != null ? effect.value : 1f)));
        lastThreadPosition = transform.position;
        nextThreadTime = Time.time + 0.14f;
    }

    public void Refresh()
    {
        ItemInventoryManager inventory = ItemInventoryManager.Instance;
        if (inventory == null)
            return;

        eyeItem = inventory.GetEquipped(ItemEquipLocation.Eye);
        armItem = inventory.GetEquipped(ItemEquipLocation.Arm);
        bodyItem = inventory.GetEquipped(ItemEquipLocation.Body);
        legItem = inventory.GetEquipped(ItemEquipLocation.Leg);

        moveSpeedBonus = inventory.RoomMoveSpeedBonus;
        leftArmDamageBonus = inventory.RoomArmDamageBonus;
        rightArmDamageBonus = inventory.RoomArmDamageBonus;
        maxHealthModifier = 0;

        AccumulateEquippedEffects(eyeItem);
        AccumulateEquippedEffects(armItem);
        AccumulateEquippedEffects(bodyItem);
        AccumulateEquippedEffects(legItem);

        if (controller == null)
            controller = GetComponent<PlayerController>();
        controller?.SetItemMoveSpeedBonus(moveSpeedBonus);

        PlayerManager.Instance?.SetItemMaxHpModifier(maxHealthModifier);

        Camera camera = Camera.main;
        if (camera != null)
        {
            visionEffect = camera.GetComponent<ItemVisionEffect>();
            if (visionEffect == null)
                visionEffect = camera.gameObject.AddComponent<ItemVisionEffect>();

            visionEffect.Configure(
                eyeItem != null && eyeItem.HasEffect(ItemEffectType.VerticalViewFlip),
                eyeItem != null && eyeItem.HasEffect(ItemEffectType.PixelatedView),
                eyeItem != null && eyeItem.HasEffect(ItemEffectType.InvertedView),
                eyeItem != null && eyeItem.HasEffect(ItemEffectType.ButtonView));
        }
    }

    public float ModifiedAttackDamage(bool leftArm, float baseDamage)
    {
        float bonus = leftArm ? leftArmDamageBonus : rightArmDamageBonus;
        if (armItem != null && armItem.HasEffect(ItemEffectType.AxeAttack))
        {
            ItemEffectData axe = armItem.GetEffect(ItemEffectType.AxeAttack);
            bonus += axe != null ? axe.value : 0f;
        }
        return Mathf.Max(0f, baseDamage + bonus);
    }

    public float AttackSizeMultiplier
    {
        get
        {
            if (armItem == null || !armItem.HasEffect(ItemEffectType.AxeAttack))
                return 1f;
            ItemEffectData axe = armItem.GetEffect(ItemEffectType.AxeAttack);
            return axe != null && axe.duration > 0f ? Mathf.Max(1f, axe.duration) : 1.45f;
        }
    }

    public bool TryPerformProjectileAttack(bool leftArm, Vector2 direction, Vector3 origin)
    {
        if (armItem == null)
            return false;

        ItemEffectData keyring = armItem.GetEffect(ItemEffectType.KeyringProjectile);
        if (keyring != null)
        {
            int damage = Mathf.Max(1, Mathf.RoundToInt(ModifiedAttackDamage(leftArm, Mathf.Max(1f, keyring.value))));
            ItemProjectile.Spawn(origin, direction, 10f, damage, 1.6f, 0.18f, true, armItem.PlaceholderColor, ItemPlaceholderShape.Diamond);
            return true;
        }

        ItemEffectData nail = armItem.GetEffect(ItemEffectType.NailProjectile);
        if (nail != null)
        {
            int damage = Mathf.Max(1, Mathf.RoundToInt(ModifiedAttackDamage(leftArm, Mathf.Max(1f, nail.value))));
            ItemProjectile.Spawn(origin, direction, 12f, damage, 1.4f, 0.11f, false, armItem.PlaceholderColor, ItemPlaceholderShape.Diamond);
            return true;
        }

        return false;
    }

    void AccumulateEquippedEffects(ItemData item)
    {
        if (item == null)
            return;

        IReadOnlyList<ItemEffectData> effects = item.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            ItemEffectData effect = effects[i];
            if (effect == null)
                continue;

            switch (effect.effectType)
            {
                case ItemEffectType.MoveSpeed:
                    moveSpeedBonus += effect.value;
                    break;
                case ItemEffectType.LeftArmDamage:
                    leftArmDamageBonus += effect.value;
                    break;
                case ItemEffectType.RightArmDamage:
                    rightArmDamageBonus += effect.value;
                    break;
                case ItemEffectType.BothArmDamage:
                    leftArmDamageBonus += effect.value;
                    rightArmDamageBonus += effect.value;
                    break;
                case ItemEffectType.MaxHealth:
                    maxHealthModifier += Mathf.RoundToInt(effect.value);
                    break;
            }
        }
    }

    void OnPlayerDamaged()
    {
        if (bodyItem == null)
            return;

        ItemEffectData burst = bodyItem.GetEffect(ItemEffectType.NeedleBurstOnHit);
        if (burst == null)
            return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(burst.value));
        const int count = 12;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            ItemProjectile.Spawn(transform.position, direction, 8f, damage, 1.2f, 0.09f, true, new Color(0.88f, 0.78f, 0.64f, 1f));
        }
    }
}

public class PlayerThreadTrail : MonoBehaviour
{
    int damage;
    float expiresAt;
    readonly HashSet<EnemyBase> damaged = new();

    public static void Spawn(Vector3 position, int damage)
    {
        GameObject go = new GameObject("PlayerThreadTrail");
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.75f, 0.12f, 1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = BossVisuals.SquareSprite();
        renderer.color = new Color(0.92f, 0.78f, 0.72f, 0.82f);
        renderer.sortingOrder = 2;

        PlayerThreadTrail trail = go.AddComponent<PlayerThreadTrail>();
        trail.damage = Mathf.Max(1, damage);
        trail.expiresAt = Time.time + 8f;
    }

    void Update()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, new Vector2(0.8f, 0.22f), 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyBase enemy = hits[i] != null ? hits[i].GetComponentInParent<EnemyBase>() : null;
            if (enemy == null || !damaged.Add(enemy))
                continue;

            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (Time.time >= expiresAt)
            Destroy(gameObject);
    }
}
