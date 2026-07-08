using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerAttack : MonoBehaviour
{
    enum AttackArm
    {
        Left,
        Right
    }

    // 팔에 장착된 아이템(ArmWeaponKind)에 따라 스윙 모션과 이펙트 색을 다르게 준다.
    struct ArmAttackStyle
    {
        public float durationScale;
        public float arcHeightScale;
        public float rotationScale;
        public float fistScaleMultiplier;
        public Color tint;
        public Sprite spriteOverride;
        // true면 주먹/무기 스윙 연출 없이 투사체만 발사한다 (나무판자 등 순수 원거리 무기용).
        public bool skipSwing;
        // 슬래시 이펙트 크기 배율. 도끼처럼 확실히 휘둘렀다는 느낌을 주고 싶은 무기는 1보다 크게 준다.
        public float slashScaleMultiplier;
        // 무기 스프라이트가 기본 주먹과 다른 방향으로 그려져 있을 때 보정하는 회전값(도).
        public float spriteRotationOffsetDegrees;
        public bool preserveSpriteAspect;
    }

    [Header("Direction")]
    [SerializeField] PlayerController playerController;

    [Header("Renderers")]
    [SerializeField] SpriteRenderer bodyRenderer;
    [SerializeField] SpriteRenderer leftArmRenderer;
    [SerializeField] SpriteRenderer rightArmRenderer;

    [Header("Fist")]
    [SerializeField] Sprite fistSprite;
    [SerializeField] SpriteRenderer fistPrefab;

    [Header("Weapon Sprites (비워두면 기본 주먹 사용, 나중에 교체용)")]
    [SerializeField] Sprite axeWeaponSprite;
    [SerializeField] Sprite keyringWeaponSprite;
    [SerializeField] Sprite nailWeaponSprite;
    [SerializeField] Sprite starWeaponSprite;
    [SerializeField] Sprite sunflowerWeaponSprite;
    [SerializeField] Transform leftAttackStart;
    [SerializeField] Transform rightAttackStart;
    [SerializeField] bool useFixedBodyAttackOrigin = true;
    [FormerlySerializedAs("attackRange")]
    [SerializeField, Min(0f)] float attackDistance = 1.4f;
    [FormerlySerializedAs("flashDuration")]
    [SerializeField, Range(0.05f, 0.5f)] float attackDuration = 0.15f;
    [SerializeField, Min(0f)] float swingArcHeight = 0.22f;
    [SerializeField] float swingRotation = 70f;
    [SerializeField] float fistRotationOffset;
    [SerializeField] Vector2 fistScale = new Vector2(1.65f, 1.65f);
    [FormerlySerializedAs("sideAttackStartOffset")]
    [SerializeField, Min(0f)] float attackStartOffset = 0.22f;
    [FormerlySerializedAs("sideAttackDistanceBonus")]
    [SerializeField, Min(0f)] float attackDistanceBonus = 0.2f;
    [SerializeField, Min(0f)] float verticalAttackDistanceBonus = 0.32f;

    [Header("Trail")]
    [SerializeField] bool useTrailEffect = true;
    [SerializeField, Min(0.01f)] float trailSpawnInterval = 0.025f;
    [SerializeField, Range(0.02f, 0.5f)] float trailLifetime = 0.12f;
    [SerializeField, Range(0f, 1f)] float trailStartAlpha = 0.32f;
    [SerializeField] int prewarmTrailCount = 6;

    [Header("Red Slash")]
    [SerializeField] bool useRedSlashEffect = true;
    [SerializeField, Min(0.01f)] float slashSpawnInterval = 0.05f;
    [SerializeField, Range(0.02f, 0.5f)] float slashLifetime = 0.10f;
    [SerializeField] Color slashColor = new Color(1f, 0.08f, 0.02f, 0.42f);
    [SerializeField] Vector2 slashScale = new Vector2(1.3f, 0.45f);
    [SerializeField] int prewarmSlashCount = 8;

    [Header("Sorting")]
    [SerializeField] int frontAndSideAttackSortingOrder = 10;
    [SerializeField] int backAttackSortingOrder = -1;
    [SerializeField] int fistSortingOrderOffset = 1;
    [SerializeField] int effectSortingOrderOffset = -1;

    [Header("Hit")]
    [SerializeField] int attackDamage = 1;
    [SerializeField] float attackCooldown = 0.3f;
    // 도끼는 무겁게 느껴지도록 팔당 별도의 긴 재사용대기시간을 쓴다 (반대팔은 기존 속도 유지).
    [SerializeField, Min(0f)] float axeAttackCooldown = 2f;
    [SerializeField, Min(0f)] float starAttackCooldown = 1.25f;
    [SerializeField, Min(0f)] float sunflowerAttackCooldown = 1f;
    [SerializeField] Vector2 attackSize = new Vector2(1f, 1f);
    [SerializeField, Min(0f)] float hitAreaExtraLength = 0.35f;
    [SerializeField] float attackFacingLockDuration = 0.25f;

    [Header("Missing Arm Input")]
    [SerializeField, Min(0f)] float pressTimeout = 0.5f;
    [SerializeField, Min(1)] int requiredPressCount = 3;

    static readonly Key[] DirKeys = { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow };
    static readonly Vector2[] DirVectors = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    readonly Queue<SpriteRenderer> trailPool = new Queue<SpriteRenderer>();
    readonly List<SpriteRenderer> activeTrails = new List<SpriteRenderer>();
    readonly Queue<SpriteRenderer> slashPool = new Queue<SpriteRenderer>();
    readonly List<SpriteRenderer> activeSlashes = new List<SpriteRenderer>();

    float leftArmCooldownTimer;
    float rightArmCooldownTimer;
    float pendingKeyTimer;
    Key pendingAttackKey = Key.None;
    int pendingPressCount;
    bool isAttacking;
    bool nextAttackUsesLeftArm = true;
    float leftArmBlockedUntil;
    float rightArmBlockedUntil;
    Vector2 lastFacingDirection = Vector2.down;
    SpriteRenderer fistRenderer;
    SpriteRenderer suppressedArmRenderer;
    bool suppressedArmWasEnabled;
    PlayerItemEffects itemEffects;

    void Awake()
    {
        ResolveReferences();
        EnsureFistRenderer();
        PrewarmTrails();
        PrewarmSlashes();
    }

    void Start()
    {
        ResolveReferences();
        PlayerManager.Instance?.ApplyTo(gameObject);
    }

    public void ApplyPlayerManagerSettings(
        int newAttackDamage,
        float newAttackCooldown,
        float newAttackDistance,
        float newAttackDistanceBonus,
        float newVerticalAttackDistanceBonus,
        Vector2 newAttackSize,
        float newAttackDuration,
        Vector2 newFistScale)
    {
        attackDamage = Mathf.Max(0, newAttackDamage);
        attackCooldown = Mathf.Max(0f, newAttackCooldown);
        attackDistance = Mathf.Max(0f, newAttackDistance);
        attackDistanceBonus = Mathf.Max(0f, newAttackDistanceBonus);
        verticalAttackDistanceBonus = Mathf.Max(0f, newVerticalAttackDistanceBonus);
        attackSize = new Vector2(Mathf.Max(0f, newAttackSize.x), Mathf.Max(0f, newAttackSize.y));
        attackDuration = Mathf.Clamp(newAttackDuration, 0.05f, 0.5f);
        fistScale = new Vector2(Mathf.Max(0f, newFistScale.x), Mathf.Max(0f, newFistScale.y));
    }

    public void ApplyTemporaryArmBlock(BodySlot slot, float duration)
    {
        float blockedUntil = Time.time + Mathf.Max(0f, duration);
        if (slot == BodySlot.ArmLeft)
            leftArmBlockedUntil = Mathf.Max(leftArmBlockedUntil, blockedUntil);
        else if (slot == BodySlot.ArmRight)
            rightArmBlockedUntil = Mathf.Max(rightArmBlockedUntil, blockedUntil);
    }

    void Update()
    {
        ResolveReferences();

        if (playerController != null)
            lastFacingDirection = playerController.FacingVector;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        leftArmCooldownTimer -= Time.deltaTime;
        rightArmCooldownTimer -= Time.deltaTime;
        pendingKeyTimer -= Time.deltaTime;
        if (pendingKeyTimer <= 0f)
        {
            pendingAttackKey = Key.None;
            pendingPressCount = 0;
        }

        // 팔별 재사용대기시간은 BeginAttack에서 개별 확인한다 (도끼 쪽 팔만 느려도 반대팔은 계속 공격 가능).
        if (isAttacking)
            return;

        // 팔이 하나든 둘이든 한 번 누르면 바로 공격 (연타 불필요)
        bool needsMultiplePress = false;

        for (int i = 0; i < DirKeys.Length; i++)
        {
            if (!keyboard[DirKeys[i]].wasPressedThisFrame && !keyboard[DirKeys[i]].isPressed)
                continue;

            Vector2 attackDirection = DirVectors[i];
            lastFacingDirection = attackDirection;
            playerController?.FaceDirection(attackDirection, attackFacingLockDuration);

            if (needsMultiplePress)
            {
                if (pendingAttackKey != DirKeys[i])
                {
                    pendingAttackKey = DirKeys[i];
                    pendingPressCount = 1;
                    pendingKeyTimer = pressTimeout;
                }
                else
                {
                    pendingPressCount++;
                    pendingKeyTimer = pressTimeout;
                    if (pendingPressCount >= requiredPressCount)
                    {
                        BeginAttack(attackDirection);
                        ResetPendingAttack();
                        break;
                    }
                }

                break;
            }

            BeginAttack(attackDirection);
            ResetPendingAttack();
            break;
        }
    }

    void LateUpdate()
    {
        if (isAttacking && suppressedArmRenderer != null)
            suppressedArmRenderer.enabled = false;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        RestoreSuppressedArm();
        isAttacking = false;

        if (fistRenderer != null)
            fistRenderer.gameObject.SetActive(false);

        for (int i = 0; i < activeTrails.Count; i++)
        {
            SpriteRenderer trail = activeTrails[i];
            if (trail == null)
                continue;

            trail.gameObject.SetActive(false);
            trailPool.Enqueue(trail);
        }

        activeTrails.Clear();

        for (int i = 0; i < activeSlashes.Count; i++)
        {
            SpriteRenderer slash = activeSlashes[i];
            if (slash == null)
                continue;

            slash.gameObject.SetActive(false);
            slashPool.Enqueue(slash);
        }

        activeSlashes.Clear();
    }

    void BeginAttack(Vector2 direction)
    {

        BodyState bodyState = BodyConditionUtility.CurrentState();
        bool hasLeft = bodyState == null || bodyState.armLeft;
        bool hasRight = bodyState == null || bodyState.armRight;

        // 양팔 다 떨어졌으면 공격 불가
        if (!hasLeft && !hasRight)
            return;

        bool leftReady = hasLeft && leftArmCooldownTimer <= 0f;
        bool rightReady = hasRight && rightArmCooldownTimer <= 0f;

        AttackArm arm;
        bool oneArmedOnly;
        if (hasLeft && hasRight)
        {
            // 도끼처럼 팔별 재사용대기시간이 다를 수 있어서, 선호하는(교대 순서상) 팔이
            // 아직 대기 중이면 반대쪽 준비된 팔로 대신 공격한다 (둘 다 대기 중이면 스킵).
            if (!leftReady && !rightReady)
                return;

            bool preferLeft = nextAttackUsesLeftArm;
            if (preferLeft && leftReady)
                arm = AttackArm.Left;
            else if (!preferLeft && rightReady)
                arm = AttackArm.Right;
            else
                arm = leftReady ? AttackArm.Left : AttackArm.Right;

            nextAttackUsesLeftArm = arm != AttackArm.Left; // 다음엔 방금 안 쓴 팔을 우선
            oneArmedOnly = false;
        }
        else
        {
            // 한 팔만 남으면 그 팔로만 공격 (없는 팔 차례는 건너뜀 → 모션도 안 나감)
            arm = hasLeft ? AttackArm.Left : AttackArm.Right;
            if (arm == AttackArm.Left && !leftReady)
                return;
            if (arm == AttackArm.Right && !rightReady)
                return;
            nextAttackUsesLeftArm = !hasLeft; // 나중에 반대 팔 되찾으면 그 팔부터
            oneArmedOnly = true;
        }

        bool leftArm = arm == AttackArm.Left;
        if (itemEffects == null)
            itemEffects = GetComponent<PlayerItemEffects>();
        ArmWeaponKind weaponKind = itemEffects != null ? itemEffects.GetArmWeaponKind(leftArm) : ArmWeaponKind.Fist;
        float baseCooldown = weaponKind == ArmWeaponKind.Axe ? axeAttackCooldown
            : weaponKind == ArmWeaponKind.Star ? starAttackCooldown
            : weaponKind == ArmWeaponKind.Sunflower ? sunflowerAttackCooldown
            : attackCooldown;

        // 한 팔만 남으면 없는 팔 차례(왼→[오 생략]→왼…)를 건너뛰는 만큼
        // 다음 공격까지 두 배로 기다린다 → 실효 공격 속도 절반
        float resolvedCooldown = oneArmedOnly ? baseCooldown * 2f : baseCooldown;

        if (leftArm)
            leftArmCooldownTimer = resolvedCooldown;
        else
            rightArmCooldownTimer = resolvedCooldown;

        StartCoroutine(AttackRoutine(NormalizedDirection(direction), arm));
    }

    bool IsArmBlocked(AttackArm arm)
    {
        return arm == AttackArm.Left
            ? Time.time < leftArmBlockedUntil
            : Time.time < rightArmBlockedUntil;
    }

    IEnumerator AttackRoutine(Vector2 direction, AttackArm arm)
    {
        isAttacking = true;
        SuppressArm(arm);

        if (itemEffects == null)
            itemEffects = GetComponent<PlayerItemEffects>();
        bool leftArm = arm == AttackArm.Left;
        ArmAttackStyle style = ResolveAttackStyle(leftArm);

        Vector3 hitStart = AttackStartPosition(arm, direction, AttackOrigin());
        Vector3 hitEnd = hitStart + (Vector3)(direction * EffectiveAttackDistance(direction));
        bool usedProjectile = itemEffects != null
            && itemEffects.TryPerformProjectileAttack(leftArm, direction, hitStart);
        if (!usedProjectile)
            DealDamage(hitStart, hitEnd, direction, arm);

        // 나무판자(못) 등 순수 원거리 무기는 주먹/무기 스윙 연출 없이 투사체만 발사하고 끝낸다.
        if (style.skipSwing)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, attackDuration * style.durationScale));
            FinishAttack();
            yield break;
        }

        SpriteRenderer fist = EnsureFistRenderer();
        if (fist == null || fist.sprite == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, attackDuration * style.durationScale));
            FinishAttack();
            yield break;
        }

        int fistSortingOrder = SortingOrderForDirection(direction);
        int effectSortingOrder = fistSortingOrder + effectSortingOrderOffset;
        ConfigureFistRenderer(fist, fistSortingOrder, style);

        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float armSign = leftArm ? 1f : -1f;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + fistRotationOffset + style.spriteRotationOffsetDegrees;
        float arcHeight = swingArcHeight * style.arcHeightScale;
        float rotationAmount = swingRotation * style.rotationScale;
        Vector3 fistRenderScale = fist.transform.localScale;
        Vector3 slashRenderScale = fistRenderScale * Mathf.Max(0.01f, style.slashScaleMultiplier);
        Color trailTint = style.tint;
        Color slashRenderColor = new Color(style.tint.r, style.tint.g, style.tint.b, slashColor.a);
        Sprite effectSprite = EffectiveFistSprite(style);
        float elapsed = 0f;
        float nextTrailTime = 0f;
        float nextSlashTime = 0f;
        float duration = Mathf.Max(0.01f, attackDuration * style.durationScale);

        fist.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 start = AttackStartPosition(arm, direction, AttackOrigin());
            Vector3 end = start + (Vector3)(direction * EffectiveAttackDistance(direction));
            ApplyFistPose(fist, start, end, perpendicular, armSign, baseAngle, t, arcHeight, rotationAmount);

            if (useTrailEffect && elapsed >= nextTrailTime)
            {
                SpawnTrail(fist.transform.position, fist.transform.rotation, effectSortingOrder, fistRenderScale, trailTint, effectSprite);
                nextTrailTime += trailSpawnInterval;
            }

            if (useRedSlashEffect && elapsed >= nextSlashTime)
            {
                SpawnSlash(fist.transform.position, fist.transform.rotation, effectSortingOrder, slashRenderScale, slashRenderColor, effectSprite, style.preserveSpriteAspect);
                nextSlashTime += slashSpawnInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalStart = AttackStartPosition(arm, direction, AttackOrigin());
        Vector3 finalEnd = finalStart + (Vector3)(direction * EffectiveAttackDistance(direction));
        ApplyFistPose(fist, finalStart, finalEnd, perpendicular, armSign, baseAngle, 1f, arcHeight, rotationAmount);
        if (useTrailEffect)
            SpawnTrail(fist.transform.position, fist.transform.rotation, effectSortingOrder, fistRenderScale, trailTint, effectSprite);
        if (useRedSlashEffect)
            SpawnSlash(fist.transform.position, fist.transform.rotation, effectSortingOrder, slashRenderScale, slashRenderColor, effectSprite, style.preserveSpriteAspect);

        fist.gameObject.SetActive(false);
        FinishAttack();
    }

    // 장착된 팔 아이템(주먹/도끼/열쇠고리/못)에 따라 스윙 모션·크기·색을 결정한다.
    ArmAttackStyle ResolveAttackStyle(bool leftArm)
    {
        ArmWeaponKind kind = itemEffects != null ? itemEffects.GetArmWeaponKind(leftArm) : ArmWeaponKind.Fist;

        switch (kind)
        {
            case ArmWeaponKind.Axe:
                Sprite axeSprite = ArmItemSprite(leftArm, axeWeaponSprite);
                return new ArmAttackStyle
                {
                    durationScale = 1.35f,
                    arcHeightScale = 1.6f,
                    rotationScale = 1.5f,
                    fistScaleMultiplier = 0.72f,
                    tint = axeSprite != null ? Color.white : ArmItemColor(leftArm),
                    spriteOverride = axeSprite,
                    slashScaleMultiplier = 1f,
                    // axe2.png는 자루(갈색)가 그림 오른쪽(진행 방향 쪽)에 그려져 있어, 보정 없이 회전하면
                    // 자루가 플레이어 반대쪽(적 쪽)을 향해 완전히 뒤집혀 보인다. 180도 돌려서 자루가
                    // 플레이어 쪽, 도끼날이 진행 방향(적 쪽)을 향하도록 바로잡는다.
                    spriteRotationOffsetDegrees = 180f,
                    preserveSpriteAspect = true,
                };
            case ArmWeaponKind.Keyring:
                return new ArmAttackStyle
                {
                    durationScale = 0.6f,
                    arcHeightScale = 0.45f,
                    rotationScale = 0.55f,
                    fistScaleMultiplier = 0.85f,
                    tint = keyringWeaponSprite != null ? Color.white : ArmItemColor(leftArm),
                    spriteOverride = keyringWeaponSprite,
                    slashScaleMultiplier = 1f,
                };
            case ArmWeaponKind.Nail:
                return new ArmAttackStyle
                {
                    durationScale = 0.7f,
                    arcHeightScale = 0.4f,
                    rotationScale = 0.65f,
                    fistScaleMultiplier = 0.8f,
                    tint = nailWeaponSprite != null ? Color.white : ArmItemColor(leftArm),
                    spriteOverride = nailWeaponSprite,
                    skipSwing = true,
                    slashScaleMultiplier = 1f,
                };
            case ArmWeaponKind.Star:
                return new ArmAttackStyle
                {
                    durationScale = 0.55f,
                    arcHeightScale = 0.4f,
                    rotationScale = 0.5f,
                    fistScaleMultiplier = 0.8f,
                    tint = starWeaponSprite != null ? Color.white : ArmItemColor(leftArm),
                    spriteOverride = starWeaponSprite,
                    slashScaleMultiplier = 1f,
                };
            case ArmWeaponKind.Sunflower:
                return new ArmAttackStyle
                {
                    durationScale = 0.7f,
                    arcHeightScale = 0.4f,
                    rotationScale = 0.65f,
                    fistScaleMultiplier = 0.8f,
                    tint = sunflowerWeaponSprite != null ? Color.white : ArmItemColor(leftArm),
                    spriteOverride = sunflowerWeaponSprite,
                    skipSwing = true,
                    slashScaleMultiplier = 1f,
                };
            default:
                return new ArmAttackStyle
                {
                    durationScale = 1f,
                    arcHeightScale = 1f,
                    rotationScale = 1f,
                    fistScaleMultiplier = 1f,
                    tint = Color.white,
                    spriteOverride = null,
                    slashScaleMultiplier = 1f,
                };
        }
    }

    Sprite ArmItemSprite(bool leftArm, Sprite fallback)
    {
        ItemData armItemData = itemEffects != null ? itemEffects.GetArmItem(leftArm) : null;
        return armItemData != null && armItemData.Sprite != null ? armItemData.Sprite : fallback;
    }

    Color ArmItemColor(bool leftArm)
    {
        ItemData armItemData = itemEffects != null ? itemEffects.GetArmItem(leftArm) : null;
        return armItemData != null ? armItemData.PlaceholderColor : Color.white;
    }

    // 무기 전용 스프라이트가 없으면 기본 주먹 스프라이트로 대체 (교체용 슬롯이 비어있어도 항상 동작).
    Sprite EffectiveFistSprite(ArmAttackStyle style)
    {
        return style.spriteOverride != null ? style.spriteOverride : fistSprite;
    }

    void ApplyFistPose(
        SpriteRenderer fist,
        Vector3 start,
        Vector3 end,
        Vector2 perpendicular,
        float armSign,
        float baseAngle,
        float t,
        float arcHeight,
        float rotationAmount)
    {
        float eased = EaseOut(t);
        Vector3 position = Vector3.Lerp(start, end, eased);
        position += (Vector3)(perpendicular * (Mathf.Sin(t * Mathf.PI) * arcHeight * armSign));
        fist.transform.position = position;

        float rotation = Mathf.Lerp(-rotationAmount * armSign, rotationAmount * 0.35f * armSign, eased);
        fist.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + rotation);
    }

    void FinishAttack()
    {
        RestoreSuppressedArm();
        isAttacking = false;
    }

    void SuppressArm(AttackArm arm)
    {
        suppressedArmRenderer = arm == AttackArm.Left ? leftArmRenderer : rightArmRenderer;
        suppressedArmWasEnabled = suppressedArmRenderer != null && suppressedArmRenderer.enabled;

        if (suppressedArmRenderer != null)
            suppressedArmRenderer.enabled = false;
    }

    void RestoreSuppressedArm()
    {
        if (suppressedArmRenderer != null)
            suppressedArmRenderer.enabled = suppressedArmWasEnabled;

        suppressedArmRenderer = null;
        suppressedArmWasEnabled = false;
    }

    void DealDamage(Vector3 start, Vector3 end, Vector2 direction, AttackArm arm)
    {
        Vector2 origin = (start + end) * 0.5f;
        Vector2 hitSize = AttackHitSize(direction, Vector2.Distance(start, end));
        if (itemEffects == null)
            itemEffects = GetComponent<PlayerItemEffects>();
        if (itemEffects != null)
            hitSize *= itemEffects.AttackSizeMultiplier(arm == AttackArm.Left);

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, hitSize, 0f);
        HashSet<EnemyBase> damaged = new HashSet<EnemyBase>();
        float damage = attackDamage;
        if (itemEffects != null)
            damage = itemEffects.ModifiedAttackDamage(arm == AttackArm.Left, damage);
        foreach (Collider2D hit in hits)
        {
            EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
            if (enemy != null && damaged.Add(enemy))
                enemy.TakeDamage(damage);
        }
        if (damaged.Count > 0)
            SoundManager.PlayPunch();
    }

    SpriteRenderer EnsureFistRenderer()
    {
        if (fistRenderer != null)
            return fistRenderer;

        if (fistPrefab != null)
        {
            fistRenderer = Instantiate(fistPrefab, transform);
            fistRenderer.name = "PlayerAttack_Fist";
        }
        else
        {
            GameObject fistObject = new GameObject("PlayerAttack_Fist");
            fistObject.transform.SetParent(transform, false);
            fistRenderer = fistObject.AddComponent<SpriteRenderer>();
        }

        if (fistRenderer.sprite == null)
            fistRenderer.sprite = fistSprite;

        fistRenderer.gameObject.SetActive(false);
        return fistRenderer;
    }

    void ConfigureFistRenderer(SpriteRenderer renderer, int sortingOrder, ArmAttackStyle style)
    {
        Sprite sprite = EffectiveFistSprite(style);
        renderer.sprite = sprite != null ? sprite : renderer.sprite;
        renderer.color = style.tint;
        if (style.preserveSpriteAspect)
        {
            float uniformScale = Mathf.Max(Mathf.Abs(fistScale.x), Mathf.Abs(fistScale.y)) * style.fistScaleMultiplier;
            renderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }
        else
        {
            renderer.transform.localScale = new Vector3(
                fistScale.x * style.fistScaleMultiplier,
                fistScale.y * style.fistScaleMultiplier,
                1f);
        }

        SpriteRenderer sortingSource = bodyRenderer != null ? bodyRenderer : renderer;
        renderer.sortingLayerID = sortingSource.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
    }

    void PrewarmTrails()
    {
        int count = Mathf.Max(0, prewarmTrailCount);
        for (int i = 0; i < count; i++)
            trailPool.Enqueue(CreateTrailRenderer());
    }

    void PrewarmSlashes()
    {
        int count = Mathf.Max(0, prewarmSlashCount);
        for (int i = 0; i < count; i++)
            slashPool.Enqueue(CreateSlashRenderer());
    }

    void SpawnTrail(Vector3 position, Quaternion rotation, int sortingOrder, Vector3 scale, Color tint, Sprite sprite)
    {
        SpriteRenderer trail = trailPool.Count > 0 ? trailPool.Dequeue() : CreateTrailRenderer();
        trail.sprite = sprite != null ? sprite : (fistRenderer != null ? fistRenderer.sprite : null);
        if (trail.sprite == null)
        {
            trailPool.Enqueue(trail);
            return;
        }

        SpriteRenderer sortingSource = bodyRenderer != null ? bodyRenderer : trail;
        trail.sortingLayerID = sortingSource.sortingLayerID;
        trail.sortingOrder = sortingOrder;
        trail.transform.position = position;
        trail.transform.rotation = rotation;
        trail.transform.localScale = scale;
        trail.color = new Color(tint.r, tint.g, tint.b, trailStartAlpha);
        trail.gameObject.SetActive(true);
        activeTrails.Add(trail);

        StartCoroutine(FadeTrail(trail));
    }

    IEnumerator FadeTrail(SpriteRenderer trail)
    {
        float duration = Mathf.Max(0.01f, trailLifetime);
        float elapsed = 0f;
        Color color = trail.color;

        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(trailStartAlpha, 0f, elapsed / duration);
            trail.color = new Color(color.r, color.g, color.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        trail.color = new Color(color.r, color.g, color.b, 0f);
        trail.gameObject.SetActive(false);
        activeTrails.Remove(trail);
        trailPool.Enqueue(trail);
    }

    void SpawnSlash(Vector3 position, Quaternion rotation, int sortingOrder, Vector3 fistRenderScale, Color color, Sprite sprite, bool preserveSpriteAspect = false)
    {
        SpriteRenderer slash = slashPool.Count > 0 ? slashPool.Dequeue() : CreateSlashRenderer();
        slash.sprite = sprite != null ? sprite : (fistRenderer != null ? fistRenderer.sprite : null);
        if (slash.sprite == null)
        {
            slashPool.Enqueue(slash);
            return;
        }

        SpriteRenderer sortingSource = bodyRenderer != null ? bodyRenderer : slash;
        slash.sortingLayerID = sortingSource.sortingLayerID;
        slash.sortingOrder = sortingOrder;
        slash.transform.position = position;
        slash.transform.rotation = rotation;
        slash.transform.localScale = preserveSpriteAspect
            ? new Vector3(fistRenderScale.x, fistRenderScale.y, 1f)
            : new Vector3(fistRenderScale.x * slashScale.x, fistRenderScale.y * slashScale.y, 1f);
        slash.color = color;
        slash.gameObject.SetActive(true);
        activeSlashes.Add(slash);

        StartCoroutine(FadeSlash(slash, color));
    }

    IEnumerator FadeSlash(SpriteRenderer slash, Color color)
    {
        float duration = Mathf.Max(0.01f, slashLifetime);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(color.a, 0f, elapsed / duration);
            slash.color = new Color(color.r, color.g, color.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        slash.color = new Color(color.r, color.g, color.b, 0f);
        slash.gameObject.SetActive(false);
        activeSlashes.Remove(slash);
        slashPool.Enqueue(slash);
    }

    SpriteRenderer CreateTrailRenderer()
    {
        GameObject trailObject = new GameObject("PlayerAttack_FistTrail");
        trailObject.transform.SetParent(transform, false);
        SpriteRenderer trail = trailObject.AddComponent<SpriteRenderer>();
        trail.gameObject.SetActive(false);
        return trail;
    }

    SpriteRenderer CreateSlashRenderer()
    {
        GameObject slashObject = new GameObject("PlayerAttack_RedSlash");
        slashObject.transform.SetParent(transform, false);
        SpriteRenderer slash = slashObject.AddComponent<SpriteRenderer>();
        slash.gameObject.SetActive(false);
        return slash;
    }

    Vector3 AttackStartPosition(AttackArm arm, Vector2 direction, Vector3 fixedOrigin)
    {
        if (useFixedBodyAttackOrigin)
            return fixedOrigin + (Vector3)(direction * attackStartOffset);

        Transform startTransform = arm == AttackArm.Left ? leftAttackStart : rightAttackStart;
        if (startTransform != null)
            return ApplyStartOffset(startTransform.position, direction);

        SpriteRenderer armRenderer = arm == AttackArm.Left ? leftArmRenderer : rightArmRenderer;
        if (armRenderer != null)
            return ApplyStartOffset(armRenderer.transform.position, direction);

        return ApplyStartOffset(transform.position, direction);
    }

    Vector3 AttackOrigin()
    {
        if (useFixedBodyAttackOrigin && bodyRenderer != null)
        {
            Vector3 origin = bodyRenderer.bounds.center;
            origin.z = transform.position.z;
            return origin;
        }

        return transform.position;
    }

    Vector3 ApplyStartOffset(Vector3 start, Vector2 direction)
    {
        return start + (Vector3)(direction * attackStartOffset);
    }

    float EffectiveAttackDistance(Vector2 direction)
    {
        float distance = attackDistance + attackDistanceBonus;
        if (!IsSideDirection(direction))
            distance += verticalAttackDistanceBonus;

        return distance;
    }

    bool IsSideDirection(Vector2 direction)
    {
        return Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
    }

    Vector2 AttackHitSize(Vector2 direction, float swingDistance)
    {
        Vector2 hitSize = attackSize;
        float pathLength = swingDistance + hitAreaExtraLength;

        if (IsSideDirection(direction))
            hitSize.x = Mathf.Max(hitSize.x, pathLength);
        else
            hitSize.y = Mathf.Max(hitSize.y, pathLength);

        return hitSize;
    }

    int SortingOrderForDirection(Vector2 direction)
    {
        if (bodyRenderer == null)
            return direction.y > Mathf.Abs(direction.x) ? backAttackSortingOrder : frontAndSideAttackSortingOrder;

        if (direction.y > Mathf.Abs(direction.x))
            return Mathf.Min(backAttackSortingOrder, bodyRenderer.sortingOrder - 1);

        return Mathf.Max(frontAndSideAttackSortingOrder, bodyRenderer.sortingOrder + 1) + fistSortingOrderOffset;
    }

    Vector2 NormalizedDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (lastFacingDirection.sqrMagnitude > 0.001f)
            return lastFacingDirection.normalized;

        return Vector2.down;
    }

    float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
    }

    void ResetPendingAttack()
    {
        pendingAttackKey = Key.None;
        pendingPressCount = 0;
        pendingKeyTimer = 0f;
    }

    void ResolveReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (bodyRenderer == null)
            bodyRenderer = playerController != null ? playerController.BodyRenderer : GetComponent<SpriteRenderer>();

        if (leftArmRenderer == null && playerController != null)
            leftArmRenderer = playerController.LeftArmRenderer;

        if (rightArmRenderer == null && playerController != null)
            rightArmRenderer = playerController.RightArmRenderer;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (Vector2 direction in DirVectors)
        {
            Vector3 start = AttackOrigin() + (Vector3)(direction * attackStartOffset);
            Vector3 end = start + (Vector3)(direction * EffectiveAttackDistance(direction));
            Gizmos.DrawWireCube((start + end) * 0.5f, AttackHitSize(direction, Vector2.Distance(start, end)));
        }

        Gizmos.color = Color.yellow;
        DrawStartGizmo(leftAttackStart, leftArmRenderer);
        DrawStartGizmo(rightAttackStart, rightArmRenderer);
    }

    void DrawStartGizmo(Transform startTransform, SpriteRenderer fallbackRenderer)
    {
        Vector3 position = transform.position;
        if (startTransform != null)
            position = startTransform.position;
        else if (fallbackRenderer != null)
            position = fallbackRenderer.transform.position;

        Gizmos.DrawWireSphere(position, 0.06f);
    }
}
