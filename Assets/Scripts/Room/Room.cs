using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Room : MonoBehaviour
{
    [SerializeField] EnemyBase enemyPrefab;
    [SerializeField] int minEnemies = 2;
    [SerializeField] int maxEnemies = 3;
    [SerializeField] int waveCount = 3;
    [SerializeField] int spawnBlinkCount = 6;
    [SerializeField] float spawnBlinkInterval = 0.12f;
    [SerializeField] float nextWaveDelay = 0.75f;
    [SerializeField] Vector2 spawnRange = new Vector2(4f, 3f);
    [SerializeField] bool randomizeEnemyPositions = true;

    [SerializeField] GameObject[] doors;
    [SerializeField] Vector2 doorLine = new Vector2(8f, 4f);

    readonly List<EnemyBase> enemies = new List<EnemyBase>();
    readonly List<DoorTrigger> activeDoors = new List<DoorTrigger>();
    GameObject sceneEnemyTemplate;
    bool waveCleared;
    bool isCleared;

    static Sprite markerSprite;

    IEnumerator Start()
    {
        MapRunState.EnsureRun();
        SetDoorsOpen(false);
        PrepareSceneEnemyTemplate();

        if (EnemyTemplate() == null)
        {
            OnRoomCleared();
            yield break;
        }

        bool useWaves = IsRoomScene();
        int totalWaves = useWaves ? Mathf.Max(1, waveCount) : 1;
        for (int wave = 1; wave <= totalWaves; wave++)
        {
            if (useWaves)
                RunHudUI.SetWave(wave, totalWaves);

            yield return StartCoroutine(SpawnWave(wave));

            waveCleared = false;
            EnemyManager.Instance?.RegisterRoom(enemies, OnWaveCleared);
            while (!waveCleared)
                yield return null;

            if (wave < totalWaves)
                yield return new WaitForSeconds(nextWaveDelay);
        }

        if (useWaves)
            RunHudUI.ShowWaveClear();

        OnRoomCleared();
    }

    static bool IsRoomScene()
    {
        return SceneManager.GetActiveScene().name.StartsWith("RoomScene");
    }

    Vector3 RandomPos() => new Vector3(
        Random.Range(-spawnRange.x, spawnRange.x),
        Random.Range(-spawnRange.y, spawnRange.y),
        0f);

    void PrepareSceneEnemyTemplate()
    {
        if (enemyPrefab != null)
            return;

        EnemyBase[] existingEnemies = GetComponentsInChildren<EnemyBase>(true);
        for (int i = 0; i < existingEnemies.Length; i++)
        {
            if (sceneEnemyTemplate == null)
                sceneEnemyTemplate = existingEnemies[i].gameObject;

            existingEnemies[i].gameObject.SetActive(false);
        }
    }

    GameObject EnemyTemplate()
    {
        if (enemyPrefab != null)
            return enemyPrefab.gameObject;

        return sceneEnemyTemplate;
    }

    IEnumerator SpawnWave(int wave)
    {
        enemies.Clear();

        int minCount = Mathf.Max(1, minEnemies);
        int maxCount = Mathf.Max(minCount, maxEnemies);
        int count = Random.Range(minCount, maxCount + 1);
        GameObject template = EnemyTemplate();

        List<Vector3> positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
            positions.Add(randomizeEnemyPositions ? RandomPos() : template.transform.position);

        yield return StartCoroutine(BlinkSpawnPositions(positions));

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject enemyGO = Instantiate(template, positions[i], Quaternion.identity, transform);
            enemyGO.name = template.name + "_Wave" + wave + "_" + (i + 1);
            enemyGO.SetActive(true);

            EnemyBase enemy = enemyGO.GetComponent<EnemyBase>();
            if (enemy != null)
                enemies.Add(enemy);
        }
    }

    IEnumerator BlinkSpawnPositions(List<Vector3> positions)
    {
        List<SpriteRenderer> markers = new List<SpriteRenderer>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
            markers.Add(CreateSpawnMarker(positions[i]));

        int blinkCount = Mathf.Max(1, spawnBlinkCount);
        float interval = Mathf.Max(0.03f, spawnBlinkInterval);
        for (int blink = 0; blink < blinkCount; blink++)
        {
            bool visible = blink % 2 == 0;
            for (int i = 0; i < markers.Count; i++)
                if (markers[i] != null)
                    markers[i].enabled = visible;

            yield return new WaitForSeconds(interval);
        }

        for (int i = 0; i < markers.Count; i++)
            if (markers[i] != null)
                Destroy(markers[i].gameObject);
    }

    SpriteRenderer CreateSpawnMarker(Vector3 position)
    {
        GameObject marker = new GameObject("EnemySpawnBlink");
        marker.transform.SetParent(transform, false);
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateMarkerSprite();
        renderer.color = new Color(1f, 1f, 1f, 0.88f);
        renderer.sortingOrder = 30;
        return renderer;
    }

    static Sprite CreateMarkerSprite()
    {
        if (markerSprite != null)
            return markerSprite;

        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                bool border = x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1;
                bool cross = x == y || x == texture.width - 1 - y;
                texture.SetPixel(x, y, border || cross ? Color.white : clear);
            }

        texture.Apply();
        markerSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 8f);
        return markerSprite;
    }

    void OnWaveCleared()
    {
        waveCleared = true;
    }

    public void OnRoomCleared()
    {
        if (isCleared) return;
        isCleared = true;
        CompleteCurrentRoom();
        BuildNextDoors();
        Debug.Log("Room Cleared!");
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

            doorGO.name = "Door_ToNode_" + current.children[i].id;
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
