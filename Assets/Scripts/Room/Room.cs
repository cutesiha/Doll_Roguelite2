using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Room : MonoBehaviour
{
    [SerializeField] EnemyBase enemyPrefab;
    [SerializeField] int minEnemies = 2;
    [SerializeField] int maxEnemies = 3;
    [SerializeField] Vector2 spawnRange = new Vector2(4f, 3f);
    [SerializeField] bool randomizeEnemyPositions = true;

    [SerializeField] GameObject[] doors;

    List<EnemyBase> enemies = new();
    bool isCleared;

    void Start()
    {
        if (enemyPrefab != null)
        {
            int count = Random.Range(minEnemies, maxEnemies + 1);
            for (int i = 0; i < count; i++)
            {
                var e = Instantiate(enemyPrefab, RandomPos(), Quaternion.identity, transform);
                enemies.Add(e);
            }
        }
        else
        {
            foreach (var e in GetComponentsInChildren<EnemyBase>())
            {
                if (randomizeEnemyPositions)
                    e.transform.position = RandomPos();
                enemies.Add(e);
            }

            // 일반 방: 50% 확률로 1마리 복제해 2~3마리 구성
            if (randomizeEnemyPositions && enemies.Count > 0 && enemies.Count < maxEnemies && Random.value < 0.5f)
            {
                var clone = Instantiate(enemies[0].gameObject, RandomPos(), Quaternion.identity, transform);
                var eb = clone.GetComponent<EnemyBase>();
                if (eb != null) enemies.Add(eb);
            }
        }

        EnemyManager.Instance?.RegisterRoom(enemies, OnRoomCleared);
        SetDoorsOpen(false);
    }

    Vector3 RandomPos() => new Vector3(
        Random.Range(-spawnRange.x, spawnRange.x),
        Random.Range(-spawnRange.y, spawnRange.y), 0f);

    public void OnRoomCleared()
    {
        if (isCleared) return;
        isCleared = true;
        SetDoorsOpen(true);
        Debug.Log("Room Cleared!");
        // 플레이어가 문에 직접 닿아야 맵으로 이동 (DoorTrigger가 처리)
    }

    void SetDoorsOpen(bool open)
    {
        foreach (var door in doors)
            if (door != null) door.SetActive(open);
    }
}
