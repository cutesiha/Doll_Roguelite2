using UnityEngine;

public class TutorialEnemy : EnemyBase
{
    [SerializeField, Min(0f)] float approachSpeed = 0.65f;
    [SerializeField, Min(0f)] float stopDistance = 1.05f;

    Transform playerTarget;
    bool approachPlayer;

    protected override void Awake()
    {
        maxHp = 3;
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        MoveTowardPlayer();
    }

    public void BeginApproachPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        playerTarget = playerObject != null ? playerObject.transform : null;
        approachPlayer = playerTarget != null;
    }

    void MoveTowardPlayer()
    {
        if (!approachPlayer || playerTarget == null || approachSpeed <= 0f)
            return;

        Vector2 current = transform.position;
        Vector2 target = playerTarget.position;
        Vector2 toTarget = target - current;
        float distance = toTarget.magnitude;
        if (distance <= stopDistance)
            return;

        Vector2 next = Vector2.MoveTowards(current, target, approachSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }
}
