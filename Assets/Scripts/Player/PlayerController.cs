using UnityEngine;
using UnityEngine.InputSystem;

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
        bool leftPressed = kb.aKey.isPressed;
        bool rightPressed = kb.dKey.isPressed;
        bool downPressed = kb.sKey.isPressed;
        bool upPressed = kb.wKey.isPressed;

        if (leftPressed) h -= 1f;
        if (rightPressed) h += 1f;
        if (downPressed) v -= 1f;
        if (upPressed) v += 1f;
        moveInput = new Vector2(h, v).normalized;

        bool changedDirection = false;
        if (kb.aKey.wasPressedThisFrame)
            changedDirection = SetFacing(FacingDirection.Left);
        if (kb.dKey.wasPressedThisFrame)
            changedDirection = SetFacing(FacingDirection.Right);
        if (kb.sKey.wasPressedThisFrame)
            changedDirection = SetFacing(FacingDirection.Down);
        if (kb.wKey.wasPressedThisFrame)
            changedDirection = SetFacing(FacingDirection.Up);

        if (!changedDirection && moveInput != Vector2.zero && !IsFacingInputStillPressed(leftPressed, rightPressed, downPressed, upPressed))
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

    bool IsFacingInputStillPressed(bool leftPressed, bool rightPressed, bool downPressed, bool upPressed)
    {
        return facingDirection switch
        {
            FacingDirection.Left => leftPressed,
            FacingDirection.Right => rightPressed,
            FacingDirection.Down => downPressed,
            FacingDirection.Up => upPressed,
            _ => false
        };
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
            upSprite = LoadFirstSprite("Sprites/Player/Player_up");
        if (downSprite == null)
            downSprite = LoadFirstSprite("Sprites/Player/Player_down");
        if (leftSprite == null)
            leftSprite = LoadFirstSprite("Sprites/Player/Player_left");
        if (rightSprite == null)
            rightSprite = LoadFirstSprite("Sprites/Player/Player_right");
    }

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
