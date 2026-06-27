using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ThreadMazeNeedleHazard : MonoBehaviour
{
    [SerializeField] Vector2 localMoveAxis = Vector2.right;
    [SerializeField, Min(0f)] float moveDistance = 2.2f;
    [SerializeField, Min(0f)] float moveSpeed = 1.1f;
    [SerializeField, Min(1)] int damage = 1;
    [SerializeField, Min(0.05f)] float damageCooldown = 0.9f;
    [SerializeField, Min(0f)] float knockbackDistance = 0.45f;

    Vector3 startLocalPosition;
    float nextDamageTime;

    void Awake()
    {
        startLocalPosition = transform.localPosition;
        Collider2D hazardCollider = GetComponent<Collider2D>();
        hazardCollider.isTrigger = true;
    }

    void OnValidate()
    {
        Collider2D hazardCollider = GetComponent<Collider2D>();
        if (hazardCollider != null)
            hazardCollider.isTrigger = true;
    }

    void Update()
    {
        Vector2 axis = localMoveAxis.sqrMagnitude > 0.001f ? localMoveAxis.normalized : Vector2.right;
        float offset = Mathf.Sin(Time.time * moveSpeed) * moveDistance;
        transform.localPosition = startLocalPosition + (Vector3)(axis * offset);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ApplyHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        ApplyHit(other);
    }

    void ApplyHit(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player") || Time.time < nextDamageTime)
            return;

        PlayerDamageReceiver receiver = other.GetComponent<PlayerDamageReceiver>();
        if (receiver != null && receiver.TryTakePatternDamage(damage, damageCooldown))
            nextDamageTime = Time.time + damageCooldown;

        Rigidbody2D body = other.attachedRigidbody;
        if (body != null && knockbackDistance > 0f)
        {
            Vector2 direction = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.down;
            body.MovePosition(body.position + direction * knockbackDistance);
        }
    }
}
