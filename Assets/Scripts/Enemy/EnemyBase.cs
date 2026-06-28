using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnemyBase : MonoBehaviour
{
    [SerializeField] protected int maxHp = 2;
    [SerializeField] Color bodyColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField, Min(1f)] float framesPerSecond = 8f;
    [Header("Hit Area")]
    [SerializeField] Collider2D hitCollider;
    [SerializeField] bool fitColliderToCurrentSprite = false;
    [SerializeField, Min(0f)] float colliderPadding = 0f;
    [Header("Hit Feedback")]
    [SerializeField, Range(0.03f, 0.35f)] float hitFeedbackDuration = 0.18f;
    [SerializeField, Min(0f)] float hitShakeDistance = 0.28f;
    [SerializeField] Color hitTint = new Color(1f, 0.28f, 0.24f, 1f);
    [Header("Camera Shake")]
    [SerializeField] bool shakeCameraOnHit = true;
    [SerializeField, Range(0.02f, 0.35f)] float cameraShakeDuration = 0.12f;
    [SerializeField, Min(0f)] float cameraShakeMagnitude = 0.10f;
    protected int currentHp;
    float fractionalDamageRemainder;
    public bool HasManagedProfile { get; private set; }

    // 공중에 떠 있는 동안(예: 단추 적 점프) 접촉 데미지를 무시하게 한다.
    // 착지 시 패턴 데미지(슬램)만 적용되도록 EnemyChaser가 점프 동안 true로 설정한다.
    public bool SuppressContactDamage { get; set; }

    // Which profile bucket in EnemyManager applies to this enemy. Defaults to None
    // so bosses / plain EnemyBase objects are never assigned a doll profile.
    public virtual EnemyKind Kind => EnemyKind.None;

    // Raised right before this enemy is destroyed. Used by systems that track per-kill
    // events (e.g. the Book boss reducing its body HP as summoned minions are defeated).
    public System.Action<EnemyBase> OnDied;

    SpriteRenderer spriteRenderer;
    Sprite[] animationFrames;
    float animationTime;
    int currentFrame = -1;
    Coroutine hitFeedbackRoutine;
    Color spriteBaseColor = Color.white;
    readonly List<GameObject> ownedTelegraphs = new List<GameObject>();
    Transform spawnApproachTarget;
    Rigidbody2D spawnApproachBody;
    float spawnApproachEndsAt;
    float spawnApproachSpeed;

    protected virtual void Awake()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureCharacterShadow();
        if (spriteRenderer != null)
        {
            animationFrames = LoadRandomEnemyFrames();
            ApplyAnimationFrame(0);
            spriteRenderer.color = Color.white;
            spriteBaseColor = Color.white;
            EnsureHitCollider();
            return;
        }

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", bodyColor);
            rend.material = mat;
        }

        EnsureHitCollider();
    }

    protected void EnsureCharacterShadow()
    {
        if (GetComponent<CharacterOvalShadow>() == null && GetComponent<SpriteRenderer>() != null)
            gameObject.AddComponent<CharacterOvalShadow>();
    }

    protected virtual void Start()
    {
        EnemyManager.Instance?.ConfigureEnemy(this);
    }

    protected virtual void Update()
    {
        if (animationFrames == null || animationFrames.Length == 0 || spriteRenderer == null)
            return;

        animationTime += Time.deltaTime;
        int frame = Mathf.FloorToInt(animationTime * framesPerSecond) % animationFrames.Length;
        ApplyAnimationFrame(frame);
    }

    void ApplyAnimationFrame(int frame)
    {
        if (animationFrames == null || animationFrames.Length == 0 || spriteRenderer == null)
            return;

        frame = Mathf.Clamp(frame, 0, animationFrames.Length - 1);
        if (frame == currentFrame)
            return;

        spriteRenderer.sprite = animationFrames[frame];
        currentFrame = frame;
        FitColliderToSprite(spriteRenderer.sprite);
    }

    void EnsureHitCollider()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();

        if (hitCollider != null
            && !(hitCollider is PolygonCollider2D)
            && !(hitCollider is BoxCollider2D)
            && !(hitCollider is CapsuleCollider2D))
        {
            hitCollider.enabled = false;
            hitCollider = null;
        }

        if (hitCollider == null && spriteRenderer != null)
            hitCollider = gameObject.AddComponent<PolygonCollider2D>();

        FitColliderToSprite(spriteRenderer != null ? spriteRenderer.sprite : null);
    }

    void FitColliderToSprite(Sprite sprite)
    {
        if (!fitColliderToCurrentSprite || sprite == null)
            return;

        if (hitCollider == null)
            EnsureHitCollider();

        if (hitCollider == null)
            return;

        if (hitCollider is PolygonCollider2D polygon)
        {
            if (TryApplySpritePhysicsShape(polygon, sprite))
                return;

            ApplyBoxPath(polygon, sprite);
            return;
        }

        if (hitCollider is BoxCollider2D box)
        {
            Bounds bounds = sprite.bounds;
            box.offset = bounds.center;
            box.size = new Vector2(
                Mathf.Max(0.01f, bounds.size.x + colliderPadding * 2f),
                Mathf.Max(0.01f, bounds.size.y + colliderPadding * 2f));
            return;
        }

        if (hitCollider is CapsuleCollider2D capsule)
        {
            Bounds bounds = sprite.bounds;
            capsule.offset = bounds.center;
            capsule.size = new Vector2(
                Mathf.Max(0.01f, bounds.size.x + colliderPadding * 2f),
                Mathf.Max(0.01f, bounds.size.y + colliderPadding * 2f));
            capsule.direction = bounds.size.y >= bounds.size.x
                ? CapsuleDirection2D.Vertical
                : CapsuleDirection2D.Horizontal;
        }
    }

    bool TryApplySpritePhysicsShape(PolygonCollider2D polygon, Sprite sprite)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
            return false;

        List<Vector2> path = new List<Vector2>(32);
        List<List<Vector2>> validPaths = new List<List<Vector2>>(shapeCount);
        for (int i = 0; i < shapeCount; i++)
        {
            path.Clear();
            sprite.GetPhysicsShape(i, path);
            if (path.Count < 3)
                continue;

            if (colliderPadding > 0f)
                ExpandPath(path, colliderPadding);

            validPaths.Add(new List<Vector2>(path));
        }

        if (validPaths.Count == 0)
            return false;

        polygon.pathCount = validPaths.Count;
        for (int i = 0; i < validPaths.Count; i++)
            polygon.SetPath(i, validPaths[i]);
        return true;
    }

    void ApplyBoxPath(PolygonCollider2D polygon, Sprite sprite)
    {
        Bounds bounds = sprite.bounds;
        float minX = bounds.min.x - colliderPadding;
        float minY = bounds.min.y - colliderPadding;
        float maxX = bounds.max.x + colliderPadding;
        float maxY = bounds.max.y + colliderPadding;

        polygon.pathCount = 1;
        polygon.SetPath(0, new[]
        {
            new Vector2(minX, minY),
            new Vector2(minX, maxY),
            new Vector2(maxX, maxY),
            new Vector2(maxX, minY)
        });
    }

    void ExpandPath(List<Vector2> path, float padding)
    {
        if (path == null || path.Count == 0 || padding <= 0f)
            return;

        Vector2 center = Vector2.zero;
        for (int i = 0; i < path.Count; i++)
            center += path[i];
        center /= path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 direction = path[i] - center;
            if (direction.sqrMagnitude > 0.0001f)
                path[i] += direction.normalized * padding;
        }
    }

    // Applies only the shared stats (HP) without touching sprites/colors. Used by
    // the special enemies that keep their own art but still want HP tunable from
    // the EnemyManager profile list.
    protected void ApplyProfileStats(EnemyProfile profile)
    {
        if (profile == null)
            return;

        HasManagedProfile = true;
        maxHp = Mathf.Max(1, profile.maxHp);
        currentHp = maxHp;
    }

    public virtual void ApplyProfile(EnemyProfile profile)
    {
        if (profile == null)
            return;

        HasManagedProfile = true;
        maxHp = Mathf.Max(1, profile.maxHp);
        currentHp = maxHp;
        framesPerSecond = Mathf.Max(1f, profile.framesPerSecond);
        bodyColor = profile.tint;

        if (profile.HasAnimationFrames)
        {
            animationFrames = SortSprites(profile.animationFrames);
            animationTime = 0f;
            currentFrame = -1;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = bodyColor;
            spriteBaseColor = bodyColor;
            ApplyAnimationFrame(0);
            return;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = renderer.material;
            if (material != null)
                material.SetColor("_BaseColor", bodyColor);
        }
    }

    Sprite[] LoadRandomEnemyFrames()
    {
        string[] spriteNames = { "d1", "d2", "d3" };
        int start = Random.Range(0, spriteNames.Length);
        for (int i = 0; i < spriteNames.Length; i++)
        {
            Sprite[] frames = LoadEnemyFrames(spriteNames[(start + i) % spriteNames.Length]);
            if (frames.Length > 0)
                return frames;
        }

        return new Sprite[0];
    }

    Sprite[] LoadEnemyFrames(string spriteName)
    {
        Sprite[] frames = Resources.LoadAll<Sprite>("Sprites/enemy/" + spriteName);
        if (frames != null && frames.Length > 0)
            return SortSprites(frames);

#if UNITY_EDITOR
        return SortSprites(AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/enemy/" + spriteName + ".png").OfType<Sprite>().ToArray());
#else
        return new Sprite[0];
#endif
    }

    Sprite[] SortSprites(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return new Sprite[0];

        return sprites
            .OrderBy(sprite => sprite.rect.x)
            .ThenBy(sprite => sprite.rect.y)
            .ThenBy(sprite => sprite.name)
            .ToArray();
    }

    public virtual void TakeDamage(int damage)
    {
        currentHp -= damage;
        OnDamaged();
        if (currentHp <= 0)
            Die();
    }

    public void TakeDamage(float damage)
    {
        float accumulated = Mathf.Max(0f, damage) + fractionalDamageRemainder;
        int wholeDamage = Mathf.FloorToInt(accumulated);
        fractionalDamageRemainder = accumulated - wholeDamage;
        if (wholeDamage > 0)
            TakeDamage(wholeDamage);
    }

    public void StartSpawnApproach(float duration, float speed)
    {
        if (duration <= 0f || speed <= 0f)
            return;

        GameObject playerObject = GameObject.FindWithTag("Player");
        spawnApproachTarget = playerObject != null ? playerObject.transform : null;
        if (spawnApproachTarget == null)
            return;

        spawnApproachBody = GetComponent<Rigidbody2D>();
        spawnApproachSpeed = Mathf.Max(0f, speed);
        spawnApproachEndsAt = Time.time + Mathf.Max(0f, duration);
    }

    protected bool TryMoveSpawnApproach()
    {
        if (spawnApproachTarget == null || Time.time >= spawnApproachEndsAt || spawnApproachSpeed <= 0f)
            return false;

        Vector2 current = spawnApproachBody != null ? spawnApproachBody.position : (Vector2)transform.position;
        Vector2 direction = ((Vector2)spawnApproachTarget.position - current).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
            return true;

        Vector2 next = current + direction * spawnApproachSpeed * Time.fixedDeltaTime;
        if (spawnApproachBody != null)
            spawnApproachBody.MovePosition(next);
        else
            transform.position = new Vector3(next.x, next.y, transform.position.z);

        return true;
    }

    protected virtual void OnDamaged()
    {
        PlayHitFeedback();
    }

    protected virtual void PlayHitFeedback()
    {
        SoundManager.PlayEnemyHit();
        // 플레이어가 적을 때릴 때는 화면을 흔들지 않는다.
        // (화면 흔들림은 플레이어가 피격당할 때만 — PlayerDamageReceiver.ShakeCamera)

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null || !gameObject.activeInHierarchy)
            return;

        if (hitFeedbackRoutine != null)
        {
            StopCoroutine(hitFeedbackRoutine);
            spriteRenderer.color = spriteBaseColor;
        }

        spriteBaseColor = spriteRenderer.color;

        hitFeedbackRoutine = StartCoroutine(HitFeedbackRoutine());
    }

    protected GameObject TrackTelegraph(GameObject telegraph)
    {
        if (telegraph != null && !ownedTelegraphs.Contains(telegraph))
            ownedTelegraphs.Add(telegraph);

        return telegraph;
    }

    protected void DestroyOwnedTelegraph(GameObject telegraph)
    {
        if (telegraph == null)
            return;

        ownedTelegraphs.Remove(telegraph);
        Destroy(telegraph);
    }

    protected void ClearOwnedTelegraphs()
    {
        for (int i = ownedTelegraphs.Count - 1; i >= 0; i--)
            if (ownedTelegraphs[i] != null)
                Destroy(ownedTelegraphs[i]);

        ownedTelegraphs.Clear();
    }

    protected virtual void OnDestroy()
    {
        ClearOwnedTelegraphs();
    }

    IEnumerator HitFeedbackRoutine()
    {
        float duration = Mathf.Max(0.01f, Mathf.Max(hitFeedbackDuration, 0.22f));
        float elapsed = 0f;
        Color baseColor = spriteBaseColor;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        float prevWave = 0f;
        float shakeDist = Mathf.Max(hitShakeDistance, 0.55f);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float wave = Mathf.Sin(elapsed * 45f) * shakeDist * (1f - t);
            float delta = wave - prevWave;

            if (rb != null)
                rb.position = new Vector2(rb.position.x + delta, rb.position.y);
            else
                transform.position += new Vector3(delta, 0f, 0f);

            prevWave = wave;

            if (spriteRenderer != null)
                spriteRenderer.color = Color.Lerp(hitTint, baseColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rb != null)
            rb.position = new Vector2(rb.position.x - prevWave, rb.position.y);
        else
            transform.position -= new Vector3(prevWave, 0f, 0f);

        if (spriteRenderer != null)
            spriteRenderer.color = baseColor;
        hitFeedbackRoutine = null;
    }

    protected virtual void Die()
    {
        Color effectColor = spriteRenderer != null ? spriteRenderer.color : Color.gray;
        EnemyDeathEffect.Spawn(transform.position, effectColor);
        ItemInventoryManager.Instance?.NotifyEnemyKilled(transform.position);
        OnDied?.Invoke(this);
        EnemyManager.Instance?.OnEnemyDied(this);
        Destroy(gameObject);
    }
}
