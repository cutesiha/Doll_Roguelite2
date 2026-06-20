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

    [Header("Direction")]
    [SerializeField] PlayerController playerController;

    [Header("Renderers")]
    [SerializeField] SpriteRenderer bodyRenderer;
    [SerializeField] SpriteRenderer leftArmRenderer;
    [SerializeField] SpriteRenderer rightArmRenderer;

    [Header("Fist")]
    [SerializeField] Sprite fistSprite;
    [SerializeField] SpriteRenderer fistPrefab;
    [SerializeField] Transform leftAttackStart;
    [SerializeField] Transform rightAttackStart;
    [SerializeField] bool useFixedBodyAttackOrigin = true;
    [FormerlySerializedAs("attackRange")]
    [SerializeField, Min(0f)] float attackDistance = 1.0f;
    [FormerlySerializedAs("flashDuration")]
    [SerializeField, Range(0.05f, 0.5f)] float attackDuration = 0.15f;
    [SerializeField, Min(0f)] float swingArcHeight = 0.22f;
    [SerializeField] float swingRotation = 70f;
    [SerializeField] float fistRotationOffset;
    [SerializeField] Vector2 fistScale = new Vector2(1.65f, 1.65f);
    [FormerlySerializedAs("sideAttackStartOffset")]
    [SerializeField, Min(0f)] float attackStartOffset = 0.22f;
    [FormerlySerializedAs("sideAttackDistanceBonus")]
    [SerializeField, Min(0f)] float attackDistanceBonus = 0.4f;
    [SerializeField, Min(0f)] float verticalAttackDistanceBonus = 0.12f;

    [Header("Trail")]
    [SerializeField, Min(0.01f)] float trailSpawnInterval = 0.025f;
    [SerializeField, Range(0.02f, 0.5f)] float trailLifetime = 0.12f;
    [SerializeField, Range(0f, 1f)] float trailStartAlpha = 0.32f;
    [SerializeField] int prewarmTrailCount = 6;

    [Header("Red Slash")]
    [SerializeField] bool useRedSlashEffect = true;
    [SerializeField, Min(0.01f)] float slashSpawnInterval = 0.018f;
    [SerializeField, Range(0.02f, 0.5f)] float slashLifetime = 0.10f;
    [SerializeField] Color slashColor = new Color(1f, 0.08f, 0.02f, 0.42f);
    [SerializeField] Vector2 slashScale = new Vector2(2.0f, 0.72f);
    [SerializeField] int prewarmSlashCount = 8;

    [Header("Sorting")]
    [SerializeField] int frontAndSideAttackSortingOrder = 10;
    [SerializeField] int backAttackSortingOrder = -1;
    [SerializeField] int fistSortingOrderOffset = 1;
    [SerializeField] int effectSortingOrderOffset = -1;

    [Header("Hit")]
    [SerializeField] int attackDamage = 1;
    [SerializeField] float attackCooldown = 0.3f;
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

    float cooldownTimer;
    float pendingKeyTimer;
    Key pendingAttackKey = Key.None;
    int pendingPressCount;
    bool isAttacking;
    bool nextAttackUsesLeftArm = true;
    Vector2 lastFacingDirection = Vector2.down;
    SpriteRenderer fistRenderer;
    SpriteRenderer suppressedArmRenderer;
    bool suppressedArmWasEnabled;

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

    void Update()
    {
        ResolveReferences();

        if (playerController != null)
            lastFacingDirection = playerController.FacingVector;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        cooldownTimer -= Time.deltaTime;
        pendingKeyTimer -= Time.deltaTime;
        if (pendingKeyTimer <= 0f)
        {
            pendingAttackKey = Key.None;
            pendingPressCount = 0;
        }

        if (cooldownTimer > 0f || isAttacking)
            return;

        BodyState bodyState = BodyConditionUtility.CurrentState();
        bool needsMultiplePress = bodyState != null && bodyState.armLeft != bodyState.armRight;

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
        AttackArm arm = nextAttackUsesLeftArm ? AttackArm.Left : AttackArm.Right;
        nextAttackUsesLeftArm = !nextAttackUsesLeftArm;
        cooldownTimer = attackCooldown;

        StartCoroutine(AttackRoutine(NormalizedDirection(direction), arm));
    }

    IEnumerator AttackRoutine(Vector2 direction, AttackArm arm)
    {
        isAttacking = true;
        SuppressArm(arm);

        SpriteRenderer fist = EnsureFistRenderer();
        if (fist == null || fist.sprite == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, attackDuration));
            FinishAttack();
            yield break;
        }

        int fistSortingOrder = SortingOrderForDirection(direction);
        int effectSortingOrder = fistSortingOrder + effectSortingOrderOffset;
        ConfigureFistRenderer(fist, fistSortingOrder);

        Vector3 hitStart = AttackStartPosition(arm, direction, AttackOrigin());
        Vector3 hitEnd = hitStart + (Vector3)(direction * EffectiveAttackDistance(direction));
        DealDamage(hitStart, hitEnd, direction);

        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float armSign = arm == AttackArm.Left ? 1f : -1f;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + fistRotationOffset;
        float elapsed = 0f;
        float nextTrailTime = 0f;
        float nextSlashTime = 0f;
        float duration = Mathf.Max(0.01f, attackDuration);

        fist.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 start = AttackStartPosition(arm, direction, AttackOrigin());
            Vector3 end = start + (Vector3)(direction * EffectiveAttackDistance(direction));
            ApplyFistPose(fist, start, end, perpendicular, armSign, baseAngle, t);

            if (elapsed >= nextTrailTime)
            {
                SpawnTrail(fist.transform.position, fist.transform.rotation, effectSortingOrder);
                nextTrailTime += trailSpawnInterval;
            }

            if (useRedSlashEffect && elapsed >= nextSlashTime)
            {
                SpawnSlash(fist.transform.position, fist.transform.rotation, effectSortingOrder);
                nextSlashTime += slashSpawnInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalStart = AttackStartPosition(arm, direction, AttackOrigin());
        Vector3 finalEnd = finalStart + (Vector3)(direction * EffectiveAttackDistance(direction));
        ApplyFistPose(fist, finalStart, finalEnd, perpendicular, armSign, baseAngle, 1f);
        SpawnTrail(fist.transform.position, fist.transform.rotation, effectSortingOrder);
        if (useRedSlashEffect)
            SpawnSlash(fist.transform.position, fist.transform.rotation, effectSortingOrder);

        fist.gameObject.SetActive(false);
        FinishAttack();
    }

    void ApplyFistPose(
        SpriteRenderer fist,
        Vector3 start,
        Vector3 end,
        Vector2 perpendicular,
        float armSign,
        float baseAngle,
        float t)
    {
        float eased = EaseOut(t);
        Vector3 position = Vector3.Lerp(start, end, eased);
        position += (Vector3)(perpendicular * (Mathf.Sin(t * Mathf.PI) * swingArcHeight * armSign));
        fist.transform.position = position;

        float rotation = Mathf.Lerp(-swingRotation * armSign, swingRotation * 0.35f * armSign, eased);
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

    void DealDamage(Vector3 start, Vector3 end, Vector2 direction)
    {
        Vector2 origin = (start + end) * 0.5f;
        Vector2 hitSize = AttackHitSize(direction, Vector2.Distance(start, end));

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, hitSize, 0f);
        foreach (Collider2D hit in hits)
        {
            EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
            if (enemy != null)
                enemy.TakeDamage(attackDamage);
        }
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

    void ConfigureFistRenderer(SpriteRenderer renderer, int sortingOrder)
    {
        renderer.sprite = fistSprite != null ? fistSprite : renderer.sprite;
        renderer.color = Color.white;
        renderer.transform.localScale = new Vector3(fistScale.x, fistScale.y, 1f);

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

    void SpawnTrail(Vector3 position, Quaternion rotation, int sortingOrder)
    {
        SpriteRenderer trail = trailPool.Count > 0 ? trailPool.Dequeue() : CreateTrailRenderer();
        trail.sprite = fistSprite != null ? fistSprite : (fistRenderer != null ? fistRenderer.sprite : null);
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
        trail.transform.localScale = new Vector3(fistScale.x, fistScale.y, 1f);
        trail.color = new Color(1f, 1f, 1f, trailStartAlpha);
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

    void SpawnSlash(Vector3 position, Quaternion rotation, int sortingOrder)
    {
        SpriteRenderer slash = slashPool.Count > 0 ? slashPool.Dequeue() : CreateSlashRenderer();
        slash.sprite = fistSprite != null ? fistSprite : (fistRenderer != null ? fistRenderer.sprite : null);
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
        slash.transform.localScale = new Vector3(fistScale.x * slashScale.x, fistScale.y * slashScale.y, 1f);
        slash.color = slashColor;
        slash.gameObject.SetActive(true);
        activeSlashes.Add(slash);

        StartCoroutine(FadeSlash(slash));
    }

    IEnumerator FadeSlash(SpriteRenderer slash)
    {
        float duration = Mathf.Max(0.01f, slashLifetime);
        float elapsed = 0f;
        Color color = slashColor;

        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(slashColor.a, 0f, elapsed / duration);
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
