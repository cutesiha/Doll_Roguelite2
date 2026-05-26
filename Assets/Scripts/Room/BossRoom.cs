using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class BossRoom : MonoBehaviour
{
    [SerializeField] EnemyBase bossPrefab;
    [SerializeField] Vector3 bossSpawnPos    = new Vector3(0f,  3f, 0f);
    [SerializeField] Vector3 playerSpawnPos  = new Vector3(0f, -3f, 0f);
    [SerializeField] Vector3 bossScale       = new Vector3(2f,  2f, 1f);

    [SerializeField] GameObject[] doors;
    [SerializeField] string mapSceneName     = "MapScene";
    [SerializeField, Min(0f)] float returnToMapDelay = 1.2f;

    List<EnemyBase> enemies = new();
    bool isCleared;

    void Start()
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerObj.transform.position = playerSpawnPos;

        if (bossPrefab != null)
        {
            var boss = Instantiate(bossPrefab, bossSpawnPos, Quaternion.identity);
            boss.transform.localScale = bossScale;
            enemies.Add(boss);
        }

        EnemyManager.Instance?.RegisterRoom(enemies, OnRoomCleared);
        SetDoorsOpen(false);
    }

    public void OnRoomCleared()
    {
        if (isCleared) return;
        isCleared = true;
        SetDoorsOpen(true);
        Debug.Log("Boss Cleared!");
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
