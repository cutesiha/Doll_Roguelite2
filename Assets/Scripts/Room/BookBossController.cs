using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// The Book final boss. Self-bootstraps inside BookBossScene and runs the whole fight: three
// independently-damageable parts (body + two arms), four attack patterns across three waves,
// the dropped-letter collection system and the closing cutscene.
public class BookBossController : MonoBehaviour
{
    const string SceneName = "BookBossScene";

    [SerializeField] Vector2 arenaCenter = Vector2.zero;
    [SerializeField] Vector2 arenaSize = new Vector2(26f, 14f);
    [SerializeField] Vector3 playerSpawn = new Vector3(0f, -5f, 0f);
    [SerializeField, Min(0f)] float cameraFramePadding = 2.0f;
    [SerializeField, Min(0.1f)] float fallbackRestoreCameraSize = 6.2f;
    [SerializeField, Min(0.1f)] float cameraZoomDuration = 1.15f;
    [SerializeField] int armHp = 28;
    [SerializeField] int bodyHp = 120;
    [SerializeField] int minionKillBodyDamage = 10;

    [Header("Attack Damage")]
    [SerializeField, Min(1)] int letterDamage = 14;
    [SerializeField, Min(1)] int paperDamage = 18;
    [SerializeField, Min(1)] int poisonTickDamage = 1;

    [Header("Floor Sentence Attack")]
    [SerializeField, Min(1f)] float floorSentenceFontSize = 300f;
    [SerializeField, Min(0.001f)] float floorSentenceWorldScale = 0.05f;
    [SerializeField, Min(1)] int floorSentenceMinCount = 3;
    [SerializeField, Min(1)] int floorSentenceMaxCount = 5;
    [SerializeField, Min(0f)] float floorSentenceSpawnDelay = 0.14f;
    [SerializeField, Min(0.1f)] float floorSentenceWarningTime = 1.1f;
    [SerializeField, Min(0.1f)] float floorSentenceActiveTime = 1.55f;
    [SerializeField, Min(0f)] float floorSentenceToPaperDelay = 1.1f;
    [SerializeField, Min(0.01f)] float floorSentenceImpactShakeDuration = 0.18f;
    [SerializeField, Min(0f)] float floorSentenceImpactShakeMagnitude = 0.18f;
    [SerializeField, Min(0f)] float floorSentencePlayerSafeRadius = 3.2f;

    [Header("Paper Scrap Attack")]
    [SerializeField, Min(0.1f)] float paperScrapSpeed = 10.2f;
    [SerializeField, Min(0.1f)] float paperScrapMaxTime = 2.8f;
    [SerializeField, Min(0.1f)] float paperScrapWarningTime = 1.75f;
    [SerializeField, Min(0.1f)] float paperScrapWarningRadius = 1.05f;
    [SerializeField] Vector2 paperScrapVisualSize = new Vector2(0.82f, 1.05f);

    [Header("Wave 2 Ink Rain")]
    [SerializeField] Vector2 wave2FloorSentenceRestRange = new Vector2(1.6f, 2.2f);
    [SerializeField] Vector2 inkRainIntervalRange = new Vector2(0.65f, 0.95f);
    [SerializeField] Vector2Int inkRainBurstCountRange = new Vector2Int(2, 4);
    [SerializeField, Min(0.1f)] float inkRainWarningTime = 0.85f;
    [SerializeField, Min(0.1f)] float inkRainWarningRadius = 0.8f;
    [SerializeField] Color inkRainWarningColor = new Color(0.38f, 0.58f, 1f, 0.72f);

    [Header("Wave 3 Frenzy")]
    [SerializeField, Min(0.1f)] float wave3IntroLockDuration = 2.8f;
    [SerializeField, Min(0.1f)] float wave3IntroCameraSize = 4.2f;
    [SerializeField, Range(0f, 0.4f)] float wave3ShakeMagnitude = 0.14f;
    [SerializeField, Min(0.05f)] float wave3ShakeInterval = 0.28f;
    [SerializeField, Min(1)] int wave3MaxMinions = 5;
    [SerializeField] Vector2 wave3MinionSpawnInterval = new Vector2(2.4f, 3.4f);
    [SerializeField, Min(0.1f)] float wave3MinionSpawnWarningTime = 1.05f;
    [SerializeField, Min(0.1f)] float wave3MinionSpawnWarningRadius = 1.15f;
    [SerializeField, Min(0f)] float wave3AttackInitialDelay = 2.3f;
    [SerializeField, Min(0f)] float wave3BasicLetterExtraDelay = 1.25f;

    [Header("Word Fragment Drop")]
    [SerializeField, Min(0f)] float wordFragmentBossColliderClearance = 0.9f;

    [Header("Rainy Screen Distortion")]
    [SerializeField] bool enableRainyScreenDistortion = true;
    [SerializeField, Range(0f, 0.08f)] float distortionStrength = 0.018f;
    [SerializeField, Min(0f)] float distortionSpeed = 0.22f;
    [SerializeField, Min(0.1f)] float distortionScale = 5.5f;

    [Header("Scene Parts")]
    [SerializeField] BookBossPart body;
    [SerializeField] BookBossPart leftArm;
    [SerializeField] BookBossPart rightArm;

    static bool hooked;
    static Sprite squareSprite;
    static Sprite wave3SpawnMarkerSprite;

    bool leftDead;
    bool rightDead;

    Transform player;
    PlayerController playerController;
    PlayerDamageReceiver receiver;

    readonly List<string> collectedWords = new List<string>();
    readonly List<EnemyBase> minions = new List<EnemyBase>();
    readonly HashSet<int> creditedMinionDeaths = new HashSet<int>();
    readonly List<PoisonZone> poisonZones = new List<PoisonZone>();
    int leftLettersDropped;
    int rightLettersDropped;

    bool bossDefeated;
    bool endingStarted;
    bool armsTyping;
    bool strongShake;
    bool wave3IntroPlayed;
    float armBobTime;
    float nextPoisonTick;

    TextMeshPro floorEndingText;
    GameObject darkenOverlay;
    GameObject wave3PulseOverlay;
    Coroutine letterFallLoop;
    Coroutine rainWobbleLoop;
    Coroutine wave3PulseLoop;
    Coroutine bodyWave3HitRoutine;
    RainyScreenDistortion rainyScreenDistortion;
    float restoreCameraSize;
    Coroutine cameraZoomRoutine;

    class PoisonZone
    {
        public Vector2 center;
        public float radius;
        public float endTime;     // float.MaxValue = persistent
        public GameObject visual;
        public string cause;
    }

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

        if (FindFirstObjectByType<BookBossController>() != null)
            return;

        new GameObject("BookBossController").AddComponent<BookBossController>();
    }

    void Awake()
    {
        PurgeLeftoverWaveRoom();
    }

    void Start()
    {
        MapRunState.EnsureRun();
        if (MapRunState.PendingNode != null)
            MapRunState.CompletePendingRoom();

        SetupCamera();
        ResolvePlayer();
        if (player != null)
            player.position = playerSpawn;

        SpawnParts();
        RunHudUI.SetWaveHudVisible(false);
        StartCoroutine(BossRoutine());
    }

    void OnDestroy()
    {
        SetRainyScreenDistortion(false);
        if (wave3PulseOverlay != null)
        {
            Destroy(wave3PulseOverlay);
            wave3PulseOverlay = null;
        }
        wave3PulseLoop = null;
        RunHudUI.SetWaveHudVisible(true);
    }

    void Update()
    {
        UpdateArmBob();
        UpdatePoison();
        UpdateHud();
    }

    // ---- setup ------------------------------------------------------------

    void PurgeLeftoverWaveRoom()
    {
        Room[] rooms = FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != null)
                rooms[i].enabled = false;

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
            if (enemies[i] != null && enemies[i].GetComponent<BookBossPart>() == null)
                Destroy(enemies[i].gameObject);

        GameObject door = GameObject.Find("Door_Exit");
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
        if (player != null)
            targetPosition = new Vector3(player.position.x, player.position.y, startPosition.z);

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
                    follow.ConfigureBounds(arenaSize, arenaCenter, targetSize, true);
                    follow.enabled = true;
                }
            }));
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

    void ResolvePlayer()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerController = playerObject.GetComponent<PlayerController>();
        receiver = playerObject.GetComponent<PlayerDamageReceiver>();
    }

    void SpawnParts()
    {
        Vector2 bodyCenter = new Vector2(arenaCenter.x, arenaCenter.y + arenaSize.y * 0.5f - 3.2f);
        body = ResolvePart(body, BookPartType.Body, bodyHp, "bookboss1", bodyCenter, 1.7f, 70, false);
        leftArm = ResolvePart(leftArm, BookPartType.LeftArm, armHp, "bookboss_leftarm", bodyCenter + new Vector2(-3.4f, -0.4f), 1.4f, 72, true);
        rightArm = ResolvePart(rightArm, BookPartType.RightArm, armHp, "bookboss_rightarm", bodyCenter + new Vector2(3.4f, -0.4f), 1.4f, 72, true);
        ConfigureBodyIdleAnimation();

        leftArm.Destroyed += OnArmDestroyed;
        rightArm.Destroyed += OnArmDestroyed;
        leftArm.Damaged += OnArmDamaged;
        rightArm.Damaged += OnArmDamaged;
    }

    BookBossPart ResolvePart(BookBossPart placedPart, BookPartType type, int hp, string spriteName, Vector2 fallbackCenter, float fallbackScale, int sortingOrder, bool damageable)
    {
        Sprite sprite = LoadEnemySprite(spriteName);
        if (placedPart != null)
        {
            // A hierarchy-authored part owns its own Max HP value in the Inspector.
            placedPart.ConfigurePlaced(type, placedPart.MaxHp, sprite, sortingOrder, damageable);
            return placedPart;
        }

        return CreatePart(type, hp, spriteName, fallbackCenter, fallbackScale, sortingOrder, damageable);
    }

    BookBossPart CreatePart(BookPartType type, int hp, string spriteName, Vector2 center, float scale, int sortingOrder, bool damageable)
    {
        GameObject go = new GameObject("BookBossPart_" + type);
        go.AddComponent<SpriteRenderer>();
        BookBossPart part = go.AddComponent<BookBossPart>();
        part.Configure(type, hp, LoadEnemySprite(spriteName), center, scale, sortingOrder, damageable);
        return part;
    }

    void ConfigureBodyIdleAnimation()
    {
        if (body == null)
            return;

        SpriteFrameAnimator animator = body.GetComponent<SpriteFrameAnimator>();
        if (animator == null)
            animator = body.gameObject.AddComponent<SpriteFrameAnimator>();

        Sprite[] frames =
        {
            LoadEnemySprite("bookboss1"),
            LoadEnemySprite("bookboss2"),
            LoadEnemySprite("bookboss3"),
            LoadEnemySprite("bookboss2")
        };

        animator.Configure(frames, new[] { 0.18f, 0.14f, 0.22f, 0.14f }, true, true, true);
    }

    // ---- wave state -------------------------------------------------------

    int CurrentWave()
    {
        if (leftDead && rightDead) return 3;
        if (leftDead || rightDead) return 2;
        return 1;
    }

    void OnArmDestroyed(BookBossPart arm)
    {
        if (arm.PartType == BookPartType.LeftArm) leftDead = true;
        if (arm.PartType == BookPartType.RightArm) rightDead = true;
        CameraShake.Shake(0.3f, 0.34f);
    }

    void OnArmDamaged(BookBossPart arm)
    {
        int dropped = arm.PartType == BookPartType.LeftArm ? leftLettersDropped : rightLettersDropped;
        int shouldHave = (arm.MaxHp - Mathf.Max(0, arm.CurrentHp)) / 7;
        while (dropped < shouldHave)
        {
            DropLetter(arm);
            dropped++;
        }

        if (arm.PartType == BookPartType.LeftArm) leftLettersDropped = dropped;
        else rightLettersDropped = dropped;
    }

    void DropLetter(BookBossPart sourcePart)
    {
        string word = BookLetters.Fragments[Random.Range(0, BookLetters.Fragments.Length)];
        Vector2 pos = LetterDropPosition(sourcePart);
        LetterPickup.Spawn(word, pos, OnWordCollected);
    }

    Vector2 LetterDropPosition(BookBossPart sourcePart)
    {
        Rect bounds = FloorSentencePlacementBounds();
        Vector2 sourcePosition = sourcePart != null ? sourcePart.BasePosition : arenaCenter;
        Vector2 outward = WordFragmentOutwardDirection(sourcePart);
        Vector2 best = ClampToDropBounds(ProjectOutsideBossColliders(sourcePosition + outward * 2.8f), bounds);
        float bestScore = float.NegativeInfinity;

        for (int attempt = 0; attempt < 90; attempt++)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f)) * outward;
            float distance = Random.Range(2.4f, 5.2f);
            Vector2 candidate = ClampToDropBounds(sourcePosition + direction.normalized * distance, bounds);
            candidate = ClampToDropBounds(ProjectOutsideBossColliders(candidate), bounds);

            if (TouchesAnyBossPartCollider(candidate, wordFragmentBossColliderClearance))
                continue;

            float distanceFromSource = Vector2.Distance(candidate, sourcePosition);
            float colliderDistance = DistanceToNearestBossPartCollider(candidate);
            float score = distanceFromSource * 0.75f + colliderDistance * 0.9f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }

            if (colliderDistance > wordFragmentBossColliderClearance + 0.6f && distanceFromSource >= 2.6f)
                break;
        }

        return ClampToDropBounds(ProjectOutsideBossColliders(best), bounds);
    }

    Vector2 WordFragmentOutwardDirection(BookBossPart sourcePart)
    {
        Vector2 sourcePosition = sourcePart != null ? sourcePart.BasePosition : arenaCenter;
        Vector2 bodyPosition = body != null ? body.BasePosition : arenaCenter;
        Vector2 direction = sourcePosition - bodyPosition;

        if (sourcePart != null && sourcePart.PartType == BookPartType.LeftArm)
            direction += Vector2.left * 1.4f + Vector2.down * 0.35f;
        else if (sourcePart != null && sourcePart.PartType == BookPartType.RightArm)
            direction += Vector2.right * 1.4f + Vector2.down * 0.35f;
        else
            direction += Vector2.down;

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
    }

    Vector2 ClampToDropBounds(Vector2 point, Rect bounds)
    {
        return new Vector2(
            Mathf.Clamp(point.x, bounds.xMin + 1.2f, bounds.xMax - 1.2f),
            Mathf.Clamp(point.y, bounds.yMin + 1.2f, bounds.yMax - 1.2f));
    }

    Vector2 ProjectOutsideBossColliders(Vector2 point)
    {
        Vector2 projected = point;
        for (int i = 0; i < 6; i++)
        {
            bool moved = false;
            moved |= PushOutsideBossPartCollider(body, ref projected);
            moved |= PushOutsideBossPartCollider(leftArm, ref projected);
            moved |= PushOutsideBossPartCollider(rightArm, ref projected);
            if (!moved)
                break;
        }

        return projected;
    }

    bool PushOutsideBossPartCollider(BookBossPart part, ref Vector2 point)
    {
        if (part == null)
            return false;

        Collider2D collider = part.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled)
            return false;

        Vector2 closest = collider.ClosestPoint(point);
        float distance = Vector2.Distance(closest, point);
        float clearance = Mathf.Max(0f, wordFragmentBossColliderClearance);
        if (distance > clearance && !collider.OverlapPoint(point))
            return false;

        Vector2 partCenter = collider.bounds.center;
        Vector2 direction = point - partCenter;
        if (direction.sqrMagnitude < 0.001f)
            direction = WordFragmentOutwardDirection(part);

        point = closest + direction.normalized * (clearance + 0.35f);
        return true;
    }

    bool TouchesAnyBossPartCollider(Vector2 point, float clearance)
    {
        return TouchesBossPartCollider(body, point, clearance)
            || TouchesBossPartCollider(leftArm, point, clearance)
            || TouchesBossPartCollider(rightArm, point, clearance);
    }

    bool TouchesBossPartCollider(BookBossPart part, Vector2 point, float clearance)
    {
        if (part == null)
            return false;

        Collider2D collider = part.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled)
            return false;

        Vector2 closest = collider.ClosestPoint(point);
        if (Vector2.Distance(closest, point) <= Mathf.Max(0f, clearance))
            return true;

        return collider.OverlapPoint(point);
    }

    float DistanceToNearestBossPartCollider(Vector2 point)
    {
        float nearest = float.MaxValue;
        nearest = Mathf.Min(nearest, DistanceToBossPartCollider(body, point));
        nearest = Mathf.Min(nearest, DistanceToBossPartCollider(leftArm, point));
        nearest = Mathf.Min(nearest, DistanceToBossPartCollider(rightArm, point));
        return nearest < float.MaxValue ? nearest : 999f;
    }

    float DistanceToBossPartCollider(BookBossPart part, Vector2 point)
    {
        if (part == null)
            return float.MaxValue;

        Collider2D collider = part.GetComponent<Collider2D>();
        if (collider == null || !collider.enabled)
            return float.MaxValue;

        return Vector2.Distance(collider.ClosestPoint(point), point);
    }

    void OnWordCollected(string word)
    {
        if (!collectedWords.Contains(word))
            collectedWords.Add(word);
        RunHudUI.SetCollectedWords(collectedWords);
    }

    // ---- main loop --------------------------------------------------------

    IEnumerator BossRoutine()
    {
        RunHudUI.SetCollectedWords(collectedWords);
        yield return StartCoroutine(UnfoldIntro());

        while (!endingStarted && !bossDefeated)
        {
            int wave = CurrentWave();
            RunHudUI.SetWave(wave, 3);

            if (wave >= 3)
            {
                yield return StartCoroutine(Wave3Routine());
                break;
            }

            if (wave == 2 && letterFallLoop == null)
                letterFallLoop = StartCoroutine(LetterFallLoop());

            yield return StartCoroutine(BasicLetterAttack());
            if (CurrentWave() >= 3) continue;

            if (CurrentWave() == 1)
            {
                yield return new WaitForSeconds(floorSentenceToPaperDelay);
                yield return StartCoroutine(PaperAttack());
            }
            else if (CurrentWave() == 2)
            {
                yield return new WaitForSeconds(RandomRest(wave2FloorSentenceRestRange));
            }

            yield return new WaitForSeconds(0.65f);
        }
    }

    IEnumerator UnfoldIntro()
    {
        if (body == null)
            yield break;

        Vector3 baseScale = body.transform.localScale;
        float elapsed = 0f;
        float duration = 0.7f;
        body.transform.localScale = new Vector3(baseScale.x * 0.1f, baseScale.y, baseScale.z);
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            body.transform.localScale = new Vector3(baseScale.x * Mathf.Lerp(0.1f, 1.12f, eased), baseScale.y, baseScale.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        body.transform.localScale = baseScale;
        SoundManager.PlayPanel();
    }

    // ---- pattern 1: typed sentences --------------------------------------

    IEnumerator BasicLetterAttack()
    {
        armsTyping = true;

        int minCount = Mathf.Max(1, floorSentenceMinCount);
        int maxCount = Mathf.Max(minCount, floorSentenceMaxCount);
        int sentenceCount = Random.Range(minCount, maxCount + 1);
        string[] sentences = new string[sentenceCount];
        for (int i = 0; i < sentenceCount; i++)
            sentences[i] = BookLetters.AttackSentences[Random.Range(0, BookLetters.AttackSentences.Length)];

        List<Vector2> positions = GenerateFloorSentencePositions(sentences);
        List<Coroutine> running = new List<Coroutine>();
        for (int i = 0; i < sentenceCount; i++)
        {
            string sentence = sentences[i];
            Vector2 pos = positions.Count > i ? positions[i] : RandomFloorSentencePosition();
            running.Add(StartCoroutine(FloorSentenceRoutine(sentence, pos)));
            yield return new WaitForSeconds(floorSentenceSpawnDelay);
        }

        for (int i = 0; i < running.Count; i++)
            yield return running[i];

        armsTyping = false;
    }

    List<Vector2> GenerateFloorSentencePositions(string[] sentences)
    {
        List<Vector2> positions = new List<Vector2>();
        int count = sentences != null ? sentences.Length : 0;
        if (count <= 0)
            return positions;

        Rect placementBounds = FloorSentencePlacementBounds();
        float minX = placementBounds.xMin;
        float maxX = placementBounds.xMax;
        float minY = placementBounds.yMin;
        float maxY = placementBounds.yMax;
        float usableHeight = Mathf.Max(0.01f, maxY - minY);
        List<Rect> occupied = new List<Rect>();
        List<int> verticalBands = new List<int>(count);
        for (int i = 0; i < count; i++)
            verticalBands.Add(i);
        for (int i = 0; i < verticalBands.Count; i++)
        {
            int swap = Random.Range(i, verticalBands.Count);
            int temp = verticalBands[i];
            verticalBands[i] = verticalBands[swap];
            verticalBands[swap] = temp;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 halfExtents = EstimateFloorSentenceHalfExtents(sentences[i]);
            Rect bestRect = default;
            Vector2 bestPos = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            bool placed = false;

            for (int attempt = 0; attempt < 140; attempt++)
            {
                float x = RandomRangeSafe(minX + halfExtents.x, maxX - halfExtents.x);
                float bandHeight = usableHeight / count;
                float bandMinY = minY + bandHeight * verticalBands[i] + halfExtents.y;
                float bandMaxY = minY + bandHeight * (verticalBands[i] + 1) - halfExtents.y;
                float y = attempt < 95
                    ? RandomRangeSafe(bandMinY, bandMaxY)
                    : RandomRangeSafe(minY + halfExtents.y, maxY - halfExtents.y);
                Vector2 candidate = new Vector2(x, y);
                Rect rect = SentenceRect(candidate, halfExtents);
                if (IsInsideFloorSentencePlayerSafeZone(candidate, halfExtents))
                    continue;

                float score = DistanceScore(rect, occupied);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRect = rect;
                    bestPos = candidate;
                }

                if (!OverlapsAny(rect, occupied))
                {
                    placed = true;
                    break;
                }
            }

            positions.Add(bestPos);
            occupied.Add(placed ? SentenceRect(bestPos, halfExtents) : bestRect);
        }

        return positions;
    }

    Rect FloorSentencePlacementBounds()
    {
        StageBackgroundSprite background = FindFirstObjectByType<StageBackgroundSprite>();
        SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            float insetX = Mathf.Max(1.2f, bounds.size.x * 0.07f);
            float insetY = Mathf.Max(1.0f, bounds.size.y * 0.08f);
            return new Rect(
                bounds.min.x + insetX,
                bounds.min.y + insetY,
                Mathf.Max(1f, bounds.size.x - insetX * 2f),
                Mathf.Max(1f, bounds.size.y - insetY * 2f));
        }

        return new Rect(
            arenaCenter.x - arenaSize.x * 0.5f,
            arenaCenter.y - arenaSize.y * 0.5f,
            arenaSize.x,
            arenaSize.y);
    }

    Vector2 EstimateFloorSentenceHalfExtents(string sentence)
    {
        float fontSize = Mathf.Max(1f, floorSentenceFontSize);
        float worldScale = Mathf.Max(0.001f, floorSentenceWorldScale);
        int length = Mathf.Max(4, string.IsNullOrEmpty(sentence) ? 4 : sentence.Length);
        float halfWidth = Mathf.Clamp(length * fontSize * worldScale * 0.017f, 2.4f, arenaSize.x * 0.22f);
        float halfHeight = Mathf.Clamp(fontSize * worldScale * 0.055f, 0.55f, arenaSize.y * 0.10f);
        return new Vector2(halfWidth + 0.55f, halfHeight + 0.32f);
    }

    Rect SentenceRect(Vector2 center, Vector2 halfExtents)
    {
        return new Rect(center.x - halfExtents.x, center.y - halfExtents.y, halfExtents.x * 2f, halfExtents.y * 2f);
    }

    bool OverlapsAny(Rect rect, List<Rect> occupied)
    {
        for (int i = 0; i < occupied.Count; i++)
            if (rect.Overlaps(occupied[i]))
                return true;
        return false;
    }

    bool IsInsideFloorSentencePlayerSafeZone(Vector2 candidate, Vector2 halfExtents)
    {
        float radius = Mathf.Max(0f, floorSentencePlayerSafeRadius);
        if (radius <= 0f)
            return false;

        Vector2 safeCenter = player != null ? (Vector2)player.position : (Vector2)playerSpawn;
        float paddedRadius = radius + Mathf.Max(halfExtents.x * 0.45f, halfExtents.y);
        return Vector2.Distance(candidate, safeCenter) < paddedRadius;
    }

    float DistanceScore(Rect rect, List<Rect> occupied)
    {
        if (occupied.Count == 0)
            return 999f;

        Vector2 center = rect.center;
        float nearest = float.MaxValue;
        for (int i = 0; i < occupied.Count; i++)
            nearest = Mathf.Min(nearest, Vector2.Distance(center, occupied[i].center));
        return nearest;
    }

    float RandomRangeSafe(float min, float max)
    {
        if (max < min)
            return (min + max) * 0.5f;
        return Random.Range(min, max);
    }

    Vector2 RandomFloorSentencePosition()
    {
        Rect bounds = FloorSentencePlacementBounds();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector2 candidate = new Vector2(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax));
            if (!IsInsideFloorSentencePlayerSafeZone(candidate, Vector2.zero))
                return candidate;
        }

        return new Vector2(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax));
    }

    IEnumerator FloorSentenceRoutine(string sentence, Vector2 pos)
    {
        float fontSize = Mathf.Max(1f, floorSentenceFontSize);
        float worldScale = Mathf.Max(0.001f, floorSentenceWorldScale);
        TextMeshPro text = CreateWorldText(sentence, pos, fontSize, new Color(0.85f, 0.18f, 0.18f, 0.45f), 55);
        text.fontStyle = FontStyles.Bold;
        text.transform.localScale = Vector3.one * worldScale;
        text.maxVisibleCharacters = 0;

        int total = sentence.Length;
        float typeDuration = 0.45f;
        float elapsed = 0f;
        while (elapsed < typeDuration)
        {
            text.maxVisibleCharacters = Mathf.RoundToInt(Mathf.Lerp(0, total, elapsed / typeDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        text.maxVisibleCharacters = total;

        Color warningDim = new Color(0.85f, 0.18f, 0.18f, 0.28f);
        Color warningBright = new Color(1f, 0.08f, 0.08f, 0.82f);
        float warningElapsed = 0f;
        while (warningElapsed < floorSentenceWarningTime && text != null)
        {
            float blink = Mathf.PingPong(warningElapsed * 9f, 1f);
            text.color = Color.Lerp(warningDim, warningBright, blink);
            warningElapsed += Time.deltaTime;
            yield return null;
        }

        text.color = new Color(0.34f, 0.13f, 0.07f, 1f);
        CameraShake.Shake(floorSentenceImpactShakeDuration, floorSentenceImpactShakeMagnitude);

        text.ForceMeshUpdate();
        Vector2 textWorldCenter = text.transform.TransformPoint(text.textBounds.center);
        float halfWidth  = text.textBounds.extents.x * worldScale;
        float halfHeight = text.textBounds.extents.y * worldScale;

        float activeTime = floorSentenceActiveTime;
        float activeElapsed = 0f;
        while (activeElapsed < activeTime)
        {
            if (receiver == null)
                receiver = FindFirstObjectByType<PlayerDamageReceiver>();

            // 바닥 글자(문장) 피격판정은 플레이어의 Box 히트박스 기준.
            if (receiver != null
                && receiver.FloorAttackHitsRect(textWorldCenter, new Vector2(halfWidth, halfHeight)))
            {
                DamagePlayer(letterDamage, 0.5f, $"바닥 문장: \"{sentence}\"");
            }

            activeElapsed += Time.deltaTime;
            yield return null;
        }

        float fade = 0.28f;
        float fadeElapsed = 0f;
        Color from = text.color;
        while (fadeElapsed < fade && text != null)
        {
            from.a = 1f - fadeElapsed / fade;
            text.color = from;
            fadeElapsed += Time.deltaTime;
            yield return null;
        }

        if (text != null)
            Destroy(text.gameObject);
    }

    // ---- pattern 2: paper scraps -----------------------------------------

    IEnumerator PaperAttack()
    {
        if (body == null)
            yield break;

        int scraps = Random.Range(3, 6);
        for (int i = 0; i < scraps; i++)
        {
            StartCoroutine(PaperScrapRoutine());
            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator PaperScrapRoutine()
    {
        Vector2 start = body.BasePosition;
        Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
        yield return StartCoroutine(PaperScrapWarningRoutine(target));

        GameObject scrap = new GameObject("PaperScrap");
        scrap.transform.position = start;
        scrap.transform.localScale = new Vector3(
            Mathf.Max(0.1f, paperScrapVisualSize.x),
            Mathf.Max(0.1f, paperScrapVisualSize.y),
            1f);
        scrap.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        SpriteRenderer renderer = scrap.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = new Color(0.95f, 0.92f, 0.84f, 1f);
        renderer.sortingOrder = 58;

        Vector2 direction = (target - start).normalized;
        float speed = paperScrapSpeed;
        float maxTime = paperScrapMaxTime;
        float elapsed = 0f;
        bool hit = false;
        while (elapsed < maxTime)
        {
            scrap.transform.position += (Vector3)(direction * speed * Time.deltaTime);
            scrap.transform.Rotate(0f, 0f, 360f * Time.deltaTime);

            if (player != null && Vector2.Distance(scrap.transform.position, player.position) <= 0.7f)
            {
                DamagePlayer(paperDamage, 0.5f, "종이 조각");
                hit = true;
                break;
            }

            if (Vector2.Distance(scrap.transform.position, target) <= 0.4f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hit)
        {
            Destroy(scrap);
            yield break;
        }

        // embedded: spawn a dashed poison ring around the landing spot
        Vector2 landing = scrap.transform.position;
        renderer.color = new Color(0.82f, 0.78f, 0.68f, 1f);
        GameObject ringVisual = CreatePoisonRingVisual(landing, 1.8f);
        AddPoisonZone(landing, 1.8f, Time.time + 4f, ringVisual, "종이 독 링");

        float life = 4f;
        float lifeElapsed = 0f;
        while (lifeElapsed < life && scrap != null)
        {
            float a = 1f - lifeElapsed / life;
            renderer.color = new Color(0.82f, 0.78f, 0.68f, a);
            lifeElapsed += Time.deltaTime;
            yield return null;
        }

        if (scrap != null)
            Destroy(scrap);
    }

    // ---- pattern 3: falling letters --------------------------------------

    IEnumerator LetterFallLoop()
    {
        if (rainWobbleLoop == null)
            rainWobbleLoop = StartCoroutine(RainWobbleLoop());
        SetRainyScreenDistortion(CurrentWave() == 2);

        while (!endingStarted && !bossDefeated && (CurrentWave() == 2 || CurrentWave() == 3))
        {
            SetRainyScreenDistortion(CurrentWave() == 2);
            int burstCount = CurrentWave() == 2 ? RandomInkRainBurstCount() : 1;
            for (int i = 0; i < burstCount; i++)
            {
                StartCoroutine(FallingGlyphRoutine());
                if (i < burstCount - 1)
                    yield return new WaitForSeconds(Random.Range(0.08f, 0.18f));
            }
            yield return new WaitForSeconds(RandomRest(inkRainIntervalRange));
        }

        SetRainyScreenDistortion(false);
        letterFallLoop = null;
    }

    IEnumerator FallingGlyphRoutine()
    {
        string glyph = RandomAttackSentenceGlyph();
        Rect rainBounds = FloorSentencePlacementBounds();
        float x = Random.Range(rainBounds.xMin, rainBounds.xMax);
        float groundY = Random.Range(rainBounds.yMin, rainBounds.yMax);
        float topY = rainBounds.yMax + 1.6f;
        Vector2 target = new Vector2(x, groundY);

        yield return StartCoroutine(InkRainWarningRoutine(target));

        TextMeshPro text = CreateWorldText(glyph, new Vector2(x, topY), 2.2f, new Color(0.1f, 0.08f, 0.08f, 1f), 60);
        text.fontStyle = FontStyles.Bold;

        float fallDuration = 0.7f;
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            float t = elapsed / fallDuration;
            text.transform.position = new Vector3(x, Mathf.Lerp(topY, groundY, t), 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        CameraShake.Shake(0.16f, 0.11f);
        StartCoroutine(GlyphSplashRoutine(target));

        // ink stain: small persistent poison
        GameObject stain = new GameObject("InkStain");
        stain.transform.position = new Vector3(x, groundY, 0f);
        SpriteRenderer stainRenderer = stain.AddComponent<SpriteRenderer>();
        stainRenderer.sprite = BossVisuals.CircleSprite();
        stainRenderer.color = new Color(0.06f, 0.05f, 0.08f, 0.7f);
        stainRenderer.sortingOrder = 52;
        stain.transform.localScale = new Vector3(1.5f, 1.0f, 1f);
        AddPoisonZone(new Vector2(x, groundY), 0.8f, Time.time + 12f, stain, "잉크 얼룩");

        if (text != null)
        {
            float fade = 0.4f;
            float fe = 0f;
            Color c = text.color;
            while (fe < fade && text != null)
            {
                c.a = 1f - fe / fade;
                text.color = c;
                fe += Time.deltaTime;
                yield return null;
            }
            Destroy(text.gameObject);
        }
    }

    int RandomInkRainBurstCount()
    {
        int min = Mathf.Max(1, Mathf.Min(inkRainBurstCountRange.x, inkRainBurstCountRange.y));
        int max = Mathf.Max(min, Mathf.Max(inkRainBurstCountRange.x, inkRainBurstCountRange.y));
        return Random.Range(min, max + 1);
    }

    IEnumerator PaperScrapWarningRoutine(Vector2 target)
    {
        GameObject warning = CreateDashedCircle("PaperScrapWarning", target, paperScrapWarningRadius, new Color(0.35f, 1f, 0.28f, 0.8f), 57);
        SpriteRenderer[] renderers = warning.GetComponentsInChildren<SpriteRenderer>();
        Color[] baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;

        float duration = Mathf.Max(0.1f, paperScrapWarningTime);
        float elapsed = 0f;
        while (elapsed < duration && warning != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 18f);
            warning.transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.12f, pulse);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = baseColors[i];
                c.a = Mathf.Lerp(0.35f, 0.95f, pulse);
                renderers[i].color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (warning != null)
            Destroy(warning);
    }

    IEnumerator InkRainWarningRoutine(Vector2 target)
    {
        GameObject warning = CreateDashedCircle("InkRainWarning", target, inkRainWarningRadius, inkRainWarningColor, 57);
        SpriteRenderer[] renderers = warning.GetComponentsInChildren<SpriteRenderer>();
        Color[] baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].color;

        float duration = Mathf.Max(0.1f, inkRainWarningTime);
        float elapsed = 0f;
        while (elapsed < duration && warning != null)
        {
            float t = elapsed / duration;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 10f);
            float breathe = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t * 1.6f, 1f));
            warning.transform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.08f, breathe);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = baseColors[i];
                c.a = Mathf.Lerp(0.22f, 0.82f, pulse);
                renderers[i].color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (warning != null)
            Destroy(warning);
    }

    float RandomRest(Vector2 range)
    {
        float min = Mathf.Max(0f, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return Random.Range(min, max);
    }

    void SetRainyScreenDistortion(bool active)
    {
        if (!enableRainyScreenDistortion)
            active = false;

        if (rainyScreenDistortion == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                rainyScreenDistortion = cam.GetComponent<RainyScreenDistortion>();
                if (rainyScreenDistortion == null)
                    rainyScreenDistortion = cam.gameObject.AddComponent<RainyScreenDistortion>();
            }
        }

        if (rainyScreenDistortion == null)
            return;

        rainyScreenDistortion.Configure(distortionStrength, distortionSpeed, distortionScale);
        rainyScreenDistortion.SetEffectActive(active);
    }

    GameObject CreateDashedCircle(string name, Vector2 center, float radius, Color color, int order)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(center.x, center.y, -0.08f);
        int segments = 28;
        for (int i = 0; i < segments; i += 2)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
            Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            BossVisuals.CreateDashedLine(root.transform, "Arc_" + i, p0, p1, 0.08f, color, order, 0.18f, 0.08f);
        }

        return root;
    }

    string RandomAttackSentenceGlyph()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            string sentence = BookLetters.AttackSentences[Random.Range(0, BookLetters.AttackSentences.Length)];
            if (string.IsNullOrEmpty(sentence))
                continue;

            char ch = sentence[Random.Range(0, sentence.Length)];
            if (!char.IsWhiteSpace(ch))
                return ch.ToString();
        }

        return BookLetters.FallingGlyphs[Random.Range(0, BookLetters.FallingGlyphs.Length)];
    }

    IEnumerator RainWobbleLoop()
    {
        while (!endingStarted && !bossDefeated && (CurrentWave() == 2 || CurrentWave() == 3))
        {
            CameraShake.Shake(0.16f, 0.035f);
            yield return new WaitForSeconds(Random.Range(0.35f, 0.55f));
        }

        rainWobbleLoop = null;
    }

    IEnumerator GlyphSplashRoutine(Vector2 center)
    {
        int droplets = Random.Range(7, 11);
        for (int i = 0; i < droplets; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            StartCoroutine(SplashDroplet(center, direction, Random.Range(0.35f, 0.75f)));
        }

        GameObject ring = CreateDashedCircle("GlyphSplashRing", center, 0.34f, new Color(0.58f, 0.72f, 1f, 0.55f), 61);
        float elapsed = 0f;
        while (elapsed < 0.28f && ring != null)
        {
            float t = elapsed / 0.28f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.7f, t);
            SpriteRenderer[] renderers = ring.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = renderers[i].color;
                c.a = Mathf.Lerp(0.55f, 0f, t);
                renderers[i].color = c;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ring != null)
            Destroy(ring);
    }

    IEnumerator SplashDroplet(Vector2 center, Vector2 direction, float distance)
    {
        GameObject droplet = new GameObject("GlyphSplashDroplet");
        droplet.transform.position = new Vector3(center.x, center.y, -0.09f);
        droplet.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
        SpriteRenderer renderer = droplet.AddComponent<SpriteRenderer>();
        renderer.sprite = BossVisuals.CircleSprite();
        renderer.color = new Color(0.48f, 0.62f, 0.95f, 0.75f);
        renderer.sortingOrder = 62;

        Vector2 start = center;
        Vector2 end = center + direction.normalized * distance;
        float duration = 0.32f;
        float elapsed = 0f;
        while (elapsed < duration && droplet != null)
        {
            float t = elapsed / duration;
            Vector2 pos = Vector2.Lerp(start, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.25f;
            droplet.transform.position = new Vector3(pos.x, pos.y, -0.09f);
            Color c = renderer.color;
            c.a = Mathf.Lerp(0.75f, 0f, t);
            renderer.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (droplet != null)
            Destroy(droplet);
    }

    // ---- pattern 4: wave 3 minions + ending ------------------------------

    IEnumerator Wave3Routine()
    {
        RunHudUI.SetWave(3, 3);
        if (!wave3IntroPlayed)
            yield return StartCoroutine(Wave3IntroRoutine());

        strongShake = true;
        if (wave3PulseLoop == null)
            wave3PulseLoop = StartCoroutine(Wave3PulseLoop());
        if (letterFallLoop == null)
            letterFallLoop = StartCoroutine(LetterFallLoop());

        // the book writes the doll's ending on the floor
        floorEndingText = CreateWorldText(BookLetters.BookEnding, new Vector2(arenaCenter.x, arenaCenter.y - arenaSize.y * 0.5f + 1.6f), 1.3f, new Color(0.75f, 0.12f, 0.12f, 0.9f), 56);

        StartCoroutine(StrongShakeLoop());
        StartCoroutine(MinionSpawnLoop());
        StartCoroutine(Wave3AttackLoop());

        int threshold = Mathf.CeilToInt(body.MaxHp * 0.05f);
        while (!bossDefeated && body != null && body.CurrentHp > threshold)
            yield return null;

        endingStarted = true;
        strongShake = false;
        StopWave3Pulse();
        yield return StartCoroutine(EndingSequence());
    }

    IEnumerator Wave3IntroRoutine()
    {
        wave3IntroPlayed = true;

        float lockDuration = Mathf.Max(0.1f, wave3IntroLockDuration);
        if (playerController != null)
            playerController.LockMovement(lockDuration);

        Camera cam = Camera.main;
        Vector3 restorePosition = cam != null ? cam.transform.position : Vector3.zero;
        float restoreSize = cam != null ? cam.orthographicSize : fallbackRestoreCameraSize;

        if (cam != null && body != null)
        {
            Vector3 target = new Vector3(body.BasePosition.x, body.BasePosition.y - 0.35f, cam.transform.position.z);
            if (cameraZoomRoutine != null)
                StopCoroutine(cameraZoomRoutine);

            yield return StartCoroutine(SmoothCameraFrame(
                cam,
                cam.transform.position,
                target,
                cam.orthographicSize,
                Mathf.Max(0.1f, wave3IntroCameraSize),
                0.65f,
                null));
        }

        if (body != null)
            yield return StartCoroutine(BookFrenzyTremble(1.25f));
        else
            yield return new WaitForSeconds(1.25f);

        if (cam != null)
        {
            if (cameraZoomRoutine != null)
                StopCoroutine(cameraZoomRoutine);

            yield return StartCoroutine(SmoothCameraFrame(
                cam,
                cam.transform.position,
                restorePosition,
                cam.orthographicSize,
                restoreSize,
                0.65f,
                null));
        }
    }

    IEnumerator BookFrenzyTremble(float duration)
    {
        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (renderer == null)
            yield break;

        Color baseColor = renderer.color;
        Vector3 basePosition = body.transform.position;
        float elapsed = 0f;
        while (elapsed < duration && body != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 26f);
            renderer.color = Color.Lerp(baseColor, new Color(1f, 0.23f, 0.17f, 1f), 0.35f + pulse * 0.35f);
            Vector2 jitter = Random.insideUnitCircle * 0.08f;
            body.transform.position = basePosition + new Vector3(jitter.x, jitter.y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (body != null)
            body.transform.position = basePosition;
        if (renderer != null)
            renderer.color = baseColor;
    }

    IEnumerator StrongShakeLoop()
    {
        while (strongShake && !bossDefeated)
        {
            CameraShake.Shake(0.18f, wave3ShakeMagnitude);
            yield return new WaitForSeconds(wave3ShakeInterval);
        }
    }

    IEnumerator MinionSpawnLoop()
    {
        while (strongShake && !endingStarted && !bossDefeated)
        {
            PruneMinionList();
            if (minions.Count < wave3MaxMinions)
            {
                bool left = Random.value < 0.5f;
                float ex = left ? arenaCenter.x - arenaSize.x * 0.5f + 1f : arenaCenter.x + arenaSize.x * 0.5f - 1f;
                float ey = arenaCenter.y + Random.Range(-arenaSize.y * 0.4f, arenaSize.y * 0.4f);
                Vector2 spawnPosition = new Vector2(ex, ey);
                yield return StartCoroutine(MinionSpawnWarningRoutine(spawnPosition));
                if (strongShake && !endingStarted && !bossDefeated)
                    SpawnMinion(spawnPosition, Random.Range(0, 3));
            }

            yield return new WaitForSeconds(RandomRest(wave3MinionSpawnInterval));
        }
    }

    IEnumerator MinionSpawnWarningRoutine(Vector2 target)
    {
        SpriteRenderer warning = CreateWave3SpawnMarker(target, Vector2.one * (wave3MinionSpawnWarningRadius * 2f));

        float duration = Mathf.Max(0.1f, wave3MinionSpawnWarningTime);
        float blinkInterval = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration && warning != null)
        {
            int blink = Mathf.FloorToInt(elapsed / blinkInterval);
            warning.enabled = blink % 2 == 0;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (warning != null)
            Destroy(warning.gameObject);
    }

    SpriteRenderer CreateWave3SpawnMarker(Vector2 position, Vector2 markerSize)
    {
        GameObject marker = new GameObject("BookMinionSpawnBlink");
        marker.transform.position = new Vector3(position.x, position.y, -0.08f);
        marker.transform.localScale = new Vector3(Mathf.Max(0.5f, markerSize.x), Mathf.Max(0.5f, markerSize.y), 1f);

        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = Wave3SpawnMarkerSprite();
        renderer.color = new Color(1f, 0.38f, 0.22f, 0.9f);
        renderer.sortingOrder = 58;
        return renderer;
    }

    static Sprite Wave3SpawnMarkerSprite()
    {
        if (wave3SpawnMarkerSprite != null)
            return wave3SpawnMarkerSprite;

        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool border = x == 0 || y == 0 || x == texture.width - 1 || y == texture.height - 1;
                bool cross = x == y || x == texture.width - 1 - y;
                texture.SetPixel(x, y, border || cross ? Color.white : clear);
            }
        }

        texture.Apply();
        wave3SpawnMarkerSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 8f);
        return wave3SpawnMarkerSprite;
    }

    void PruneMinionList()
    {
        for (int i = minions.Count - 1; i >= 0; i--)
            if (minions[i] == null)
                minions.RemoveAt(i);
    }

    IEnumerator Wave3AttackLoop()
    {
        yield return new WaitForSeconds(wave3AttackInitialDelay);
        while (strongShake && !endingStarted && !bossDefeated)
        {
            yield return new WaitForSeconds(Random.Range(2.5f, 4f));
            if (endingStarted) break;
            if (Random.value < 0.5f)
                yield return StartCoroutine(PaperAttack());
            else
            {
                yield return new WaitForSeconds(wave3BasicLetterExtraDelay);
                if (endingStarted || bossDefeated || !strongShake)
                    break;
                yield return StartCoroutine(BasicLetterAttack());
            }
        }
    }

    void SpawnMinion(Vector2 position, int kind)
    {
        GameObject go = new GameObject("BookMinion");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 1.18f;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        EnemyBase enemy;
        if (kind == 0)
        {
            enemy = go.AddComponent<EnemyChaser>();
        }
        else if (kind == 1)
        {
            enemy = go.AddComponent<RibbonEnemy>();
        }
        else
        {
            enemy = go.AddComponent<NeedleEnemy>();
        }

        renderer.sortingOrder = 6;
        EnemyManager.Instance?.ConfigureEnemy(enemy, true);
        enemy.OnDied += OnMinionDied;
        StartCoroutine(TrackMinionDeath(enemy, enemy.GetInstanceID()));
        enemy.StartSpawnApproach(1.2f, 1.2f);
        minions.Add(enemy);
        EnemyManager.Instance?.RegisterSpawnedEnemy(enemy);
    }

    void OnMinionDied(EnemyBase enemy)
    {
        if (enemy != null)
            CreditMinionDeath(enemy.GetInstanceID(), enemy);
    }

    IEnumerator TrackMinionDeath(EnemyBase enemy, int enemyId)
    {
        while (enemy != null && !endingStarted && !bossDefeated)
            yield return null;

        if (!endingStarted && !bossDefeated)
            CreditMinionDeath(enemyId, null);
    }

    void CreditMinionDeath(int enemyId, EnemyBase enemy)
    {
        if (enemyId == 0 || creditedMinionDeaths.Contains(enemyId))
            return;

        creditedMinionDeaths.Add(enemyId);
        if (enemy != null)
            minions.Remove(enemy);

        if (body != null && !endingStarted)
        {
            body.ReduceHp(minionKillBodyDamage);
            if (CurrentWave() == 3)
                StartBodyWave3HitFeedback();
        }
    }

    // ---- ending cutscene --------------------------------------------------

    IEnumerator EndingSequence()
    {
        // stop chaos
        ClearMinions();
        if (letterFallLoop != null)
        {
            StopCoroutine(letterFallLoop);
            letterFallLoop = null;
        }

        // darken + body stun
        darkenOverlay = CreateDarkenOverlay(0.42f);
        if (body != null)
            StartCoroutine(BodyStunGlow());

        // the book's ending glows brightly
        if (floorEndingText != null)
            StartCoroutine(GlowText(floorEndingText, new Color(1f, 0.95f, 0.7f, 1f)));

        // wait until the player approaches the glowing sentence
        if (playerController != null)
            playerController.ApplyTemporarySpeedMultiplier(1f, 0f);

        TextMeshPro hint = CreateWorldText("결말에 다가가세요", new Vector2(arenaCenter.x, arenaCenter.y), 1.2f, new Color(1f, 0.9f, 0.6f, 1f), 80);
        Vector2 endingPos = floorEndingText != null ? (Vector2)floorEndingText.transform.position : arenaCenter;
        while (player != null && Vector2.Distance(player.position, endingPos) > 2.6f)
            yield return null;
        if (hint != null) Destroy(hint.gameObject);

        yield return StartCoroutine(CutsceneRoutine());
    }

    IEnumerator CutsceneRoutine()
    {
        if (playerController != null)
            playerController.LockMovement(999f);

        // 1) the doll lifts its arm and skims through the letters it gathered
        yield return StartCoroutine(SkimCollectedWords());

        // 2) erase the book's ending and retype the doll's own
        if (floorEndingText != null)
        {
            Vector2 pos = floorEndingText.transform.position;
            Destroy(floorEndingText.gameObject);
            floorEndingText = CreateWorldText("", pos, 1.4f, new Color(1f, 0.97f, 0.8f, 1f), 80);
            yield return StartCoroutine(TypeText(floorEndingText, BookLetters.TrueEnding, 1.4f));
        }
        yield return new WaitForSeconds(0.6f);

        // 3) walk the doll toward the body
        if (player != null && body != null)
        {
            Vector2 from = player.position;
            Vector2 to = body.BasePosition + new Vector2(0f, -2.4f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.2f;
                player.position = Vector2.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
        }

        // 4) final-blow prompt with a dashed outline
        Vector2 promptPos = body != null ? body.BasePosition + new Vector2(0f, -1.2f) : arenaCenter;
        TextMeshPro prompt = CreateWorldText("[E] 키를 눌러 마지막 공격", promptPos, 1.1f, new Color(1f, 0.95f, 0.7f, 1f), 82);
        GameObject promptBox = BuildDashedBox(promptPos, new Vector2(8.5f, 1.8f), new Color(1f, 0.95f, 0.7f, 0.9f), 81);

        bool pressed = false;
        while (!pressed)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                pressed = true;
            yield return null;
        }

        if (prompt != null) Destroy(prompt.gameObject);
        if (promptBox != null) Destroy(promptBox);

        yield return StartCoroutine(FinalBlow());
    }

    IEnumerator SkimCollectedWords()
    {
        if (collectedWords.Count == 0)
        {
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        TextMeshPro skim = CreateWorldText("", new Vector2(arenaCenter.x, arenaCenter.y + 1.5f), 2.0f, new Color(1f, 0.95f, 0.6f, 1f), 84);
        for (int i = 0; i < collectedWords.Count; i++)
        {
            skim.text = collectedWords[i];
            yield return new WaitForSeconds(0.14f);
        }
        skim.text = string.Join(" ", collectedWords);
        yield return new WaitForSeconds(0.8f);
        if (skim != null) Destroy(skim.gameObject);
    }

    IEnumerator FinalBlow()
    {
        CameraShake.Shake(0.4f, 0.5f);
        SoundManager.PlayEnemyHit();

        // tear the body into scattering paper scraps
        Vector2 center = body != null ? body.BasePosition : arenaCenter;
        for (int i = 0; i < 28; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            StartCoroutine(ScatterScrap(center, dir));
        }

        if (leftArm != null) Destroy(leftArm.gameObject);
        if (rightArm != null) Destroy(rightArm.gameObject);

        // fade the body out
        if (body != null)
        {
            SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
            float fade = 1.2f;
            float elapsed = 0f;
            while (elapsed < fade && renderer != null)
            {
                Color c = renderer.color;
                c.a = 1f - elapsed / fade;
                renderer.color = c;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Destroy(body.gameObject);
        }

        bossDefeated = true;
        RunHudUI.HideBossParts();

        yield return new WaitForSeconds(0.8f);
        if (darkenOverlay != null)
            Destroy(darkenOverlay);

        if (playerController != null)
            playerController.ApplyTemporarySpeedMultiplier(1f, 0f);

        FinishRun();
    }

    IEnumerator ScatterScrap(Vector2 center, Vector2 dir)
    {
        GameObject scrap = new GameObject("TornScrap");
        scrap.transform.position = center + dir * 0.3f;
        scrap.transform.localScale = new Vector3(Random.Range(0.2f, 0.45f), Random.Range(0.25f, 0.5f), 1f);
        scrap.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        SpriteRenderer renderer = scrap.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = new Color(0.95f, 0.92f, 0.84f, 1f);
        renderer.sortingOrder = 90;

        float speed = Random.Range(2.5f, 5.5f);
        float life = 1.4f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            scrap.transform.position += (Vector3)(dir * speed * Time.deltaTime);
            scrap.transform.Rotate(0f, 0f, 540f * Time.deltaTime);
            Color c = renderer.color;
            c.a = 1f - elapsed / life;
            renderer.color = c;
            speed *= 0.96f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(scrap);
    }

    void FinishRun()
    {
        RestoreCameraAfterBoss();

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText("인형은 공방을 떠나 바깥세상을 보았다.", 5f);

        BuildNextDoors();
    }

    // ---- shared visuals / helpers ----------------------------------------

    IEnumerator BodyStunGlow()
    {
        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        while (!bossDefeated && renderer != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            renderer.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.6f, 1f), pulse);
            yield return null;
        }
    }

    void StartBodyWave3HitFeedback()
    {
        if (bodyWave3HitRoutine != null)
            StopCoroutine(bodyWave3HitRoutine);

        bodyWave3HitRoutine = StartCoroutine(BodyWave3HitFeedbackRoutine());
    }

    IEnumerator BodyWave3HitFeedbackRoutine()
    {
        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (body == null || renderer == null)
            yield break;

        Color baseColor = renderer.color;
        Vector3 basePosition = body.BasePosition;
        float duration = 0.38f;
        float elapsed = 0f;
        while (elapsed < duration && body != null)
        {
            float t = elapsed / duration;
            float wave = Mathf.Sin(elapsed * 48f) * 0.18f * (1f - t);
            body.transform.position = basePosition + new Vector3(wave, 0f, 0f);
            renderer.color = Color.Lerp(new Color(1f, 0.26f, 0.2f, 1f), baseColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (body != null)
            body.transform.position = basePosition;
        if (renderer != null)
            renderer.color = baseColor;
        bodyWave3HitRoutine = null;
    }

    IEnumerator Wave3PulseLoop()
    {
        Image image = CreateWave3PulseOverlay();
        if (image == null)
            yield break;

        Color clear = new Color(0.8f, 0.03f, 0.02f, 0f);
        Color peak = new Color(0.9f, 0.02f, 0.02f, 0.34f);
        while (strongShake && !endingStarted && !bossDefeated && image != null)
        {
            float elapsed = 0f;
            float duration = 1.05f;
            while (elapsed < duration && image != null && strongShake && !endingStarted && !bossDefeated)
            {
                float t = elapsed / duration;
                float pulse = Mathf.Sin(t * Mathf.PI);
                image.color = Color.Lerp(clear, peak, pulse);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (image != null)
                image.color = clear;
            yield return new WaitForSeconds(0.08f);
        }

        StopWave3Pulse();
    }

    Image CreateWave3PulseOverlay()
    {
        if (wave3PulseOverlay != null)
        {
            Image existing = wave3PulseOverlay.GetComponentInChildren<Image>(true);
            if (existing != null)
                return existing;
        }

        wave3PulseOverlay = new GameObject("BookWave3PulseOverlay");
        Canvas canvas = wave3PulseOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 140;

        CanvasScaler scaler = wave3PulseOverlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Pulse");
        panel.transform.SetParent(wave3PulseOverlay.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.8f, 0.03f, 0.02f, 0f);
        image.raycastTarget = false;
        return image;
    }

    void StopWave3Pulse()
    {
        if (wave3PulseLoop != null)
        {
            StopCoroutine(wave3PulseLoop);
            wave3PulseLoop = null;
        }

        if (wave3PulseOverlay != null)
        {
            Destroy(wave3PulseOverlay);
            wave3PulseOverlay = null;
        }
    }

    IEnumerator GlowText(TextMeshPro text, Color glow)
    {
        Color baseColor = text != null ? text.color : Color.white;
        while (text != null && !bossDefeated)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            text.color = Color.Lerp(baseColor, glow, pulse);
            yield return null;
        }
    }

    IEnumerator TypeText(TextMeshPro text, string content, float fontSize)
    {
        text.text = content;
        text.fontSize = fontSize;
        text.maxVisibleCharacters = 0;
        float duration = 1.4f;
        float elapsed = 0f;
        while (elapsed < duration && text != null)
        {
            text.maxVisibleCharacters = Mathf.RoundToInt(Mathf.Lerp(0, content.Length, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (text != null)
            text.maxVisibleCharacters = content.Length;
    }

    void UpdateArmBob()
    {
        if (!armsTyping)
            return;

        armBobTime += Time.deltaTime;
        float bob = Mathf.Sin(armBobTime * 8f) * 0.25f;
        if (leftArm != null) leftArm.SetBobOffset(bob);
        if (rightArm != null) rightArm.SetBobOffset(-bob);
    }

    void UpdatePoison()
    {
        for (int i = poisonZones.Count - 1; i >= 0; i--)
        {
            if (Time.time >= poisonZones[i].endTime)
            {
                if (poisonZones[i].visual != null)
                    Destroy(poisonZones[i].visual);
                poisonZones.RemoveAt(i);
            }
        }

        if (player == null || Time.time < nextPoisonTick)
            return;

        for (int i = 0; i < poisonZones.Count; i++)
        {
            if (Vector2.Distance(player.position, poisonZones[i].center) <= poisonZones[i].radius)
            {
                DamagePlayer(poisonTickDamage, 0.5f, poisonZones[i].cause);
                nextPoisonTick = Time.time + 0.5f;
                break;
            }
        }
    }

    void AddPoisonZone(Vector2 center, float radius, float endTime, GameObject visual, string cause = "독 구역")
    {
        poisonZones.Add(new PoisonZone { center = center, radius = radius, endTime = endTime, visual = visual, cause = cause });

        // cap persistent stains so the floor doesn't fill up
        int persistent = 0;
        for (int i = poisonZones.Count - 1; i >= 0; i--)
            if (poisonZones[i].endTime > Time.time + 8f)
                persistent++;

        if (persistent > 14)
        {
            for (int i = 0; i < poisonZones.Count; i++)
            {
                if (poisonZones[i].endTime > Time.time + 8f)
                {
                    if (poisonZones[i].visual != null)
                        Destroy(poisonZones[i].visual);
                    poisonZones.RemoveAt(i);
                    break;
                }
            }
        }
    }

    GameObject CreatePoisonRingVisual(Vector2 center, float radius)
    {
        GameObject root = new GameObject("PoisonRing");
        root.transform.position = center;
        int segments = 16;
        for (int i = 0; i < segments; i += 2)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
            Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            BossVisuals.CreateRect(root.transform, "Dash_" + i, (p0 + p1) * 0.5f, new Vector2(Vector2.Distance(p0, p1), 0.1f), new Color(0.5f, 0.8f, 0.35f, 0.8f), 56, Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg);
        }

        BossVisuals.CreateCircle(root.transform, "Fill", Vector3.zero, radius * 2f, new Color(0.4f, 0.7f, 0.3f, 0.14f), 55);
        return root;
    }

    GameObject BuildDashedBox(Vector2 center, Vector2 size, Color color, int order)
    {
        GameObject root = new GameObject("DashedPrompt");
        root.transform.position = new Vector3(center.x, center.y, -0.2f);
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        BossVisuals.CreateDashedLine(root.transform, "T", new Vector2(-hx, hy), new Vector2(hx, hy), 0.08f, color, order);
        BossVisuals.CreateDashedLine(root.transform, "B", new Vector2(-hx, -hy), new Vector2(hx, -hy), 0.08f, color, order);
        BossVisuals.CreateDashedLine(root.transform, "L", new Vector2(-hx, -hy), new Vector2(-hx, hy), 0.08f, color, order);
        BossVisuals.CreateDashedLine(root.transform, "R", new Vector2(hx, -hy), new Vector2(hx, hy), 0.08f, color, order);
        return root;
    }

    GameObject CreateDarkenOverlay(float alpha)
    {
        GameObject canvasGO = new GameObject("BookDarkenOverlay");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("Dark");
        panel.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.02f, 0.02f, 0.05f, alpha);
        image.raycastTarget = false;
        return canvasGO;
    }

    void DamagePlayer(int damage, float cooldown, string cause = "알 수 없음")
    {
        if (receiver == null)
            receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver != null && receiver.TryTakePatternDamage(damage, cooldown))
            Debug.Log($"[BookBoss 피격] 원인: {cause} | 데미지: {damage}");
    }

    void UpdateHud()
    {
        if (bossDefeated)
            return;

        string[] names = { "몸통(책)", "왼팔", "오른팔" };
        int[] cur =
        {
            body != null ? body.CurrentHp : 0,
            leftDead || leftArm == null ? 0 : leftArm.CurrentHp,
            rightDead || rightArm == null ? 0 : rightArm.CurrentHp
        };
        int[] max =
        {
            body != null ? body.MaxHp : bodyHp,
            leftArm != null ? leftArm.MaxHp : armHp,
            rightArm != null ? rightArm.MaxHp : armHp
        };
        RunHudUI.SetBossParts(names, cur, max);
    }

    void ClearMinions()
    {
        for (int i = minions.Count - 1; i >= 0; i--)
            if (minions[i] != null)
                Destroy(minions[i].gameObject);
        minions.Clear();
    }

    void BuildNextDoors()
    {
        MapNode current = MapRunState.CurrentNode;
        if (current == null || current.children == null || current.children.Count == 0)
        {
            CreateWorldText("바깥세상으로", new Vector2(arenaCenter.x, arenaCenter.y), 1.4f, Color.white, 90);
            return;
        }

        for (int i = 0; i < current.children.Count; i++)
        {
            MapNode child = current.children[i];
            float x = current.children.Count <= 1
                ? arenaCenter.x
                : Mathf.Lerp(arenaCenter.x - 4f, arenaCenter.x + 4f, i / (float)(current.children.Count - 1));

            GameObject door = new GameObject("NextDoor_ToNode_" + child.id);
            door.transform.position = new Vector3(x, arenaCenter.y, 0f);
            door.transform.localScale = new Vector3(2.8f, 0.85f, 1f);
            SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSprite();
            renderer.color = new Color(0.85f, 0.62f, 0.25f, 1f);
            renderer.sortingOrder = 14;

            BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            door.AddComponent<DoorTrigger>().Configure(child, true);
        }
    }

    TextMeshPro CreateWorldText(string content, Vector2 position, float fontSize, Color color, int sortingOrder)
    {
        GameObject go = new GameObject("BookWorldText");
        go.transform.position = new Vector3(position.x, position.y, -0.1f);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = content;
        tmp.font = UIThinDungFont.Get();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.sortingOrder = sortingOrder;
        tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, content.Length * fontSize * 0.7f), fontSize * 1.6f);
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

    static Sprite LoadEnemySprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/enemy/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        Object[] all = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/enemy/" + spriteName + ".png");
        for (int i = 0; i < all.Length; i++)
            if (all[i] is Sprite found)
                return found;

        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/" + spriteName + ".png");
#else
        return null;
#endif
    }
}
