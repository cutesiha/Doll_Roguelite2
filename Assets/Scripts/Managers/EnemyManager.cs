using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyProfile
{
    public string profileName = "Enemy";
    [Min(1)] public int maxHp = 2;
    [Min(0f)] public float moveSpeed = 1f;
    [Min(1f)] public float framesPerSecond = 8f;
    public Color tint = Color.white;
    public Sprite[] animationFrames;

    public bool HasAnimationFrames => animationFrames != null && animationFrames.Length > 0;
}

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [SerializeField] List<EnemyProfile> enemyProfiles = new();
    [SerializeField] bool randomizeEnemyProfiles = true;

    List<EnemyBase> activeEnemies = new();
    System.Action onRoomCleared;
    int nextProfileIndex;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool ConfigureEnemy(EnemyBase enemy, bool force = false)
    {
        if (enemy == null)
            return false;

        if (!force && enemy.HasManagedProfile)
            return true;

        EnemyProfile profile = SelectProfile();
        if (profile == null)
            return false;

        enemy.ApplyProfile(profile);
        return true;
    }

    public void RegisterRoom(List<EnemyBase> enemies, System.Action onCleared)
    {
        activeEnemies = new List<EnemyBase>(enemies);
        onRoomCleared = onCleared;

        if (activeEnemies.Count == 0)
            onRoomCleared?.Invoke();
    }

    public void OnEnemyDied(EnemyBase enemy)
    {
        if (!activeEnemies.Remove(enemy))
            return;

        if (activeEnemies.Count == 0)
            onRoomCleared?.Invoke();
    }

    EnemyProfile SelectProfile()
    {
        if (enemyProfiles == null || enemyProfiles.Count == 0)
            return null;

        if (randomizeEnemyProfiles)
            return enemyProfiles[Random.Range(0, enemyProfiles.Count)];

        EnemyProfile profile = enemyProfiles[nextProfileIndex % enemyProfiles.Count];
        nextProfileIndex++;
        return profile;
    }
}
