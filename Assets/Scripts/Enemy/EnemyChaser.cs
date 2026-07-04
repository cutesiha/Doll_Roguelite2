using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser : EnemyBase
{
    [SerializeField] float moveSpeed = 1f;
    [Header("Button Slam")]
    [SerializeField] bool useButtonSlam = true;
    [SerializeField] Vector2 slamCooldownRange = new Vector2(3f, 5f);
    [SerializeField, Min(0.1f)] float slamTelegraphDuration = 1.35f;
    [SerializeField, Min(0.1f)] float slamRadius = 0.75f;
    [SerializeField, Min(1)] int slamDamage = 28;
    [SerializeField, Min(1)] int slamEdgeDamage = 40;
    [SerializeField, Range(0.1f, 1f)] float slamEdgeInnerRatio = 0.68f;
    [SerializeField, Min(0.05f)] float slamJumpDuration = 0.78f;
    [SerializeField, Min(0f)] float slamJumpArcHeight = 1.15f;
    [SerializeField, Min(0f)] float slamJumpScaleBoost = 0.26f;
    [SerializeField, Min(0.01f)] float slamSquashDuration = 0.32f;
    [SerializeField] Color slamTelegraphColor = new Color(1f, 0.12f, 0.08f, 0.28f);
    Rigidbody2D rb;
    Transform player;
    float nextSlamTime;
    bool isSlamming;

    public override EnemyKind Kind => EnemyKind.Chaser;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    protected override void Start()
    {
        base.Start();

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        ResetSlamTimer();
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
        base.ApplyProfile(profile);

        if (profile != null)
            moveSpeed = Mathf.Max(0f, profile.moveSpeed);
    }

    public override void ApplyCombatScaling(float speedMultiplier, float cooldownMultiplier, int extraDamage)
    {
        moveSpeed *= Mathf.Max(0.1f, speedMultiplier);
        slamCooldownRange = ScaleRange(slamCooldownRange, cooldownMultiplier, 0.6f);
        slamTelegraphDuration = Mathf.Max(0.55f, slamTelegraphDuration * Mathf.Lerp(1f, cooldownMultiplier, 0.45f));
        slamDamage += Mathf.Max(0, extraDamage);
        slamEdgeDamage += Mathf.Max(0, extraDamage);
    }

    void FixedUpdate()
    {
        if (player == null) return;
        if (isSlamming) return;
        if (TryMoveSpawnApproach()) return;

        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
    }

    protected override void Update()
    {
        base.Update();

        if (!useButtonSlam || isSlamming || player == null)
            return;

        if (Time.time >= nextSlamTime)
            StartCoroutine(SlamRoutine());
    }

    System.Collections.IEnumerator SlamRoutine()
    {
        isSlamming = true;
        Vector3 baseScale = transform.localScale;
        Vector2 targetPosition = player != null ? (Vector2)player.position : rb.position;
        float impactRadius = EffectiveSlamRadius();

        GameObject telegraph = TrackTelegraph(EnemyTelegraph.CreateCircle("ButtonSlamTelegraph", targetPosition, impactRadius, slamTelegraphColor, 70));
        yield return EnemyTelegraph.Blink(telegraph, 2, slamTelegraphDuration * 0.25f);

        float elapsed = 0f;
        while (elapsed < slamSquashDuration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slamSquashDuration));
            float stand = Mathf.Sin(t * Mathf.PI);
            transform.localScale = new Vector3(baseScale.x * (1f - stand * 0.18f), baseScale.y * (1f + stand * 0.38f), baseScale.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Collider2D[] cols = GetComponents<Collider2D>();
        bool[] wasTrigger = new bool[cols.Length];
        for (int i = 0; i < cols.Length; i++)
        {
            wasTrigger[i] = cols[i].isTrigger;
            cols[i].isTrigger = true;
        }

        // 점프 중(공중)에는 닿아도 접촉 데미지를 주지 않는다 — 착지 슬램만 데미지.
        SuppressContactDamage = true;

        yield return StartCoroutine(JumpToTarget(targetPosition, baseScale));

        // 착지: 슬램 데미지 적용 후 접촉 데미지 복구
        SuppressContactDamage = false;
        DealSlamDamage(targetPosition, impactRadius);

        for (int i = 0; i < cols.Length; i++)
            cols[i].isTrigger = wasTrigger[i];

        elapsed = 0f;
        while (elapsed < slamSquashDuration)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slamSquashDuration));
            float squash = Mathf.Sin(t * Mathf.PI);
            transform.localScale = new Vector3(baseScale.x * (1f + squash * 0.35f), baseScale.y * (1f - squash * 0.35f), baseScale.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = baseScale;
        if (telegraph != null)
            DestroyOwnedTelegraph(telegraph);

        ResetSlamTimer();
        isSlamming = false;
    }

    System.Collections.IEnumerator JumpToTarget(Vector2 targetPosition, Vector3 baseScale)
    {
        Vector2 startPosition = rb != null ? rb.position : (Vector2)transform.position;
        float startZ = transform.position.z;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slamJumpDuration);
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float easedMove = t * t * (3f - 2f * t);
            float jump = Mathf.Sin(t * Mathf.PI);
            Vector2 groundPosition = Vector2.Lerp(startPosition, targetPosition, easedMove);
            Vector2 nextPosition = groundPosition + Vector2.up * (jump * slamJumpArcHeight);
            if (rb != null)
                rb.MovePosition(nextPosition);
            else
                transform.position = new Vector3(nextPosition.x, nextPosition.y, startZ);

            transform.localScale = new Vector3(
                baseScale.x * (1f + jump * slamJumpScaleBoost * 0.45f),
                baseScale.y * (1f + jump * slamJumpScaleBoost),
                baseScale.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rb != null)
            rb.MovePosition(targetPosition);
        transform.position = new Vector3(targetPosition.x, targetPosition.y, startZ);
        transform.localScale = baseScale;
    }

    float EffectiveSlamRadius()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer == null)
            return slamRadius;

        Bounds bounds = renderer.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        return Mathf.Max(0.1f, radius > 0.01f ? radius : slamRadius);
    }

    void DealSlamDamage(Vector2 center, float radius)
    {
        PlayerDamageReceiver receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver == null)
            return;

        float distance = Vector2.Distance(receiver.transform.position, center);
        if (distance > radius)
            return;

        int damage = distance >= radius * slamEdgeInnerRatio ? slamEdgeDamage : slamDamage;
        receiver.TryTakePatternDamage(damage);
    }

    void ResetSlamTimer()
    {
        float min = Mathf.Max(0.1f, slamCooldownRange.x);
        float max = Mathf.Max(min, slamCooldownRange.y);
        nextSlamTime = Time.time + Random.Range(min, max);
    }

    static Vector2 ScaleRange(Vector2 range, float multiplier, float minValue)
    {
        float safe = Mathf.Max(0.1f, multiplier);
        float min = Mathf.Max(minValue, range.x * safe);
        float max = Mathf.Max(min, range.y * safe);
        return new Vector2(min, max);
    }

}
