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
    [SerializeField] string spriteName = "mino_jjin";
    [SerializeField, Min(1)] int bossHp = 100;
    [SerializeField, Min(0.5f)] float bossScale = 3.0f;

    [Header("Breathing")]
    [SerializeField] MinoBreathAnimator breathAnimator;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] float introDelay = 1.0f;
    [SerializeField, Min(0.1f)] float betweenActions = 1.6f;
    [SerializeField, Min(0.1f)] float strikeTime = 0.35f;
    [SerializeField, Min(1f)] float solveTime = 8f;

    [Header("Room-wide Sewing Lines")]
    [SerializeField, Min(0.5f)] float sewingLineThickness = 2.9f;
    [SerializeField, Min(1f)] float diagonalLengthMultiplier = 1.2f;
    [SerializeField, Min(0.1f)] float largeAttackWarningTime = 1f;

    [Header("Mechanic Timing")]
    [SerializeField, Min(1f)] float firstJudgementTime = 8f;
    [SerializeField, Min(1f)] float repeatJudgementTime = 6f;
    [SerializeField, Min(1f)] float designMatchTime = 6f;
    [SerializeField, Min(0.1f)] float pinClosureTime = 2f;
    [SerializeField, Min(0.1f)] float successStunTime = 1.5f;

    [Header("Mechanic Layout")]
    [SerializeField] Vector2 judgementPaperOffset = new Vector2(0f, -6.4f);
    [SerializeField, Min(0.1f)] float judgementMapIconSize = 0.9f;
    [SerializeField, Min(0.1f)] float designMapIconSize = 2.5f;

    [Header("Damage")]
    [SerializeField, Min(1)] int basicDamage = 18;
    [SerializeField, Min(1)] int judgementPartDamage = 80;
    [SerializeField, Min(1)] int matchFailDamage = 34;
    [SerializeField, Min(1)] int pinFailDamage = 45;
    [SerializeField, Min(1)] int debuffNeedleDamage = 10;

    [Header("Colors")]
    [SerializeField] Color telegraphColor = new Color(0.95f, 0.12f, 0.12f, 0.30f);
    [SerializeField] Color strikeColor = new Color(0.96f, 0.10f, 0.10f, 0.72f);
    [SerializeField] Color stunColor = new Color(1f, 0.30f, 0.26f, 1f);
    [SerializeField] Color trapGlowColor = new Color(0.95f, 0.10f, 0.10f, 0.34f);

    [Header("Basic Attack Impact")]
    [SerializeField] Color strikeBandColor = new Color(0.34f, 0.18f, 0.08f, 0.82f);
    [SerializeField, Min(0.02f)] float strikeShakeDuration = 0.6f;
    [SerializeField, Min(0f)] float strikeShakeMagnitude = 0.45f;
    [SerializeField, Min(1f)] float strikeShakeOscillations = 8f;

    public System.Action Defeated;

    SpriteRenderer bossRenderer;
    Transform player;
    PlayerController playerController;
    Vector3 baseScale;
    Vector2 arenaCenter = Vector2.zero;
    Vector2 arenaSize = new Vector2(26f, 14f);
    bool stunned;
    bool bossDefeated;
    bool pinTurn;
    int judgementUseCount;
    int designMatchUseCount;
    int lastReportedWave = -1;
    bool wave3IntroDone;
    bool wave3Active;
    GameObject wave3RedOverlay;
    Image wave3RedImage;
    static readonly Dictionary<string, Sprite> mapIconCache = new Dictionary<string, Sprite>();

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
        EnsureMinoBreathAnimator();
        transform.localScale = new Vector3(bossScale, bossScale, 1f);
        baseScale = transform.localScale;

        SetupRigidbody();
        EnsureCharacterShadow();   // 플레이어와 동일한 실루엣 그림자(자기 스프라이트 모양 + 숨쉬기 애니메이션 따라감)
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
    }

    protected override void OnDamaged()
    {
        base.OnDamaged();
        ReportHealth();
    }

    protected override void Die()
    {
        if (bossDefeated)
            return;

        bossDefeated = true;
        StopAllCoroutines();   // 진행중 패턴 중단 (아래 페이드 코루틴은 이 이후에 시작되어 살아남음)
        RunHudUI.HideBossHealth();
        StartCoroutine(DeathFadeRoutine());
    }

    // req B3: 죽으면 즉시 사라지지 않고 살짝 페이드되며 주변에 파티클이 흩날린다.
    IEnumerator DeathFadeRoutine()
    {
        if (breathAnimator != null)
            breathAnimator.Stop();
        ClearOwnedTelegraphs();

        Color baseColor = bossRenderer != null ? bossRenderer.color : Color.gray;

        // 초기 파티클 폭발
        for (int i = 0; i < 6; i++)
            EnemyDeathEffect.Spawn(transform.position + (Vector3)(Random.insideUnitCircle * 1.3f), baseColor);

        float dur = 0.9f;
        float elapsed = 0f;
        float nextBurst = 0.12f;
        Color start = bossRenderer != null ? bossRenderer.color : Color.white;
        while (elapsed < dur)
        {
            float t = elapsed / dur;
            if (bossRenderer != null)
            {
                Color c = start;
                c.a = Mathf.Lerp(1f, 0f, t);
                bossRenderer.color = c;
            }

            if (elapsed >= nextBurst)
            {
                nextBurst += 0.13f;
                EnemyDeathEffect.Spawn(transform.position + (Vector3)(Random.insideUnitCircle * 1.5f), baseColor);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        RemoveWave3RedOverlay();

        // base.Die() 알림들을 페이드 후 직접 수행.
        ItemInventoryManager.Instance?.NotifyEnemyKilled(transform.position);
        OnDied?.Invoke(this);
        EnemyManager.Instance?.OnEnemyDied(this);
        Defeated?.Invoke();
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        RunHudUI.HideBossHealth();
        RemoveWave3RedOverlay();
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

            // req A6/A7: 3웨이브 진입 시 1회 연출(플레이어 정지 → 카메라 미노 클로즈업 → 붉게 → 복귀).
            if (wave >= 3 && !wave3IntroDone)
            {
                wave3IntroDone = true;
                yield return StartCoroutine(Wave3IntroRoutine());
                if (bossDefeated) yield break;
            }

            // req B1: 3웨이브부터 행동 사이 대기시간을 크게 단축.
            float gap = wave >= 3 ? betweenActions * 0.3f : betweenActions;
            bool fast = wave >= 3;

            yield return StartCoroutine(BasicAttackRoutine(fast));
            if (bossDefeated) yield break;
            yield return new WaitForSeconds(gap);

            yield return StartCoroutine(WaveMechanic(wave));
            if (bossDefeated) yield break;
            yield return new WaitForSeconds(gap);
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

    // req3: X(0)·+(1) 확률을 높이고 단일 가로(2)/세로(3)는 낮춘다.
    int PickBasicShape()
    {
        float r = Random.value;
        if (r < 0.40f) return 0;    // X (40%)
        if (r < 0.75f) return 1;    // + (35%)
        if (r < 0.875f) return 2;   // 가로 단일 (12.5%)
        return 3;                    // 세로 단일 (12.5%)
    }

    IEnumerator BasicAttackRoutine(bool fast = false)
    {
        List<Band> bands = BuildBasicBands(PickBasicShape());
        List<GameObject> telegraphs = new List<GameObject>();
        for (int i = 0; i < bands.Count; i++)
            telegraphs.Add(TrackTelegraph(EnemyTelegraph.CreateBox("BossBandTelegraph", bands[i].center, bands[i].size, bands[i].angle, telegraphColor, 40)));

        // req3a: 강타 직전 빨간 경고를 점점 빠르게 "번쩍번쩍" 깜빡인다.
        // req B1: 3웨이브(fast)에선 경고 시간을 조금 줄여 기본공격을 아주 조금 빠르게.
        float warnTime = Mathf.Max(0.4f, largeAttackWarningTime) * (fast ? 0.8f : 1f);
        float warnElapsed = 0f;
        while (warnElapsed < warnTime)
        {
            float p = warnElapsed / warnTime;
            float blinkHz = Mathf.Lerp(2.5f, 9f, p);           // 후반부일수록 빠르게
            float pulse = Mathf.Abs(Mathf.Sin(warnElapsed * blinkHz * Mathf.PI));
            float alpha = Mathf.Lerp(0.10f, 0.62f, pulse);
            for (int i = 0; i < telegraphs.Count; i++)
                if (telegraphs[i] != null)
                    EnemyTelegraph.SetUniformAlpha(telegraphs[i], alpha);
            warnElapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < telegraphs.Count; i++)
            if (telegraphs[i] != null)
                DestroyOwnedTelegraph(telegraphs[i]);

        List<GameObject> strikes = new List<GameObject>();
        for (int i = 0; i < bands.Count; i++)
            strikes.Add(TrackTelegraph(EnemyTelegraph.CreateBox("BossBandStrike", bands[i].center, bands[i].size, bands[i].angle, strikeBandColor, 41)));

        // req3b: 강타(스트라이크 밴드)가 나타나는 바로 그 프레임에 화면을 흔든다(강타 후 지연 없음).
        CameraShake.ShakeHorizontal(strikeShakeDuration, strikeShakeMagnitude, strikeShakeOscillations);
        SoundManager.PlayEnemyHit();

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

        // req1: 실제 벽 콜라이더 안쪽 가장자리 기준으로 밴드 길이를 잡아, 기본 공격이 벽에 닿을 때까지 뻗게 한다.
        Rect arena = ArenaWallRect();
        Vector2 center = arena.center;
        float fullW = arena.width;
        float fullH = arena.height;

        // req2: 폭 상한을 넓혀 더 두꺼운 밴드를 허용(직렬화된 sewingLineThickness 로 조절).
        float thickness = Mathf.Clamp(sewingLineThickness, 0.5f, Mathf.Min(fullW, fullH) * 0.5f);
        // req3: 가로/세로 '단일' 공격은 폭을 훨씬 크게(피할 공간이 좁아지도록).
        float singleThickness = Mathf.Clamp(sewingLineThickness * 2.2f, thickness, Mathf.Min(fullW, fullH) * 0.62f);
        float roomDiagonal = Mathf.Sqrt(fullW * fullW + fullH * fullH);
        float diagonal = roomDiagonal * Mathf.Max(1f, diagonalLengthMultiplier);

        switch (shape)
        {
            case 0: // X
                bands.Add(new Band { center = center, size = new Vector2(diagonal, thickness), angle = 45f });
                bands.Add(new Band { center = center, size = new Vector2(diagonal, thickness), angle = -45f });
                break;
            case 1: // cross
                bands.Add(new Band { center = center, size = new Vector2(fullW, thickness), angle = 0f });
                bands.Add(new Band { center = center, size = new Vector2(thickness, fullH), angle = 0f });
                break;
            case 2: // horizontal: left wall to right wall through the room centre
                bands.Add(new Band { center = center, size = new Vector2(fullW, singleThickness), angle = 0f });
                break;
            default: // vertical: top wall to bottom wall through the room centre
                bands.Add(new Band { center = center, size = new Vector2(singleThickness, fullH), angle = 0f });
                break;
        }

        return bands;
    }

    // req1: 방의 벽 콜라이더(Wall_Left/Right/Top/Bottom) 안쪽 면으로 실제 플레이 영역 사각형을 계산.
    // 벽을 못 찾으면 기존 arenaSize 로 폴백.
    Rect ArenaWallRect()
    {
        Collider2D left = FindWallCollider("Wall_Left");
        Collider2D right = FindWallCollider("Wall_Right");
        Collider2D top = FindWallCollider("Wall_Top");
        Collider2D bottom = FindWallCollider("Wall_Bottom");

        if (left != null && right != null && top != null && bottom != null)
        {
            float xMin = left.bounds.max.x;    // 왼쪽 벽 안쪽 면
            float xMax = right.bounds.min.x;   // 오른쪽 벽 안쪽 면
            float yMin = bottom.bounds.max.y;  // 아래 벽 안쪽 면
            float yMax = top.bounds.min.y;     // 위 벽 안쪽 면
            if (xMax > xMin && yMax > yMin)
                return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        return new Rect(arenaCenter.x - arenaSize.x * 0.5f, arenaCenter.y - arenaSize.y * 0.5f, arenaSize.x, arenaSize.y);
    }

    static Collider2D FindWallCollider(string wallName)
    {
        GameObject go = GameObject.Find(wallName);
        return go != null ? go.GetComponent<Collider2D>() : null;
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
        // req: 웨이브1 판정 종이 크기·아이콘·마크 위치를 크게 키워 잘 보이게.
        const float S = 2.0f;
        Vector2 paperPos = (Vector2)transform.position + judgementPaperOffset;
        GameObject paper = TrackTelegraph(BossVisuals.CreatePaper("JudgementPaper", paperPos, new Vector2(5.4f * S, 3.6f * S), 30));
        BossVisuals.CreateRect(paper.transform, "Title", new Vector3(0f, 1.45f * S, 0f), new Vector2(3.2f * S, 0.06f), BossVisuals.PaperLineColor, 31);

        BodySlot[] slots =
        {
            BodySlot.EyeLeft, BodySlot.EyeRight,
            BodySlot.ArmLeft, BodySlot.ArmRight,
            BodySlot.LegLeft, BodySlot.LegRight
        };
        Vector3[] layout =
        {
            new Vector3(-1.4f * S, 1.0f * S, 0f), new Vector3(1.4f * S, 1.0f * S, 0f),
            new Vector3(-1.4f * S, 0.0f, 0f), new Vector3(1.4f * S, 0.0f, 0f),
            new Vector3(-1.4f * S, -1.0f * S, 0f), new Vector3(1.4f * S, -1.0f * S, 0f)
        };

        bool firstUse = judgementUseCount == 0;
        int maxMarked = firstUse || (float)currentHp / Mathf.Max(1, maxHp) > 0.85f ? 1 : 2;
        List<BodySlot> marked = PickMarkedSlots(maxMarked);
        solveTime = firstUse ? firstJudgementTime : repeatJudgementTime;
        judgementUseCount++;
        for (int i = 0; i < slots.Length; i++)
        {
            bool isX = marked.Contains(slots[i]);
            CreateJudgementMapIcon(paper.transform, slots[i], layout[i] + new Vector3(-0.55f * S, 0f, 0f), 32);
            if (isX)
                BossVisuals.CreateXMark(paper.transform, layout[i] + new Vector3(0.55f * S, 0f, 0f), 1.0f * S, 33);
            else
                BossVisuals.CreateOkMark(paper.transform, layout[i] + new Vector3(0.55f * S, 0f, 0f), 1.0f * S, 33);
        }

        SoundManager.PlayPanel();
        yield return StartCoroutine(CountdownRoutine("X 표시 부위를 빼내세요!", solveTime, new Color(1f, 0.85f, 0.3f, 1f)));

        List<BodySlot> stillEquipped = new List<BodySlot>();
        InventoryManager inventory = InventoryManager.Instance;
        for (int i = 0; i < marked.Count; i++)
            if (inventory != null && inventory.IsEquipped(marked[i]))
                stillEquipped.Add(marked[i]);

        if (paper != null)
            DestroyOwnedTelegraph(paper);

        if (stillEquipped.Count == 0)
        {
            yield return StartCoroutine(StunRoutine(successStunTime));
        }
        else
        {
            // req4: 부위를 통째로 없애지 않고, 랜덤으로 한 부위에만 소량(평균 1.5) 데미지.
            yield return StartCoroutine(JudgementChipRandomPartRoutine());
        }
    }

    float partChipRemainder;

    // req4: 랜덤 장착 부위 하나에 1.5 데미지(정수 HP라 누적해 평균 1.5/회 적용). 부위를 제거하지 않는다.
    IEnumerator JudgementChipRandomPartRoutine()
    {
        InventoryManager inventory = InventoryManager.Instance;

        BodySlot? targetSlot = null;
        if (inventory != null)
        {
            List<BodySlot> equipped = new List<BodySlot>();
            foreach (BodySlot s in System.Enum.GetValues(typeof(BodySlot)))
                if (inventory.IsEquipped(s))
                    equipped.Add(s);
            if (equipped.Count > 0)
                targetSlot = equipped[Random.Range(0, equipped.Count)];
        }

        Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
        BodySlot iconSlot = targetSlot ?? BodySlot.EyeLeft;
        GameObject mark = TrackTelegraph(BossVisuals.CreatePartIcon(null, "JudgementChip", iconSlot, target + Vector2.up * 1.1f, 2.2f, BossVisuals.MarkBad, 75));
        BossVisuals.CreateXMark(mark.transform, Vector3.zero, 1.8f, 76);
        yield return new WaitForSeconds(0.45f);
        CameraShake.Shake(0.22f, 0.28f);

        if (inventory != null && targetSlot.HasValue)
        {
            partChipRemainder += 1.5f;
            int dmg = Mathf.FloorToInt(partChipRemainder);
            partChipRemainder -= dmg;
            if (dmg > 0)
                inventory.TryDamageEquippedPart(targetSlot.Value, dmg, out _);
        }

        yield return new WaitForSeconds(0.25f);
        if (mark != null) DestroyOwnedTelegraph(mark);
    }

    List<BodySlot> PickMarkedSlots(int maxMarked)
    {
        BodySlot[] all = { BodySlot.EyeLeft, BodySlot.EyeRight, BodySlot.ArmLeft, BodySlot.ArmRight, BodySlot.LegLeft, BodySlot.LegRight };
        List<BodySlot> equipped = new List<BodySlot>();
        InventoryManager inventory = InventoryManager.Instance;
        for (int i = 0; i < all.Length; i++)
            if (inventory == null || inventory.IsEquipped(all[i]))
                equipped.Add(all[i]);

        if (equipped.Count == 0)
            equipped.AddRange(all);

        int count = Mathf.Clamp(maxMarked <= 1 ? 1 : Random.Range(1, 3), 1, equipped.Count);
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
        GameObject mark = TrackTelegraph(BossVisuals.CreatePartIcon(null, "JudgementPart_" + slot, slot, target + Vector2.up * 1.1f, 2.2f, BossVisuals.MarkBad, 75));
        BossVisuals.CreateXMark(mark.transform, Vector3.zero, 1.8f, 76);
        yield return new WaitForSeconds(0.45f);
        CameraShake.Shake(0.22f, 0.28f);

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null && inventory.IsEquipped(slot))
            inventory.TryDamageEquippedPart(slot, judgementPartDamage, out _);

        yield return new WaitForSeconds(0.25f);
        if (mark != null) DestroyOwnedTelegraph(mark);
    }

    // ---- pattern 2: design matching (wave 2) ------------------------------

    IEnumerator DesignMatchRoutine()
    {
        BodySlot?[] options = { null, BodySlot.ArmLeft, BodySlot.EyeLeft, BodySlot.LegLeft };
        string[] labels = { "온전한 도안", "왼팔 없는 도안", "왼눈 없는 도안", "왼다리 없는 도안" };
        Vector2[] spots =
        {
            arenaCenter + new Vector2(-arenaSize.x * 0.32f, arenaSize.y * 0.25f),
            arenaCenter + new Vector2(arenaSize.x * 0.32f, arenaSize.y * 0.25f),
            arenaCenter + new Vector2(-arenaSize.x * 0.32f, -arenaSize.y * 0.25f),
            arenaCenter + new Vector2(arenaSize.x * 0.32f, -arenaSize.y * 0.25f)
        };
        Vector2 paperSize = new Vector2(
            Mathf.Min(4.6f, arenaSize.x * 0.22f),
            Mathf.Min(4.4f, arenaSize.y * 0.34f));

        List<GameObject> papers = new List<GameObject>();
        for (int i = 0; i < options.Length; i++)
        {
            GameObject paper = TrackTelegraph(BossVisuals.CreatePaper("MatchPaper_" + i, spots[i], paperSize, 12));
            CreateDesignMapIcon(paper.transform, options[i], new Vector3(0f, 0.08f, 0f), 14);
            papers.Add(paper);
        }

        int correct = Random.Range(0, options.Length);
        BodySlot? covered = options[correct];
        ShowCoveredPart(covered);
        SoundManager.PlayPanel();
        // req: 2웨이브 첫 패턴은 +2초로 더 길게, 반복될수록 0.5초씩 짧아짐(최소 2.5초).
        solveTime = Mathf.Max(2.5f, designMatchTime + 2f - 0.5f * designMatchUseCount);
        designMatchUseCount++;

        yield return StartCoroutine(CountdownRoutine("보스가 가린 곳과 같은 도안 위로!", solveTime, new Color(1f, 0.08f, 0.08f, 1f)));

        bool success = player != null
            && PointInPaper(player.position, spots[correct], paperSize);

        for (int i = 0; i < papers.Count; i++)
            if (papers[i] != null)
                DestroyOwnedTelegraph(papers[i]);
        ClearCoveredPart();

        if (success)
        {
            yield return StartCoroutine(StunRoutine(successStunTime));
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

    void CreateJudgementMapIcon(Transform parent, BodySlot slot, Vector3 localPos, int order)
    {
        Sprite sprite = LoadMapIconSprite(MapIconName(slot));
        if (sprite != null)
        {
            BossVisuals.CreateSpriteIcon(parent, "MapIcon_" + slot, sprite, localPos, judgementMapIconSize, Color.white, order);
            return;
        }

        BossVisuals.CreatePartIcon(parent, "Icon_" + slot, slot, localPos, 1.65f, BossVisuals.InkColor, order);
    }

    void CreateDesignMapIcon(Transform parent, BodySlot? missingSlot, Vector3 localPos, int order)
    {
        Sprite sprite = LoadMapIconSprite(MapIconName(missingSlot));
        if (sprite != null)
        {
            BossVisuals.CreateSpriteIcon(parent, "DesignMapIcon_" + (missingSlot.HasValue ? missingSlot.Value.ToString() : "Whole"), sprite, localPos, designMapIconSize, Color.white, order);
            return;
        }

        BossVisuals.CreateDollSilhouette(parent, "Doll", localPos, 0.95f, missingSlot, order);
    }

    static string MapIconName(BodySlot? slot)
    {
        if (!slot.HasValue)
            return "startroom";

        return MapIconName(slot.Value);
    }

    static string MapIconName(BodySlot slot)
    {
        switch (slot)
        {
            case BodySlot.EyeLeft: return "nolefteye";
            case BodySlot.EyeRight: return "norighteye";
            case BodySlot.ArmLeft: return "noleftarm";
            case BodySlot.ArmRight: return "norightarm";
            case BodySlot.LegLeft: return "noleftleg";
            case BodySlot.LegRight: return "norightleg";
            default: return "startroom";
        }
    }

    static Sprite LoadMapIconSprite(string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
            return null;

        if (mapIconCache.TryGetValue(iconName, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>("Sprites/mapicon/" + iconName);
#if UNITY_EDITOR
        if (sprite == null)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/" + iconName + ".png");
#endif
        mapIconCache[iconName] = sprite;
        return sprite;
    }

    void ShowCoveredPart(BodySlot? slot)
    {
        ClearCoveredPart();
        if (slot == null)
            return;

        // req: 미노타우로스 실제 생김새(눈=상단중앙, 손=양옆 중간, 발=하단)에 맞춰 X 위치 조정.
        Vector3 local;
        switch (slot.Value)
        {
            case BodySlot.ArmLeft: local = new Vector3(-0.42f, -0.05f, 0f); break;  // 왼손(화면 좌측)
            case BodySlot.EyeLeft: local = new Vector3(-0.11f, 0.30f, 0f); break;   // 왼눈(상단 중앙)
            default: local = new Vector3(-0.14f, -0.42f, 0f); break;                // 왼발(하단)
        }

        // req: X 표시 크기를 더 키운다(0.72 → 1.15).
        coveredPatch = TrackTelegraph(BossVisuals.CreateXMark(transform, local, 1.15f, 77));
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
            arenaCenter.x + Random.Range(-3.5f, 3.5f),
            arenaCenter.y - arenaSize.y * 0.18f + Random.Range(-0.6f, 0.6f));

        int pinCount = Random.Range(3, 5);
        Vector2[] corners = BuildPinPolygon(zoneCenter, zoneSize, pinCount);

        // req: 핀을 한 개씩이 아니라 두 개씩 순차적으로 박아 더 박력있게.
        List<GameObject> pins = new List<GameObject>();
        for (int i = 0; i < corners.Length; i += 2)
        {
            pins.Add(TrackTelegraph(BossVisuals.CreatePin("SewingPin_" + i, corners[i], 50)));
            if (i + 1 < corners.Length)
                pins.Add(TrackTelegraph(BossVisuals.CreatePin("SewingPin_" + (i + 1), corners[i + 1], 50)));
            CameraShake.ShakeHorizontal(0.28f, 0.34f);
            SoundManager.PlayEnemyHit();
            yield return new WaitForSeconds(0.28f);
        }

        List<GameObject> threads = new List<GameObject>();
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 a = corners[i];
            Vector2 b = corners[(i + 1) % corners.Length];
            threads.Add(TrackTelegraph(BossVisuals.CreateDashedLine(null, "PinThread_" + i, a, b, 0.12f, new Color(0.85f, 0.2f, 0.2f, 0.9f), 51)));
            yield return new WaitForSeconds(0.18f);
        }

        GameObject glow = TrackTelegraph(EnemyTelegraph.CreatePolygon("PinTrapGlow", corners, trapGlowColor, 49));
        float blinkElapsed = 0f;
        while (blinkElapsed < pinClosureTime)
        {
            EnemyTelegraph.SetUniformAlpha(glow, Mathf.PingPong(blinkElapsed * 2.4f, 0.45f) + 0.12f);
            blinkElapsed += Time.deltaTime;
            yield return null;
        }

        GameObject slam = TrackTelegraph(EnemyTelegraph.CreatePolygon("PinSlam", corners, strikeColor, 52));
        CameraShake.Shake(0.32f, 0.42f);
        SoundManager.PlayEnemyHit();

        bool caught = player != null && PointInPolygon(player.position, corners);
        if (caught)
        {
            if (playerController != null)
                playerController.LockMovement(0.85f);
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

    Vector2[] BuildPinPolygon(Vector2 center, Vector2 size, int pinCount)
    {
        Vector2 half = size * 0.5f;
        if (pinCount <= 3)
        {
            return new[]
            {
                center + new Vector2(0f, half.y),
                center + new Vector2(half.x, -half.y),
                center + new Vector2(-half.x, -half.y)
            };
        }

        return new[]
        {
            center + new Vector2(-half.x, -half.y),
            center + new Vector2(half.x, -half.y),
            center + new Vector2(half.x, half.y),
            center + new Vector2(-half.x, half.y)
        };
    }

    // ---- pattern 4: stitch debuff (wave 3) --------------------------------

    IEnumerator DebuffRoutine()
    {
        Vector2 start = transform.position;
        Vector2 target = player != null ? (Vector2)player.position : arenaCenter;
        GameObject warning = TrackTelegraph(EnemyTelegraph.CreateLine("DebuffNeedleWarning", start, target, 0.48f, telegraphColor, 54));
        yield return new WaitForSeconds(1f);
        if (warning != null) DestroyOwnedTelegraph(warning);

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

        bool hit = player != null && Vector2.Distance(player.position, target) <= 1.1f;
        if (hit)
        {
            ApplyPlayerDamage(debuffNeedleDamage, 0.1f);
            yield return StartCoroutine(ApplyStitchDebuff(3f));
        }
    }

    IEnumerator ApplyStitchDebuff(float duration)
    {
        int effect = Random.Range(0, 3);
        GameObject temporaryVisual = null;

        if (effect == 0)
        {
            if (playerController != null)
                playerController.ApplyTemporarySpeedMultiplier(0.5f, duration);
        }
        else if (effect == 1)
        {
            temporaryVisual = TrackTelegraph(CreateEyeDarkOverlay(Random.value < 0.5f));
        }
        else
        {
            BodySlot blockedArm = Random.value < 0.5f ? BodySlot.ArmLeft : BodySlot.ArmRight;
            PlayerAttack attack = player != null ? player.GetComponent<PlayerAttack>() : null;
            if (attack != null)
                attack.ApplyTemporaryArmBlock(blockedArm, duration);

            if (player != null)
            {
                temporaryVisual = TrackTelegraph(BossVisuals.CreatePartIcon(player, "BlockedArmIcon", blockedArm, new Vector3(0f, 0.7f, 0f), 0.7f, BossVisuals.MarkBad, 90));
                BossVisuals.CreateXMark(temporaryVisual.transform, Vector3.zero, 0.58f, 91);
            }
        }

        yield return new WaitForSeconds(duration);

        if (temporaryVisual != null)
            DestroyOwnedTelegraph(temporaryVisual);
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

    GameObject CreateEyeDarkOverlay(bool darkenLeft)
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
        rect.anchorMin = darkenLeft ? new Vector2(0f, 0f) : new Vector2(0.54f, 0f);
        rect.anchorMax = darkenLeft ? new Vector2(0.46f, 1f) : new Vector2(1f, 1f);
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

    IEnumerator CountdownRoutine(string message, float duration, Color color)
    {
        RunHudUI.ShowJudgementTimer(message, color);

        float remaining = duration;
        while (remaining > 0f && !bossDefeated)
        {
            RunHudUI.SetJudgementTimer(remaining, duration);
            remaining -= Time.deltaTime;
            yield return null;
        }

        RunHudUI.SetJudgementTimer(0f, duration);
        RunHudUI.HideJudgementTimer();
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

    bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
            return false;

        bool inside = false;
        int previous = polygon.Length - 1;
        for (int current = 0; current < polygon.Length; current++)
        {
            Vector2 a = polygon[current];
            Vector2 b = polygon[previous];
            bool crosses = (a.y > point.y) != (b.y > point.y)
                && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
            if (crosses)
                inside = !inside;
            previous = current;
        }

        return inside;
    }

    // ---- req A6/A7: 3웨이브 진입 연출 ---------------------------------------

    IEnumerator Wave3IntroRoutine()
    {
        wave3Active = true;
        Camera cam = Camera.main;

        if (playerController == null)
        {
            GameObject po = GameObject.FindWithTag("Player");
            if (po != null)
                playerController = po.GetComponent<PlayerController>();
        }

        Vector3 arenaCamPos = cam != null ? cam.transform.position : Vector3.zero;
        float arenaCamSize = cam != null ? cam.orthographicSize : 6f;
        Vector3 focusPos = new Vector3(transform.position.x, transform.position.y, arenaCamPos.z);
        float focusSize = Mathf.Max(2f, arenaCamSize * 0.62f);

        EnsureWave3RedOverlay();

        Color bossBase = bossRenderer != null ? bossRenderer.color : Color.white;
        Color bossRed = new Color(1f, 0.42f, 0.36f, 1f);

        // req2: 3웨이브 진입 연출 시간을 조금 단축.
        const float zoomIn = 0.8f;
        const float hold = 1.5f;
        const float zoomBack = 0.75f;

        // 1) 미노 쪽으로 클로즈업 + 숨쉬기 가속 + 붉게 물듦
        float e = 0f;
        while (e < zoomIn)
        {
            float t = Mathf.SmoothStep(0f, 1f, e / zoomIn);
            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(arenaCamPos, focusPos, t);
                cam.orthographicSize = Mathf.Lerp(arenaCamSize, focusSize, t);
            }
            breathAnimator?.SetSpeedMultiplier(Mathf.Lerp(1f, 4.5f, t));
            if (bossRenderer != null) bossRenderer.color = Color.Lerp(bossBase, bossRed, t);
            SetWave3RedAlpha(Mathf.Lerp(0f, 0.14f, t));
            playerController?.LockMovement(0.3f);
            e += Time.deltaTime;
            yield return null;
        }

        // 2) 클로즈업 유지: 겁나 빠른 숨 + 좌우 흔들림 + 붉은 화면
        e = 0f;
        while (e < hold)
        {
            float shakeX = Mathf.Sin(e * Mathf.PI * 2f * 6f) * 0.12f;
            if (cam != null) cam.transform.position = focusPos + new Vector3(shakeX, 0f, 0f);
            breathAnimator?.SetSpeedMultiplier(4.5f);
            if (bossRenderer != null) bossRenderer.color = bossRed;
            SetWave3RedAlpha(0.14f);
            playerController?.LockMovement(0.3f);
            e += Time.deltaTime;
            yield return null;
        }

        // 3) 카메라를 보스 전용 원위치로 복귀
        e = 0f;
        while (e < zoomBack)
        {
            float t = Mathf.SmoothStep(0f, 1f, e / zoomBack);
            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(focusPos, arenaCamPos, t);
                cam.orthographicSize = Mathf.Lerp(focusSize, arenaCamSize, t);
            }
            playerController?.LockMovement(0.3f);
            e += Time.deltaTime;
            yield return null;
        }

        if (cam != null)
        {
            cam.transform.position = arenaCamPos;
            cam.orthographicSize = arenaCamSize;
        }

        // req B2: 3웨이브 내내 숨쉬기 빠르게 유지 / 화면은 살짝 붉은 상태 유지
        breathAnimator?.SetSpeedMultiplier(3f);
        SetWave3RedAlpha(0.12f);
        // 이후로 LockMovement를 더 호출하지 않으므로 플레이어 이동이 곧 풀린다.
    }

    void EnsureWave3RedOverlay()
    {
        if (wave3RedOverlay != null)
            return;

        wave3RedOverlay = new GameObject("Wave3RedOverlay");
        Canvas canvas = wave3RedOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        wave3RedOverlay.AddComponent<CanvasScaler>();

        GameObject imageObject = new GameObject("Red");
        imageObject.transform.SetParent(wave3RedOverlay.transform, false);
        RectTransform rt = imageObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        wave3RedImage = imageObject.AddComponent<Image>();
        wave3RedImage.color = new Color(0.70f, 0.05f, 0.04f, 0f);
        wave3RedImage.raycastTarget = false;
    }

    void SetWave3RedAlpha(float alpha)
    {
        if (wave3RedImage == null)
            return;

        Color c = wave3RedImage.color;
        c.a = Mathf.Clamp01(alpha);
        wave3RedImage.color = c;
    }

    void RemoveWave3RedOverlay()
    {
        if (wave3RedOverlay != null)
        {
            Destroy(wave3RedOverlay);
            wave3RedOverlay = null;
            wave3RedImage = null;
        }
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

    void EnsureMinoBreathAnimator()
    {
        if (breathAnimator == null)
            breathAnimator = GetComponent<MinoBreathAnimator>();
        if (breathAnimator == null)
            breathAnimator = gameObject.AddComponent<MinoBreathAnimator>();

#if UNITY_EDITOR
        if (!breathAnimator.HasFrames)
        {
            Sprite[] frames =
            {
                LoadEditorSprite("mino_jjin"),
                LoadEditorSprite("mino_head1"),
                LoadEditorSprite("mino_body1"),
                LoadEditorSprite("mino_head2"),
                LoadEditorSprite("mino_body2"),
                LoadEditorSprite("mino_head2"),
                LoadEditorSprite("mino_body1"),
                LoadEditorSprite("mino_head1"),
                LoadEditorSprite("mino_jjin")
            };

            bool hasAllFrames = true;
            for (int i = 0; i < frames.Length; i++)
                hasAllFrames &= frames[i] != null;

            if (hasAllFrames)
            {
                breathAnimator.Configure(
                    frames,
                    new[] { 0.12f, 0.08f, 0.09f, 0.08f, 0.22f, 0.08f, 0.09f, 0.08f, 0.24f },
                    true,
                    true,
                    true);
            }
        }
#endif
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

#if UNITY_EDITOR
    Sprite LoadEditorSprite(string frameName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/" + frameName + ".png");
    }
#endif
}
