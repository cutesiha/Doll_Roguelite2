using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ThreadMazeGoalZone : MonoBehaviour
{
    [SerializeField] ThreadMazeChallengeManager challengeManager;

    void Reset()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;
        challengeManager = GetComponentInParent<ThreadMazeChallengeManager>();
    }

    void OnValidate()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    void Awake()
    {
        if (challengeManager == null)
            challengeManager = GetComponentInParent<ThreadMazeChallengeManager>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player") && challengeManager != null)
            challengeManager.NotifyGoalReached();
    }
}
