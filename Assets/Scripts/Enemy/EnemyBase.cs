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
    protected int currentHp;

    SpriteRenderer spriteRenderer;
    Sprite[] animationFrames;
    float animationTime;
    int currentFrame = -1;

    protected virtual void Awake()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            animationFrames = LoadRandomEnemyFrames();
            ApplyAnimationFrame(0);
            spriteRenderer.color = Color.white;
            return;
        }

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", bodyColor);
            rend.material = mat;
        }
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

    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        OnDamaged();
        if (currentHp <= 0)
            Die();
    }

    protected virtual void OnDamaged() { }

    protected virtual void Die()
    {
        EnemyManager.Instance?.OnEnemyDied(this);
        Destroy(gameObject);
    }
}
