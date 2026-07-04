using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class SmallButtonEnemy : EnemyBase
{
    [SerializeField, Min(0f)] float moveSpeed = 0.65f;
    [SerializeField] string fallbackSpriteName = "pixil-frame-0 (22)";

    Rigidbody2D rb;
    Transform player;
    Vector2 popStart;
    Vector2 popEnd;
    float popStartedAt;
    float popDuration;
    bool isPopping;

    public override EnemyKind Kind => EnemyKind.SmallButton;

    protected override void Awake()
    {
        maxHp = 1;
        currentHp = maxHp;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        EnsureCharacterShadow();
        EnsureSprite();
        EnsureCollider();
    }

    // SmallButtonEnemy never receives a managed profile and (when spawned as a
    // standalone room enemy) may have no sprite assigned, which made it invisible.
    // Load a fallback so it is always rendered.
    void EnsureSprite()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite != null)
            return;

        Sprite sprite = Resources.Load<Sprite>("Sprites/enemy/" + fallbackSpriteName);
#if UNITY_EDITOR
        if (sprite == null)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/" + fallbackSpriteName + ".png");
#endif
        if (sprite == null)
            sprite = FallbackEnemySprite();

        if (sprite != null)
            renderer.sprite = sprite;
    }

    protected override void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
        ApplyProfileStats(profile);
        if (profile != null)
            moveSpeed = Mathf.Max(0f, profile.moveSpeed);
    }

    public override void ApplyCombatScaling(float speedMultiplier, float cooldownMultiplier, int extraDamage)
    {
        moveSpeed *= Mathf.Max(0.1f, speedMultiplier);
    }

    void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null)
            return;

        CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.35f;
    }

    void FixedUpdate()
    {
        if (isPopping)
        {
            float t = Mathf.Clamp01((Time.time - popStartedAt) / Mathf.Max(0.01f, popDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            MoveEnemyBody(rb, Vector2.Lerp(popStart, popEnd, eased));
            if (t >= 1f)
                isPopping = false;

            return;
        }

        if (player == null)
            return;
        if (TryMoveSpawnApproach())
            return;

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        MoveEnemyBody(rb, rb.position + direction * moveSpeed * Time.fixedDeltaTime);
    }

    public void PopOut(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        popStart = rb != null ? rb.position : (Vector2)transform.position;
        popEnd = popStart + direction.normalized * Mathf.Max(0f, distance);
        popStartedAt = Time.time;
        popDuration = Mathf.Max(0.01f, duration);
        isPopping = true;
    }
}
