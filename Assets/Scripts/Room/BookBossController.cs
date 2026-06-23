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
    [SerializeField] int armHp = 28;
    [SerializeField] int bodyHp = 120;
    [SerializeField] int minionKillBodyDamage = 10;

    [Header("Attack Damage")]
    [SerializeField, Min(1)] int letterDamage = 14;
    [SerializeField, Min(1)] int paperDamage = 18;
    [SerializeField, Min(1)] int poisonTickDamage = 1;

    [Header("Scene Parts")]
    [SerializeField] BookBossPart body;
    [SerializeField] BookBossPart leftArm;
    [SerializeField] BookBossPart rightArm;

    static bool hooked;
    static Sprite squareSprite;

    bool leftDead;
    bool rightDead;

    Transform player;
    PlayerController playerController;
    PlayerDamageReceiver receiver;

    readonly List<string> collectedWords = new List<string>();
    readonly List<EnemyBase> minions = new List<EnemyBase>();
    readonly List<PoisonZone> poisonZones = new List<PoisonZone>();
    int leftLettersDropped;
    int rightLettersDropped;

    bool bossDefeated;
    bool endingStarted;
    bool armsTyping;
    bool strongShake;
    float armBobTime;
    float nextPoisonTick;

    TextMeshPro floorEndingText;
    GameObject darkenOverlay;
    Coroutine letterFallLoop;

    class PoisonZone
    {
        public Vector2 center;
        public float radius;
        public float endTime;     // float.MaxValue = persistent
        public GameObject visual;
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
        StartCoroutine(BossRoutine());
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
        float aspect = cam.aspect > 0.01f ? cam.aspect : 1.6f;
        cam.orthographicSize = Mathf.Max(arenaSize.y * 0.5f, arenaSize.x * 0.5f / aspect) + 0.5f;
        cam.transform.position = new Vector3(arenaCenter.x, arenaCenter.y, cam.transform.position.z);
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
        body = ResolvePart(body, BookPartType.Body, bodyHp, "bookboss", bodyCenter, 1.7f, 70, false);
        leftArm = ResolvePart(leftArm, BookPartType.LeftArm, armHp, "bookboss_leftarm", bodyCenter + new Vector2(-3.4f, -0.4f), 1.4f, 72, true);
        rightArm = ResolvePart(rightArm, BookPartType.RightArm, armHp, "bookboss_rightarm", bodyCenter + new Vector2(3.4f, -0.4f), 1.4f, 72, true);

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
        int shouldHave = (arm.MaxHp - arm.CurrentHp) / 7;
        while (dropped < shouldHave)
        {
            DropLetter(arm.BasePosition);
            dropped++;
        }

        if (arm.PartType == BookPartType.LeftArm) leftLettersDropped = dropped;
        else rightLettersDropped = dropped;
    }

    void DropLetter(Vector2 nearPosition)
    {
        string word = BookLetters.Fragments[Random.Range(0, BookLetters.Fragments.Length)];
        Vector2 pos = nearPosition + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-1.6f, -0.6f));
        pos.x = Mathf.Clamp(pos.x, arenaCenter.x - arenaSize.x * 0.5f + 1f, arenaCenter.x + arenaSize.x * 0.5f - 1f);
        pos.y = Mathf.Clamp(pos.y, arenaCenter.y - arenaSize.y * 0.5f + 1f, arenaCenter.y + arenaSize.y * 0.5f - 1f);
        LetterPickup.Spawn(word, pos, OnWordCollected);
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
                yield return StartCoroutine(PaperAttack());

            yield return new WaitForSeconds(1.1f);
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

        int sentenceCount = Random.Range(1, 3);
        List<Coroutine> running = new List<Coroutine>();
        for (int i = 0; i < sentenceCount; i++)
        {
            string sentence = BookLetters.AttackSentences[Random.Range(0, BookLetters.AttackSentences.Length)];
            float y = arenaCenter.y + Random.Range(-arenaSize.y * 0.32f, arenaSize.y * 0.18f);
            Vector2 pos = new Vector2(arenaCenter.x + Random.Range(-1.5f, 1.5f), y);
            running.Add(StartCoroutine(FloorSentenceRoutine(sentence, pos)));
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(2.4f);
        armsTyping = false;
    }

    IEnumerator FloorSentenceRoutine(string sentence, Vector2 pos)
    {
        float fontSize = 1.1f;
        TextMeshPro text = CreateWorldText(sentence, pos, fontSize, new Color(0.85f, 0.18f, 0.18f, 0.45f), 55);
        text.maxVisibleCharacters = 0;

        int total = sentence.Length;
        float typeDuration = 0.7f;
        float elapsed = 0f;
        while (elapsed < typeDuration)
        {
            text.maxVisibleCharacters = Mathf.RoundToInt(Mathf.Lerp(0, total, elapsed / typeDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        text.maxVisibleCharacters = total;

        yield return new WaitForSeconds(0.8f);

        text.color = new Color(0.08f, 0.06f, 0.06f, 1f);

        float halfWidth = total * fontSize * 0.32f;
        float halfHeight = fontSize * 0.6f;
        float activeTime = 1.6f;
        float activeElapsed = 0f;
        while (activeElapsed < activeTime)
        {
            if (player != null
                && Mathf.Abs(player.position.x - pos.x) <= halfWidth
                && Mathf.Abs(player.position.y - pos.y) <= halfHeight)
            {
                DamagePlayer(letterDamage, 0.5f);
            }

            activeElapsed += Time.deltaTime;
            yield return null;
        }

        float fade = 0.5f;
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

        GameObject scrap = new GameObject("PaperScrap");
        scrap.transform.position = start;
        scrap.transform.localScale = new Vector3(0.5f, 0.65f, 1f);
        scrap.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        SpriteRenderer renderer = scrap.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = new Color(0.95f, 0.92f, 0.84f, 1f);
        renderer.sortingOrder = 58;

        Vector2 direction = (target - start).normalized;
        float speed = 7.5f;
        float maxTime = 2.2f;
        float elapsed = 0f;
        bool hit = false;
        while (elapsed < maxTime)
        {
            scrap.transform.position += (Vector3)(direction * speed * Time.deltaTime);
            scrap.transform.Rotate(0f, 0f, 360f * Time.deltaTime);

            if (player != null && Vector2.Distance(scrap.transform.position, player.position) <= 0.7f)
            {
                DamagePlayer(paperDamage, 0.5f);
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
        AddPoisonZone(landing, 1.8f, Time.time + 4f, ringVisual);

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
        while (!endingStarted && !bossDefeated && (CurrentWave() == 2 || CurrentWave() == 3))
        {
            StartCoroutine(FallingGlyphRoutine());
            yield return new WaitForSeconds(Random.Range(0.55f, 1.0f));
        }

        letterFallLoop = null;
    }

    IEnumerator FallingGlyphRoutine()
    {
        string glyph = BookLetters.FallingGlyphs[Random.Range(0, BookLetters.FallingGlyphs.Length)];
        float x = arenaCenter.x + Random.Range(-arenaSize.x * 0.46f, arenaSize.x * 0.46f);
        float groundY = arenaCenter.y - arenaSize.y * 0.5f + Random.Range(1.2f, arenaSize.y * 0.7f);
        float topY = arenaCenter.y + arenaSize.y * 0.5f + 1f;

        GameObject marker = new GameObject("FallMarker");
        marker.transform.position = new Vector3(x, groundY, 0f);
        SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = SquareSprite();
        markerRenderer.color = new Color(0.9f, 0.15f, 0.15f, 0.5f);
        markerRenderer.sortingOrder = 53;
        marker.transform.localScale = new Vector3(1.4f, 0.25f, 1f);

        TextMeshPro text = CreateWorldText(glyph, new Vector2(x, topY), 2.2f, new Color(0.1f, 0.08f, 0.08f, 1f), 60);

        float fallDuration = 0.7f;
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            float t = elapsed / fallDuration;
            text.transform.position = new Vector3(x, Mathf.Lerp(topY, groundY, t), 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(marker);
        CameraShake.ShakeHorizontal(0.16f, 0.14f);

        // ink stain: small persistent poison
        GameObject stain = new GameObject("InkStain");
        stain.transform.position = new Vector3(x, groundY, 0f);
        SpriteRenderer stainRenderer = stain.AddComponent<SpriteRenderer>();
        stainRenderer.sprite = BossVisuals.CircleSprite();
        stainRenderer.color = new Color(0.06f, 0.05f, 0.08f, 0.7f);
        stainRenderer.sortingOrder = 52;
        stain.transform.localScale = new Vector3(1.5f, 1.0f, 1f);
        AddPoisonZone(new Vector2(x, groundY), 0.8f, Time.time + 12f, stain);

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

    // ---- pattern 4: wave 3 minions + ending ------------------------------

    IEnumerator Wave3Routine()
    {
        RunHudUI.SetWave(3, 3);
        strongShake = true;
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
        yield return StartCoroutine(EndingSequence());
    }

    IEnumerator StrongShakeLoop()
    {
        while (strongShake && !bossDefeated)
        {
            CameraShake.ShakeHorizontal(0.3f, 0.32f);
            yield return new WaitForSeconds(0.22f);
        }
    }

    IEnumerator MinionSpawnLoop()
    {
        while (strongShake && !endingStarted && !bossDefeated)
        {
            int batch = Random.Range(1, 3);
            for (int i = 0; i < batch; i++)
            {
                bool left = Random.value < 0.5f;
                float ex = left ? arenaCenter.x - arenaSize.x * 0.5f + 1f : arenaCenter.x + arenaSize.x * 0.5f - 1f;
                float ey = arenaCenter.y + Random.Range(-arenaSize.y * 0.4f, arenaSize.y * 0.4f);
                SpawnMinion(new Vector2(ex, ey), Random.Range(0, 3));
            }

            yield return new WaitForSeconds(Random.Range(1.6f, 2.6f));
        }
    }

    IEnumerator Wave3AttackLoop()
    {
        while (strongShake && !endingStarted && !bossDefeated)
        {
            yield return new WaitForSeconds(Random.Range(2.5f, 4f));
            if (endingStarted) break;
            if (Random.value < 0.5f)
                yield return StartCoroutine(PaperAttack());
            else
                yield return StartCoroutine(BasicLetterAttack());
        }
    }

    void SpawnMinion(Vector2 position, int kind)
    {
        GameObject go = new GameObject("BookMinion");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.9f;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        EnemyBase enemy;
        if (kind == 0)
        {
            enemy = go.AddComponent<RibbonEnemy>();
        }
        else if (kind == 1)
        {
            enemy = go.AddComponent<EnemyChaser>();
        }
        else
        {
            // SmallButtonEnemy keeps whatever sprite is on its renderer; give it one.
            renderer.sprite = BossVisuals.CircleSprite();
            renderer.color = new Color(0.82f, 0.52f, 0.28f, 1f);
            renderer.sortingOrder = 6;
            enemy = go.AddComponent<SmallButtonEnemy>();
        }

        enemy.OnDied += OnMinionDied;
        enemy.StartSpawnApproach(1.2f, 1.2f);
        minions.Add(enemy);
        EnemyManager.Instance?.RegisterSpawnedEnemy(enemy);
    }

    void OnMinionDied(EnemyBase enemy)
    {
        minions.Remove(enemy);
        if (body != null && !endingStarted)
            body.ReduceHp(minionKillBodyDamage);
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
                DamagePlayer(poisonTickDamage, 0.5f);
                nextPoisonTick = Time.time + 0.5f;
                break;
            }
        }
    }

    void AddPoisonZone(Vector2 center, float radius, float endTime, GameObject visual)
    {
        poisonZones.Add(new PoisonZone { center = center, radius = radius, endTime = endTime, visual = visual });

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

    void DamagePlayer(int damage, float cooldown)
    {
        if (receiver == null)
            receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver != null)
            receiver.TryTakePatternDamage(damage, cooldown);
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
        int[] max = { bodyHp, armHp, armHp };
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
