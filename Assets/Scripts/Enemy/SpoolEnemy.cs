using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpoolEnemy : EnemyBase
{
    [Header("Spool Animation")]
    [SerializeField] Sprite idleSprite;
    [SerializeField] string fallbackSpriteName = "sp1";
    [SerializeField, Min(0f)] float breathingAmplitude = 0.055f;
    [SerializeField, Min(0.1f)] float breathingFrequency = 1.15f;
    [SerializeField] Vector2 threadCooldownRange = new Vector2(3f, 4.5f);
    [SerializeField, Min(0.05f)] float prepareDuration = 0.55f;
    [SerializeField, Min(0.05f)] float threadWarningDuration = 1.15f;
    [SerializeField, Min(0.05f)] float threadGrowDuration = 0.55f;
    [SerializeField, Min(2)] int strandCount = 8;
    [SerializeField, Min(0.5f)] float strandLength = 6f;
    [SerializeField, Min(0.03f)] float strandWidth = 0.16f;
    [SerializeField, Min(0.1f)] float strandDuration = 2.1f;
    [SerializeField, Min(1)] int strandDamage = 20;
    [SerializeField] Color warningColor = new Color(1f, 0.02f, 0.02f, 0.32f);
    [SerializeField] Color strandIvoryColor = new Color(0.96f, 0.91f, 0.82f, 0.96f);
    [SerializeField] Color strandPeachColor = new Color(0.91f, 0.63f, 0.48f, 0.94f);
    [SerializeField] Color strandGrayColor = new Color(0.58f, 0.56f, 0.54f, 0.92f);

    readonly List<Strand> activeStrands = new List<Strand>();
    SpriteRenderer spoolRenderer;
    Rigidbody2D rb;
    Vector3 baseScale;
    float idleTime;
    float nextThreadTime;
    bool isAttacking;

    public override EnemyKind Kind => EnemyKind.Spool;

    struct Strand
    {
        public Vector2 start;
        public Vector2 end;
        public GameObject visual;
    }

    protected override void Awake()
    {
        currentHp = maxHp;
        spoolRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        baseScale = transform.localScale;
        LoadIdleSpriteIfMissing();
        ApplyIdleSprite();
        EnsureCharacterShadow();
        EnsureCollider();
    }

    protected override void Start()
    {
        ResetThreadTimer();
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
        // Spool is stationary, so only HP is profile-driven.
        ApplyProfileStats(profile);
    }

    void FixedUpdate()
    {
        if (!isAttacking)
            TryMoveSpawnApproach();
    }

    protected override void Update()
    {
        UpdateIdleMotion();

        if (!isAttacking && Time.time >= nextThreadTime)
            StartCoroutine(ThreadRoutine());

        CheckStrandDamage();
    }

    IEnumerator ThreadRoutine()
    {
        isAttacking = true;
        Vector3 attackBaseScale = baseScale;

        float elapsed = 0f;
        while (elapsed < prepareDuration)
        {
            float pulse = Mathf.Sin((elapsed / Mathf.Max(0.01f, prepareDuration)) * Mathf.PI);
            transform.localScale = attackBaseScale * (1f + pulse * 0.12f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = attackBaseScale;
        ClearStrands();

        Vector2 origin = transform.position;
        float angleOffset = Random.Range(0f, 360f / Mathf.Max(1, strandCount));
        List<Strand> plannedStrands = new List<Strand>(strandCount);
        for (int i = 0; i < strandCount; i++)
        {
            float angle = (angleOffset + 360f * i / strandCount) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 end = origin + direction * strandLength;
            GameObject visual = TrackTelegraph(EnemyTelegraph.CreateLine("SpoolThreadWarning", origin, end, strandWidth, warningColor, 69));
            plannedStrands.Add(new Strand { start = origin, end = end, visual = visual });
        }

        GameObject warningRoot = TrackTelegraph(StrandRoot(plannedStrands));
        yield return EnemyTelegraph.Blink(warningRoot, 2, threadWarningDuration * 0.25f);
        ClearStrands(plannedStrands);
        if (warningRoot != null)
            DestroyOwnedTelegraph(warningRoot);

        activeStrands.Clear();
        Color[] threadPalette = { strandIvoryColor, strandPeachColor, strandGrayColor };
        for (int i = 0; i < plannedStrands.Count; i++)
        {
            GameObject thread = TrackTelegraph(EnemyTelegraph.CreateThread("SpoolThread", plannedStrands[i].start, plannedStrands[i].end, strandWidth, threadPalette, 69));
            EnemyTelegraph.SetRevealFraction(thread, 0f);
            activeStrands.Add(new Strand { start = plannedStrands[i].start, end = plannedStrands[i].start, visual = thread });
        }

        float growElapsed = 0f;
        while (growElapsed < threadGrowDuration)
        {
            float t = Mathf.Clamp01(growElapsed / Mathf.Max(0.01f, threadGrowDuration));
            UpdateGrowingStrands(plannedStrands, t);
            CheckStrandDamage();
            growElapsed += Time.deltaTime;
            yield return null;
        }

        UpdateGrowingStrands(plannedStrands, 1f);
        yield return new WaitForSeconds(strandDuration);
        ClearStrands();
        ResetThreadTimer();
        isAttacking = false;
    }

    void UpdateIdleMotion()
    {
        if (isAttacking)
            return;

        idleTime += Time.deltaTime;
        float breath = Mathf.Sin(idleTime * Mathf.PI * 2f * breathingFrequency);
        transform.localScale = new Vector3(
            baseScale.x * (1f + breath * breathingAmplitude),
            baseScale.y * (1f - breath * breathingAmplitude * 0.55f),
            baseScale.z);
    }

    void ApplyIdleSprite()
    {
        if (spoolRenderer == null)
            return;

        if (idleSprite != null)
            spoolRenderer.sprite = idleSprite;
    }

    void LoadIdleSpriteIfMissing()
    {
        if (idleSprite == null && spoolRenderer != null && spoolRenderer.sprite != null)
            idleSprite = spoolRenderer.sprite;

        if (idleSprite == null)
            idleSprite = LoadEnemySprite(fallbackSpriteName);
    }

    Sprite LoadEnemySprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/enemy/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/" + spriteName + ".png");
#else
        return null;
#endif
    }

    GameObject StrandRoot(List<Strand> strands)
    {
        GameObject root = new GameObject("SpoolThreadWarnings");
        root.transform.position = Vector3.zero;
        for (int i = 0; i < strands.Count; i++)
        {
            if (strands[i].visual != null)
                strands[i].visual.transform.SetParent(root.transform, true);
        }

        return root;
    }

    void UpdateGrowingStrands(List<Strand> plannedStrands, float t)
    {
        for (int i = 0; i < plannedStrands.Count; i++)
        {
            Strand active = activeStrands[i];
            active.end = Vector2.Lerp(plannedStrands[i].start, plannedStrands[i].end, t);
            EnemyTelegraph.SetRevealFraction(active.visual, t);
            activeStrands[i] = active;
        }
    }

    void CheckStrandDamage()
    {
        if (activeStrands.Count == 0)
            return;

        PlayerDamageReceiver receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver == null)
            return;

        for (int i = 0; i < activeStrands.Count; i++)
        {
            // 바닥 실 피격판정은 플레이어의 Box 히트박스 기준.
            if (receiver.FloorAttackHitsSegment(activeStrands[i].start, activeStrands[i].end, strandWidth * 0.85f))
            {
                receiver.TryTakePatternDamage(strandDamage, 0.45f);
                return;
            }
        }
    }

    float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
            return Vector2.Distance(point, start);

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
        return Vector2.Distance(point, start + segment * t);
    }

    void ClearStrands()
    {
        ClearStrands(activeStrands);

        activeStrands.Clear();
    }

    void ClearStrands(List<Strand> strands)
    {
        for (int i = 0; i < strands.Count; i++)
            if (strands[i].visual != null)
                DestroyOwnedTelegraph(strands[i].visual);
    }

    void ResetThreadTimer()
    {
        float min = Mathf.Max(0.1f, threadCooldownRange.x);
        float max = Mathf.Max(min, threadCooldownRange.y);
        nextThreadTime = Time.time + Random.Range(min, max);
    }

    protected override void Die()
    {
        ClearStrands();
        base.Die();
    }

    void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null)
            return;

        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }
}
