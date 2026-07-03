using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Orchestrates the Minotaur middle-boss fight inside MiddleBossScene. Self-bootstraps on
// scene load so no manual scene wiring is needed: it removes the leftover wave room and
// ribbon/spool enemies, frames the arena, spawns the boss and opens the exit on defeat.
public class MiddleBossController : MonoBehaviour
{
    const string SceneName = "MiddleBossScene";

    [SerializeField] Vector2 arenaCenter = Vector2.zero;
    [SerializeField] Vector2 arenaSize = new Vector2(26f, 14f);
    [SerializeField] Vector3 playerSpawn = new Vector3(0f, -5f, 0f);
    [SerializeField, Min(0f)] float cameraFramePadding = 2.0f;
    [SerializeField, Min(0.1f)] float fallbackRestoreCameraSize = 6.2f;
    [SerializeField, Min(0.1f)] float cameraZoomDuration = 1.15f;
    [SerializeField] Vector2 nextDoorLine = new Vector2(8f, 5.4f);
    [SerializeField] Vector2 nextDoorSize = new Vector2(2.8f, 0.85f);
    [SerializeField] Color doorColor = new Color(0.85f, 0.62f, 0.25f, 1f);

    readonly List<DoorTrigger> nextDoors = new List<DoorTrigger>();
    [SerializeField] MinotaurBoss boss;
    bool defeated;
    float restoreCameraSize;
    Coroutine cameraZoomRoutine;
    static Sprite squareSprite;
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (hooked)
            return;

        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying || scene.name != SceneName)
            return;

        if (FindFirstObjectByType<MiddleBossController>() != null)
            return;

        GameObject go = new GameObject("MiddleBossController");
        go.AddComponent<MiddleBossController>();
    }

    void Awake()
    {
        PurgeLeftoverWaveRoom();
    }

    void Start()
    {
        MapRunState.EnsureRun();
        CompletePendingRoomIfNeeded();
        SetupCamera();
        SetupPlayer();
        SpawnBoss();

        // 보스 씬에서는 웨이브 HUD 를 숨긴다. (보스 자체 웨이브는 보스 HP 바로 표현)
        RunHudUI.SetWaveHudVisible(false);
    }

    void OnDestroy()
    {
        // HUD 는 씬 사이에서 유지되므로, 보스 씬을 떠날 때 웨이브 HUD 를 다시 켠다.
        RunHudUI.SetWaveHudVisible(true);
    }

    // Disable the copied wave Room and delete the ribbon/spool templates so they never run.
    void PurgeLeftoverWaveRoom()
    {
        Room[] rooms = FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != null)
                rooms[i].enabled = false;

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
            if (enemies[i] != null && enemies[i].GetComponent<MinotaurBoss>() == null)
                Destroy(enemies[i].gameObject);

        DeactivateLegacyDoor("Door_Exit");
    }

    void DeactivateLegacyDoor(string doorName)
    {
        GameObject door = GameObject.Find(doorName);
        if (door != null)
            door.SetActive(false);
    }

    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        PlayerCameraFollow follow = cam.GetComponent<PlayerCameraFollow>();
        if (follow != null)
            follow.enabled = false;

        cam.orthographic = true;
        float targetSize = BossCameraSize(cam);
        Vector3 startPosition = cam.transform.position;
        Vector3 targetPosition = new Vector3(arenaCenter.x, arenaCenter.y, startPosition.z);
        float startSize = cam.orthographicSize > 0.01f ? cam.orthographicSize : fallbackRestoreCameraSize;
        restoreCameraSize = startSize;

        if (cameraZoomRoutine != null)
            StopCoroutine(cameraZoomRoutine);

        cameraZoomRoutine = StartCoroutine(SmoothCameraFrame(
            cam,
            startPosition,
            targetPosition,
            startSize,
            targetSize,
            cameraZoomDuration,
            null));
    }

    float BossCameraSize(Camera cam)
    {
        float aspect = cam.aspect > 0.01f ? cam.aspect : 1.6f;
        float fullHdAspect = 16f / 9f;
        float framingAspect = Mathf.Max(aspect, fullHdAspect);
        return Mathf.Max(
            arenaSize.y * 0.5f + cameraFramePadding,
            arenaSize.x * 0.5f / framingAspect + cameraFramePadding);
    }

    void RestoreCameraAfterBoss()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 startPosition = cam.transform.position;
        Vector3 targetPosition = startPosition;
        float targetSize = restoreCameraSize > 0.01f ? restoreCameraSize : fallbackRestoreCameraSize;
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            targetPosition = new Vector3(playerObject.transform.position.x, playerObject.transform.position.y, startPosition.z);

        if (cameraZoomRoutine != null)
            StopCoroutine(cameraZoomRoutine);

        cameraZoomRoutine = StartCoroutine(SmoothCameraFrame(
            cam,
            startPosition,
            targetPosition,
            cam.orthographicSize,
            targetSize,
            cameraZoomDuration,
            () =>
            {
                PlayerCameraFollow follow = cam.GetComponent<PlayerCameraFollow>();
                if (follow != null)
                {
                    follow.ConfigureBounds(RestoredCameraBoundsSize(), RestoredCameraBoundsCenter(), targetSize, true);
                    follow.enabled = true;
                }
            }));
    }

    Vector2 RestoredCameraBoundsSize()
    {
        SpriteRenderer background = FindStageBackgroundRenderer();
        if (background != null)
        {
            Vector3 size = background.bounds.size;
            if (size.x > 0.1f && size.y > 0.1f)
                return new Vector2(size.x, size.y);
        }

        return new Vector2(38.4f, 21.6f);
    }

    Vector2 RestoredCameraBoundsCenter()
    {
        SpriteRenderer background = FindStageBackgroundRenderer();
        return background != null ? (Vector2)background.bounds.center : Vector2.zero;
    }

    SpriteRenderer FindStageBackgroundRenderer()
    {
        StageBackgroundSprite background = FindFirstObjectByType<StageBackgroundSprite>();
        return background != null ? background.GetComponent<SpriteRenderer>() : null;
    }

    IEnumerator SmoothCameraFrame(
        Camera cam,
        Vector3 startPosition,
        Vector3 targetPosition,
        float startSize,
        float targetSize,
        float duration,
        System.Action onComplete)
    {
        if (cam == null)
            yield break;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration && cam != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / safeDuration);
            cam.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cam != null)
        {
            cam.transform.position = targetPosition;
            cam.orthographicSize = targetSize;
        }

        cameraZoomRoutine = null;
        onComplete?.Invoke();
    }

    void SetupPlayer()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            playerObject.transform.position = playerSpawn;
    }

    void SpawnBoss()
    {
        if (boss == null)
            boss = FindFirstObjectByType<MinotaurBoss>(FindObjectsInactive.Include);

        if (boss == null)
        {
            GameObject bossObject = new GameObject("MinotaurBoss");
            bossObject.transform.position = new Vector3(arenaCenter.x, arenaCenter.y + arenaSize.y * 0.5f - 2.8f, 0f);
            boss = bossObject.AddComponent<MinotaurBoss>();
        }

        boss.SetArena(arenaCenter, arenaSize);
        boss.Defeated += OnBossDefeated;
    }

    void OnBossDefeated()
    {
        if (defeated)
            return;

        Vector3 rewardPosition = boss != null ? boss.transform.position : new Vector3(arenaCenter.x, arenaCenter.y, 0f);
        defeated = true;
        RunHudUI.ShowWaveClear();
        RestoreCameraAfterBoss();

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText("미노타우로스를 쓰러뜨렸다.");

        ItemRoomRewardSystem.SpawnMiddleBossRewards(rewardPosition);
        BuildNextDoors();
    }

    void CompletePendingRoomIfNeeded()
    {
        if (MapRunState.PendingNode != null)
            MapRunState.CompletePendingRoom();
    }

    void BuildNextDoors()
    {
        nextDoors.Clear();

        MapNode current = MapRunState.CurrentNode;
        if (current == null || current.children == null || current.children.Count == 0)
        {
            CreateWorldText("NoNextNodeLabel", "다음 노드가 없습니다", new Vector2(arenaCenter.x, nextDoorLine.y), 0.6f, Color.white, 60);
            return;
        }

        for (int i = 0; i < current.children.Count; i++)
        {
            MapNode child = current.children[i];
            GameObject door = CreateRect(
                "NextDoor_ToNode_" + child.id,
                NextDoorPosition(i, current.children.Count),
                nextDoorSize,
                doorColor,
                14);

            BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            DoorTrigger trigger = door.AddComponent<DoorTrigger>();
            trigger.Configure(child, true);
            nextDoors.Add(trigger);
        }
    }

    Vector2 NextDoorPosition(int index, int count)
    {
        float x = count <= 1
            ? arenaCenter.x
            : Mathf.Lerp(arenaCenter.x - nextDoorLine.x * 0.5f, arenaCenter.x + nextDoorLine.x * 0.5f, index / (float)(count - 1));
        return new Vector2(x, nextDoorLine.y);
    }

    GameObject CreateRect(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    TextMeshPro CreateWorldText(string objectName, string text, Vector2 position, float fontSize, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(position.x, position.y, -0.1f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.font = UIThinDungFont.Get();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.sortingOrder = sortingOrder;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 1.2f);
        return tmp;
    }

    static Sprite SquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }
}
