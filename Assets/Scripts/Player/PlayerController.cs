using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField, Range(0f, 1f)] float missingLegSpeedMultiplier = 0.5f;
    [SerializeField] Color bodyColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Sprite upSprite;
    [SerializeField] Sprite downSprite;
    [SerializeField] Sprite leftSprite;
    [SerializeField] Sprite rightSprite;

    Rigidbody2D rb;
    Vector2 moveInput;
    FacingDirection facingDirection = FacingDirection.Down;

    enum FacingDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        LoadDefaultSpritesIfMissing();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            ApplyFacingSprite();
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        bool leftPressed = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
        bool rightPressed = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
        bool downPressed = kb.sKey.isPressed || kb.downArrowKey.isPressed;
        bool upPressed = kb.wKey.isPressed || kb.upArrowKey.isPressed;

        if (leftPressed) h -= 1f;
        if (rightPressed) h += 1f;
        if (downPressed) v -= 1f;
        if (upPressed) v += 1f;
        moveInput = new Vector2(h, v).normalized;

        if (moveInput != Vector2.zero)
            SetFacingFromMoveInput();
    }

    void FixedUpdate()
    {
        float speed = moveSpeed;
        var state = BodyManager.Instance != null ? BodyManager.Instance.State : null;
        if (state != null && (!state.legLeft || !state.legRight))
            speed *= missingLegSpeedMultiplier;

        rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);
    }

    public void FaceDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            SetFacing(direction.x < 0f ? FacingDirection.Left : FacingDirection.Right);
        else
            SetFacing(direction.y < 0f ? FacingDirection.Down : FacingDirection.Up);
    }

    bool SetFacing(FacingDirection direction)
    {
        if (facingDirection == direction)
            return false;

        facingDirection = direction;
        ApplyFacingSprite();
        return true;
    }

    void SetFacingFromMoveInput()
    {
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            SetFacing(moveInput.x < 0f ? FacingDirection.Left : FacingDirection.Right);
        else
            SetFacing(moveInput.y < 0f ? FacingDirection.Down : FacingDirection.Up);
    }

    void ApplyFacingSprite()
    {
        if (spriteRenderer == null)
            return;

        Sprite nextSprite = facingDirection switch
        {
            FacingDirection.Up => upSprite,
            FacingDirection.Left => leftSprite,
            FacingDirection.Right => rightSprite,
            _ => downSprite
        };

        if (nextSprite != null)
            spriteRenderer.sprite = nextSprite;
    }

    void LoadDefaultSpritesIfMissing()
    {
        if (upSprite == null)
            upSprite = LoadPlayerSprite("3333333333333", "Player_up");
        if (downSprite == null)
            downSprite = LoadPlayerSprite("1111111", "Player_down");
        if (leftSprite == null)
            leftSprite = LoadPlayerSprite("2222222", "Player_left");
        if (rightSprite == null)
            rightSprite = LoadPlayerSprite("4444444444", "Player_right");
    }

    Sprite LoadPlayerSprite(string spriteName, string fallbackName)
    {
        Sprite sprite = LoadFirstSprite("Sprites/Player/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = LoadEditorSprite("Assets/TextMesh Pro/Sprites/Player/" + spriteName + ".png");
        if (sprite != null)
            return sprite;
#endif

        return LoadFirstSprite("Sprites/Player/" + fallbackName);
    }

#if UNITY_EDITOR
    Sprite LoadEditorSprite(string assetPath)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
        if (sprites.Length > 0)
            return sprites[0];

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
#endif

    Sprite LoadFirstSprite(string resourcePath)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites.Length > 0)
            return sprites[0];

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
    }
}
