using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class Room : MonoBehaviour
{
    [SerializeField] EnemyBase enemyPrefab;
    [SerializeField] int minEnemies = 2;
    [SerializeField] int maxEnemies = 3;
    [SerializeField] Vector2 spawnRange = new Vector2(4f, 3f);

    [SerializeField] GameObject[] doors;
    [SerializeField] string mapSceneName = "MapScene";
    [SerializeField, Min(0f)] float returnToMapDelay = 0.8f;
    [SerializeField] bool returnToMapOnClear = true;

    List<EnemyBase> enemies = new();
    bool isCleared;

    void Start()
    {
        SpawnEnemies();
        EnemyManager.Instance?.RegisterRoom(enemies, OnRoomCleared);
        SetDoorsOpen(false);
    }

    void SpawnEnemies()
    {
        if (enemyPrefab == null) return;
        int count = Random.Range(minEnemies, maxEnemies + 1);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-spawnRange.x, spawnRange.x);
            float y = Random.Range(-spawnRange.y, spawnRange.y);
            var enemy = Instantiate(enemyPrefab, new Vector3(x, y, 0f), Quaternion.identity);
            enemies.Add(enemy);
        }
    }

    public void OnRoomCleared()
    {
        if (isCleared) return;
        isCleared = true;
        SetDoorsOpen(true);
        Debug.Log("Room Cleared!");

        if (returnToMapOnClear)
            StartCoroutine(ReturnToMap());
    }

    void SetDoorsOpen(bool open)
    {
        foreach (var door in doors)
            if (door != null) door.SetActive(open);
    }

    IEnumerator ReturnToMap()
    {
        yield return new WaitForSeconds(returnToMapDelay);
        MapRunState.CompletePendingRoom();
        SceneManager.LoadScene(mapSceneName);
    }
}
