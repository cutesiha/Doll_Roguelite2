using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Room : MonoBehaviour
{
    const int BaseEnemiesInsidePerWave = 4;
    const int BaseEnemiesOutsidePerWave = 2;

    [SerializeField] EnemyBase enemyPrefab;
    [SerializeField] int waveCount = 3;
    [SerializeField, Min(0)] int extraEnemyEveryLayers = 2;
    [SerializeField, Min(0)] int maxExtraEnemiesPerWave = 3;
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
    [Tooltip("단추/바늘/리본 등 일반 적의 가중치(spool 은 1). 클수록 spool보다 더 자주 나온다.")]
    [SerializeField, Min(1)] int commonEnemyWeight = 2;

    [SerializeField] GameObject[] doors;
    [SerializeField] Vector2 doorLine = new Vector2(8f, 4f);

    // Spool(실타래) 스폰 제한: 웨이브당 최대 2개, 한 방(3웨이브) 누적 최대 5개.
    const int SpoolMaxPerWave = 2;
    const int SpoolMaxPerRoom = 5;

    readonly List<EnemyBase> enemies = new List<EnemyBase>();
    readonly List<DoorTrigger> activeDoors = new List<DoorTrigger>();
    readonly List<GameObject> sceneEnemyTemplates = new List<GameObject>();
    readonly List<GameObject> nonSpoolTemplateBuffer = new List<GameObject>();
    GameObject sceneEnemyTemplate;
    int spoolSpawnedThisRoom;
    bool waveCleared;
    bool isCleared;

    static readonly Color SpawnMarkerColor = new Color(1f, 0.38f, 0.22f, 0.9f);
    static Sprite markerSprite;

    IEnumerator Start()
    {
        MapRunState.EnsureRun();
        if (ThreadMazeChallengeManager.ShouldHandleCurrentRoom())
        {
            SetDoorsOpen(false);
            yield break;
        }

        SetDoorsOpen(false);
        PrepareSceneEnemyTemplate();
        spoolSpawnedThisRoom = 0;

        if (EnemyTemplate() == null)
        {
            OnRoomCleared();
            yield break;
        }

        bool useWaves = IsRoomScene();
        if (useWaves)
        {
            FarEnemyArrowIndicator.Ensure();
            yield return null;
        }

        int totalWaves = useWaves ? EffectiveWaveCount() : 1;
        for (int wave = 1; wave <= totalWaves; wave++)
        {
            if (useWaves)
                RunHudUI.SetWave(wave, totalWaves);

            yield return StartCoroutine(SpawnWave(wave));

            waveCleared = false;
            EnemyManager.Instance?.RegisterRoom(enemies, OnWaveCleared);
            while (!waveCleared)
                yield return null;

            // 웨이브 클리어 동전 보너스
            GiveWaveClearCoins();

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

                if (IsValidSpawnPosition(position, enemySize, hasPlayerExclusion, playerExclusionRect))
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

            if (IsValidSpawnPosition(position, enemySize, hasPlayerExclusion, playerExclusionRect))
                return position;
        }

        return hasPlayerExclusion
            ? PushSpawnBesidePlayer(Rect.MinMaxRect(-spawnRange.x, -spawnRange.y, spawnRange.x, spawnRange.y), playerExclusionRect)
            : Vector3.zero;
    }

    bool IsValidSpawnPosition(Vector3 position, Vector2 enemySize, bool hasPlayerExclusion, Rect playerExclusionRect)
    {
        if (hasPlayerExclusion && playerExclusionRect.Contains(position))
            return false;

        if (SpawnOverlapsBlockingCollider(position, enemySize))
            return false;

        if (minSpawnDistanceFromPlayer <= 0f)
            return true;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return true;

        return Vector2.Distance(position, player.transform.position) >= minSpawnDistanceFromPlayer;
    }

    bool SpawnOverlapsBlockingCollider(Vector3 position, Vector2 enemySize)
    {
        Vector2 size = new Vector2(
            Mathf.Max(0.35f, enemySize.x),
            Mathf.Max(0.35f, enemySize.y));
        Collider2D[] hits = Physics2D.OverlapBoxAll(position, size * 0.96f, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled || hit.isTrigger)
                continue;

            GameObject go = hit.gameObject;
            if (go.CompareTag("Player")
                || go.GetComponentInParent<PlayerController>() != null
                || go.GetComponentInParent<PlayerDamageReceiver>() != null
                || go.GetComponentInParent<EnemyBase>() != null)
                continue;

            string objectName = go.name;
            if (objectName.Contains("Floor_Background") || objectName.Contains("SpawnBlink"))
                continue;

            return true;
        }

        return false;
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

        // 카메라가 벽 밖까지 보일 수 있으므로 방 안쪽(벽 경계)과 교집합으로 제한
        if (TryGetRoomInnerRect(enemySize, out Rect innerRect))
        {
            float clampMinX = Mathf.Max(minX, innerRect.xMin);
            float clampMaxX = Mathf.Min(maxX, innerRect.xMax);
            float clampMinY = Mathf.Max(minY, innerRect.yMin);
            float clampMaxY = Mathf.Min(maxY, innerRect.yMax);

            if (clampMinX <= clampMaxX && clampMinY <= clampMaxY)
            {
                minX = clampMinX; maxX = clampMaxX;
                minY = clampMinY; maxY = clampMaxY;
            }
            else
            {
                // 카메라와 겹치는 방 안쪽이 없으면 방 안쪽을 그대로 사용
                rect = innerRect;
                return true;
            }
        }

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    // 4개 벽(Wall_Left/Right/Top/Bottom)의 안쪽 가장자리로 플레이 가능 영역을 계산
    bool TryGetRoomInnerRect(Vector2 enemySize, out Rect rect)
    {
        rect = default;

        Collider2D left = FindWallCollider("Wall_Left");
        Collider2D right = FindWallCollider("Wall_Right");
        Collider2D top = FindWallCollider("Wall_Top");
        Collider2D bottom = FindWallCollider("Wall_Bottom");
        if (left == null || right == null || top == null || bottom == null)
            return false;

        float padX = Mathf.Max(0.25f, enemySize.x * 0.5f);
        float padY = Mathf.Max(0.25f, enemySize.y * 0.5f);

        float minX = left.bounds.max.x + padX;    // 왼쪽 벽 안쪽 면
        float maxX = right.bounds.min.x - padX;   // 오른쪽 벽 안쪽 면
        float minY = bottom.bounds.max.y + padY;  // 아래 벽 안쪽 면
        float maxY = top.bounds.min.y - padY;     // 위 벽 안쪽 면

        if (minX > maxX || minY > maxY)
            return false;

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    static Collider2D FindWallCollider(string wallName)
    {
        GameObject go = GameObject.Find(wallName);
        return go != null ? go.GetComponent<Collider2D>() : null;
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

            // SmallButtonEnemy was created for the old death-split effect. It must
            // never enter the normal room template pool as a one-frame enemy.
            if (template.GetComponent<SmallButtonEnemy>() != null)
            {
                template.SetActive(false);
                continue;
            }

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

        int totalWeight = 0;
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
            totalWeight += TemplateWeight(sceneEnemyTemplates[i], extraButtonWeight);

        int roll = Random.Range(0, Mathf.Max(1, totalWeight));
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
        {
            int weight = TemplateWeight(sceneEnemyTemplates[i], extraButtonWeight);
            if (roll < weight)
                return sceneEnemyTemplates[i];

            roll -= weight;
        }

        return sceneEnemyTemplates[Random.Range(0, sceneEnemyTemplates.Count)];
    }

    // spool 은 가중치 1, 단추/바늘/리본 등 일반 적은 commonEnemyWeight(기본 2) → spool보다 더 자주 뜬다.
    int TemplateWeight(GameObject template, int extraButtonWeight)
    {
        int weight = IsSpoolTemplate(template) ? 1 : Mathf.Max(1, commonEnemyWeight);
        if (extraButtonWeight > 0 && IsButtonTemplate(template))
            weight += extraButtonWeight;
        return weight;
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

        int insideCount = EffectiveEnemiesInsidePerWave();
        int outsideCount = EffectiveEnemiesOutsidePerWave();
        int totalCount = insideCount + outsideCount;
        List<GameObject> templates = BuildWaveTemplates(wave, totalCount);
        GameObject markerTemplate = templates.Count > 0 ? templates[0] : EnemyTemplate(wave);

        Vector2 markerSize = GetEnemyMarkerSize(markerTemplate);

        List<Vector3> insidePositions = new List<Vector3>(insideCount);
        for (int i = 0; i < insideCount; i++)
            insidePositions.Add(randomizeEnemyPositions ? RandomPos(markerSize) : markerTemplate.transform.position);

        List<Vector3> outsidePositions = new List<Vector3>(outsideCount);
        for (int i = 0; i < outsideCount; i++)
            outsidePositions.Add(RandomPosOutsideScreen(markerSize));

        yield return StartCoroutine(BlinkSpawnPositions(insidePositions, markerSize));

        List<Vector3> allPositions = new List<Vector3>(insidePositions);
        allPositions.AddRange(outsidePositions);

        // spool 은 절대 멀리(화면 밖/구석)에 스폰되지 않도록, 플레이어에서 가까운 위치에 우선 배정한다.
        AssignSpoolsToClosestPositions(templates, allPositions);

        for (int i = 0; i < allPositions.Count; i++)
        {
            GameObject template = i < templates.Count ? templates[i] : EnemyTemplate(wave);
            if (template == null)
                continue;

            GameObject enemyGO = Instantiate(template, allPositions[i], Quaternion.identity, transform);
            enemyGO.name = template.name + "_Wave" + wave + "_" + (i + 1);
            enemyGO.SetActive(true);

            EnemyBase enemy = enemyGO.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                EnemyManager.Instance?.ConfigureEnemy(enemy, true);
                float approachDur = i >= insideCount ? spawnApproachDuration * 2.5f : spawnApproachDuration;
                enemy.StartSpawnApproach(approachDur, EffectiveSpawnApproachSpeed());
                enemies.Add(enemy);
            }
        }
    }

    // 위치 목록 중 플레이어에서 가까운 자리부터 spool 템플릿을 배정하고, 나머지(먼 자리 포함)는
    // 비-spool 적이 채운다. templates 는 positions 와 인덱스로 짝지어져 있으므로 templates 를 재배열한다.
    void AssignSpoolsToClosestPositions(List<GameObject> templates, List<Vector3> positions)
    {
        int count = Mathf.Min(templates.Count, positions.Count);
        if (count <= 1)
            return;

        List<GameObject> spools = new List<GameObject>();
        List<GameObject> others = new List<GameObject>();
        for (int i = 0; i < count; i++)
        {
            if (IsSpoolTemplate(templates[i]))
                spools.Add(templates[i]);
            else
                others.Add(templates[i]);
        }

        if (spools.Count == 0)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        List<int> order = new List<int>(count);
        for (int i = 0; i < count; i++)
            order.Add(i);
        order.Sort((a, b) =>
            Vector2.Distance(positions[a], playerPos).CompareTo(Vector2.Distance(positions[b], playerPos)));

        int si = 0, oi = 0;
        for (int k = 0; k < count; k++)
        {
            int posIndex = order[k];
            if (si < spools.Count)
                templates[posIndex] = spools[si++];
            else
                templates[posIndex] = others[oi++];
        }
    }

    Vector3 RandomPosOutsideScreen(Vector2 enemySize)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return Vector3.zero;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 center = cam.transform.position;
        const float offscreenMargin = 2.5f;

        if (TryGetRoomInnerRect(enemySize, out Rect innerRect))
        {
            Vector2[] farCandidates =
            {
                new Vector2(innerRect.xMin, innerRect.yMin),
                new Vector2(innerRect.xMin, innerRect.yMax),
                new Vector2(innerRect.xMax, innerRect.yMin),
                new Vector2(innerRect.xMax, innerRect.yMax)
            };

            Vector2 cameraCenter = center;
            Vector2 farthest = farCandidates[0];
            float farthestDistance = (farthest - cameraCenter).sqrMagnitude;
            for (int i = 1; i < farCandidates.Length; i++)
            {
                float distance = (farCandidates[i] - cameraCenter).sqrMagnitude;
                if (distance > farthestDistance)
                {
                    farthest = farCandidates[i];
                    farthestDistance = distance;
                }
            }

            // Keep a little variation without ever moving the fourth enemy back
            // into the visible camera rectangle.
            farthest.x = Mathf.Clamp(farthest.x + Random.Range(-0.6f, 0.6f), innerRect.xMin, innerRect.xMax);
            farthest.y = Mathf.Clamp(farthest.y + Random.Range(-0.6f, 0.6f), innerRect.yMin, innerRect.yMax);
            return new Vector3(farthest.x, farthest.y, 0f);
        }

        float x, y;
        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0:
                x = center.x - halfW - offscreenMargin;
                y = Random.Range(center.y - halfH, center.y + halfH);
                break;
            case 1:
                x = center.x + halfW + offscreenMargin;
                y = Random.Range(center.y - halfH, center.y + halfH);
                break;
            case 2:
                x = Random.Range(center.x - halfW, center.x + halfW);
                y = center.y + halfH + offscreenMargin;
                break;
            default:
                x = Random.Range(center.x - halfW, center.x + halfW);
                y = center.y - halfH - offscreenMargin;
                break;
        }

        return new Vector3(x, y, 0f);
    }

    int EffectiveWaveCount()
    {
        return Mathf.Max(1, waveCount);
    }

    int EffectiveEnemiesInsidePerWave()
    {
        return BaseEnemiesInsidePerWave + ExtraEnemiesForLayer();
    }

    int EffectiveEnemiesOutsidePerWave()
    {
        return BaseEnemiesOutsidePerWave + Mathf.Clamp(ExtraEnemiesForLayer() / 2, 0, 2);
    }

    int ExtraEnemiesForLayer()
    {
        int every = Mathf.Max(1, extraEnemyEveryLayers);
        return Mathf.Clamp(Mathf.Max(0, RoomLayer() - 1) / every, 0, maxExtraEnemiesPerWave);
    }

    float EffectiveSpawnApproachSpeed()
    {
        return spawnApproachSpeed * (1.35f + RoomLayer() * 0.05f);
    }

    static int RoomLayer()
    {
        MapNode node = MapRunState.PendingNode != null ? MapRunState.PendingNode : MapRunState.CurrentNode;
        return node != null ? Mathf.Max(1, node.layer) : 1;
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

        if (guaranteedButtons == 0 && enemyPrefab == null)
            AddVariedSceneTemplates(templates, count);

        while (templates.Count < count)
            templates.Add(EnemyTemplate(wave));

        EnforceSpoolLimit(templates);
        Shuffle(templates);
        return templates;
    }

    void AddVariedSceneTemplates(List<GameObject> templates, int count)
    {
        if (sceneEnemyTemplates.Count == 0)
            return;

        List<GameObject> pool = new List<GameObject>(sceneEnemyTemplates);
        Shuffle(pool);

        for (int i = 0; i < pool.Count && templates.Count < count; i++)
            if (pool[i] != null)
                templates.Add(pool[i]);
    }

    // 스폰 목록에서 spool 개수를 웨이브당/방 누적 한도로 제한하고, 초과분은 다른 적으로 교체한다.
    void EnforceSpoolLimit(List<GameObject> templates)
    {
        int allowedThisWave = Mathf.Min(SpoolMaxPerWave, SpoolMaxPerRoom - spoolSpawnedThisRoom);
        if (allowedThisWave < 0)
            allowedThisWave = 0;

        int spoolCount = 0;
        for (int i = 0; i < templates.Count; i++)
        {
            if (!IsSpoolTemplate(templates[i]))
                continue;

            if (spoolCount < allowedThisWave)
            {
                spoolCount++;
                continue;
            }

            GameObject replacement = RandomNonSpoolTemplate();
            if (replacement != null)
                templates[i] = replacement;
            else
                spoolCount++; // 대체할 비-spool 적이 없으면 그대로 둔다.
        }

        spoolSpawnedThisRoom += spoolCount;
    }

    bool IsSpoolTemplate(GameObject template)
    {
        return template != null && template.GetComponent<SpoolEnemy>() != null;
    }

    GameObject RandomNonSpoolTemplate()
    {
        if (enemyPrefab != null && !IsSpoolTemplate(enemyPrefab.gameObject))
            return enemyPrefab.gameObject;

        nonSpoolTemplateBuffer.Clear();
        for (int i = 0; i < sceneEnemyTemplates.Count; i++)
            if (sceneEnemyTemplates[i] != null && !IsSpoolTemplate(sceneEnemyTemplates[i]))
                nonSpoolTemplateBuffer.Add(sceneEnemyTemplates[i]);

        if (nonSpoolTemplateBuffer.Count > 0)
            return nonSpoolTemplateBuffer[Random.Range(0, nonSpoolTemplateBuffer.Count)];

        return null;
    }

    int GuaranteedButtonCountForWave(int count)
    {
        if (!IsFirstCombatRoom() || enemyPrefab != null)
            return 0;

        int buttonTemplateCount = ButtonTemplateCount();
        if (buttonTemplateCount == 0 || count == 0)
            return 0;

        return Random.Range(2, 4); // 2 or 3 buttons guaranteed in first room
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

    static void GiveWaveClearCoins()
    {
        ItemInventoryManager inv = ItemInventoryManager.Instance;
        if (inv == null)
            return;

        ItemSystemSettings settings = ItemSystemSettings.Load();
        int bonus = settings != null ? settings.waveClearCoinBonus : 1;
        if (bonus > 0)
            inv.AddCoins(bonus);
    }

    public void OnRoomCleared()
    {
        if (isCleared) return;
        isCleared = true;
        ItemRoomRewardSystem.HandleCombatRoomCleared(MapRunState.PendingNode, transform.position);
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
        BodyConditionUtility.UnlockRequiredMissingSlot(MapRunState.PendingNode);
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
