using UnityEngine;

public enum BookPartType { Body, LeftArm, RightArm }

// One body part of the Book boss. Arms are directly attackable; the body is invulnerable to
// melee and only loses HP through the controller (when summoned minions are defeated).
public class BookBossPart : EnemyBase
{
    public BookPartType PartType { get; private set; }
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public bool Damageable { get; private set; }

    public System.Action<BookBossPart> Damaged;
    public System.Action<BookBossPart> Destroyed;

    SpriteRenderer partRenderer;
    Vector3 baseLocalPosition;

    protected override void Awake()
    {
        partRenderer = GetComponent<SpriteRenderer>();
    }

    protected override void Start()
    {
        // No EnemyManager profile; the boss controller owns all stats.
    }

    public override void ApplyProfile(EnemyProfile profile)
    {
    }

    protected override void Update()
    {
        // Static part; the controller animates bobbing explicitly.
    }

    public void Configure(BookPartType type, int hp, Sprite sprite, Vector2 visualCenter, float scale, int sortingOrder, bool isDamageable)
    {
        PartType = type;
        maxHp = Mathf.Max(1, hp);
        currentHp = maxHp;
        Damageable = isDamageable;

        if (partRenderer == null)
            partRenderer = GetComponent<SpriteRenderer>();
        if (partRenderer == null)
            partRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (sprite != null)
            partRenderer.sprite = sprite;
        partRenderer.color = Color.white;
        partRenderer.sortingOrder = sortingOrder;

        transform.localScale = new Vector3(scale, scale, 1f);

        Vector3 centerOffset = sprite != null ? (Vector3)(Vector2)sprite.bounds.center * scale : Vector3.zero;
        transform.position = new Vector3(visualCenter.x, visualCenter.y, 0f) - centerOffset;
        baseLocalPosition = transform.position;

        EnsureCollider(sprite);
        EnsurePartShadow();
    }

    // Scene-authored variant: preserve the hierarchy transform and collider so designers
    // can tune each book part directly in the Inspector.
    public void ConfigurePlaced(BookPartType type, int hp, Sprite sprite, int sortingOrder, bool isDamageable)
    {
        PartType = type;
        maxHp = Mathf.Max(1, hp);
        currentHp = maxHp;
        Damageable = isDamageable;

        if (partRenderer == null)
            partRenderer = GetComponent<SpriteRenderer>();
        if (partRenderer == null)
            partRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (sprite != null)
            partRenderer.sprite = sprite;
        partRenderer.color = Color.white;
        partRenderer.sortingOrder = sortingOrder;
        baseLocalPosition = transform.position;
        EnsureCollider(sprite);
        EnsurePartShadow();
    }

    // req: 최종보스도 플레이어와 동일한 실루엣 그림자. 파트(몸통/양팔)마다 붙여서 팔 움직임(bob)도 따라감.
    // BookBossPart는 Awake에서 base.Awake()를 호출하지 않아 EnsureCharacterShadow가 자동 실행되지 않으므로 여기서 붙인다.
    void EnsurePartShadow()
    {
        EnsureCharacterShadow();
        CharacterOvalShadow shadow = GetComponent<CharacterOvalShadow>();
        if (shadow != null)
            shadow.SetShadowRotation(-20f);   // req: 보스 그림자 각도 20°
    }

    public Vector2 BasePosition => baseLocalPosition;

    public void SetBobOffset(float yOffset)
    {
        transform.position = baseLocalPosition + new Vector3(0f, yOffset, 0f);
    }

    public override void TakeDamage(int damage)
    {
        if (!Damageable)
            return;

        base.TakeDamage(damage);
    }

    // Controller-driven HP loss for the invulnerable body (minion kills).
    public void ReduceHp(int amount)
    {
        int damage = Mathf.Max(0, amount);
        if (damage <= 0)
            return;

        currentHp = Mathf.Clamp(currentHp - damage, 0, maxHp);
        PlayHitFeedback();
    }

    protected override void OnDamaged()
    {
        base.OnDamaged();
        Damaged?.Invoke(this);
    }

    protected override void Die()
    {
        Destroyed?.Invoke(this);
        base.Die();
    }

    void EnsureCollider(Sprite sprite)
    {
        if (GetComponent<Collider2D>() != null)
            return;

        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        if (sprite != null)
        {
            Bounds bounds = sprite.bounds;
            collider.offset = bounds.center;
            collider.size = new Vector2(Mathf.Max(0.3f, bounds.size.x), Mathf.Max(0.3f, bounds.size.y));
        }
        else
        {
            collider.size = Vector2.one;
        }
    }
}
