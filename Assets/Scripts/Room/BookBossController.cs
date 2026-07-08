using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
    [SerializeField] int armHp = 50;
    [SerializeField] int bodyHp = 120;
    [SerializeField] int minionKillBodyDamage = 10;

    [Header("Attack Damage")]
    // 바닥 문장 공격이 너무 강하다는 피드백에 따라 데미지를 낮춤 (14 → 9).
    [SerializeField, Min(1)] int letterDamage = 9;
    // 나머지 공격들도 조금씩 낮춤 (18 → 15).
    [SerializeField, Min(1)] int paperDamage = 15;
    [SerializeField, Min(1)] int poisonTickDamage = 1;

    [Header("Floor Sentence Attack")]
    [SerializeField, Min(1f)] float floorSentenceFontSize = 300f;
    [SerializeField, Min(0.001f)] float floorSentenceWorldScale = 0.05f;
    [SerializeField, Min(1)] int floorSentenceMinCount = 4;
    [SerializeField, Min(1)] int floorSentenceMaxCount = 6;
    [SerializeField, Min(0f)] float floorSentenceSpawnDelay = 0.08f;
    // task6: 기본공격(바닥 문장) 경고 시간을 조금 줄인다 (0.95 → 0.78).
    [SerializeField, Min(0.1f)] float floorSentenceWarningTime = 0.78f;
    [SerializeField, Min(0.1f)] float floorSentenceActiveTime = 1.35f;
    [SerializeField, Min(0f)] float floorSentenceToPaperDelay = 0.35f;
    [SerializeField, Min(0.01f)] float floorSentenceImpactShakeDuration = 0.18f;
    [SerializeField, Min(0f)] float floorSentenceImpactShakeMagnitude = 0.18f;
    [SerializeField, Min(0f)] float floorSentencePlayerSafeRadius = 3.2f;

    [Header("Paper Scrap Attack")]
    [SerializeField, Min(0.1f)] float paperScrapSpeed = 13.8f;
    [SerializeField, Min(0.1f)] float paperScrapMaxTime = 2.8f;
    // task6: 종이 조각 패턴 경고 시간을 조금 줄인다 (1.75 → 1.45).
    [SerializeField, Min(0.1f)] float paperScrapWarningTime = 1.45f;
    [SerializeField, Min(0.1f)] float paperScrapWarningRadius = 1.05f;
    [SerializeField] Vector2 paperScrapVisualSize = new Vector2(0.82f, 1.05f);
    // 기본 바닥 문장 공격과 겹쳐 보인다는 피드백에 따라 텀을 조금 늘림 (1.6~2.6 → 2.2~3.2).
    [SerializeField] Vector2 wave1PaperAttackRestRange = new Vector2(2.2f, 3.2f);
    [SerializeField, Min(0f)] float wave1PaperAttackInitialDelay = 0.75f;

    [Header("Wave 2 Ink Rain")]
    [SerializeField] Vector2 wave2FloorSentenceRestRange = new Vector2(0.45f, 0.75f);
    [SerializeField] Vector2 inkRainIntervalRange = new Vector2(0.65f, 0.95f);
    [SerializeField] Vector2Int inkRainBurstCountRange = new Vector2Int(2, 4);
    // task6: 잉크비 패턴 경고 시간을 조금 줄인다 (0.85 → 0.7).
    [SerializeField, Min(0.1f)] float inkRainWarningTime = 0.7f;
    [SerializeField, Min(0.1f)] float inkRainWarningRadius = 0.8f;
    [SerializeField, Min(0.1f)] float inkRainStainLifetime = 7f;
    // 잉크비도 조금 더 약하게: 틱 사이 간격을 늘려 초당 데미지를 낮춤 (1.25 → 1.6).
    [SerializeField, Min(0.1f)] float inkRainDamageCooldown = 1.6f;
    [SerializeField] Vector2 inkRainStainHitboxSize = new Vector2(1.35f, 0.9f);

    [Header("Wave 3 Frenzy")]
    [SerializeField, Min(0.1f)] float wave3IntroLockDuration = 2.8f;
    [SerializeField, Min(0.1f)] float wave3IntroCameraSize = 4.2f;
    [SerializeField, Range(0f, 0.4f)] float wave3ShakeMagnitude = 0.14f;
    [SerializeField, Min(0.05f)] float wave3ShakeInterval = 0.28f;
    [SerializeField, Min(1)] int wave3MaxMinions = 5;
    [SerializeField] Vector2 wave3MinionSpawnInterval = new Vector2(2.4f, 3.4f);
    // task6: 3웨이브 미니언 소환 경고 시간을 조금 줄인다 (1.05 → 0.9).
    [SerializeField, Min(0.1f)] float wave3MinionSpawnWarningTime = 0.9f;
    [SerializeField, Min(0.1f)] float wave3MinionSpawnWarningRadius = 1.15f;
    [SerializeField, Min(0f)] float wave3AttackInitialDelay = 1.4f;
    [SerializeField, Min(0f)] float wave3BasicLetterExtraDelay = 0.02f;
    [SerializeField] Vector2 wave3AttackRestRange = new Vector2(0.22f, 0.42f);
    [SerializeField, Range(0f, 1f)] float wave3FloorSentenceChance = 1f;
    [SerializeField, Min(0.1f)] float wave3MinionScale = 1.85f;

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
    readonly HashSet<string> droppedWords = new HashSet<string>();
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
    Coroutine wave1PaperLoop;
    Coroutine rainWobbleLoop;
    Coroutine wave3PulseLoop;
    Coroutine bodyWave3HitRoutine;
    bool bookProximityHintShown;
    RainyScreenDistortion rainyScreenDistortion;
    float restoreCameraSize;
    Coroutine cameraZoomRoutine;
    Color bodyBaseColor = Color.white;
    bool bodyBaseColorCaptured;
    Canvas endingCanvas;
    RectTransform endingPanelRect;
    CanvasGroup endingPanelGroup;
    GameObject endingSentenceAura;
    GameObject finalDoorAura;
    ParticleSystem endingSentenceParticles;
    ParticleSystem finalDoorParticles;
    TextMeshPro endingPromptText;
    TextMeshPro floorEndingInteractText;
    readonly string[] endingSlots = new string[5];
    readonly List<string> endingWordPool = new List<string>();
    readonly List<EndingWordView> endingWordViews = new List<EndingWordView>();
    bool endingPanelOpen;
    bool endingPanelClosing;
    bool endingSolved;
    bool finalDoorEntered;
    Coroutine endingPanelMotionRoutine;

    class PoisonZone
    {
        public Vector2 center;
        public float radius;
        public Vector2 rectHalfExtents;
        public float endTime;     // float.MaxValue = persistent
        public GameObject visual;
        public string cause;
        public bool useFloorHitbox;
        public int damage;
        public float tickCooldown;
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
        StartCoroutine(BookProximityHintLoop());
        StartCoroutine(BossRoutine());
    }

    void OnDestroy()
    {
        SetRainyScreenDistortion(false);
        SoundManager.StopBookBossRage();
        SoundManager.StopBookBossRainLoop();
        SoundManager.StopBookBossSirenLoop();
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

    // 최종보스를 처치한 직후, 아레나 전체를 비추던 카메라를 플레이어 쪽으로 확대한다.
    void ZoomCameraToPlayerForEnding()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 startPosition = cam.transform.position;
        Vector3 targetPosition = player != null
            ? new Vector3(player.position.x, player.position.y, startPosition.z)
            : startPosition;
        float targetSize = EndingCloseUpCameraSize(cam);

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

    // Full HD(16:9) 기준의 살짝 확대된 클로즈업 사이즈. 화면이 더 넓으면 세로 프레이밍을 유지하도록 늘어난다.
    float EndingCloseUpCameraSize(Camera cam)
    {
        const float closeUpHalfHeight = 4.2f;
        const float fullHdAspect = 16f / 9f;
        float aspect = cam.aspect > 0.01f ? cam.aspect : fullHdAspect;
        float framingAspect = Mathf.Max(aspect, fullHdAspect);
        return closeUpHalfHeight * (framingAspect / fullHdAspect);
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
        CaptureBodyBaseColor();

        leftArm.Destroyed += OnArmDestroyed;
        rightArm.Destroyed += OnArmDestroyed;
        leftArm.Damaged += OnArmDamaged;
        rightArm.Damaged += OnArmDamaged;
        if (body != null)
            body.Damaged += OnBodyDamaged;
    }

    BookBossPart ResolvePart(BookBossPart placedPart, BookPartType type, int hp, string spriteName, Vector2 fallbackCenter, float fallbackScale, int sortingOrder, bool damageable)
    {
        Sprite sprite = LoadEnemySprite(spriteName);
        if (placedPart != null)
        {
            int effectiveHp = type == BookPartType.Body ? placedPart.MaxHp : hp;
            placedPart.ConfigurePlaced(type, effectiveHp, sprite, sortingOrder, damageable);
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

        if (leftDead && rightDead)
        {
            if (body != null)
                body.SetDamageable(true);
            DropRemainingLetters();
        }
    }

    void OnBodyDamaged(BookBossPart part)
    {
        if (CurrentWave() == 3)
            StartBodyWave3HitFeedback();
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
        string word = NextUndroppedLetter();
        Vector2 pos = LetterDropPosition(sourcePart);
        droppedWords.Add(word);
        LetterPickup.Spawn(word, pos, OnWordCollected);
    }

    string NextUndroppedLetter()
    {
        for (int i = 0; i < BookLetters.Fragments.Length; i++)
            if (!droppedWords.Contains(BookLetters.Fragments[i]))
                return BookLetters.Fragments[i];

        return BookLetters.Fragments[Random.Range(0, BookLetters.Fragments.Length)];
    }

    void DropRemainingLetters()
    {
        for (int i = 0; i < BookLetters.Fragments.Length; i++)
        {
            string word = BookLetters.Fragments[i];
            if (droppedWords.Contains(word))
                continue;

            BookBossPart source = i % 2 == 0 ? leftArm : rightArm;
            Vector2 pos = LetterDropPosition(source);
            pos += Random.insideUnitCircle * 0.55f;
            droppedWords.Add(word);
            LetterPickup.Spawn(word, pos, OnWordCollected);
        }
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
        yield return StartCoroutine(ShowScreenHint("바닥의 문장을 피하세요!", new Vector2(34f, -224f), 2.4f));

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
            {
                letterFallLoop = StartCoroutine(LetterFallLoop());
            }

            if (wave == 1 && wave1PaperLoop == null)
            {
                wave1PaperLoop = StartCoroutine(Wave1PaperAttackLoop());
            }

            if (CurrentWave() == 1)
            {
                yield return StartCoroutine(BasicLetterAttack());
                if (CurrentWave() >= 3) continue;
                yield return new WaitForSeconds(floorSentenceToPaperDelay);
            }
            else if (CurrentWave() == 2)
            {
                yield return StartCoroutine(BasicLetterAttack());
                if (CurrentWave() >= 3) continue;
                yield return new WaitForSeconds(RandomRest(wave2FloorSentenceRestRange));
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    IEnumerator Wave1PaperAttackLoop()
    {
        if (wave1PaperAttackInitialDelay > 0f)
            yield return new WaitForSeconds(wave1PaperAttackInitialDelay);

        while (!endingStarted && !bossDefeated && CurrentWave() == 1)
        {
            yield return StartCoroutine(PaperAttack());
            yield return new WaitForSeconds(RandomRest(wave1PaperAttackRestRange));
        }

        wave1PaperLoop = null;
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

    IEnumerator BookProximityHintLoop()
    {
        while (!bookProximityHintShown && !bossDefeated)
        {
            if (player != null && body != null && Vector2.Distance(player.position, body.BasePosition) <= 4.2f)
            {
                bookProximityHintShown = true;
                yield return StartCoroutine(ShowWorldBalloonHint(
                    "책 괴물의 팔을 공격하여\n글자 조각을 획득하세요.",
                    body.BasePosition + new Vector2(0f, 2.4f),
                    2.8f));
                yield break;
            }

            yield return null;
        }
    }

    IEnumerator ShowScreenHint(string message, Vector2 anchoredPosition, float duration)
    {
        GameObject canvasObject = new GameObject("BookBossScreenHint");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject textObject = new GameObject("HintText");
        textObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.font = UIThinDungFont.Get();
        text.fontSize = 34f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.86f, 0.52f, 0f);
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(520f, 62f);

        float elapsed = 0f;
        while (elapsed < duration && text != null)
        {
            float blink = 0.55f + 0.45f * Mathf.Sin(elapsed * 12f);
            float fade = Mathf.Clamp01(Mathf.Min(elapsed / 0.35f, (duration - elapsed) / 0.45f));
            text.color = new Color(1f, Mathf.Lerp(0.72f, 0.95f, blink), 0.42f, fade);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    IEnumerator ShowWorldBalloonHint(string message, Vector2 position, float duration)
    {
        TextMeshPro text = CreateWorldText(message, position, 0.78f, new Color(1f, 0.84f, 0.56f, 1f), 95);
        if (text == null)
            yield break;

        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.sizeDelta = new Vector2(8f, 2.4f);

        float elapsed = 0f;
        while (elapsed < duration && text != null)
        {
            float fade = Mathf.Clamp01(Mathf.Min(elapsed / 0.25f, (duration - elapsed) / 0.45f));
            text.color = new Color(1f, 0.84f, 0.56f, fade);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (text != null)
            Destroy(text.gameObject);
    }

    // ---- pattern 1: typed sentences --------------------------------------

    IEnumerator BasicLetterAttack()
    {
        armsTyping = true;

        int minCount = Mathf.Max(1, floorSentenceMinCount);
        int maxCount = Mathf.Max(minCount, floorSentenceMaxCount);
        int sentenceCount = Random.Range(minCount, maxCount + 1) + 1;
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
        SoundManager.PlayBookBossFloorSlam();
        CameraShake.Shake(floorSentenceImpactShakeDuration * 1.12f, floorSentenceImpactShakeMagnitude * 1.28f);

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

        const int scraps = 5;
        Vector2[] targets = new Vector2[scraps];
        Coroutine[] warnings = new Coroutine[scraps];

        for (int i = 0; i < scraps; i++)
        {
            targets[i] = PaperScrapTarget(i, scraps);
            warnings[i] = StartCoroutine(PaperScrapWarningRoutine(targets[i]));
        }

        for (int i = 0; i < warnings.Length; i++)
            if (warnings[i] != null)
                yield return warnings[i];

        for (int i = 0; i < targets.Length; i++)
        {
            StartCoroutine(PaperScrapProjectileRoutine(targets[i]));
            yield return new WaitForSeconds(0.07f);
        }

        yield return new WaitForSeconds(0.25f);
    }

    Vector2 PaperScrapTarget(int index, int count)
    {
        Vector2 center = player != null ? (Vector2)player.position : arenaCenter;
        if (count <= 1)
            return center;

        float spread = Mathf.Lerp(-1.8f, 1.8f, index / (float)(count - 1));
        float vertical = index % 2 == 0 ? 0.28f : -0.22f;
        return center + new Vector2(spread, vertical);
    }

    IEnumerator PaperScrapProjectileRoutine(Vector2 target)
    {
        Vector2 start = body != null ? body.BasePosition : arenaCenter;
        SoundManager.PlayBookBossPaperFly(0f);

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

        Vector2 direction = (target - start).sqrMagnitude > 0.0001f ? (target - start).normalized : Vector2.down;
        Vector2 halfExtents = PaperScrapHitHalfExtents();
        float elapsed = 0f;
        bool hit = false;
        while (elapsed < paperScrapMaxTime && scrap != null)
        {
            Vector2 current = scrap.transform.position;
            Vector2 next = current + direction * paperScrapSpeed * Time.deltaTime;
            scrap.transform.position = next;
            scrap.transform.Rotate(0f, 0f, 480f * Time.deltaTime);

            if (receiver == null)
                receiver = FindFirstObjectByType<PlayerDamageReceiver>();

            if (receiver != null && receiver.FloorAttackHitsRect(next, halfExtents))
            {
                DamagePlayer(paperDamage, 0.5f, "Paper scrap");
                hit = true;
                break;
            }

            if (Vector2.Distance(next, target) <= 0.28f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hit)
        {
            Destroy(scrap);
            yield break;
        }

        Vector2 landing = scrap != null ? (Vector2)scrap.transform.position : target;
        if (renderer != null)
            renderer.color = new Color(0.82f, 0.78f, 0.68f, 1f);

        GameObject ringVisual = CreatePoisonRingVisual(landing, 1.8f);
        // task7: 1웨이브 종이 독 장판이 아주 조금 더 빨리 사라지도록 수명 단축 (4 → 3.2초).
        // 사라질 때는 UpdatePoison 의 FadeAndDestroyPoisonVisual 로 페이드아웃된다.
        AddPoisonZone(landing, 1.8f, Time.time + 3.2f, ringVisual, "Paper poison ring", true, Vector2.one * 1.8f);

        float life = 4f;
        float lifeElapsed = 0f;
        while (lifeElapsed < life && scrap != null)
        {
            float a = 1f - lifeElapsed / life;
            if (renderer != null)
                renderer.color = new Color(0.82f, 0.78f, 0.68f, a);
            lifeElapsed += Time.deltaTime;
            yield return null;
        }

        if (scrap != null)
            Destroy(scrap);
    }

    Vector2 PaperScrapHitHalfExtents()
    {
        return new Vector2(
            Mathf.Max(0.1f, paperScrapVisualSize.x) * 0.5f,
            Mathf.Max(0.1f, paperScrapVisualSize.y) * 0.5f);
    }

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
        SoundManager.StopBookBossRainLoop();
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

        yield return StartCoroutine(InkRainWarningRoutine(target, glyph));

        TextMeshPro text = CreateWorldText(glyph, new Vector2(x, topY), 2.2f, InkRainColor(1f), 60);
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
        SoundManager.PlayBookBossInkDrop(0f);
        Color impactColor = InkRainColor(1f);
        CreateInkImpactParticles(target, impactColor);
        StartCoroutine(GlyphSplashRoutine(target, impactColor));

        // ink stain: small persistent poison
        GameObject stain = new GameObject("InkStain");
        stain.transform.position = new Vector3(x, groundY, 0f);
        SpriteRenderer stainRenderer = stain.AddComponent<SpriteRenderer>();
        stainRenderer.sprite = BossVisuals.CircleSprite();
        stainRenderer.color = InkRainColor(0.68f);
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

    IEnumerator InkRainWarningRoutine(Vector2 target, string glyph)
    {
        TextMeshPro warning = CreateWorldText(glyph, target, 2.35f, InkRainColor(0.32f), 57);
        warning.gameObject.name = "InkRainWarningGlyph";
        warning.fontStyle = FontStyles.Bold;
        GameObject ring = CreateDashedCircle("InkRainWarningCircle", target, inkRainWarningRadius, InkRainColor(0.82f), 58);
        SpriteRenderer[] ringRenderers = ring.GetComponentsInChildren<SpriteRenderer>();
        Color[] ringBaseColors = new Color[ringRenderers.Length];
        for (int i = 0; i < ringRenderers.Length; i++)
            ringBaseColors[i] = ringRenderers[i].color;

        float duration = Mathf.Max(0.1f, inkRainWarningTime);
        float elapsed = 0f;
        while (elapsed < duration && warning != null)
        {
            float t = elapsed / duration;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 13f);
            float grow = Mathf.SmoothStep(0f, 1f, t);
            float radiusScale = Mathf.Max(0.1f, inkRainWarningRadius);
            warning.transform.localScale = Vector3.one * Mathf.Lerp(0.72f * radiusScale, 1.16f * radiusScale, grow);
            warning.color = InkRainColor(Mathf.Lerp(0.24f, 0.78f, pulse));
            if (ring != null)
            {
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.22f, grow);
                for (int i = 0; i < ringRenderers.Length; i++)
                {
                    if (ringRenderers[i] == null) continue;
                    Color c = ringBaseColors[i];
                    c.a = Mathf.Lerp(0.28f, 0.88f, pulse);
                    ringRenderers[i].color = c;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (warning != null)
            Destroy(warning.gameObject);
        if (ring != null)
            Destroy(ring);
    }

    static Color InkRainColor(float alpha)
    {
        return new Color(0.07f, 0.055f, 0.04f, alpha);
    }

    Vector2 InkRainHalfExtents()
    {
        return new Vector2(
            Mathf.Max(0.1f, inkRainStainHitboxSize.x) * 0.5f,
            Mathf.Max(0.1f, inkRainStainHitboxSize.y) * 0.5f);
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

    void CreateInkImpactParticles(Vector2 center, Color color)
    {
        GameObject go = new GameObject("InkRainImpactParticles");
        go.transform.position = new Vector3(center.x, center.y, -0.12f);

        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(color, 0.95f), WithAlpha(color, 0.38f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.08f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24, 34) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.radiusThickness = 1f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 63;

        Destroy(go, 1.25f);
    }

    IEnumerator GlyphSplashRoutine(Vector2 center, Color splashColor)
    {
        int droplets = Random.Range(14, 20);
        for (int i = 0; i < droplets; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            StartCoroutine(SplashDroplet(center, direction, Random.Range(0.45f, 1.15f), splashColor));
        }

        for (int i = 0; i < 5; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.06f, 0.24f);
            StartCoroutine(SplashDroplet(center + offset, offset.sqrMagnitude > 0.001f ? offset.normalized : Random.insideUnitCircle.normalized, Random.Range(0.18f, 0.42f), splashColor, true));
        }

        GameObject ring = CreateDashedCircle("GlyphSplashRing", center, 0.48f, WithAlpha(splashColor, 0.72f), 61);
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

    IEnumerator SplashDroplet(Vector2 center, Vector2 direction, float distance, Color splashColor, bool blot = false)
    {
        GameObject droplet = new GameObject("GlyphSplashDroplet");
        droplet.transform.position = new Vector3(center.x, center.y, -0.09f);
        droplet.transform.localScale = Vector3.one * (blot ? Random.Range(0.16f, 0.28f) : Random.Range(0.11f, 0.23f));
        SpriteRenderer renderer = droplet.AddComponent<SpriteRenderer>();
        renderer.sprite = BossVisuals.CircleSprite();
        renderer.color = WithAlpha(splashColor, blot ? 0.88f : 0.94f);
        renderer.sortingOrder = 62;

        Vector2 start = center;
        Vector2 end = center + direction.normalized * distance;
        float duration = blot ? 0.42f : 0.36f;
        float elapsed = 0f;
        while (elapsed < duration && droplet != null)
        {
            float t = elapsed / duration;
            Vector2 pos = Vector2.Lerp(start, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * (blot ? 0.08f : 0.25f);
            droplet.transform.position = new Vector3(pos.x, pos.y, -0.09f);
            Color c = renderer.color;
            c.a = Mathf.Lerp(blot ? 0.88f : 0.94f, 0f, t);
            renderer.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (droplet != null)
            Destroy(droplet);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
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
        SoundManager.StartBookBossSirenLoop();
        if (letterFallLoop == null)
        {
            letterFallLoop = StartCoroutine(LetterFallLoop());
        }

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
        SoundManager.StopBookBossSirenLoop();
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
        {
            SoundManager.PlayBookBossRage();
            yield return StartCoroutine(BookFrenzyTremble(1.25f));
            SoundManager.StopBookBossRage();
        }
        else
        {
            yield return new WaitForSeconds(1.25f);
            SoundManager.StopBookBossRage();
        }

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

        Color baseColor = BodyBaseColor(renderer);
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

    void CaptureBodyBaseColor()
    {
        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (renderer == null)
            return;

        bodyBaseColor = renderer.color;
        bodyBaseColorCaptured = true;
    }

    Color BodyBaseColor(SpriteRenderer renderer)
    {
        if (!bodyBaseColorCaptured && renderer != null)
            CaptureBodyBaseColor();

        return bodyBaseColorCaptured ? bodyBaseColor : Color.white;
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
            yield return new WaitForSeconds(RandomRest(wave3AttackRestRange));
            if (endingStarted) break;
            Coroutine paper = StartCoroutine(PaperAttack());

            if (Random.value <= Mathf.Clamp01(wave3FloorSentenceChance))
            {
                yield return new WaitForSeconds(Mathf.Min(wave3BasicLetterExtraDelay, 0.08f));
                if (endingStarted || bossDefeated || !strongShake)
                    break;

                yield return StartCoroutine(BasicLetterAttack());
            }

            if (paper != null)
                yield return paper;
        }
    }

    void SpawnMinion(Vector2 position, int kind)
    {
        GameObject go = new GameObject("BookMinion");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * Mathf.Max(0.1f, wave3MinionScale);
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
        ForceVisibleMinion(go);
        StartCoroutine(ForceVisibleMinionNextFrame(go));
        enemy.OnDied += OnMinionDied;
        StartCoroutine(TrackMinionDeath(enemy, enemy.GetInstanceID()));
        enemy.StartSpawnApproach(1.2f, 1.2f);
        minions.Add(enemy);
        EnemyManager.Instance?.RegisterSpawnedEnemy(enemy);
    }

    void ForceVisibleMinion(GameObject root)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Color color = renderer.color;
            if (color.a < 0.98f)
                color.a = 1f;
            renderer.color = color;
            renderer.enabled = true;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 6);
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null)
                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }
    }

    IEnumerator ForceVisibleMinionNextFrame(GameObject root)
    {
        yield return null;
        ForceVisibleMinion(root);
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
        yield return StartCoroutine(RewriteEndingSequence());
        yield break;

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

    IEnumerator RewriteEndingSequence()
    {
        ClearMinions();
        if (letterFallLoop != null)
        {
            StopCoroutine(letterFallLoop);
            letterFallLoop = null;
        }
        SoundManager.StopBookBossRainLoop();
        SoundManager.StopBookBossSirenLoop();

        StopWave3Pulse();
        if (darkenOverlay != null)
            Destroy(darkenOverlay);

        if (playerController != null)
            playerController.ApplyTemporarySpeedMultiplier(1f, 0f);

        ZoomCameraToPlayerForEnding();

        Vector2 endingPos = body != null ? body.BasePosition : arenaCenter;
        yield return StartCoroutine(FadeOutBossBodyWithScraps(endingPos));

        if (floorEndingText != null)
            Destroy(floorEndingText.gameObject);

        floorEndingText = CreateWorldText(BookLetters.BookEnding, endingPos + new Vector2(0f, -0.35f), 2.15f, new Color(0.45f, 0.20f, 0.08f, 1f), 92);
        floorEndingText.fontStyle = FontStyles.Bold;
        floorEndingText.rectTransform.sizeDelta = new Vector2(18f, 3.4f);
        BuildWorldEndingDashedBox(floorEndingText.transform.position, new Vector2(16.5f, 2.4f));

        endingSentenceAura = CreateSoftAura(endingPos, new Color(1f, 0.68f, 0.38f, 0.26f), 5.4f, 88);
        endingSentenceParticles = CreateSoftParticles("EndingSentenceParticles", endingPos, new Color(1f, 0.73f, 0.48f, 0.72f), 40, 0.22f, 0.35f, 93);
        endingPromptText = CreateWorldText("[E] 키를 눌러 상호작용", endingPos + new Vector2(0f, 1.9f), 2.0f, new Color(1f, 1f, 1f, 0f), 94);

        while (!endingSolved)
        {
            float distance = player != null ? Vector2.Distance(player.position, endingPos) : 999f;
            bool near = distance <= 3.2f && !endingPanelOpen;
            SetAuraVisible(endingSentenceAura, endingSentenceParticles, near);
            if (endingPromptText != null)
            {
                Color c = endingPromptText.color;
                c.a = Mathf.MoveTowards(c.a, near ? 1f : 0f, Time.deltaTime * 4f);
                endingPromptText.color = c;
            }

            Keyboard keyboard = Keyboard.current;
            if (near && keyboard != null && keyboard.eKey.wasPressedThisFrame && !endingPanelOpen)
                OpenEndingRewritePanel();

            yield return null;
        }

        yield return StartCoroutine(RevealFinalDoorRoutine(endingPos + new Vector2(0f, -2.9f)));
    }

    IEnumerator FadeOutBossBodyWithScraps(Vector2 center)
    {
        CameraShake.Shake(0.3f, 0.22f);
        SoundManager.PlayEnemyHit();

        for (int i = 0; i < 24; i++)
        {
            Vector2 dir = Random.insideUnitCircle.sqrMagnitude > 0.01f ? Random.insideUnitCircle.normalized : Vector2.up;
            StartCoroutine(ScatterScrap(center, dir));
        }

        for (int i = 0; i < 58; i++)
        {
            Vector2 dir = Random.insideUnitCircle.sqrMagnitude > 0.01f ? Random.insideUnitCircle.normalized : Vector2.up;
            StartCoroutine(ScatterDeathDust(center + Random.insideUnitCircle * 2.2f, dir));
        }

        if (leftArm != null) Destroy(leftArm.gameObject);
        if (rightArm != null) Destroy(rightArm.gameObject);

        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        float fade = 1.45f;
        float elapsed = 0f;
        Color start = renderer != null ? renderer.color : Color.white;
        while (elapsed < fade && renderer != null)
        {
            float t = elapsed / fade;
            renderer.color = Color.Lerp(start, new Color(1f, 0.82f, 0.72f, 0f), t);
            if (elapsed > 0.15f && Random.value < 0.15f)
                StartCoroutine(ScatterDeathDust(center + Random.insideUnitCircle * 2.5f, Random.insideUnitCircle.normalized));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (body != null)
            Destroy(body.gameObject);

        body = null;
        bossDefeated = true;
        SoundManager.StopBookBossSirenLoop();
        SoundManager.PlayAfterVictoryBgmWithFade();
        RunHudUI.HideBossParts();
        yield return new WaitForSeconds(0.35f);
    }

    IEnumerator ScatterDeathDust(Vector2 center, Vector2 dir)
    {
        GameObject dust = new GameObject("BookDeathDust");
        dust.transform.position = center;
        float size = Random.Range(0.08f, 0.22f);
        dust.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer renderer = dust.AddComponent<SpriteRenderer>();
        renderer.sprite = BossVisuals.CircleSprite();
        renderer.color = new Color(1f, 0.66f, 0.34f, Random.Range(0.24f, 0.48f));
        renderer.sortingOrder = 92;

        float speed = Random.Range(0.55f, 1.65f);
        float drift = Random.Range(0.10f, 0.34f);
        float life = Random.Range(1.1f, 2.2f);
        float elapsed = 0f;
        while (elapsed < life && dust != null)
        {
            Vector2 softDir = dir + Vector2.up * drift;
            dust.transform.position += (Vector3)(softDir * speed * Time.deltaTime);
            dust.transform.localScale *= 1f + Time.deltaTime * 0.22f;
            Color c = renderer.color;
            c.a = Mathf.Lerp(c.a, 0f, elapsed / life);
            renderer.color = c;
            speed *= 0.985f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (dust != null)
            Destroy(dust);
    }

    void OpenEndingRewritePanel()
    {
        if (endingPanelOpen)
            return;

        EnsureRuntimeEventSystem();
        endingPanelOpen = true;
        RunHudUI.SetCollectedWords(new List<string>());

        endingWordPool.Clear();
        for (int i = 0; i < BookLetters.Fragments.Length; i++)
            AddEndingPoolWord(BookLetters.Fragments[i]);

        for (int i = 0; i < BookLetters.BookEndingFragments.Length; i++)
            endingSlots[i] = BookLetters.BookEndingFragments[i];

        BuildEndingPanel(true);
    }

    void BuildEndingPanel(bool animateOpen = false)
    {
        if (endingPanelMotionRoutine != null)
        {
            StopCoroutine(endingPanelMotionRoutine);
            endingPanelMotionRoutine = null;
        }

        if (endingCanvas != null)
            Destroy(endingCanvas.gameObject);

        endingWordViews.Clear();
        endingPanelClosing = false;
        GameObject canvasGO = new GameObject("EndingRewriteCanvas");
        endingCanvas = canvasGO.AddComponent<Canvas>();
        endingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        endingCanvas.overrideSorting = true;
        endingCanvas.sortingOrder = 260;
        canvasGO.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RectTransform root = canvasGO.GetComponent<RectTransform>();
        GameObject backdrop = CreateUIRect(root, "Backdrop", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.18f));
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        EndingBackdropClick click = backdrop.AddComponent<EndingBackdropClick>();
        click.owner = this;

        GameObject panel = CreateUIRect(root, "EndingPanel", Vector2.zero, new Vector2(1360f, 650f), new Color(0.98f, 0.88f, 0.68f, 0.97f));
        endingPanelRect = panel.GetComponent<RectTransform>();
        endingPanelGroup = panel.AddComponent<CanvasGroup>();
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.25f, 0.11f, 0.04f, 0.92f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        RectTransform closeButton = CreateUIRect(endingPanelRect, "CloseButton", new Vector2(620f, 285f), new Vector2(58f, 58f), new Color(0.98f, 0.88f, 0.68f, 0.88f)).GetComponent<RectTransform>();
        BuildUIDashedBox(closeButton, closeButton.sizeDelta, new Color(0.24f, 0.11f, 0.04f, 0.95f));
        AddUIText(closeButton, "X", "X", Vector2.zero, new Vector2(44f, 44f), 34f, new Color(0.24f, 0.11f, 0.04f, 1f), FontStyles.Bold);
        EndingCloseButton close = closeButton.gameObject.AddComponent<EndingCloseButton>();
        close.owner = this;
        close.image = closeButton.GetComponent<Image>();

        AddUIText(endingPanelRect, "Title", "\uACB0\uB9D0\uC744 \uB2E4\uC2DC \uC4F0\uC138\uC694", new Vector2(0f, 260f), new Vector2(900f, 70f), 44f, new Color(0.30f, 0.12f, 0.04f, 1f), FontStyles.Bold);

        RectTransform sentenceFrame = CreateUIRect(endingPanelRect, "SentenceFrame", new Vector2(0f, 125f), new Vector2(1140f, 150f), new Color(1f, 0.95f, 0.80f, 0.82f)).GetComponent<RectTransform>();
        BuildUIDashedBox(sentenceFrame, new Vector2(1140f, 150f), new Color(0.24f, 0.11f, 0.04f, 0.95f));

        float startX = -430f;
        for (int i = 0; i < endingSlots.Length; i++)
        {
            RectTransform slot = CreateUIRect(sentenceFrame, "EndingSlot_" + i, new Vector2(startX + i * 215f, 0f), new Vector2(i == 3 ? 250f : 190f, 82f), i == 0 ? new Color(0.86f, 0.74f, 0.54f, 0.88f) : new Color(1f, 0.89f, 0.62f, 0.88f)).GetComponent<RectTransform>();
            BuildUIDashedBox(slot, slot.sizeDelta, new Color(0.35f, 0.17f, 0.06f, 0.90f));
            EndingWordDropSlot drop = slot.gameObject.AddComponent<EndingWordDropSlot>();
            drop.owner = this;
            drop.slotIndex = i;
            if (!string.IsNullOrEmpty(endingSlots[i]))
                CreateEndingWordView(slot, endingSlots[i], i, i == 0);
        }

        AddUIText(endingPanelRect, "PoolTitle", "모은 글자 조각", new Vector2(0f, -20f), new Vector2(900f, 60f), 34f, new Color(0.34f, 0.14f, 0.04f, 1f), FontStyles.Bold);
        RectTransform pool = CreateUIRect(endingPanelRect, "WordPool", new Vector2(0f, -170f), new Vector2(1120f, 210f), new Color(0.52f, 0.28f, 0.12f, 0.16f)).GetComponent<RectTransform>();
        EndingWordPoolDrop poolDrop = pool.gameObject.AddComponent<EndingWordPoolDrop>();
        poolDrop.owner = this;

        GridLayoutGroup grid = pool.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 62f);
        grid.spacing = new Vector2(16f, 14f);
        grid.padding = new RectOffset(22, 22, 22, 22);
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < endingWordPool.Count; i++)
            CreateEndingWordView(pool, endingWordPool[i], -1, false);

        if (animateOpen)
        {
            if (endingPanelMotionRoutine != null)
                StopCoroutine(endingPanelMotionRoutine);
            endingPanelMotionRoutine = StartCoroutine(EndingPanelOpenRoutine());
        }
    }

    void CreateEndingWordView(RectTransform parent, string word, int slotIndex, bool locked)
    {
        GameObject chip = CreateUIRect(parent, "Word_" + word, Vector2.zero, new Vector2(190f, 62f), locked ? new Color(0.66f, 0.51f, 0.34f, 0.90f) : new Color(1f, 0.78f, 0.46f, 0.96f));
        TextMeshProUGUI label = AddUIText(chip.GetComponent<RectTransform>(), "Label", DisplayEndingWord(word, slotIndex), Vector2.zero, new Vector2(190f, 62f), 33f, new Color(0.24f, 0.09f, 0.03f, 1f), FontStyles.Bold);
        EndingWordView view = chip.AddComponent<EndingWordView>();
        view.owner = this;
        view.word = word;
        view.slotIndex = slotIndex;
        view.lockedWord = locked;
        view.image = chip.GetComponent<Image>();
        view.label = label;
        endingWordViews.Add(view);
    }

    void PlaceEndingWord(EndingWordView view, int targetSlot)
    {
        if (view == null || view.lockedWord || targetSlot <= 0 || targetSlot >= endingSlots.Length)
            return;

        string incoming = view.word;
        if (view.slotIndex >= 0)
            endingSlots[view.slotIndex] = string.Empty;
        else
            endingWordPool.Remove(incoming);

        string displaced = endingSlots[targetSlot];
        if (!string.IsNullOrEmpty(displaced))
            AddEndingPoolWord(displaced);

        endingSlots[targetSlot] = incoming;
        BuildEndingPanel();
        CheckEndingSolved();
    }

    void ReturnEndingWordToPool(EndingWordView view)
    {
        if (view == null || view.lockedWord || view.slotIndex <= 0)
            return;

        string word = endingSlots[view.slotIndex];
        endingSlots[view.slotIndex] = string.Empty;
        AddEndingPoolWord(word);
        BuildEndingPanel();
    }

    void CheckEndingSolved()
    {
        bool solved =
            endingSlots[0] == "\uC778\uD615\uC740" &&
            endingSlots[1] == "\uACF5\uBC29\uC744" &&
            endingSlots[2] == "\uB5A0\uB098" &&
            endingSlots[3] == "\uBC14\uAE65\uC138\uC0C1\uC744" &&
            (endingSlots[4] == "\uBCF4\uC558\uB2E4" || endingSlots[4] == "\uBCF4\uC558\uB2E4.");

        if (solved && !endingSolved)
            StartCoroutine(EndingSolvedRoutine());
    }

    IEnumerator EndingSolvedRoutine()
    {
        endingSolved = true;
        if (endingPanelRect != null)
        {
            Vector2 start = endingPanelRect.anchoredPosition;
            Vector2 up = start + new Vector2(0f, 34f);
            float elapsed = 0f;
            while (elapsed < 0.18f && endingPanelRect != null)
            {
                endingPanelRect.anchoredPosition = Vector2.Lerp(start, up, elapsed / 0.18f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.22f && endingPanelRect != null)
            {
                endingPanelRect.anchoredPosition = Vector2.Lerp(up, start, elapsed / 0.22f);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (floorEndingText != null)
            floorEndingText.text = BookLetters.TrueEnding;

        CloseEndingPanel(false);
    }

    IEnumerator EndingPanelOpenRoutine()
    {
        if (endingPanelRect == null)
            yield break;

        Vector2 start = new Vector2(0f, -120f);
        Vector2 overshoot = new Vector2(0f, 34f);
        Vector2 center = Vector2.zero;
        endingPanelRect.anchoredPosition = start;
        if (endingPanelGroup != null)
            endingPanelGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < 0.28f && endingPanelRect != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.28f);
            endingPanelRect.anchoredPosition = Vector2.Lerp(start, overshoot, t);
            if (endingPanelGroup != null)
                endingPanelGroup.alpha = Mathf.Lerp(0f, 1f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.22f && endingPanelRect != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.22f);
            endingPanelRect.anchoredPosition = Vector2.Lerp(overshoot, center, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (endingPanelRect != null)
            endingPanelRect.anchoredPosition = center;
        if (endingPanelGroup != null)
            endingPanelGroup.alpha = 1f;
        endingPanelMotionRoutine = null;
    }

    void RequestCloseEndingPanel(bool restoreHudWords)
    {
        if (!endingPanelOpen || endingPanelClosing)
            return;

        if (endingPanelMotionRoutine != null)
            StopCoroutine(endingPanelMotionRoutine);

        endingPanelMotionRoutine = StartCoroutine(EndingPanelCloseRoutine(restoreHudWords));
    }

    IEnumerator EndingPanelCloseRoutine(bool restoreHudWords)
    {
        endingPanelClosing = true;
        if (endingPanelRect == null)
        {
            CloseEndingPanel(restoreHudWords);
            yield break;
        }

        Vector2 center = endingPanelRect.anchoredPosition;
        Vector2 up = center + new Vector2(0f, 32f);
        Vector2 down = center + new Vector2(0f, -150f);

        float elapsed = 0f;
        while (elapsed < 0.14f && endingPanelRect != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.14f);
            endingPanelRect.anchoredPosition = Vector2.Lerp(center, up, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.26f && endingPanelRect != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.26f);
            endingPanelRect.anchoredPosition = Vector2.Lerp(up, down, t);
            if (endingPanelGroup != null)
                endingPanelGroup.alpha = Mathf.Lerp(1f, 0f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        CloseEndingPanel(restoreHudWords);
    }

    void CloseEndingPanel(bool restoreHudWords)
    {
        if (endingCanvas != null)
            Destroy(endingCanvas.gameObject);

        endingCanvas = null;
        endingPanelRect = null;
        endingPanelGroup = null;
        endingPanelOpen = false;
        endingPanelClosing = false;
        endingPanelMotionRoutine = null;
        endingWordViews.Clear();

        if (restoreHudWords)
            RunHudUI.SetCollectedWords(collectedWords);
    }

    IEnumerator RevealFinalDoorRoutine(Vector2 doorPos)
    {
        if (endingPromptText != null)
            Destroy(endingPromptText.gameObject);

        SetAuraVisible(endingSentenceAura, endingSentenceParticles, false);
        RunHudUI.SetCollectedWords(collectedWords);
        CameraShake.Shake(1.8f, 0.12f);

        GameObject door = new GameObject("FinalEndingDoor");
        door.transform.position = new Vector3(doorPos.x, doorPos.y, 0f);
        door.transform.localScale = new Vector3(2.2f, 3.2f, 1f);
        SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = new Color(1f, 0.92f, 0.76f, 0f);
        renderer.sortingOrder = 91;

        finalDoorAura = CreateSoftAura(doorPos, new Color(1f, 0.72f, 0.42f, 0.28f), 4.6f, 89);
        finalDoorParticles = CreateSoftParticles("FinalDoorParticles", doorPos, new Color(1f, 0.78f, 0.50f, 0.8f), 34, 0.18f, 0.28f, 94);
        TextMeshPro enterPrompt = CreateWorldText("[Enter]를 눌러 나가기", doorPos + new Vector2(0f, 2.25f), 2.0f, new Color(1f, 1f, 1f, 0f), 96);

        float elapsed = 0f;
        while (elapsed < 2.4f && renderer != null)
        {
            Color c = renderer.color;
            c.a = Mathf.Lerp(0f, 0.92f, elapsed / 2.4f);
            renderer.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        while (!finalDoorEntered)
        {
            float distance = player != null ? Vector2.Distance(player.position, doorPos) : 999f;
            bool near = distance <= 2.6f;
            SetAuraVisible(finalDoorAura, finalDoorParticles, near);
            if (enterPrompt != null)
            {
                Color c = enterPrompt.color;
                c.a = Mathf.MoveTowards(c.a, near ? 1f : 0f, Time.deltaTime * 4f);
                enterPrompt.color = c;
            }

            Keyboard keyboard = Keyboard.current;
            if (near && keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                finalDoorEntered = true;

            yield return null;
        }

        yield return StartCoroutine(WhiteOutEndingRoutine());
    }

    IEnumerator WhiteOutEndingRoutine()
    {
        EnsureRuntimeEventSystem();
        GameObject canvasGO = new GameObject("EndingWhiteOutCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 400;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        RectTransform root = canvasGO.GetComponent<RectTransform>();
        GameObject white = CreateUIRect(root, "WhiteFlash", Vector2.zero, new Vector2(80f, 80f), Color.white);
        RectTransform rect = white.GetComponent<RectTransform>();
        Image image = white.GetComponent<Image>();
        float elapsed = 0f;
        while (elapsed < 0.45f)
        {
            float t = elapsed / 0.45f;
            float size = Mathf.Lerp(80f, 2600f, t * t);
            rect.sizeDelta = new Vector2(size, size);
            image.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, t));
            elapsed += Time.deltaTime;
            yield return null;
        }
        image.color = Color.white;

        yield return StartCoroutine(PlayEndingNarrationRoutine(root));
    }

    static readonly string[] EndingNarrationSentences =
    {
        "나는 아직 내가 누구인지 모른다.",
        "하지만 멈춰 있던 시간은 끝났다.",
        "나를 알아가기 위해,",
        "나는 바깥세상으로 나아간다.",
    };

    IEnumerator PlayEndingNarrationRoutine(RectTransform canvasRoot)
    {
        TextMeshProUGUI narrationText = AddUIText(canvasRoot, "EndingNarrationText", "", Vector2.zero, new Vector2(1500f, 320f), 64f, Color.black, FontStyles.Bold);

        for (int i = 0; i < EndingNarrationSentences.Length; i++)
        {
            yield return StartCoroutine(TypeEndingSentence(narrationText, EndingNarrationSentences[i]));
            yield return StartCoroutine(WaitForEndingAdvanceInput());
        }

        SceneManager.LoadScene("StartScene");
    }

    IEnumerator TypeEndingSentence(TextMeshProUGUI text, string sentence)
    {
        text.text = "";
        for (int i = 0; i < sentence.Length; i++)
        {
            text.text += sentence[i];
            yield return new WaitForSeconds(0.06f);
        }
    }

    IEnumerator WaitForEndingAdvanceInput()
    {
        // 문장이 다 써지자마자 이전 입력이 그대로 씹혀 넘어가지 않도록 한 프레임 쉬어간다.
        yield return null;
        while (true)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool advance = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (advance)
                yield break;
            yield return null;
        }
    }

    void AddEndingPoolWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return;
        if (!endingWordPool.Contains(word))
            endingWordPool.Add(word);
    }

    string DisplayEndingWord(string word, int slotIndex)
    {
        if (string.IsNullOrEmpty(word))
            return "";
        if (slotIndex == 4 && word == "\uBCF4\uC558\uB2E4")
            return "\uBCF4\uC558\uB2E4.";
        return word;
    }

    GameObject BuildWorldEndingDashedBox(Vector2 center, Vector2 size)
    {
        return BuildDashedBox(center, size, new Color(0.27f, 0.12f, 0.04f, 0.94f), 91);
    }

    GameObject CreateSoftAura(Vector2 center, Color color, float diameter, int order)
    {
        GameObject root = new GameObject("SoftAura");
        root.transform.position = new Vector3(center.x, center.y, -0.15f);
        SpriteRenderer renderer = BossVisuals.CreateCircle(root.transform, "Glow", Vector3.zero, diameter, color, order);
        renderer.enabled = false;
        return root;
    }

    ParticleSystem CreateSoftParticles(string name, Vector2 center, Color color, int count, float size, float speed, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.position = new Vector3(center.x, center.y, -0.1f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = count;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.65f;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = order;
        renderer.material = SoftParticleMaterial();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    static Material softParticleMaterial;

    // ParticleSystemRenderer는 머티리얼을 지정하지 않으면 기본 셰이더가 깨져 보라색으로 렌더링된다.
    // InteractableHighlight의 부드러운 원형 텍스처를 재사용해 은은한 입자로 보이게 한다.
    static Material SoftParticleMaterial()
    {
        if (softParticleMaterial != null)
            return softParticleMaterial;

        softParticleMaterial = new Material(Shader.Find("Sprites/Default"));
        softParticleMaterial.mainTexture = InteractableHighlight.SoftGlowSprite().texture;
        return softParticleMaterial;
    }

    void SetAuraVisible(GameObject aura, ParticleSystem particles, bool visible)
    {
        if (aura != null)
        {
            SpriteRenderer renderer = aura.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
                renderer.enabled = visible;
        }

        if (particles != null)
        {
            if (visible && !particles.isPlaying)
                particles.Play();
            else if (!visible && particles.isPlaying)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    GameObject CreateUIRect(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.sprite = SquareSprite();
        image.color = color;
        return go;
    }

    TextMeshProUGUI AddUIText(RectTransform parent, string name, string content, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = UIThinDungFont.Get();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, fontSize * 0.58f);
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        return text;
    }

    void BuildUIDashedBox(RectTransform parent, Vector2 size, Color color)
    {
        float dash = 28f;
        float gap = 16f;
        float halfX = size.x * 0.5f;
        float halfY = size.y * 0.5f;
        BuildUIDashedLine(parent, new Vector2(-halfX, halfY), new Vector2(halfX, halfY), dash, gap, color);
        BuildUIDashedLine(parent, new Vector2(-halfX, -halfY), new Vector2(halfX, -halfY), dash, gap, color);
        BuildUIDashedLine(parent, new Vector2(-halfX, -halfY), new Vector2(-halfX, halfY), dash, gap, color);
        BuildUIDashedLine(parent, new Vector2(halfX, -halfY), new Vector2(halfX, halfY), dash, gap, color);
    }

    void BuildUIDashedLine(RectTransform parent, Vector2 start, Vector2 end, float dash, float gap, Color color)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
            return;

        Vector2 dir = delta / length;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float offset = 0f;
        int index = 0;
        while (offset < length)
        {
            float segment = Mathf.Min(dash, length - offset);
            Vector2 pos = start + dir * (offset + segment * 0.5f);
            GameObject go = CreateUIRect(parent, "Dash_" + index, pos, new Vector2(segment, 5f), color);
            go.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, angle);
            offset += dash + gap;
            index++;
        }
    }

    void EnsureRuntimeEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
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
        SoundManager.StopBookBossSirenLoop();
        SoundManager.PlayAfterVictoryBgmWithFade();
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
        {
            StopCoroutine(bodyWave3HitRoutine);
            RestoreBodyHitFeedback();
        }

        bodyWave3HitRoutine = StartCoroutine(BodyWave3HitFeedbackRoutine());
    }

    IEnumerator BodyWave3HitFeedbackRoutine()
    {
        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (body == null || renderer == null)
            yield break;

        Color baseColor = BodyBaseColor(renderer);
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

    void RestoreBodyHitFeedback()
    {
        if (body != null)
            body.transform.position = body.BasePosition;

        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (renderer != null)
            renderer.color = BodyBaseColor(renderer);
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
                // task7: 독 장판 비주얼이 툭 사라지지 않고 부드럽게 페이드아웃되며 사라지게 한다.
                if (poisonZones[i].visual != null)
                    StartCoroutine(FadeAndDestroyPoisonVisual(poisonZones[i].visual, 0.4f));
                poisonZones.RemoveAt(i);
            }
        }

        if (player == null || Time.time < nextPoisonTick)
            return;

        for (int i = 0; i < poisonZones.Count; i++)
        {
            PoisonZone zone = poisonZones[i];
            if (PoisonZoneHitsPlayer(zone))
            {
                int damage = zone.damage > 0 ? zone.damage : poisonTickDamage;
                float cooldown = zone.tickCooldown > 0f ? zone.tickCooldown : 0.5f;
                DamagePlayer(damage, cooldown, zone.cause);
                nextPoisonTick = Time.time + cooldown;
                break;
            }
        }
    }

    // task7: 독 장판(및 잉크 얼룩 등)의 비주얼을 알파 페이드아웃 후 파괴한다.
    IEnumerator FadeAndDestroyPoisonVisual(GameObject visual, float duration)
    {
        if (visual == null)
            yield break;

        SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;

        float safeDuration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < safeDuration && visual != null)
        {
            float k = 1f - elapsed / safeDuration;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color c = baseColors[i];
                c.a = baseColors[i].a * k;
                renderers[i].color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (visual != null)
            Destroy(visual);
    }

    bool PoisonZoneHitsPlayer(PoisonZone zone)
    {
        if (zone == null)
            return false;

        if (zone.useFloorHitbox)
        {
            if (receiver == null)
                receiver = FindFirstObjectByType<PlayerDamageReceiver>();

            return receiver != null && receiver.FloorAttackHitsRect(zone.center, zone.rectHalfExtents);
        }

        return player != null && Vector2.Distance(player.position, zone.center) <= zone.radius;
    }

    void AddPoisonZone(
        Vector2 center,
        float radius,
        float endTime,
        GameObject visual,
        string cause = "독 구역",
        bool forceFloorHitbox = false,
        Vector2 floorHitboxHalfExtents = default)
    {
        bool inkStain = visual != null && visual.name == "InkStain";
        bool useFloorHitbox = inkStain || forceFloorHitbox;
        poisonZones.Add(new PoisonZone
        {
            center = center,
            radius = radius,
            rectHalfExtents = inkStain ? InkRainHalfExtents() : floorHitboxHalfExtents,
            endTime = inkStain ? Mathf.Min(endTime, Time.time + Mathf.Max(0.1f, inkRainStainLifetime)) : endTime,
            visual = visual,
            cause = inkStain ? "Ink Rain" : cause,
            useFloorHitbox = useFloorHitbox,
            damage = inkStain ? poisonTickDamage : -1,
            tickCooldown = inkStain ? Mathf.Max(0.1f, inkRainDamageCooldown) : -1f
        });

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

        bool bodyLocked = !(leftDead && rightDead);
        string[] names = { bodyLocked ? "\uBAB8\uD1B5(\uCC45) \uC7A0\uAE40" : "\uBAB8\uD1B5(\uCC45)", "\uC67C\uD314", "\uC624\uB978\uD314" };
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
        bool[] locked = { bodyLocked, false, false };
        RunHudUI.SetBossParts(names, cur, max, locked);
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

    public class EndingBackdropClick : MonoBehaviour, IPointerClickHandler
    {
        public BookBossController owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner != null && eventData.pointerCurrentRaycast.gameObject == gameObject && !owner.endingSolved)
                owner.RequestCloseEndingPanel(true);
        }
    }

    public class EndingCloseButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public BookBossController owner;
        public Image image;
        Color baseColor;

        void Awake()
        {
            if (image == null)
                image = GetComponent<Image>();
            if (image != null)
                baseColor = image.color;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner != null && !owner.endingSolved)
                owner.RequestCloseEndingPanel(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (image != null)
                image.color = new Color(0.56f, 0.31f, 0.16f, 0.92f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (image != null)
                image.color = baseColor;
        }
    }

    public class EndingWordDropSlot : MonoBehaviour, IDropHandler
    {
        public BookBossController owner;
        public int slotIndex;

        public void OnDrop(PointerEventData eventData)
        {
            EndingWordView view = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<EndingWordView>() : null;
            if (owner != null && view != null)
            {
                view.consumedByDrop = true;
                owner.PlaceEndingWord(view, slotIndex);
            }
        }
    }

    public class EndingWordPoolDrop : MonoBehaviour, IDropHandler
    {
        public BookBossController owner;

        public void OnDrop(PointerEventData eventData)
        {
            EndingWordView view = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<EndingWordView>() : null;
            if (owner != null && view != null)
            {
                view.consumedByDrop = true;
                owner.ReturnEndingWordToPool(view);
            }
        }
    }

    public class EndingWordView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public BookBossController owner;
        public string word;
        public int slotIndex = -1;
        public bool lockedWord;
        public bool consumedByDrop;
        public Image image;
        public TextMeshProUGUI label;
        RectTransform rect;
        CanvasGroup canvasGroup;
        Transform startParent;
        int startSibling;
        Vector2 startAnchoredPosition;
        Color baseColor;
        Coroutine bounceRoutine;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (image != null)
                baseColor = image.color;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (lockedWord || owner == null || owner.endingCanvas == null)
                return;

            consumedByDrop = false;
            startParent = transform.parent;
            startSibling = transform.GetSiblingIndex();
            startAnchoredPosition = rect.anchoredPosition;
            transform.SetParent(owner.endingCanvas.transform, true);
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (lockedWord || rect == null)
                return;

            rect.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (lockedWord)
                return;

            canvasGroup.blocksRaycasts = true;
            if (!consumedByDrop && startParent != null)
            {
                transform.SetParent(startParent, false);
                transform.SetSiblingIndex(startSibling);
                rect.anchoredPosition = startAnchoredPosition;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (lockedWord || image == null)
                return;
            image.color = new Color(1f, 0.90f, 0.58f, 1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (image != null)
                image.color = baseColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (lockedWord || owner == null)
                return;

            if (bounceRoutine != null)
                owner.StopCoroutine(bounceRoutine);
            bounceRoutine = owner.StartCoroutine(BounceRoutine());
        }

        IEnumerator BounceRoutine()
        {
            Vector3 start = transform.localScale;
            Vector3 big = start * 1.14f;
            float elapsed = 0f;
            while (elapsed < 0.08f)
            {
                transform.localScale = Vector3.Lerp(start, big, elapsed / 0.08f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.14f)
            {
                transform.localScale = Vector3.Lerp(big, start, elapsed / 0.14f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localScale = start;
        }
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
