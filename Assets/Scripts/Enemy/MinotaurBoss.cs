using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// The Minotaur middle boss. Drives a HP-percentage based wave state machine with five
// sewing-themed mechanics. Built to run inside MiddleBossScene with no art assets.
[RequireComponent(typeof(SpriteRenderer))]
public class MinotaurBoss : EnemyBase
{
    [Header("Minotaur")]
    [SerializeField] string spriteName = "mino";
    [SerializeField, Min(1)] int bossHp = 100;
    [SerializeField, Min(0.5f)] float bossScale = 3.0f;

    [Header("Breathing")]
    [SerializeField, Min(0f)] float breathAmplitude = 0.05f;
    [SerializeField, Min(0.1f)] float breathFrequency = 0.85f;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] float introDelay = 1.0f;
    [SerializeField, Min(0.1f)] float betweenActions = 1.6f;
    [SerializeField, Min(0.1f)] float telegraphTime = 1.1f;
    [SerializeField, Min(0.1f)] float strikeTime = 0.35f;
    [SerializeField, Min(0.1f)] float stunDuration = 2.6f;
    [SerializeField, Min(1f)] float solveTime = 7f;

    [Header("Damage")]
    [SerializeField, Min(1)] int basicDamage = 18;
    [SerializeField, Min(1)] int judgementPartDamage = 80;
    [SerializeField, Min(1)] int matchFailDamage = 34;
    [SerializeField, Min(1)] int pinFailDamage = 30;
    [SerializeField, Min(1)] int debuffNeedleDamage = 10;

    [Header("Colors")]
    [SerializeField] Color telegraphColor = new Color(0.95f, 0.12f, 0.12f, 0.30f);
    [SerializeField] Color strikeColor = new Color(0.96f, 0.10f, 0.10f, 0.72f);
    [SerializeField] Color stunColor = new Color(1f, 0.30f, 0.26f, 1f);
    [SerializeField] Color trapGlowColor = new Color(0.95f, 0.10f, 0.10f, 0.34f);

    public System.Action Defeated;

    SpriteRenderer bossRenderer;
    Transform player;
    PlayerController playerController;
    Vector3 baseScale;
    Vector2 arenaCenter = Vector2.zero;
    Vector2 arenaSize = new Vector2(26f, 14f);
    float breathTime;
    bool stunned;
    bool bossDefeated;
    bool pinTurn;
    int lastReportedWave = -1;

    struct Band
    {
        public Vector2 center;
        public Vector2 size;
        public float angle;
    }

    protected override void Awake()
    {
        bossRenderer = GetComponent<SpriteRenderer>();
        maxHp = Mathf.Max(1, bossHp);
        currentHp = maxHp;

        LoadSprite();
        transform.localScale = new Vector3(bossScale, bossScale, 1f);
        baseScale = transform.localScale;

        SetupRigidbody();
        EnsureCharacterShadow();
        EnsureBossCollider();
    }

    protected override void Start()
    {
        base.Start();

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerController = playerObject.GetComponent<PlayerController>();
        }

        StartCoroutine(BossRoutine());
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
        // Boss stats are fixed; ignore randomized room profiles.
    }

    public void SetArena(Vector2 center, Vector2 size)
    {
        arenaCenter = center;
        arenaSize = size;
    }

    protected override void Update()
    {
        if (bossDefeated || stunned)
            return;

        breathTime += Time.deltaTime;
        float breath = Mathf.Sin(breathTime * Mathf.PI * 2f * breathFrequency);
        transform.localScale = new Vector3(
            baseScale.x * (1f + breath * breathAmplitude * 0.4f),
            baseScale.y * (1f + breath * breathAmplitude),
            baseScale.z);
    }

    protected override void OnDamaged()
    {
        base.OnDamaged();
        ReportHealth();
    }

    protected override void Die()
    {
        bossDefeated = true;
        StopAllCoroutines();
        RunHudUI.HideBossHealth();
        Defeated?.Invoke();
        base.Die();
    }

    protected override void OnDestroy()
    {
        RunHudUI.HideBossHealth();
        base.OnDestroy();
    }

    void ReportHealth()
    {
        RunHudUI.SetBossHealth("미노타우로스", currentHp, maxHp);
    }

    int WaveByHp()
    {
        float ratio = (float)currentHp / Mathf.Max(1, maxHp);
        if (ratio >= 0.70f) return 1;
        if (ratio >= 0.40f) return 2;
        return 3;
    }

    // ---- main loop --------------------------------------------------------

    IEnumerator BossRoutine()
    {
        ReportHealth();
        yield return new WaitForSeconds(introDelay);

        while (!bossDefeated)
        {
            int wave = WaveByHp();
            if (wave != lastReportedWave)
            {
                RunHudUI.SetWave(wave, 3);
                lastReportedWave = wave;
            }

            yield return StartCoroutine(BasicAttackRoutine());
            if (bossDefeated) yield break;
            yield return new WaitForSeconds(betweenActions);

            yield return StartCoroutine(WaveMechanic(wave));
            if (bossDefeated) yield break;
            yield return new WaitForSeconds(betweenActions);
        }
    }

    IEnumerator WaveMechanic(int wave)
    {
        switch (wave)
        {
            case 1:
                yield return StartCoroutine(DesignJudgementRoutine());
                break;
            case 2:
                yield return StartCoroutine(DesignMatchRoutine());
                break;
            default:
                pinTurn = !pinTurn;
                if (pinTurn)
                    yield return StartCoroutine(SewingPinRoutine());
                else
                    yield return StartCoroutine(DebuffRoutine());
                break;
        }
    }

    // ---- pattern 0: map-wide basic attacks --------------------------------

    IEnumerator BasicAttackRoutine()
    {
        List<Band> bands = BuildBasicBands(Random.Range(0, 4));
        List<GameObject> telegraphs = new List<GameObject>();
        for (int i = 0; i < bands.Count; i++)
            telegraphs.Add(TrackTelegraph(EnemyTelegraph.CreateBox("BossBandTelegraph", bands[i].center, bands[i].size, bands[i].angle, telegraphColor, 40)));

        yield return new WaitForSeconds(telegraphTime);

        for (int i = 0; i < telegraphs.Count; i++)
            if (telegraphs[i] != null)
                DestroyOwnedTelegraph(telegraphs[i]);

        List<GameObject> strikes = new List<GameObject>();
        for (int i = 0; i < bands.Count; i++)
            strikes.Add(TrackTelegraph(EnemyTelegraph.CreateBox("BossBandStrike", bands[i].center, bands[i].size, bands[i].angle, strikeColor, 41)));

        bool damaged = false;
        float elapsed = 0f;
        while (elapsed < strikeTime)
        {
            if (!damaged && PlayerInAnyBand(bands))
            {
                ApplyPlayerDamage(basicDamage);
                damaged = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < strikes.Count; i++)
            if (strikes[i] != null)
                DestroyOwnedTelegraph(strikes[i]);
    }

    List<Band> BuildBasicBands(int shape)
    {
        List<Band> bands = new List<Band>();
        float thickness = 2.4f;
        // Oversize the bands so the attack visibly sweeps the entire map, edge to edge.
        float fullW = arenaSize.x * 1.5f;
        float fullH = arenaSize.y * 1.5f;
        float diagonal = Mathf.Sqrt(fullW * fullW + fullH * fullH);

        switch (shape)
        {
            case 0: // X
                bands.Add(new Band { center = arenaCenter, size = new Vector2(diagonal, thickness), angle = 45f });
                bands.Add(new Band { center = arenaCenter, size = new Vector2(diagonal, thickness), angle = -45f });
                break;
            case 1: // cross
                bands.Add(new Band { center = arenaCenter, size = new Vector2(fullW, thickness), angle = 0f });
                bands.Add(new Band { center = arenaCenter, size = new Vector2(thickness, fullH), angle = 0f });
                break;
            case 2: // horizontal (player's row, full width)
                float row = player != null ? Mathf.Clamp(player.position.y, arenaCenter.y - arenaSize.y * 0.4f, arenaCenter.y + arenaSize.y * 0.4f) : arenaCenter.y;
                bands.Add(new Band { center = new Vector2(arenaCenter.x, row), size = new Vector2(fullW, thickness), angle = 0f });
                break;
            default: // vertical (player's column, full height)
                float col = player != null ? Mathf.Clamp(player.position.x, arenaCenter.x - arenaSize.x * 0.4f, arenaCenter.x + arenaSize.x * 0.4f) : arenaCenter.x;
                bands.Add(new Band { center = new Vector2(col, arenaCenter.y), size = new Vector2(thickness, fullH), angle = 0f });
                break;
        }

        return bands;
    }

    bool PlayerInAnyBand(List<Band> bands)
    {
        if (player == null)
            return false;

        for (int i = 0; i < bands.Count; i++)
            if (EnemyTelegraph.PointInOrientedBox(player.position, bands[i].center, bands[i].size, bands[i].angle))
                return true;

        return false;
    }

    // ---- pattern 1: design judgement (wave 1) -----------------------------

    IEnumerator DesignJudgementRoutine()
    {
        Vector2 paperPos = new Vector2(transform.position.x, transform.position.y - bossScale * 0.9f - 1.6f);
        GameObject paper = TrackTelegraph(BossVisuals.CreatePaper("JudgementPaper", paperPos, new Vector2(5.4f, 3.6f), 30));
        BossVisuals.CreateRect(paper.transform, "Title", new Vector3(0f, 1.45f, 0f), new Vector2(3.2f, 0.06f), BossVisuals.PaperLineColor, 31);

        BodySlot[] slots =
        {
            BodySlot.EyeLeft, BodySlot.EyeRight,
            BodySlot.ArmLeft, BodySlot.ArmRight,
            BodySlot.LegLeft, BodySlot.LegRight
        };
        Vector3[] layout =
        {
            new Vector3(-1.4f, 1.0f, 0f), new Vector3(1.4f, 1.0f, 0f),
            new Vector3(-1.4f, 0.0f, 0f), new Vector3(1.4f, 0.0f, 0f),
            new Vector3(-1.4f, -1.0f, 0f), new Vector3(1.4f, -1.0f, 0f)
        };

        List<BodySlot> marked = PickMarkedSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            bool isX = marked.Contains(slots[i]);
            BossVisuals.CreatePartIcon(paper.transform, "Icon_" + slots[i], slots[i], layout[i] + new Vector3(-0.55f, 0f, 0f), 1.1f, BossVisuals.InkColor, 32);
            if (isX)
                BossVisuals.CreateXMark(paper.transform, layout[i] + new Vector3(0.55f, 0f, 0f), 1.0f, 33);
            else
                BossVisuals.CreateOkMark(paper.transform, layout[i] + new Vector3(0.55f, 0f, 0f), 1.0f, 33);
        }

        SoundManager.PlayPanel();
        yield return StartCoroutine(CountdownRoutine("X 표시 부위를 빼내세요!", solveTime));

        List<BodySlot> stillEquipped = new List<BodySlot>();
        InventoryManager inventory = InventoryManager.Instance;
        for (int i = 0; i < marked.Count; i++)
            if (inventory != null && inventory.IsEquipped(marked[i]))
                stillEquipped.Add(marked[i]);

        if (paper != null)
            DestroyOwnedTelegraph(paper);

        if (stillEquipped.Count == 0)
        {
            yield return StartCoroutine(StunRoutine(stunDuration));
        }
        else
        {
            for (int i = 0; i < stillEquipped.Count; i++)
                yield return StartCoroutine(JudgementSlamRoutine(stillEquipped[i]));
        }
    }

    List<BodySlot> PickMarkedSlots()
    {
        BodySlot[] all = { BodySlot.EyeLeft, BodySlot.EyeRight, BodySlot.ArmLeft, BodySlot.ArmRight, BodySlot.LegLeft, BodySlot.LegRight };
        List<BodySlot> equipped = new List<BodySlot>();
        InventoryManager inventory = InventoryManager.Instance;
        for (int i = 0; i < all.Length; i++)
            if (inventory == null || inventory.IsEquipped(all[i]))
                equipped.Add(all[i]);

        if (equipped.Count == 0)
            equipped.AddRange(all);

        int count = Mathf.Clamp(Random.Range(1, 3), 1, equipped.Count);
        List<BodySlot> chosen = new List<BodySlot>();
        while (chosen.Count < count && equipped.Count > 0)
        {
            int idx = Random.Range(0, equipped.Count);
            chosen.Add(equipped[idx]);
            equipped.RemoveAt(idx);
        }

        return chosen;
    }

    IEnumerator JudgementSlamRoutine(BodySlot slot)
    {
        Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
        GameObject warn = TrackTelegraph(EnemyTelegraph.CreateCircle("JudgementSlamWarn", target, 1.6f, telegraphColor, 45));
        yield return new WaitForSeconds(0.5f);
        if (warn != null) DestroyOwnedTelegraph(warn);

        GameObject slam = TrackTelegraph(EnemyTelegraph.CreateCircle("JudgementSlam", target, 1.8f, strikeColor, 46));
        CameraShake.Shake(0.22f, 0.28f);

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null && inventory.IsEquipped(slot))
            inventory.TryDamageEquippedPart(slot, judgementPartDamage, out _);

        PlayerDamageReceiver receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver != null)
            receiver.TryTakePatternDamage(1, 0.1f);

        yield return new WaitForSeconds(0.3f);
        if (slam != null) DestroyOwnedTelegraph(slam);
    }

    // ---- pattern 2: design matching (wave 2) ------------------------------

    IEnumerator DesignMatchRoutine()
    {
        BodySlot?[] options = { null, BodySlot.ArmLeft, BodySlot.EyeLeft, BodySlot.LegLeft };
        string[] labels = { "온전한 도안", "왼팔 없는 도안", "왼눈 없는 도안", "왼다리 없는 도안" };
        Vector2[] spots =
        {
            new Vector2(arenaCenter.x - 6f, arenaCenter.y - 2.4f),
            new Vector2(arenaCenter.x - 2f, arenaCenter.y - 2.4f),
            new Vector2(arenaCenter.x + 2f, arenaCenter.y - 2.4f),
            new Vector2(arenaCenter.x + 6f, arenaCenter.y - 2.4f)
        };
        Vector2 paperSize = new Vector2(3.2f, 3.8f);

        List<GameObject> papers = new List<GameObject>();
        for (int i = 0; i < options.Length; i++)
        {
            GameObject paper = TrackTelegraph(BossVisuals.CreatePaper("MatchPaper_" + i, spots[i], paperSize, 12));
            BossVisuals.CreateDollSilhouette(paper.transform, "Doll", new Vector3(0f, 0.1f, 0f), 0.95f, options[i], 14);
            papers.Add(paper);
        }

        int correct = Random.Range(0, options.Length);
        BodySlot? covered = options[correct];
        ShowCoveredPart(covered);
        SoundManager.PlayPanel();

        yield return StartCoroutine(CountdownRoutine("보스가 가린 곳과 같은 도안 위로!", solveTime));

        bool success = player != null
            && PointInPaper(player.position, spots[correct], paperSize);

        for (int i = 0; i < papers.Count; i++)
            if (papers[i] != null)
                DestroyOwnedTelegraph(papers[i]);
        ClearCoveredPart();

        if (success)
        {
            yield return StartCoroutine(StunRoutine(stunDuration));
        }
        else
        {
            Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
            GameObject slam = TrackTelegraph(EnemyTelegraph.CreateCircle("MatchSlam", target, 2.0f, strikeColor, 46));
            CameraShake.Shake(0.25f, 0.3f);
            ApplyPlayerDamage(matchFailDamage);
            yield return new WaitForSeconds(0.35f);
            if (slam != null) DestroyOwnedTelegraph(slam);
        }
    }

    GameObject coveredPatch;

    void ShowCoveredPart(BodySlot? slot)
    {
        ClearCoveredPart();
        if (slot == null)
            return;

        Vector3 local;
        switch (slot.Value)
        {
            case BodySlot.ArmLeft: local = new Vector3(-0.55f, 0.05f, 0f); break;
            case BodySlot.EyeLeft: local = new Vector3(-0.18f, 0.35f, 0f); break;
            default: local = new Vector3(-0.2f, -0.55f, 0f); break;
        }

        coveredPatch = new GameObject("BossCoveredPatch");
        coveredPatch.transform.SetParent(transform, false);
        coveredPatch.transform.localPosition = local;
        SpriteRenderer renderer = coveredPatch.AddComponent<SpriteRenderer>();
        renderer.sprite = BossVisuals.SquareSprite();
        renderer.color = new Color(0.1f, 0.08f, 0.1f, 0.92f);
        renderer.sortingOrder = 60;
        coveredPatch.transform.localScale = new Vector3(0.32f, 0.32f, 1f);
        TrackTelegraph(coveredPatch);
    }

    void ClearCoveredPart()
    {
        if (coveredPatch != null)
        {
            DestroyOwnedTelegraph(coveredPatch);
            coveredPatch = null;
        }
    }

    // ---- pattern 3: sewing pins (wave 3) ----------------------------------

    IEnumerator SewingPinRoutine()
    {
        Vector2 zoneSize = new Vector2(Random.Range(6f, 8f), Random.Range(4f, 5f));
        Vector2 zoneCenter = new Vector2(
            arenaCenter.x + Random.Range(-3f, 3f),
            arenaCenter.y + Random.Range(-1.5f, 1.5f));

        Vector2 half = zoneSize * 0.5f;
        Vector2[] corners =
        {
            zoneCenter + new Vector2(-half.x, -half.y),
            zoneCenter + new Vector2(half.x, -half.y),
            zoneCenter + new Vector2(half.x, half.y),
            zoneCenter + new Vector2(-half.x, half.y)
        };

        List<GameObject> pins = new List<GameObject>();
        for (int i = 0; i < corners.Length; i++)
        {
            pins.Add(TrackTelegraph(BossVisuals.CreatePin("SewingPin_" + i, corners[i], 50)));
            CameraShake.ShakeHorizontal(0.28f, 0.34f);
            SoundManager.PlayEnemyHit();
            yield return new WaitForSeconds(0.45f);
        }

        List<GameObject> threads = new List<GameObject>();
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 a = corners[i];
            Vector2 b = corners[(i + 1) % corners.Length];
            threads.Add(TrackTelegraph(BossVisuals.CreateDashedLine(null, "PinThread_" + i, a, b, 0.12f, new Color(0.85f, 0.2f, 0.2f, 0.9f), 51)));
            yield return new WaitForSeconds(0.3f);
        }

        GameObject glow = TrackTelegraph(EnemyTelegraph.CreateBox("PinTrapGlow", zoneCenter, zoneSize, 0f, trapGlowColor, 49));
        float blinkElapsed = 0f;
        while (blinkElapsed < 3f)
        {
            EnemyTelegraph.SetUniformAlpha(glow, Mathf.PingPong(blinkElapsed * 2.4f, 0.45f) + 0.12f);
            blinkElapsed += Time.deltaTime;
            yield return null;
        }

        // slam
        GameObject slam = TrackTelegraph(EnemyTelegraph.CreateBox("PinSlam", zoneCenter, zoneSize, 0f, strikeColor, 52));
        CameraShake.Shake(0.32f, 0.42f);
        SoundManager.PlayEnemyHit();

        bool caught = player != null && EnemyTelegraph.PointInOrientedBox(player.position, zoneCenter, zoneSize, 0f);
        if (caught)
        {
            if (playerController != null)
                playerController.LockMovement(1.6f);
            ApplyPlayerDamage(pinFailDamage, 0.1f);
        }

        yield return new WaitForSeconds(0.4f);

        if (slam != null) DestroyOwnedTelegraph(slam);
        if (glow != null) DestroyOwnedTelegraph(glow);
        for (int i = 0; i < threads.Count; i++)
            if (threads[i] != null) DestroyOwnedTelegraph(threads[i]);
        for (int i = 0; i < pins.Count; i++)
            if (pins[i] != null) DestroyOwnedTelegraph(pins[i]);
    }

    // ---- pattern 4: stitch debuff (wave 3) --------------------------------

    IEnumerator DebuffRoutine()
    {
        Vector2 start = transform.position;
        Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
        GameObject needle = TrackTelegraph(BossVisuals.CreatePin("DebuffNeedle", start, 55, 0.7f));

        float flightTime = 0.45f;
        float elapsed = 0f;
        while (elapsed < flightTime)
        {
            float t = elapsed / flightTime;
            if (needle != null)
                needle.transform.position = Vector2.Lerp(start, target, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (needle != null) DestroyOwnedTelegraph(needle);

        ApplyPlayerDamage(debuffNeedleDamage, 0.1f);
        yield return StartCoroutine(ApplyStitchDebuff(3f));
    }

    IEnumerator ApplyStitchDebuff(float duration)
    {
        if (playerController != null)
            playerController.ApplyTemporarySpeedMultiplier(0.5f, duration);

        GameObject eyeOverlay = TrackTelegraph(CreateEyeDarkOverlay());

        InventoryManager inventory = InventoryManager.Instance;
        BodyPart blockedArm = inventory != null ? inventory.GetEquippedPart(BodySlot.ArmRight) : null;
        bool armRemoved = false;
        if (inventory != null && blockedArm != null)
            armRemoved = inventory.TryUnequip(BodySlot.ArmRight);

        List<SpriteRenderer> tinted = TintPlayer(new Color(0.7f, 0.5f, 0.85f, 1f));

        yield return new WaitForSeconds(duration);

        RestorePlayerTint(tinted);
        if (eyeOverlay != null) DestroyOwnedTelegraph(eyeOverlay);

        if (armRemoved && inventory != null && blockedArm != null)
        {
            for (int i = 0; i < inventory.storage.Length; i++)
            {
                if (inventory.storage[i] == blockedArm)
                {
                    inventory.EquipFromStorage(i);
                    break;
                }
            }
        }
    }

    List<SpriteRenderer> TintPlayer(Color tint)
    {
        List<SpriteRenderer> result = new List<SpriteRenderer>();
        if (player == null)
            return result;

        SpriteRenderer[] renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].color = tint;
            result.Add(renderers[i]);
        }

        return result;
    }

    void RestorePlayerTint(List<SpriteRenderer> renderers)
    {
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null)
                renderers[i].color = Color.white;
    }

    GameObject CreateEyeDarkOverlay()
    {
        GameObject canvasGO = new GameObject("StitchEyeOverlay");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("DarkHalf");
        panel.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.46f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);
        image.raycastTarget = false;

        return canvasGO;
    }

    // ---- shared helpers ---------------------------------------------------

    IEnumerator StunRoutine(float duration)
    {
        stunned = true;
        Vector3 basePos = transform.position;
        float elapsed = 0f;
        while (elapsed < duration && !bossDefeated)
        {
            if (bossRenderer != null)
                bossRenderer.color = Mathf.FloorToInt(elapsed * 12f) % 2 == 0 ? stunColor : Color.white;

            transform.position = basePos + (Vector3)(Random.insideUnitCircle * 0.09f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = basePos;
        transform.localScale = baseScale;
        if (bossRenderer != null)
            bossRenderer.color = Color.white;
        stunned = false;
    }

    IEnumerator CountdownRoutine(string message, float duration)
    {
        Vector2 labelPos = new Vector2(arenaCenter.x, arenaCenter.y + arenaSize.y * 0.5f - 1.0f);
        GameObject labelGO = new GameObject("BossCountdown");
        TrackTelegraph(labelGO);
        labelGO.transform.position = new Vector3(labelPos.x, labelPos.y, -1f);
        TextMeshPro label = labelGO.AddComponent<TextMeshPro>();
        label.font = UIThinDungFont.Get();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 3.6f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.85f, 0.3f, 1f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 80;

        float remaining = duration;
        while (remaining > 0f && !bossDefeated)
        {
            label.text = message + "\n" + remaining.ToString("0.0") + "초";
            remaining -= Time.deltaTime;
            yield return null;
        }

        DestroyOwnedTelegraph(labelGO);
    }

    void ApplyPlayerDamage(int damage, float cooldown = -1f)
    {
        PlayerDamageReceiver receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver != null)
            receiver.TryTakePatternDamage(damage, cooldown);
    }

    bool PointInPaper(Vector2 point, Vector2 center, Vector2 size)
    {
        return Mathf.Abs(point.x - center.x) <= size.x * 0.5f
            && Mathf.Abs(point.y - center.y) <= size.y * 0.5f;
    }

    void SetupRigidbody()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    void EnsureBossCollider()
    {
        if (GetComponent<Collider2D>() != null)
            return;

        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        if (bossRenderer != null && bossRenderer.sprite != null)
        {
            Bounds bounds = bossRenderer.sprite.bounds;
            collider.offset = bounds.center;
            collider.size = new Vector2(Mathf.Max(0.5f, bounds.size.x), Mathf.Max(0.5f, bounds.size.y));
        }
        else
        {
            collider.size = Vector2.one;
        }
    }

    void LoadSprite()
    {
        if (bossRenderer == null)
            return;

        Sprite sprite = Resources.Load<Sprite>("Sprites/enemy/" + spriteName);
#if UNITY_EDITOR
        if (sprite == null)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/" + spriteName + ".png");
#endif
        if (sprite != null)
            bossRenderer.sprite = sprite;

        bossRenderer.color = Color.white;
        bossRenderer.sortingOrder = 70;
    }
}
