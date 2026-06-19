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
    [SerializeField] int spawnPositionAttempts = 30;
    [SerializeField, Min(0f)] float minSpawnDistanceFromPlayer = 4.2f;
    [SerializeField, Min(0f)] float spawnApproachDuration = 1.75f;
    [SerializeField, Min(0f)] float spawnApproachSpeed = 0.45f;
    [SerializeField, Min(1)] int firstRoomButtonWeight = 5;
    [SerializeField, Min(0)] int firstRoomGuaranteedButtonMin = 1;
    [SerializeField, Min(0)] int firstRoomGuaranteedButtonMax = 2;

    [SerializeField] GameObject[] doors;
    [SerializeField] Vector2 doorLine = new Vector2(8f, 4f);

    readonly List<EnemyBase> enemies = new List<EnemyBase>();
    readonly List<DoorTrigger> activeDoors = new List<DoorTrigger>();
    readonly List<GameObject> sceneEnemyTemplates = new List<GameObject>();
    GameObject sceneEnemyTemplate;
    bool waveCleared;
    bool isCleared;

    static readonly Color SpawnMarkerColor = new Color(1f, 0.38f, 0.22f, 0.9f);
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
        if (useWaves)
            yield return null;

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

    Vector3 RandomPos(Vector2 enemySize)
    {
        Rect playerExclusionRect = default;
        bool hasPlayerExclusion = IsRoomScene() && TryGetPlayerExclusionRect(enemySize, out playerExclusionRect);

        if (IsRoomScene() && TryGetCameraSpawnRect(enemySize, out Rect cameraRect))
        {
            int attempts = Mathf.Max(1, spawnPositionAttempts);
            for (int i = 0; i < attempts; i++)
            {
                Vector3 position = new Vector3(
                    Random.Range(cameraRect.xMin, cameraRect.xMax),
                    Random.Range(cameraRect.yMin, cameraRect.yMax),
                    0f);

                if (IsValidSpawnPosition(position, hasPlayerExclusion, playerExclusionRect))
                    return position;
            }

            if (hasPlayerExclusion)
                return PushSpawnBesidePlayer(cameraRect, playerExclusionRect);
        }

        int fallbackAttempts = Mathf.Max(1, spawnPositionAttempts);
        for (int i = 0; i < fallbackAttempts; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(-spawnRange.x, spawnRange.x),
                Random.Range(-spawnRange.y, spawnRange.y),
                0f);

            if (IsValidSpawnPosition(position, hasPlayerExclusion, playerExclusionRect))
                return position;
        }

        return hasPlayerExclusion
            ? PushSpawnBesidePlayer(Rect.MinMaxRect(-spawnRange.x, -spawnRange.y, spawnRange.x, spawnRange.y), playerExclusionRect)
            : Vector3.zero;
    }

    bool IsValidSpawnPosition(Vector3 position, bool hasPlayerExclusion, Rect playerExclusionRect)
    {
        if (hasPlayerExclusion && playerExclusionRect.Contains(position))
            return false;

        if (minSpawnDistanceFromPlayer <= 0f)
            return true;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return true;

        return Vector2.Distance(position, player.transform.position) >= minSpawnDistanceFromPlayer;
    }

    bool TryGetPlayerExclusionRect(Vector2 enemySize, out Rect rect)
    {
        rect = default;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return false;

        Bounds bounds = new Bounds(player.transform.position, Vector3.one);
        bool hasBounds = false;

        Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || !colliders[i].enabled)
                continue;

            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        if (!hasBounds)
        {
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
        }

        Vector2 size = hasBounds ? bounds.size : Vector2.one;
        Vector2 halfSize = new Vector2(
            Mathf.Max(0.25f, size.x * 0.5f + enemySize.x * 0.5f),
            Mathf.Max(0.25f, size.y * 0.5f + enemySize.y * 0.5f));
        Vector2 center = bounds.center;

        rect = Rect.MinMaxRect(
            center.x - halfSize.x,
            center.y - halfSize.y,
            center.x + halfSize.x,
            center.y + halfSize.y);
        return true;
    }

    Vector3 PushSpawnBesidePlayer(Rect spawnRect, Rect playerExclusionRect)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && minSpawnDistanceFromPlayer > 0f)
            return FarthestSpawnPoint(spawnRect, player.transform.position);

        bool hasLeft = playerExclusionRect.xMin > spawnRect.xMin;
        bool hasRight = playerExclusionRect.xMax < spawnRect.xMax;
        bool useLeft = hasLeft && (!hasRight || playerExclusionRect.center.x - spawnRect.xMin > spawnRect.xMax - playerExclusionRect.center.x);

        float x;
        if (useLeft)
            x = Random.Range(spawnRect.xMin, playerExclusionRect.xMin);
        else if (hasRight)
            x = Random.Range(playerExclusionRect.xMax, spawnRect.xMax);
        else
            x = Mathf.Clamp(playerExclusionRect.center.x, spawnRect.xMin, spawnRect.xMax);

        float y = Mathf.Clamp(playerExclusionRect.center.y, spawnRect.yMin, spawnRect.yMax);
        return new Vector3(x, y, 0f);
    }

    Vector3 FarthestSpawnPoint(Rect spawnRect, Vector3 playerPosition)
    {
        Vector3[] candidates =
        {
            new Vector3(spawnRect.xMin, spawnRect.yMin, 0f),
            new Vector3(spawnRect.xMin, spawnRect.yMax, 0f),
            new Vector3(spawnRect.xMax, spawnRect.yMin, 0f),
            new Vector3(spawnRect.xMax, spawnRect.yMax, 0f)
        };

        Vector3 best = candidates[0];
        float bestDistance = Vector2.Distance(best, playerPosition);
        for (int i = 1; i < candidates.Length; i++)
        {
            float distance = Vector2.Distance(candidates[i], playerPosition);
            if (distance > bestDistance)
            {
                best = candidates[i];
                bestDistance = distance;
            }
        }

        return best;
    }

    bool TryGetCameraSpawnRect(Vector2 enemySize, out Rect rect)
    {
        rect = default;

        Camera mainCamera = Camera.main;
        if (mainCamera == null || !mainCamera.orthographic)
            return false;

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 center = mainCamera.transform.position;

        float paddingX = Mathf.Max(0.25f, enemySize.x * 0.5f);
        float paddingY = Mathf.Max(0.25f, enemySize.y * 0.5f);

        float minX = center.x - halfWidth + paddingX;
        float maxX = center.x + halfWidth - paddingX;
        float minY = center.y - halfHeight + paddingY;
        float maxY = center.y + halfHeight - paddingY;

        if (minX > maxX)
            minX = maxX = center.x;

        if (minY > maxY)
            minY = maxY = center.y;

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    void PrepareSceneEnemyTemplate()
    {
        if (enemyPrefab != null)
            return;

        sceneEnemyTemplates.Clear();
        EnemyBase[] existingEnemies = GetComponentsInChildren<EnemyBase>(true);
        for (int i = 0; i < existingEnemies.Length; i++)
        {
            GameObject template = existingEnemies[i].gameObject;
            if (template.GetComponentInParent<Room>() != this)
                continue;

            if (!sceneEnemyTemplates.Contains(template))
                sceneEnemyTemplates.Add(template);

            if (sceneEnemyTemplate == null)
                sceneEnemyTemplate = template;

            template.SetActive(false);
        }
    }

    GameObject EnemyTemplate()
    {
        return EnemyTemplate(1);
    }

    GameObject EnemyTemplate(int wave)
    {
        if (enemyPrefab != null)
            return enemyPrefab.gameObject;

        if (sceneEnemyTemplates.Count > 0)
            return SelectSceneEnemyTemplate(wave);

        return sceneEnemyTemplate;
    }

    GameObject SelectSceneEnemyTemplate(int wave)
    {
        if (sceneEnemyTemplates.Count == 0)
            return null;

        int extraButtonWeight = IsFirstCombatRoom() ? Mathf.Max(1, firstRoomButtonWeight) - 1 : 0;
        int totalWeight = sceneEnemyTemplates.Count;
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
            if (extraButtonWeight > 0 && IsButtonTemplate(sceneEnemyTemplates[i]))
                totalWeight += extraButtonWeight;

        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
        {
            int weight = 1;
            if (extraButtonWeight > 0 && IsButtonTemplate(sceneEnemyTemplates[i]))
                weight += extraButtonWeight;

            if (roll < weight)
                return sceneEnemyTemplates[i];

            roll -= weight;
        }

        return sceneEnemyTemplates[Random.Range(0, sceneEnemyTemplates.Count)];
    }

    bool IsFirstCombatRoom()
    {
        return MapRunState.PendingNode != null && MapRunState.PendingNode.layer == 1;
    }

    bool IsButtonTemplate(GameObject template)
    {
        return template != null && template.GetComponent<EnemyChaser>() != null;
    }

    IEnumerator SpawnWave(int wave)
    {
        enemies.Clear();

        int minCount = Mathf.Max(1, minEnemies);
        int maxCount = Mathf.Max(minCount, maxEnemies);
        int count = Random.Range(minCount, maxCount + 1);
        List<GameObject> templates = BuildWaveTemplates(wave, count);
        GameObject markerTemplate = templates.Count > 0 ? templates[0] : EnemyTemplate(wave);

        Vector2 markerSize = GetEnemyMarkerSize(markerTemplate);
        List<Vector3> positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
            positions.Add(randomizeEnemyPositions ? RandomPos(markerSize) : markerTemplate.transform.position);

        yield return StartCoroutine(BlinkSpawnPositions(positions, markerSize));

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject template = i < templates.Count ? templates[i] : EnemyTemplate(wave);
            if (template == null)
                continue;

            GameObject enemyGO = Instantiate(template, positions[i], Quaternion.identity, transform);
            enemyGO.name = template.name + "_Wave" + wave + "_" + (i + 1);
            enemyGO.SetActive(true);

            EnemyBase enemy = enemyGO.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                EnemyManager.Instance?.ConfigureEnemy(enemy, true);
                enemy.StartSpawnApproach(spawnApproachDuration, spawnApproachSpeed);
                enemies.Add(enemy);
            }
        }
    }

    List<GameObject> BuildWaveTemplates(int wave, int count)
    {
        List<GameObject> templates = new List<GameObject>(count);
        int guaranteedButtons = GuaranteedButtonCountForWave(count);
        for (int i = 0; i < guaranteedButtons; i++)
        {
            GameObject buttonTemplate = RandomButtonTemplate();
            if (buttonTemplate != null)
                templates.Add(buttonTemplate);
        }

        while (templates.Count < count)
            templates.Add(EnemyTemplate(wave));

        Shuffle(templates);
        return templates;
    }

    int GuaranteedButtonCountForWave(int count)
    {
        if (!IsFirstCombatRoom() || enemyPrefab != null)
            return 0;

        int buttonTemplateCount = ButtonTemplateCount();
        if (buttonTemplateCount == 0)
            return 0;

        int min = Mathf.Clamp(firstRoomGuaranteedButtonMin, 0, count);
        int max = Mathf.Clamp(Mathf.Max(min, firstRoomGuaranteedButtonMax), min, count);
        return Random.Range(min, max + 1);
    }

    int ButtonTemplateCount()
    {
        int total = 0;
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
            if (IsButtonTemplate(sceneEnemyTemplates[i]))
                total++;

        return total;
    }

    GameObject RandomButtonTemplate()
    {
        int count = ButtonTemplateCount();
        if (count == 0)
            return null;

        int target = Random.Range(0, count);
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
        {
            if (!IsButtonTemplate(sceneEnemyTemplates[i]))
                continue;

            if (target == 0)
                return sceneEnemyTemplates[i];

            target--;
        }

        return null;
    }

    void Shuffle(List<GameObject> templates)
    {
        for (int i = 0; i < templates.Count; i++)
        {
            int swapIndex = Random.Range(i, templates.Count);
            GameObject temp = templates[i];
            templates[i] = templates[swapIndex];
            templates[swapIndex] = temp;
        }
    }

    Vector2 GetEnemyMarkerSize(GameObject template)
    {
        if (template == null)
            return Vector2.one;

        Renderer[] renderers = template.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(template.transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
        {
            Collider2D[] colliders = template.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = colliders[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }
        }

        if (!hasBounds)
            return Vector2.one;

        float width = Mathf.Max(0.35f, bounds.size.x);
        float height = Mathf.Max(0.35f, bounds.size.y);
        return new Vector2(width, height);
    }

    IEnumerator BlinkSpawnPositions(List<Vector3> positions, Vector2 markerSize)
    {
        List<SpriteRenderer> markers = new List<SpriteRenderer>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
            markers.Add(CreateSpawnMarker(positions[i], markerSize));

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

    SpriteRenderer CreateSpawnMarker(Vector3 position, Vector2 markerSize)
    {
        GameObject marker = new GameObject("EnemySpawnBlink");
        marker.transform.SetParent(transform, false);
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(markerSize.x, markerSize.y, 1f);

        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateMarkerSprite();
        renderer.color = SpawnMarkerColor;
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
