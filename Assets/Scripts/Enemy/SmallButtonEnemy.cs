using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SmallButtonEnemy : EnemyBase
{
    [SerializeField, Min(0f)] float moveSpeed = 0.65f;

    Rigidbody2D rb;
    Transform player;
    Vector2 popStart;
    Vector2 popEnd;
    float popStartedAt;
    float popDuration;
    bool isPopping;

    protected override void Awake()
    {
        maxHp = 1;
        currentHp = maxHp;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        EnsureCharacterShadow();
        EnsureCollider();
    }

    protected override void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
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
            rb.MovePosition(Vector2.Lerp(popStart, popEnd, eased));
            if (t >= 1f)
                isPopping = false;

            return;
        }

        if (player == null)
            return;
        if (TryMoveSpawnApproach())
            return;

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
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
