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

    // 방(레이어)마다 오르던 배율이 너무 가팔라서 대폭 낮춤.
    const float BaseRoomHpMultiplier = 1.2f;
    const float HpMultiplierPerRoomLayer = 0.10f;
    const float BaseRoomSpeedMultiplier = 1.1f;
    const float SpeedMultiplierPerRoomLayer = 0.03f;
    const float BaseAttackCooldownMultiplier = 0.85f;
    const float AttackCooldownMultiplierPerLayer = 0.015f;

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
        if (profile == null && force)
            profile = DefaultProfileFor(enemy.Kind);

        if (profile == null)
            return false;

        enemy.ApplyProfile(ScaledProfile(profile));
        enemy.ApplyCombatScaling(CurrentRoomSpeedMultiplier(), CurrentRoomAttackCooldownMultiplier(), CurrentRoomExtraAttackDamage());

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

    EnemyProfile ScaledProfile(EnemyProfile source)
    {
        if (source == null)
            return null;

        return new EnemyProfile
        {
            profileName = source.profileName,
            enemyType = source.enemyType,
            maxHp = Mathf.Max(1, Mathf.CeilToInt(source.maxHp * CurrentRoomHealthMultiplier())),
            moveSpeed = Mathf.Max(0f, source.moveSpeed),
            framesPerSecond = source.framesPerSecond,
            tint = source.tint,
            animationFrames = source.animationFrames
        };
    }

    static int RoomLayer()
    {
        return CurrentRoomLayer();
    }

    public static int CurrentRoomLayer()
    {
        MapNode node = MapRunState.PendingNode != null ? MapRunState.PendingNode : MapRunState.CurrentNode;
        return node != null ? Mathf.Max(1, node.layer) : 1;
    }

    public static float CurrentRoomHealthMultiplier()
    {
        int level = Mathf.Max(0, CurrentRoomLayer() - 1);
        return BaseRoomHpMultiplier + level * HpMultiplierPerRoomLayer;
    }

    public static float CurrentRoomSpeedMultiplier()
    {
        int level = Mathf.Max(0, CurrentRoomLayer() - 1);
        return BaseRoomSpeedMultiplier + level * SpeedMultiplierPerRoomLayer;
    }

    public static float CurrentRoomAttackCooldownMultiplier()
    {
        int level = Mathf.Max(0, CurrentRoomLayer() - 1);
        return Mathf.Max(0.6f, BaseAttackCooldownMultiplier - level * AttackCooldownMultiplierPerLayer);
    }

    public static int CurrentRoomExtraAttackDamage()
    {
        return Mathf.Max(0, CurrentRoomLayer() - 1);
    }

    static EnemyProfile DefaultProfileFor(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Chaser:
                return new EnemyProfile { profileName = "DefaultChaser", enemyType = kind, maxHp = 2, moveSpeed = 1f };
            case EnemyKind.Needle:
                return new EnemyProfile { profileName = "DefaultNeedle", enemyType = kind, maxHp = 3, moveSpeed = 0.9f };
            case EnemyKind.Ribbon:
                return new EnemyProfile { profileName = "DefaultRibbon", enemyType = kind, maxHp = 3, moveSpeed = 1.0f };
            case EnemyKind.Spool:
                return new EnemyProfile { profileName = "DefaultSpool", enemyType = kind, maxHp = 4, moveSpeed = 0f };
            case EnemyKind.SmallButton:
                return new EnemyProfile { profileName = "DefaultSmallButton", enemyType = kind, maxHp = 2, moveSpeed = 1.2f };
            default:
                return null;
        }
    }
}
