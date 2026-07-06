using System.Collections.Generic;
using UnityEngine;

// 팔 공격이 어떤 무기 성질을 갖는지 (장착된 팔 아이템의 효과로 결정됨).
public enum ArmWeaponKind
{
    Fist,
    Axe,
    Keyring,
    Nail,
    Star
}

[DisallowMultipleComponent]
public class PlayerItemEffects : MonoBehaviour
{
    PlayerController controller;
    PlayerAttack attack;
    PlayerDamageReceiver damageReceiver;
    ItemVisionEffect visionEffect;
    BoxCollider2D bodyBox;
    Vector3 lastThreadPosition;
    float nextThreadTime;

    float moveSpeedBonus;
    float leftArmDamageBonus;
    float rightArmDamageBonus;
    ItemData leftArmItem;
    ItemData rightArmItem;
    ItemData legItem;
    ItemData bodyItem;
    ItemData leftEyeItem;
    ItemData rightEyeItem;

    public float MoveSpeedBonus => moveSpeedBonus;
    public float LeftArmDamageBonus => leftArmDamageBonus;
    public float RightArmDamageBonus => rightArmDamageBonus;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        attack = GetComponent<PlayerAttack>();
        damageReceiver = GetComponent<PlayerDamageReceiver>();
        bodyBox = GetComponent<BoxCollider2D>();
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
        PlayerThreadTrail.Spawn(ThreadSpawnPosition(), Mathf.Max(1, Mathf.RoundToInt(effect != null ? effect.value : 1f)));
        lastThreadPosition = transform.position;
        nextThreadTime = Time.time + 0.14f;
    }

    // 실타래(다리 아이템)의 실은 플레이어 발밑, 즉 BoxCollider2D의 아래쪽 가장자리에 생성한다.
    // (박스 콜라이더가 없으면 플레이어 위치를 그대로 사용.)
    Vector3 ThreadSpawnPosition()
    {
        if (bodyBox == null)
            bodyBox = GetComponent<BoxCollider2D>();

        if (bodyBox == null)
            return transform.position;

        Bounds b = bodyBox.bounds;
        return new Vector3(b.center.x, b.min.y, transform.position.z);
    }

    public void Refresh()
    {
        ItemInventoryManager inventory = ItemInventoryManager.Instance;
        if (inventory == null)
            return;

        leftEyeItem = inventory.GetEquippedByBodySlot(BodySlot.EyeLeft);
        rightEyeItem = inventory.GetEquippedByBodySlot(BodySlot.EyeRight);
        leftArmItem = inventory.GetEquippedByBodySlot(BodySlot.ArmLeft);
        rightArmItem = inventory.GetEquippedByBodySlot(BodySlot.ArmRight);
        bodyItem = inventory.GetEquipped(ItemEquipLocation.Body);
        legItem = inventory.GetEquipped(ItemEquipLocation.Leg);

        moveSpeedBonus = inventory.RoomMoveSpeedBonus;
        leftArmDamageBonus = inventory.RoomArmDamageBonus;
        rightArmDamageBonus = inventory.RoomArmDamageBonus;
        AccumulateEquippedEffects(leftEyeItem);
        AccumulateEquippedEffects(rightEyeItem);
        AccumulateEquippedEffects(leftArmItem);
        AccumulateEquippedEffects(rightArmItem);
        AccumulateEquippedEffects(bodyItem);
        AccumulateEquippedEffects(legItem);

        if (controller == null)
            controller = GetComponent<PlayerController>();
        controller?.SetItemMoveSpeedBonus(moveSpeedBonus);

        Camera camera = Camera.main;
        if (camera != null)
        {
            visionEffect = camera.GetComponent<ItemVisionEffect>();
            if (visionEffect == null)
                visionEffect = camera.gameObject.AddComponent<ItemVisionEffect>();

            visionEffect.Configure(VisionFlagsFor(leftEyeItem), VisionFlagsFor(rightEyeItem));
        }
    }

    static ItemVisionEffect.EyeVisionFlags VisionFlagsFor(ItemData eye)
    {
        return new ItemVisionEffect.EyeVisionFlags
        {
            flipVertical = eye != null && eye.HasEffect(ItemEffectType.VerticalViewFlip),
            pixelated = eye != null && eye.HasEffect(ItemEffectType.PixelatedView),
            inverted = eye != null && eye.HasEffect(ItemEffectType.InvertedView),
            buttonView = eye != null && eye.HasEffect(ItemEffectType.ButtonView),
        };
    }

    // 해당 팔(왼팔/오른팔)에 실제로 장착된 아이템. 좌우 슬롯은 서로 독립적이다.
    public ItemData GetArmItem(bool leftArm)
    {
        return leftArm ? leftArmItem : rightArmItem;
    }

    // 장착된 팔 아이템의 효과로 무기 종류를 판별 (스윙 모션/이펙트 분기용).
    public ArmWeaponKind GetArmWeaponKind(bool leftArm)
    {
        ItemData arm = GetArmItem(leftArm);
        if (arm == null)
            return ArmWeaponKind.Fist;
        if (arm.HasEffect(ItemEffectType.AxeAttack))
            return ArmWeaponKind.Axe;
        if (arm.HasEffect(ItemEffectType.KeyringProjectile))
            return ArmWeaponKind.Keyring;
        if (arm.HasEffect(ItemEffectType.NailProjectile))
            return ArmWeaponKind.Nail;
        if (arm.HasEffect(ItemEffectType.StarBurst))
            return ArmWeaponKind.Star;
        return ArmWeaponKind.Fist;
    }

    public float ModifiedAttackDamage(bool leftArm, float baseDamage)
    {
        float bonus = leftArm ? leftArmDamageBonus : rightArmDamageBonus;
        ItemData arm = GetArmItem(leftArm);
        if (arm != null && arm.HasEffect(ItemEffectType.AxeAttack))
        {
            ItemEffectData axe = arm.GetEffect(ItemEffectType.AxeAttack);
            bonus += axe != null ? axe.value : 0f;
        }
        return Mathf.Max(0f, baseDamage + bonus);
    }

    public float AttackSizeMultiplier(bool leftArm)
    {
        ItemData arm = GetArmItem(leftArm);
        if (arm == null || !arm.HasEffect(ItemEffectType.AxeAttack))
            return 1f;
        ItemEffectData axe = arm.GetEffect(ItemEffectType.AxeAttack);
        return axe != null && axe.duration > 0f ? Mathf.Max(1f, axe.duration) : 1.45f;
    }

    public bool TryPerformProjectileAttack(bool leftArm, Vector2 direction, Vector3 origin)
    {
        ItemData arm = GetArmItem(leftArm);
        if (arm == null)
            return false;

        ItemEffectData keyring = arm.GetEffect(ItemEffectType.KeyringProjectile);
        if (keyring != null)
        {
            int damage = Mathf.Max(1, Mathf.RoundToInt(ModifiedAttackDamage(leftArm, Mathf.Max(1f, keyring.value))));
            // 열쇠 스프라이트는 날(이빨) 부분이 그림 오른쪽에 있어 별도 회전 보정 없이도 날이 진행 방향을 향한다.
            ItemProjectile.Spawn(origin, direction, 10f, damage, 1.6f, 0.24f, true, arm.PlaceholderColor, ItemPlaceholderShape.Diamond, arm.ProjectileSprite, 0f);
            return true;
        }

        ItemEffectData nail = arm.GetEffect(ItemEffectType.NailProjectile);
        if (nail != null)
        {
            int damage = Mathf.Max(1, Mathf.RoundToInt(ModifiedAttackDamage(leftArm, Mathf.Max(1f, nail.value))));
            ItemProjectile.Spawn(origin, direction, 12f, damage, 1.4f, 0.11f, false, arm.PlaceholderColor, ItemPlaceholderShape.Diamond, arm.ProjectileSprite);
            return true;
        }

        ItemEffectData star = arm.GetEffect(ItemEffectType.StarBurst);
        if (star != null)
        {
            // 샷건처럼 짧은 사거리로 3방향(부채꼴)에 퍼지는 별 투사체 3개. 계속 회전하며 날아간다.
            int damage = Mathf.Max(1, Mathf.RoundToInt(ModifiedAttackDamage(leftArm, Mathf.Max(1f, star.value))));
            float lifetime = star.duration > 0f ? star.duration : 0.5f;
            const float spreadDegrees = 15f;
            const float spinDegreesPerSecond = 540f;
            Sprite[] sprites = { arm.ProjectileSprite, arm.ProjectileSprite2, arm.ProjectileSprite3 };
            for (int i = -1; i <= 1; i++)
            {
                Vector2 spreadDir = Quaternion.Euler(0f, 0f, i * spreadDegrees) * direction;
                ItemProjectile.Spawn(origin, spreadDir, 9f, damage, lifetime, 0.16f, false, arm.PlaceholderColor, ItemPlaceholderShape.Diamond, sprites[i + 1], 0f, spinDegreesPerSecond);
            }
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
