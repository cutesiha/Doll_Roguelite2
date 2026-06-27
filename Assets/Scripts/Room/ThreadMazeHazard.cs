using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ThreadMazeHazard : MonoBehaviour
{
    [SerializeField] bool slowsPlayer = true;
    [SerializeField, Range(0.05f, 1f)] float speedMultiplier = 0.45f;
    [SerializeField, Min(0f)] float slowDuration = 0.75f;
    [SerializeField] bool damagesPlayer;
    [SerializeField, Min(1)] int damage = 1;
    [SerializeField, Min(0.05f)] float damageCooldown = 0.9f;

    float nextDamageTime;

    void Reset()
    {
        Collider2D hazardCollider = GetComponent<Collider2D>();
        hazardCollider.isTrigger = true;
    }

    void OnValidate()
    {
        Collider2D hazardCollider = GetComponent<Collider2D>();
        if (hazardCollider != null)
            hazardCollider.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        ApplyEffect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        ApplyEffect(other);
    }

    void ApplyEffect(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player"))
            return;

        if (slowsPlayer)
        {
            PlayerController controller = other.GetComponent<PlayerController>();
            if (controller != null)
                controller.ApplyTemporarySpeedMultiplier(speedMultiplier, slowDuration);
        }

        if (!damagesPlayer || Time.time < nextDamageTime)
            return;

        PlayerDamageReceiver receiver = other.GetComponent<PlayerDamageReceiver>();
        if (receiver != null && receiver.TryTakePatternDamage(damage, damageCooldown))
            nextDamageTime = Time.time + damageCooldown;
    }
}
