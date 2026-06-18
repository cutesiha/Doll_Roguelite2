using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser : EnemyBase
{
    [SerializeField] float moveSpeed = 1f;

    Rigidbody2D rb;
    Transform player;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    protected override void Start()
    {
        base.Start();

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
        base.ApplyProfile(profile);

        if (profile != null)
            moveSpeed = Mathf.Max(0f, profile.moveSpeed);
    }

    void FixedUpdate()
    {
        if (player == null) return;

        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
    }
}
