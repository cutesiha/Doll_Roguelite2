using System.Collections;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class NeedleEnemy : EnemyBase
{
    [Header("Needle Animation")]
    [SerializeField] Sprite frame1;
    [SerializeField] Sprite frame2;
    [SerializeField] Sprite frame3;
    [SerializeField, Min(1f)] float walkFramesPerSecond = 8f;
    [SerializeField, Min(0f)] float chaseSpeed = 0.75f;
    [SerializeField] Vector2 dashCooldownRange = new Vector2(2.6f, 3.8f);
    [SerializeField, Min(0.1f)] float warningDuration = 1.2f;
    [SerializeField, Min(0.5f)] float dashDistance = 11f;
    [SerializeField, Min(0.1f)] float dashWidth = 1.05f;
    [SerializeField, Min(0.5f)] float dashSpeed = 18f;
    [SerializeField, Min(1)] int dashDamage = 30;
    [SerializeField] Color dashTelegraphColor = new Color(1f, 0.08f, 0.08f, 0.28f);

    Rigidbody2D rb;
    SpriteRenderer needleSpriteRenderer;
    Transform player;
    float needleAnimationTime;
    float nextDashTime;
    bool isBusy;
    static readonly int[] WalkSequence = { 0, 1, 2, 1 };

    public override EnemyKind Kind => EnemyKind.Needle;

    protected override void Awake()
    {
        currentHp = maxHp;
        needleSpriteRenderer = GetComponent<SpriteRenderer>();
        LoadNeedleFramesIfMissing();
        ApplyNeedleFrame(0);
        EnsureCharacterShadow();
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        EnsureCollider();
    }

    protected override void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        ResetDashTimer();
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
        ApplyProfileStats(profile);
        if (profile != null)
            chaseSpeed = Mathf.Max(0f, profile.moveSpeed);
    }

    void FixedUpdate()
    {
        if (player == null || isBusy)
            return;
        if (TryMoveSpawnApproach())
            return;

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + direction * chaseSpeed * Time.fixedDeltaTime);
    }

    protected override void Update()
    {
        if (!isBusy)
            UpdateWalkAnimation();

        if (player == null || isBusy)
            return;

        if (Time.time >= nextDashTime)
            StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        isBusy = true;
        ApplyNeedleFrame(0);

        Vector2 start = rb.position;
        Vector2 direction = player != null ? ((Vector2)player.position - start).normalized : Vector2.right;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 size = new Vector2(dashDistance, dashWidth);
        Vector2 center = start + direction * (dashDistance * 0.5f);
        GameObject telegraph = TrackTelegraph(EnemyTelegraph.CreateBox("NeedleDashTelegraph", center, size, angle, dashTelegraphColor, 72));

        yield return EnemyTelegraph.Blink(telegraph, 2, warningDuration * 0.25f);

        if (telegraph != null)
            DestroyOwnedTelegraph(telegraph);

        Vector2 end = start + direction * dashDistance;
        float duration = dashDistance / Mathf.Max(0.01f, dashSpeed);
        float elapsed = 0f;
        bool dealtDamage = false;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            rb.MovePosition(Vector2.Lerp(start, end, t));
            dealtDamage |= TryDealDashDamage(center, size, angle, dealtDamage);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(end);
        TryDealDashDamage(center, size, angle, dealtDamage);

        ResetDashTimer();
        isBusy = false;
    }

    void UpdateWalkAnimation()
    {
        if (needleSpriteRenderer == null)
            return;

        LoadNeedleFramesIfMissing();
        needleAnimationTime += Time.deltaTime;
        int sequenceIndex = Mathf.FloorToInt(needleAnimationTime * walkFramesPerSecond) % WalkSequence.Length;
        ApplyNeedleFrame(WalkSequence[sequenceIndex]);
    }

    void ApplyNeedleFrame(int frame)
    {
        if (needleSpriteRenderer == null)
            return;

        Sprite sprite = frame switch
        {
            1 => frame2,
            2 => frame3,
            _ => frame1
        };

        if (sprite != null)
            needleSpriteRenderer.sprite = sprite;
    }

    void LoadNeedleFramesIfMissing()
    {
        if (frame1 == null)
            frame1 = LoadNeedleSprite("n1");
        if (frame2 == null)
            frame2 = LoadNeedleSprite("n2");
        if (frame3 == null)
            frame3 = LoadNeedleSprite("n3");
    }

    Sprite LoadNeedleSprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/enemy/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/enemy/" + spriteName + ".png").OfType<Sprite>().ToArray();
        if (sprites.Length > 0)
            return sprites[0];

        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/enemy/" + spriteName + ".png");
#else
        return null;
#endif
    }

    bool TryDealDashDamage(Vector2 center, Vector2 size, float angle, bool alreadyDamaged)
    {
        if (alreadyDamaged)
            return false;

        PlayerDamageReceiver receiver = FindFirstObjectByType<PlayerDamageReceiver>();
        if (receiver == null)
            return false;

        if (!EnemyTelegraph.PointInOrientedBox(receiver.transform.position, center, size, angle))
            return false;

        return receiver.TryTakePatternDamage(dashDamage);
    }

    void ResetDashTimer()
    {
        float min = Mathf.Max(0.1f, dashCooldownRange.x);
        float max = Mathf.Max(min, dashCooldownRange.y);
        nextDashTime = Time.time + Random.Range(min, max);
    }

    void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null)
            return;

        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
    }
}
