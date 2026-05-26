using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;

public class Room : MonoBehaviour
{
    [SerializeField] List<EnemyBase> enemies = new();
    [SerializeField] GameObject[] doors;
    [SerializeField] string mapSceneName = "MapScene";
    [SerializeField, Min(0f)] float returnToMapDelay = 0.8f;
    [SerializeField] bool returnToMapOnClear = true;

    bool isCleared;

    void Start()
    {
        EnemyManager.Instance?.RegisterRoom(this, enemies);
        SetDoorsOpen(false);
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
