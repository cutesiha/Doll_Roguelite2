using UnityEngine;
using System.Collections.Generic;

// Which enemy class a profile applies to. Default (0) is Chaser so the existing
// d1/d2/d3 doll profiles keep working without being re-tagged. "None" is the
// default for enemies that should ignore profiles (e.g. bosses).
public enum EnemyKind { Chaser, Needle, Ribbon, Spool, SmallButton, None }

[System.Serializable]
public class EnemyProfile
{
    public string profileName = "Enemy";
    public EnemyKind enemyType = EnemyKind.Chaser;
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
    readonly List<EnemyProfile> profileMatches = new();
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

        EnemyProfile profile = SelectProfile(enemy.Kind);
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

    public void RegisterSpawnedEnemy(EnemyBase enemy)
    {
        if (enemy == null || activeEnemies.Contains(enemy))
            return;

        activeEnemies.Add(enemy);
    }

    public void OnEnemyDied(EnemyBase enemy)
    {
        if (!activeEnemies.Remove(enemy))
            return;

        if (activeEnemies.Count == 0)
            onRoomCleared?.Invoke();
    }

    EnemyProfile SelectProfile(EnemyKind kind)
    {
        if (enemyProfiles == null || enemyProfiles.Count == 0)
            return null;

        // Only consider profiles tagged for this enemy type. Multiple profiles of
        // the same type (e.g. d1/d2/d3 dolls) still give variety.
        profileMatches.Clear();
        for (int i = 0; i < enemyProfiles.Count; i++)
            if (enemyProfiles[i] != null && enemyProfiles[i].enemyType == kind)
                profileMatches.Add(enemyProfiles[i]);

        if (profileMatches.Count == 0)
            return null;

        if (randomizeEnemyProfiles)
            return profileMatches[Random.Range(0, profileMatches.Count)];

        EnemyProfile profile = profileMatches[nextProfileIndex % profileMatches.Count];
        nextProfileIndex++;
        return profile;
    }
}
