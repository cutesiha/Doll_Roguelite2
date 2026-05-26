using UnityEngine;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    List<EnemyBase> activeEnemies = new();
    System.Action onRoomCleared;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RegisterRoom(List<EnemyBase> enemies, System.Action onCleared)
    {
        activeEnemies = new List<EnemyBase>(enemies);
        onRoomCleared = onCleared;
    }

    public void OnEnemyDied(EnemyBase enemy)
    {
        activeEnemies.Remove(enemy);
        if (activeEnemies.Count == 0)
            onRoomCleared?.Invoke();
    }
}
