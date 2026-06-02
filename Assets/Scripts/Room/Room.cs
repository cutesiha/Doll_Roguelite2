using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    [SerializeField] EnemyBase enemyPrefab;
    [SerializeField] int minEnemies = 2;
    [SerializeField] int maxEnemies = 3;
    [SerializeField] Vector2 spawnRange = new Vector2(4f, 3f);
    [SerializeField] bool randomizeEnemyPositions = true;

    [SerializeField] GameObject[] doors;
    [SerializeField] Vector2 doorLine = new Vector2(8f, 4f);

    List<EnemyBase> enemies = new();
    readonly List<DoorTrigger> activeDoors = new();
    bool isCleared;

    void Start()
    {
        MapRunState.EnsureRun();
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
        CompleteCurrentRoom();
        BuildNextDoors();
        Debug.Log("Room Cleared!");
        // 플레이어가 문 근처에서 E를 눌러 다음 방을 선택한다.
    }

    void SetDoorsOpen(bool open)
    {
        foreach (var door in activeDoors)
            if (door != null)
                door.Configure(null, false);

        if (doors == null) return;
        foreach (var door in doors)
            if (door != null) door.SetActive(open);
    }

    void CompleteCurrentRoom()
    {
        if (MapRunState.PendingNode != null)
            MapRunState.CompletePendingRoom();
    }

    void BuildNextDoors()
    {
        activeDoors.Clear();

        var current = MapRunState.CurrentNode;
        if (current == null || current.children.Count == 0)
        {
            SetDoorsOpen(false);
            return;
        }

        GameObject template = FirstDoorTemplate();
        if (template == null)
            template = CreateDoorTemplate();

        for (int i = 0; i < current.children.Count; i++)
        {
            GameObject doorGO = i == 0
                ? template
                : Instantiate(template, transform);

            doorGO.name = $"Door_ToNode_{current.children[i].id}";
            doorGO.transform.SetParent(transform, false);
            doorGO.transform.localPosition = DoorPosition(i, current.children.Count);

            var trigger = doorGO.GetComponent<DoorTrigger>();
            if (trigger == null) trigger = doorGO.AddComponent<DoorTrigger>();
            trigger.Configure(current.children[i], true);
            activeDoors.Add(trigger);
        }
    }

    GameObject FirstDoorTemplate()
    {
        if (doors == null || doors.Length == 0) return null;
        foreach (var door in doors)
            if (door != null) return door;
        return null;
    }

    GameObject CreateDoorTemplate()
    {
        var doorGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorGO.name = "Door_Template";
        doorGO.transform.SetParent(transform, false);
        doorGO.transform.localScale = new Vector3(1.5f, 0.4f, 1f);
        var collider = doorGO.GetComponent<BoxCollider>();
        if (collider != null) Destroy(collider);
        var collider2D = doorGO.AddComponent<BoxCollider2D>();
        collider2D.isTrigger = true;
        doorGO.AddComponent<DoorTrigger>();
        doors = new[] { doorGO };
        return doorGO;
    }

    Vector3 DoorPosition(int index, int count)
    {
        float x = count <= 1
            ? 0f
            : Mathf.Lerp(-doorLine.x * 0.5f, doorLine.x * 0.5f, index / (float)(count - 1));
        return new Vector3(x, doorLine.y, 0f);
    }
}
