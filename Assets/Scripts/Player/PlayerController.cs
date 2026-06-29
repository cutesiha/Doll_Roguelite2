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
    [SerializeField] SpriteRenderer leftArmRenderer;
    [SerializeField] SpriteRenderer rightArmRenderer;
    [SerializeField] Color missingEyeSocketColor = new Color(25f / 255f, 23f / 255f, 23f / 255f, 1f);
    [SerializeField] int playerSortingOrder = 120;
    [SerializeField] Sprite upSprite;
    [SerializeField] Sprite downSprite;
    [SerializeField] Sprite leftSprite;
    [SerializeField] Sprite rightSprite;
    [SerializeField] Sprite standingFrontLeftArmSprite;
    [SerializeField] Sprite standingFrontRightArmSprite;
    [SerializeField] Sprite standingBehindLeftArmSprite;
    [SerializeField] Sprite standingBehindRightArmSprite;
    [SerializeField] Sprite standingLeftFacingRightArmSprite;
    [SerializeField] Sprite standingRightFacingLeftArmSprite;
    [SerializeField] Sprite[] frontWalkBodyFrames;
    [SerializeField] Sprite[] frontWalkLeftArmFrames;
    [SerializeField] Sprite[] frontWalkRightArmFrames;
    [SerializeField] Sprite[] leftWalkBodyFrames;
    [SerializeField] Sprite[] leftWalkLeftArmFrames;
    [SerializeField] Sprite[] leftWalkRightArmFrames;
    [SerializeField] Sprite[] rightWalkBodyFrames;
    [SerializeField] Sprite[] rightWalkLeftArmFrames;
    [SerializeField] Sprite[] rightWalkRightArmFrames;
    [SerializeField] Sprite[] behindWalkBodyFrames;
    [SerializeField] Sprite[] behindWalkLeftArmFrames;
    [SerializeField] Sprite[] behindWalkRightArmFrames;
    [SerializeField, Min(1f)] float frontWalkFramesPerSecond = 8f;

    [Header("다리 없음 스프라이트")]
    // 양다리 없음: 방향별 3프레임 (꿈틀꿈틀 기어가기)
    [SerializeField] Sprite[] noLegUpFrames;
    [SerializeField] Sprite[] noLegDownFrames;
    [SerializeField] Sprite[] noLegLeftFrames;
    [SerializeField] Sprite[] noLegRightFrames;
    // 한쪽 다리 없음: 방향별 단일 프레임 (콩콩 점프)
    [SerializeField] Sprite noLeftLegUpSprite;
    [SerializeField] Sprite noLeftLegDownSprite;
    [SerializeField] Sprite noLeftLegLeftSprite;
    [SerializeField] Sprite noLeftLegRightSprite;
    [SerializeField] Sprite noRightLegUpSprite;
    [SerializeField] Sprite noRightLegDownSprite;
    [SerializeField] Sprite noRightLegLeftSprite;
    [SerializeField] Sprite noRightLegRightSprite;
    [SerializeField, Min(0f)] float hopHeight = 0.16f;
    [SerializeField, Min(0f)] float hopFrequency = 11f;
    [SerializeField, Min(1f)] float crawlFramesPerSecond = 6f;
    [SerializeField] Vector2 noLegArmOffset = new Vector2(0f, 0.1f);

    Rigidbody2D rb;
    Vector2 moveInput;
    bool forwardWalkPressed;
    float movementLockedUntil;
    float speedMultiplier = 1f;
    float speedMultiplierUntil;
    float itemMoveSpeedBonus;
    FacingDirection facingDirection = FacingDirection.Down;
    FacingDirection lastWalkDirection = FacingDirection.Down;
    float facingLockTimer;
    float walkAnimationTime;
    int lastWalkFrame = -1;
    SpriteRenderer leftEyeSocketRenderer;
    SpriteRenderer rightEyeSocketRenderer;
    static Sprite eyeSocketSprite;

    const float PlayerFrameWidth = 70f;
    const float PlayerFrameHeight = 110f;

    enum FacingDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public SpriteRenderer BodyRenderer => spriteRenderer;
    public SpriteRenderer LeftArmRenderer => leftArmRenderer;
    public SpriteRenderer RightArmRenderer => rightArmRenderer;

    public Vector2 FacingVector
    {
        get
        {
            return facingDirection switch
            {
                FacingDirection.Up => Vector2.up,
                FacingDirection.Left => Vector2.left,
                FacingDirection.Right => Vector2.right,
                _ => Vector2.down
            };
        }
    }

    public void ApplyPlayerManagerSettings(float newMoveSpeed, float newMissingLegSpeedMultiplier)
    {
        moveSpeed = Mathf.Max(0f, newMoveSpeed);
        missingLegSpeedMultiplier = Mathf.Clamp01(newMissingLegSpeedMultiplier);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            var mat = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
            col.sharedMaterial = mat;
            rb.sharedMaterial = mat;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        LoadDefaultSpritesIfMissing();
        BuildLayeredRenderers();
        ApplyPlayerSorting();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            ApplyFacingSprite();
        }
    }

    void Start()
    {
        PlayerManager.Instance?.ApplyTo(gameObject);
    }

    void LateUpdate()
    {
        ApplyPlayerSorting();
        ApplyMissingEyeSockets();
        ApplyLegHopOffset();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= ApplyEditorPreviewSprite;
            EditorApplication.delayCall += ApplyEditorPreviewSprite;
        }
    }

    void ApplyEditorPreviewSprite()
    {
        if (this == null)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        LoadDefaultSpritesIfMissing();

        if (spriteRenderer != null && spriteRenderer.sprite == null)
            spriteRenderer.sprite = downSprite;
    }
#endif

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        HandleConsumableHotkey(kb);

        if (Time.time < movementLockedUntil)
        {
            moveInput = Vector2.zero;
            forwardWalkPressed = false;
            ApplyFacingSprite();
            return;
        }

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
        forwardWalkPressed = downPressed && moveInput.y < -0.01f;

        facingLockTimer -= Time.deltaTime;

        if (moveInput != Vector2.zero && facingLockTimer <= 0f)
            SetFacingFromMoveInput();

        UpdateWalkAnimationTime();
        ApplyFacingSprite();
    }

    void FixedUpdate()
    {
        // 콩콩 비주얼 오프셋이 트랜스폼에 남아있으면 물리 시뮬레이션 동기화 전에 제거한다.
        // (Physics2D.autoSyncTransforms=false 환경: 트랜스폼→바디 동기화 때 hop이 새어들어가
        //  MovePosition과 충돌해 위로 드리프트하던 버그 방지)
        if (transform.position.x != rb.position.x || transform.position.y != rb.position.y)
            transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);

        float speed = Mathf.Max(0f, moveSpeed + itemMoveSpeedBonus);
        var state = BodyConditionUtility.CurrentState();
        if (state != null && (!state.legLeft || !state.legRight))
            speed *= missingLegSpeedMultiplier;

        if (Time.time < speedMultiplierUntil)
            speed *= speedMultiplier;

        rb.MovePosition(rb.position + moveInput * speed * Time.fixedDeltaTime);
    }

    // Temporary movement slow used by status effects such as the boss's stitch debuff.
    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        speedMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        speedMultiplierUntil = Time.time + Mathf.Max(0f, duration);
    }

    public void SetItemMoveSpeedBonus(float bonus)
    {
        itemMoveSpeedBonus = bonus;
    }

    // Q: 장착된 소모성 아이템(보석 등)을 발동한다. 일시정지(인벤토리/메뉴) 중에는 무시.
    void HandleConsumableHotkey(Keyboard kb)
    {
        if (!kb.qKey.wasPressedThisFrame || Time.timeScale <= 0f)
            return;

        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.TryUseEquippedConsumable();
    }

    public void FaceDirection(Vector2 direction)
    {
        FaceDirection(direction, 0f);
    }

    public void LockMovement(float duration)
    {
        movementLockedUntil = Mathf.Max(movementLockedUntil, Time.time + Mathf.Max(0f, duration));
        moveInput = Vector2.zero;
    }

    public void FaceDirection(Vector2 direction, float lockDuration)
    {
        if (direction == Vector2.zero)
            return;

        facingLockTimer = Mathf.Max(facingLockTimer, lockDuration);

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

        // 다리 상태 먼저 확인
        bool hasLeftLeg = BodyConditionUtility.HasPart(BodySlot.LegLeft);
        bool hasRightLeg = BodyConditionUtility.HasPart(BodySlot.LegRight);

        if (!hasLeftLeg && !hasRightLeg)
        {
            ApplyNoLegSprite();
            return;
        }
        if (!hasLeftLeg || !hasRightLeg)
        {
            ApplyOneLegSprite(missingLeft: !hasLeftLeg);
            return;
        }

        if (ShouldUseFrontWalkAnimation())
        {
            ApplyDirectionalWalkFrame(FacingDirection.Down, frontWalkBodyFrames, frontWalkLeftArmFrames, frontWalkRightArmFrames);
            return;
        }

        if (ShouldUseDirectionalWalkAnimation(FacingDirection.Left, leftWalkBodyFrames))
        {
            ApplyDirectionalWalkFrame(FacingDirection.Left, leftWalkBodyFrames, leftWalkRightArmFrames, leftWalkLeftArmFrames);
            return;
        }

        if (ShouldUseDirectionalWalkAnimation(FacingDirection.Right, rightWalkBodyFrames))
        {
            ApplyDirectionalWalkFrame(FacingDirection.Right, rightWalkBodyFrames, rightWalkRightArmFrames, rightWalkLeftArmFrames);
            return;
        }

        if (ShouldUseDirectionalWalkAnimation(FacingDirection.Up, behindWalkBodyFrames))
        {
            ApplyDirectionalWalkFrame(FacingDirection.Up, behindWalkBodyFrames, behindWalkLeftArmFrames, behindWalkRightArmFrames);
            return;
        }

        lastWalkFrame = -1;

        Sprite nextSprite = facingDirection switch
        {
            FacingDirection.Up => upSprite,
            FacingDirection.Left => leftSprite,
            FacingDirection.Right => rightSprite,
            _ => downSprite
        };

        if (nextSprite != null)
            spriteRenderer.sprite = nextSprite;

        ApplyStandingArmSprites(spriteRenderer.sprite);
    }

    void LoadDefaultSpritesIfMissing()
    {
        if (upSprite == null)
            upSprite = LoadPlayerStandingSprite("player_standing_behind");
        if (upSprite == null)
            upSprite = LoadPlayerSprite("behind", "Player_up");
        if (downSprite == null)
            downSprite = LoadPlayerStandingSprite("player_standing");
        if (downSprite == null)
            downSprite = LoadPlayerSprite("front", "Player_down");
        if (leftSprite == null)
            leftSprite = LoadPlayerStandingSprite("player_standing_left");
        if (leftSprite == null)
            leftSprite = LoadPlayerSprite("left", "Player_left");
        if (rightSprite == null)
            rightSprite = LoadPlayerStandingSprite("player_standing_right");
        if (rightSprite == null)
            rightSprite = LoadPlayerSprite("right", "Player_right");
        if (standingFrontLeftArmSprite == null)
            standingFrontLeftArmSprite = LoadPlayerStandingSprite("standing_leftarm");
        if (standingFrontRightArmSprite == null)
            standingFrontRightArmSprite = LoadPlayerStandingSprite("standing_rightarm");
        if (standingBehindLeftArmSprite == null)
            standingBehindLeftArmSprite = LoadPlayerStandingSprite("standing_leftarm_behind");
        if (standingBehindRightArmSprite == null)
            standingBehindRightArmSprite = LoadPlayerStandingSprite("standing_rightarm_behind");
        if (standingLeftFacingRightArmSprite == null)
            standingLeftFacingRightArmSprite = LoadPlayerStandingSprite("standing_rightarm_left");
        if (standingRightFacingLeftArmSprite == null)
            standingRightFacingLeftArmSprite = LoadPlayerStandingSprite("standing_leftarm_right");
        if (NeedsFrameReload(frontWalkBodyFrames, "front_walk_body"))
            frontWalkBodyFrames = LoadPlayerSprites("front_walk_body");
        if (NeedsFrameReload(frontWalkLeftArmFrames, "front_onlyleft"))
            frontWalkLeftArmFrames = LoadPlayerWalkSprites("front_onlyleft");
        if (NeedsFrameReload(frontWalkRightArmFrames, "front_onlyright"))
            frontWalkRightArmFrames = LoadPlayerWalkSprites("front_onlyright");
        if (NeedsFrameReload(leftWalkBodyFrames, "left_walk_body"))
            leftWalkBodyFrames = LoadPlayerSprites("left_walk_body");
        if (NeedsFrameReload(leftWalkLeftArmFrames, "left_onlyleft"))
            leftWalkLeftArmFrames = LoadPlayerWalkSprites("left_onlyleft1");
        if (NeedsFrameReload(leftWalkRightArmFrames, "left_onltright"))
            leftWalkRightArmFrames = LoadPlayerWalkSprites("left_onltright1");
        if (NeedsFrameReload(rightWalkBodyFrames, "right_walk_body"))
            rightWalkBodyFrames = LoadPlayerSprites("right_walk_body");
        if (NeedsFrameReload(rightWalkLeftArmFrames, "right_onlyright"))
            rightWalkLeftArmFrames = LoadPlayerWalkSprites("right_onlyright1");
        if (NeedsFrameReload(rightWalkRightArmFrames, "right_onlyleft"))
            rightWalkRightArmFrames = LoadPlayerWalkSprites("right_onlyleft1");
        if (NeedsFrameReload(behindWalkBodyFrames, "behind_walk_body"))
            behindWalkBodyFrames = LoadPlayerSprites("behind_walk_body");
        if (NeedsFrameReload(behindWalkLeftArmFrames, "behind_onlyleft"))
            behindWalkLeftArmFrames = LoadPlayerWalkSprites("behind_onlyleft");
        if (NeedsFrameReload(behindWalkRightArmFrames, "behind_onlyright"))
            behindWalkRightArmFrames = LoadPlayerWalkSprites("behind_onlyright");

        LoadNoLegSpritesIfMissing();
    }

    bool NeedsFrameReload(Sprite[] frames, string expectedPrefix)
    {
        return frames == null
            || frames.Length <= 1
            || frames.Any(sprite => sprite == null || !sprite.name.StartsWith(expectedPrefix));
    }

    Sprite LoadPlayerSprite(string spriteName, string fallbackName)
    {
        Sprite sprite = LoadFirstSprite("Sprites/Player/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = LoadEditorSprite("Assets/Sprites/Player/" + spriteName + ".png");
        if (sprite != null)
            return sprite;

        sprite = LoadEditorSprite("Assets/TextMesh Pro/Sprites/Player/" + spriteName + ".png");
        if (sprite != null)
            return sprite;
#endif

        return LoadFirstSprite("Sprites/Player/" + fallbackName);
    }

    void BuildLayeredRenderers()
    {
        if (spriteRenderer == null)
            return;

        leftArmRenderer = EnsureArmRenderer(leftArmRenderer, "PlayerArm_Left");
        rightArmRenderer = EnsureArmRenderer(rightArmRenderer, "PlayerArm_Right");
        leftEyeSocketRenderer = EnsureEyeSocketRenderer("PlayerEyeSocket_Left");
        rightEyeSocketRenderer = EnsureEyeSocketRenderer("PlayerEyeSocket_Right");
        SetArmRenderersVisible(false, false);
        SetRendererVisible(leftEyeSocketRenderer, false);
        SetRendererVisible(rightEyeSocketRenderer, false);
        ApplyPlayerSorting();
    }

    SpriteRenderer EnsureArmRenderer(SpriteRenderer renderer, string objectName)
    {
        if (renderer == null)
        {
            Transform existing = transform.Find(objectName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.color = Color.white;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        renderer.sharedMaterial = spriteRenderer.sharedMaterial;
        return renderer;
    }

    SpriteRenderer EnsureEyeSocketRenderer(string objectName)
    {
        Transform existing = transform.Find(objectName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<SpriteRenderer>();

        renderer.sprite = EyeSocketSprite();
        renderer.color = missingEyeSocketColor;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = playerSortingOrder + 2;
        renderer.sharedMaterial = spriteRenderer.sharedMaterial;
        return renderer;
    }

    static Sprite EyeSocketSprite()
    {
        if (eyeSocketSprite != null)
            return eyeSocketSprite;

        const int width = 8;
        const int height = 13;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "PlayerMissingEyeSocket",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / (width * 0.5f);
                float ny = (y - center.y) / (height * 0.5f);
                texture.SetPixel(x, y, nx * nx + ny * ny <= 1f ? Color.white : clear);
            }

        texture.Apply();
        eyeSocketSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        eyeSocketSprite.name = "PlayerMissingEyeSocket";
        return eyeSocketSprite;
    }

    void ApplyPlayerSorting()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sortingOrder = playerSortingOrder;

        if (leftArmRenderer != null)
        {
            leftArmRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            leftArmRenderer.sortingOrder = playerSortingOrder + 1;
        }

        if (rightArmRenderer != null)
        {
            rightArmRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            rightArmRenderer.sortingOrder = playerSortingOrder + 1;
        }

        ApplyEyeSocketSorting(leftEyeSocketRenderer);
        ApplyEyeSocketSorting(rightEyeSocketRenderer);
    }

    void ApplyEyeSocketSorting(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = playerSortingOrder + 2;
    }

    void ApplyMissingEyeSockets()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null
            || leftEyeSocketRenderer == null || rightEyeSocketRenderer == null)
            return;

        bool missingLeft = !BodyConditionUtility.HasPart(BodySlot.EyeLeft);
        bool missingRight = !BodyConditionUtility.HasPart(BodySlot.EyeRight);

        leftEyeSocketRenderer.color = missingEyeSocketColor;
        rightEyeSocketRenderer.color = missingEyeSocketColor;

        switch (facingDirection)
        {
            case FacingDirection.Up:
                SetRendererVisible(leftEyeSocketRenderer, false);
                SetRendererVisible(rightEyeSocketRenderer, false);
                break;

            case FacingDirection.Left:
                ConfigureEyeSocket(leftEyeSocketRenderer, missingLeft, new Vector2(24.5f, 39.5f), new Vector2(0.5f, 0.78f));
                SetRendererVisible(rightEyeSocketRenderer, false);
                break;

            case FacingDirection.Right:
                SetRendererVisible(leftEyeSocketRenderer, false);
                ConfigureEyeSocket(rightEyeSocketRenderer, missingRight, new Vector2(46.5f, 38f), new Vector2(0.5f, 1f));
                break;

            default:
                ConfigureEyeSocket(leftEyeSocketRenderer, missingLeft, new Vector2(24.5f, 38f), Vector2.one);
                ConfigureEyeSocket(rightEyeSocketRenderer, missingRight, new Vector2(45.5f, 38f), Vector2.one);
                break;
        }
    }

    void ConfigureEyeSocket(SpriteRenderer renderer, bool visible, Vector2 pixelCenterFromTop, Vector2 scale)
    {
        if (renderer == null)
            return;

        renderer.enabled = visible;
        if (!visible)
            return;

        Sprite bodySprite = spriteRenderer.sprite;
        float pixelsPerUnit = Mathf.Max(1f, bodySprite.pixelsPerUnit);
        float frameStartX = Mathf.Floor(bodySprite.rect.x / PlayerFrameWidth) * PlayerFrameWidth;
        float textureX = frameStartX + pixelCenterFromTop.x;
        float textureY = PlayerFrameHeight - 1f - pixelCenterFromTop.y;
        renderer.transform.localPosition = new Vector3(
            (textureX - bodySprite.rect.x - bodySprite.pivot.x) / pixelsPerUnit,
            (textureY - bodySprite.rect.y - bodySprite.pivot.y) / pixelsPerUnit,
            0f);
        renderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    void UpdateWalkAnimationTime()
    {
        if (IsUsingWalkAnimation() || IsHopping() || IsCrawling())
            walkAnimationTime += Time.deltaTime;
        else
            walkAnimationTime = 0f;
    }

    bool IsUsingWalkAnimation()
    {
        return ShouldUseFrontWalkAnimation()
            || ShouldUseDirectionalWalkAnimation(FacingDirection.Left, leftWalkBodyFrames)
            || ShouldUseDirectionalWalkAnimation(FacingDirection.Right, rightWalkBodyFrames)
            || ShouldUseDirectionalWalkAnimation(FacingDirection.Up, behindWalkBodyFrames);
    }

    bool ShouldUseFrontWalkAnimation()
    {
        return facingDirection == FacingDirection.Down
            && forwardWalkPressed
            && frontWalkBodyFrames != null
            && frontWalkBodyFrames.Length > 0;
    }

    bool ShouldUseDirectionalWalkAnimation(FacingDirection direction, Sprite[] bodyFrames)
    {
        return facingDirection == direction
            && moveInput != Vector2.zero
            && bodyFrames != null
            && bodyFrames.Length > 0;
    }

    void ApplyDirectionalWalkFrame(FacingDirection direction, Sprite[] bodyFrames, Sprite[] leftArmFrames, Sprite[] rightArmFrames)
    {
        int sequenceFrame = CurrentWalkSequenceFrame(bodyFrames.Length);
        if (sequenceFrame != lastWalkFrame || lastWalkDirection != direction)
        {
            int bodyFrame = WalkBodyFrameIndex(sequenceFrame, bodyFrames.Length);
            spriteRenderer.sprite = bodyFrames[bodyFrame];
            ApplyArmFrame(bodyFrames, leftArmFrames, bodyFrame, leftArmRenderer, bodyFrame, bodyFrame, bodyFrames.Length, BodySlot.ArmLeft);
            ApplyArmFrame(bodyFrames, rightArmFrames, bodyFrame, rightArmRenderer, bodyFrame, bodyFrame, bodyFrames.Length, BodySlot.ArmRight);
            lastWalkFrame = sequenceFrame;
            lastWalkDirection = direction;
        }
    }

    void ApplyStandingArmSprites(Sprite bodySprite)
    {
        switch (facingDirection)
        {
            case FacingDirection.Up:
                ApplyStandingArmFrame(leftArmRenderer, standingBehindLeftArmSprite, bodySprite, BodySlot.ArmLeft);
                ApplyStandingArmFrame(rightArmRenderer, standingBehindRightArmSprite, bodySprite, BodySlot.ArmRight);
                break;
            case FacingDirection.Left:
                SetRendererVisible(leftArmRenderer, false);
                ApplyStandingArmFrame(rightArmRenderer, standingLeftFacingRightArmSprite, bodySprite, BodySlot.ArmRight);
                break;
            case FacingDirection.Right:
                ApplyStandingArmFrame(leftArmRenderer, standingRightFacingLeftArmSprite, bodySprite, BodySlot.ArmLeft);
                SetRendererVisible(rightArmRenderer, false);
                break;
            default:
                ApplyStandingArmFrame(leftArmRenderer, standingFrontLeftArmSprite, bodySprite, BodySlot.ArmLeft);
                ApplyStandingArmFrame(rightArmRenderer, standingFrontRightArmSprite, bodySprite, BodySlot.ArmRight);
                break;
        }
    }

    void ApplyStandingArmFrame(SpriteRenderer renderer, Sprite armSprite, Sprite bodySprite, BodySlot slot)
    {
        if (renderer == null)
            return;

        if (armSprite == null || bodySprite == null || !BodyConditionUtility.HasPart(slot))
        {
            renderer.enabled = false;
            return;
        }

        renderer.sprite = armSprite;
        renderer.transform.localPosition = CalculatePartOffset(bodySprite, 0, 1, armSprite, 0, 1);
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        renderer.enabled = true;
    }

    int CurrentWalkSequenceFrame(int bodyFrameCount)
    {
        int sequenceLength = WalkSequenceLength(bodyFrameCount);
        return Mathf.FloorToInt(walkAnimationTime * frontWalkFramesPerSecond) % sequenceLength;
    }

    int WalkSequenceLength(int bodyFrameCount)
    {
        return Mathf.Max(1, bodyFrameCount * 2 - 2);
    }

    int WalkBodyFrameIndex(int sequenceFrame, int bodyFrameCount)
    {
        if (bodyFrameCount <= 1)
            return 0;

        int sequenceLength = WalkSequenceLength(bodyFrameCount);
        int frame = sequenceFrame % sequenceLength;
        return frame < bodyFrameCount ? frame : sequenceLength - frame;
    }

    void ApplyArmFrame(Sprite[] bodyFrames, Sprite[] armFrames, int bodyFrame, SpriteRenderer renderer, int armFrame, int partSheetFrame, int partSheetFrameCount, BodySlot slot)
    {
        if (renderer == null)
            return;

        if (armFrames == null || armFrames.Length == 0)
        {
            renderer.enabled = false;
            return;
        }

        bool visible = BodyConditionUtility.HasPart(slot);
        Sprite bodySprite = bodyFrames[bodyFrame];
        Sprite armSprite = armFrames[Mathf.Min(armFrame, armFrames.Length - 1)];

        renderer.sprite = armSprite;
        renderer.transform.localPosition = CalculatePartOffset(bodySprite, bodyFrame, bodyFrames.Length, armSprite, partSheetFrame, partSheetFrameCount);
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        renderer.enabled = visible && armSprite != null;
    }

    Vector3 CalculatePartOffset(Sprite bodySprite, int bodyFrame, int bodyFrameCount, Sprite partSprite, int partFrame, int partFrameCount)
    {
        if (bodySprite == null || partSprite == null || bodySprite.texture == null)
            return Vector3.zero;

        float pixelsPerUnit = bodySprite.pixelsPerUnit;
        if (pixelsPerUnit <= 0f)
            pixelsPerUnit = 100f;

        float bodyFrameWidth = bodySprite.texture.width / Mathf.Max(1f, bodyFrameCount);
        float partFrameWidth = partSprite.texture.width / Mathf.Max(1f, partFrameCount);
        Vector2 bodyPivot = SpritePivotInFrame(bodySprite, bodyFrame, bodyFrameWidth);
        Vector2 partPivot = SpritePivotInFrame(partSprite, partFrame, partFrameWidth);
        Vector2 offset = (partPivot - bodyPivot) / pixelsPerUnit;
        return new Vector3(offset.x, offset.y, 0f);
    }

    Vector2 SpritePivotInFrame(Sprite sprite, int frame, float frameWidth)
    {
        Rect rect = sprite.rect;
        return new Vector2(rect.x - frame * frameWidth + sprite.pivot.x, rect.y + sprite.pivot.y);
    }

    void SetArmRenderersVisible(bool leftVisible, bool rightVisible)
    {
        SetRendererVisible(leftArmRenderer, leftVisible);
        SetRendererVisible(rightArmRenderer, rightVisible);
    }

    void SetRendererVisible(SpriteRenderer renderer, bool visible)
    {
        if (renderer != null)
            renderer.enabled = visible;
    }

    public Sprite GetShadowSourceSprite()
    {
        LoadDefaultSpritesIfMissing();

        return facingDirection switch
        {
            FacingDirection.Up => upSprite,
            FacingDirection.Left => leftSprite,
            FacingDirection.Right => rightSprite,
            _ => downSprite
        };
    }

    Sprite[] LoadPlayerSprites(string spriteName)
    {
        Sprite[] sprites = SortSprites(Resources.LoadAll<Sprite>("Sprites/Player/" + spriteName));
        if (sprites.Length > 0)
            return sprites;

        Sprite sprite = Resources.Load<Sprite>("Sprites/Player/" + spriteName);
        if (sprite != null)
            return new[] { sprite };

#if UNITY_EDITOR
        sprites = LoadEditorSprites("Assets/Sprites/Player/" + spriteName + ".png");
        if (sprites.Length > 0)
            return sprites;
#endif

        return new Sprite[0];
    }

    Sprite[] LoadPlayerWalkSprites(string spriteName)
    {
        Sprite[] sprites = SortSprites(Resources.LoadAll<Sprite>("Sprites/playerwalk/" + spriteName));
        if (sprites.Length > 0)
            return sprites;

        Sprite sprite = Resources.Load<Sprite>("Sprites/playerwalk/" + spriteName);
        if (sprite != null)
            return new[] { sprite };

#if UNITY_EDITOR
        sprites = LoadEditorSprites("Assets/Sprites/playerwalk/" + spriteName + ".png");
        if (sprites.Length > 0)
            return sprites;
#endif

        return new Sprite[0];
    }

    Sprite LoadPlayerStandingSprite(string spriteName)
    {
        Sprite sprite = LoadFirstSprite("Sprites/Playerstanding/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        return LoadEditorSprite("Assets/Sprites/Playerstanding/" + spriteName + ".png");
#else
        return null;
#endif
    }

    // ── 다리 없음 스프라이트 ──────────────────────────────────────────────

    void LoadNoLegSpritesIfMissing()
    {
        // 양다리 없음 (3프레임)
        if (NeedsNoLegFrameReload(noLegUpFrames))    noLegUpFrames    = LoadNoLegFrames("noleg_behind.png");
        if (NeedsNoLegFrameReload(noLegDownFrames))  noLegDownFrames  = LoadNoLegFrames("noleg_front.png");
        if (NeedsNoLegFrameReload(noLegLeftFrames))  noLegLeftFrames  = LoadNoLegFrames("noleg_left.png");
        if (NeedsNoLegFrameReload(noLegRightFrames)) noLegRightFrames = LoadNoLegFrames("noleg_right2.png");

        // 한쪽 다리 없음 (단일 프레임)
        if (noLeftLegUpSprite == null)    noLeftLegUpSprite    = LoadNoLegSprite("noleftlef_behind.png");
        if (noLeftLegDownSprite == null)  noLeftLegDownSprite  = LoadNoLegSprite("noleft_front.png");
        if (noLeftLegLeftSprite == null)  noLeftLegLeftSprite  = LoadNoLegSprite("noleftleg_left.png");
        if (noLeftLegRightSprite == null) noLeftLegRightSprite = LoadNoLegSprite("noleftleg_right.png");

        if (noRightLegUpSprite == null)    noRightLegUpSprite    = LoadNoLegSprite("norightleg_behind.png");
        if (noRightLegDownSprite == null)  noRightLegDownSprite  = LoadNoLegSprite("norightleg_front.png");
        if (noRightLegLeftSprite == null)  noRightLegLeftSprite  = LoadNoLegSprite("norightleg_left.png");
        if (noRightLegRightSprite == null) noRightLegRightSprite = LoadNoLegSprite("norightleg_right.png");
    }

    bool NeedsNoLegFrameReload(Sprite[] frames)
    {
        return frames == null || frames.Length == 0 || frames.Any(s => s == null);
    }

    Sprite LoadNoLegSprite(string filename)
    {
        string nameOnly = System.IO.Path.GetFileNameWithoutExtension(filename);
        Sprite s = LoadFirstSprite("Sprites/noleg/" + nameOnly);
        if (s != null) return s;
#if UNITY_EDITOR
        return LoadEditorSprite("Assets/Sprites/noleg/" + filename);
#else
        return null;
#endif
    }

    Sprite[] LoadNoLegFrames(string filename)
    {
        string nameOnly = System.IO.Path.GetFileNameWithoutExtension(filename);
        Sprite[] s = SortSprites(Resources.LoadAll<Sprite>("Sprites/noleg/" + nameOnly));
        if (s.Length > 0) return s;
#if UNITY_EDITOR
        s = LoadEditorSprites("Assets/Sprites/noleg/" + filename);
        if (s.Length > 0) return s;
#endif
        return new Sprite[0];
    }

    // 다리 두 개 없음 → noleg 3프레임으로 꿈틀꿈틀 기어가기 (이동 중에만 애니메이션)
    void ApplyNoLegSprite()
    {
        Sprite[] frames = facingDirection switch
        {
            FacingDirection.Up    => noLegUpFrames,
            FacingDirection.Left  => noLegLeftFrames,
            FacingDirection.Right => noLegRightFrames,
            _                     => noLegDownFrames
        };

        if (frames != null && frames.Length > 0)
        {
            int idx = 0;
            if (moveInput.sqrMagnitude > 0.001f && frames.Length > 1)
            {
                // 0→1→2→1 핑퐁으로 꿈틀거림
                int seqLen = Mathf.Max(1, frames.Length * 2 - 2);
                int s = Mathf.FloorToInt(walkAnimationTime * crawlFramesPerSecond) % seqLen;
                idx = s < frames.Length ? s : seqLen - s;
            }
            idx = Mathf.Clamp(idx, 0, frames.Length - 1);
            if (frames[idx] != null) spriteRenderer.sprite = frames[idx];
        }

        ApplyStandingArmSprites(spriteRenderer.sprite);
        ApplyNoLegArmOffset();
        lastWalkFrame = -1;
    }

    // 양다리 없는 몸통은 키가 달라서 팔이 어긋난다. 보정 오프셋을 더해 위치를 맞춘다.
    void ApplyNoLegArmOffset()
    {
        if (noLegArmOffset == Vector2.zero)
            return;
        if (leftArmRenderer != null && leftArmRenderer.enabled)
            leftArmRenderer.transform.localPosition += (Vector3)noLegArmOffset;
        if (rightArmRenderer != null && rightArmRenderer.enabled)
            rightArmRenderer.transform.localPosition += (Vector3)noLegArmOffset;
    }

    // 한쪽 다리 없음 → noleftleg/norightleg 스프라이트 (팔은 그대로)
    void ApplyOneLegSprite(bool missingLeft)
    {
        Sprite s;
        if (missingLeft)
        {
            s = facingDirection switch
            {
                FacingDirection.Up    => noLeftLegUpSprite,
                FacingDirection.Left  => noLeftLegLeftSprite,
                FacingDirection.Right => noLeftLegRightSprite,
                _                     => noLeftLegDownSprite
            };
        }
        else
        {
            s = facingDirection switch
            {
                FacingDirection.Up    => noRightLegUpSprite,
                FacingDirection.Left  => noRightLegLeftSprite,
                FacingDirection.Right => noRightLegRightSprite,
                _                     => noRightLegDownSprite
            };
        }
        if (s != null) spriteRenderer.sprite = s;
        ApplyStandingArmSprites(spriteRenderer.sprite);
        lastWalkFrame = -1;
    }

    // 이동 중 한쪽 다리만 없으면 true (콩콩 점프 대상)
    bool IsHopping()
    {
        bool hasLeft  = BodyConditionUtility.HasPart(BodySlot.LegLeft);
        bool hasRight = BodyConditionUtility.HasPart(BodySlot.LegRight);
        return (hasLeft != hasRight) && moveInput.sqrMagnitude > 0.001f;
    }

    // 이동 중 양다리 모두 없으면 true (기어가기 대상)
    bool IsCrawling()
    {
        return !BodyConditionUtility.HasPart(BodySlot.LegLeft)
            && !BodyConditionUtility.HasPart(BodySlot.LegRight)
            && moveInput.sqrMagnitude > 0.001f;
    }

    // 한다리 콩콩 효과: 물리(rb)는 절대 건드리지 않고 렌더(transform)만 Y로 통통 튀게 한다.
    // hop은 트랜스폼에만 얹고, 다음 FixedUpdate가 물리 동기화 전에 다시 제거한다.
    void ApplyLegHopOffset()
    {
        if (!IsHopping())
        {
            // 콩콩이 끝났는데 오프셋이 남아있으면 지면(물리 위치)으로 복귀
            if (transform.position.x != rb.position.x || transform.position.y != rb.position.y)
                transform.position = new Vector3(rb.position.x, rb.position.y, transform.position.z);
            return;
        }

        float hop = Mathf.Abs(Mathf.Sin(walkAnimationTime * hopFrequency)) * hopHeight;
        transform.position = new Vector3(rb.position.x, rb.position.y + hop, transform.position.z);
    }

    Sprite[] SortSprites(Sprite[] sprites)
    {
        return sprites.OrderBy(sprite => sprite.rect.x)
            .ThenBy(sprite => sprite.rect.y)
            .ThenBy(sprite => sprite.name)
            .ToArray();
    }

#if UNITY_EDITOR
    Sprite LoadEditorSprite(string assetPath)
    {
        Sprite[] sprites = LoadEditorSprites(assetPath);
        if (sprites.Length > 0)
            return sprites[0];

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    Sprite[] LoadEditorSprites(string assetPath)
    {
        return SortSprites(AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray());
    }
#endif

    Sprite LoadFirstSprite(string resourcePath)
    {
        Sprite[] sprites = SortSprites(Resources.LoadAll<Sprite>(resourcePath));
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
