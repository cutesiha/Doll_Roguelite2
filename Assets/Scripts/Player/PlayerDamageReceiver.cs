using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour
{
    public System.Action Damaged;

    [Header("Contact Damage")]
    [SerializeField, Min(1)] int contactDamage = 1;
    [SerializeField, Min(1)] int maxDamagePerHit = 1;   // 한 대당 깎이는 최대 HP(칸). 공격 종류 무관 캡.
    [SerializeField, Min(0.05f)] float damageCooldown = 0.65f;
    [SerializeField] LayerMask enemyLayers = ~0;

    [Header("Body HP")]
    [SerializeField, Min(1)] int bodyMaxHp = 100;
    [SerializeField, Min(0)] int bodyCurrentHp = 100;

    [Header("Hit Feedback")]
    [SerializeField, Range(0.05f, 0.45f)] float hitFeedbackDuration = 0.16f;
    [SerializeField, Min(0f)] float hitShakeDistance = 0.055f;
    [SerializeField, Min(1f)] float hitScaleMultiplier = 1.08f;
    [SerializeField] Color hitTint = new Color(1f, 0.42f, 0.38f, 1f);

    [Header("Camera Shake")]
    [SerializeField] bool shakeCameraOnHit = true;
    [SerializeField, Range(0.02f, 0.35f)] float cameraShakeDuration = 0.12f;
    [SerializeField, Min(0f)] float cameraShakeMagnitude = 0.11f;

    [Header("Dropped Parts")]
    [SerializeField, Min(0f)] float dropDownDistance = 0.52f;
    [SerializeField, Min(0f)] float dropSideDistance = 0.16f;
    [SerializeField] int droppedPartSortingOrder = 4;
    [Header("Dropped Part Sizes (world units)")]
    [Tooltip("Fallback on-screen size of a dropped part when no size anchor is assigned below. The part icons share one canvas, so each slot needs its own size to keep proportions believable (eyes small, limbs larger).")]
    [SerializeField, Min(0.05f)] float eyeDropSize = 0.55f;
    [SerializeField, Min(0.05f)] float armDropSize = 1.35f;
    [SerializeField, Min(0.05f)] float legDropSize = 1.5f;
    [Header("Dropped Part Size Anchors (optional)")]
    [Tooltip("Scale these child objects in the Hierarchy/Scene to set drop sizes visually. The dropped part matches the anchor's on-screen size. Hidden automatically during play. If empty, the world-unit sizes above are used.")]
    [SerializeField] Transform eyeDropSizeAnchor;
    [SerializeField] Transform armDropSizeAnchor;
    [SerializeField] Transform legDropSizeAnchor;

    static readonly BodySlot[] DamageSlots =
    {
        BodySlot.ArmLeft,
        BodySlot.ArmRight,
        BodySlot.LegLeft,
        BodySlot.LegRight,
        BodySlot.EyeLeft,
        BodySlot.EyeRight
    };

    readonly Dictionary<SpriteRenderer, Color> rendererColors = new Dictionary<SpriteRenderer, Color>();
    readonly List<BodySlot> damageCandidates = new List<BodySlot>();

    PlayerController playerController;
    SpriteRenderer bodyRenderer;
    Collider2D playerCollider;
    Coroutine feedbackRoutine;
    Coroutine invincibilityRoutine;
    float nextDamageTime;
    float invincibleUntil;
    bool bodyDropped;
    bool isDead;

    void Awake()
    {
        ResolveReferences();
        EnsurePlayerCollider();
        HideDropSizeAnchors();
        bodyCurrentHp = Mathf.Clamp(bodyCurrentHp, 0, bodyMaxHp);
    }

    // Size anchors are visual tuning guides shown only in the editor; hide their
    // renderers at runtime so they never appear in-game.
    void HideDropSizeAnchors()
    {
        HideAnchorRenderer(eyeDropSizeAnchor);
        HideAnchorRenderer(armDropSizeAnchor);
        HideAnchorRenderer(legDropSizeAnchor);
    }

    void HideAnchorRenderer(Transform anchor)
    {
        if (anchor == null)
            return;

        SpriteRenderer renderer = anchor.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    void OnValidate()
    {
        bodyMaxHp = Mathf.Max(1, bodyMaxHp);
        bodyCurrentHp = Mathf.Clamp(bodyCurrentHp, 0, bodyMaxHp);
        contactDamage = Mathf.Max(1, contactDamage);
        damageCooldown = Mathf.Max(0.05f, damageCooldown);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
            TryTakeHit(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision != null)
            TryTakeHit(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryTakeHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryTakeHit(other);
    }

    // 무적 보석 등으로 일정 시간 모든 피해를 무시한다.
    public void SetInvincible(float duration)
    {
        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + Mathf.Max(0f, duration));
    }

    public bool IsInvincible => Time.time < invincibleUntil;

    public void TryTakeHit(Collider2D enemyCollider)
    {
        if (isDead || enemyCollider == null || Time.time < nextDamageTime || IsInvincible)
            return;

        if (((1 << enemyCollider.gameObject.layer) & enemyLayers.value) == 0)
            return;

        EnemyBase enemy = enemyCollider.GetComponentInParent<EnemyBase>();
        if (enemy == null)
            return;

        nextDamageTime = Time.time + damageCooldown;
        if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.TryBlockHit())
            return;

        DamageNextBodyTarget(contactDamage);
        SetInvincible(1.5f);
        PlayHitFeedback();
        StartInvincibilityBlink();
        ShakeCamera();
        SoundManager.PlayPlayerHit();
        Damaged?.Invoke();
    }

    public bool TryTakePatternDamage(int damage, float cooldownOverride = -1f)
    {
        if (isDead || Time.time < nextDamageTime || IsInvincible)
            return false;

        int finalDamage = Mathf.Max(1, damage);
        float cooldown = cooldownOverride >= 0f ? cooldownOverride : damageCooldown;
        nextDamageTime = Time.time + Mathf.Max(0.01f, cooldown);
        if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.TryBlockHit())
            return false;

        DamageNextBodyTarget(finalDamage);
        SetInvincible(1.5f);
        PlayHitFeedback();
        StartInvincibilityBlink();
        ShakeCamera();
        SoundManager.PlayPlayerHit();
        Damaged?.Invoke();
        return true;
    }

    public void LockMovement(float duration)
    {
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.LockMovement(duration);
    }

    void DamageNextBodyTarget(int damage)
    {
        damage = Mathf.Clamp(damage, 1, maxDamagePerHit);

        InventoryManager inventory = InventoryManager.Instance;
        damageCandidates.Clear();
        if (inventory != null)
            for (int i = 0; i < DamageSlots.Length; i++)
                if (inventory.GetEquippedPart(DamageSlots[i]) != null)
                    damageCandidates.Add(DamageSlots[i]);

        if (damageCandidates.Count == 0)
        {
            TriggerDeath();
            return;
        }

        int pick = Random.Range(0, damageCandidates.Count);
        BodySlot slot = damageCandidates[pick];
        Sprite dropSprite = SpriteForSlot(slot);
        SpriteRenderer sourceRenderer = SourceRendererForSlot(slot);
        BodyPart brokenPart;
        if (inventory.TryDamageEquippedPart(slot, damage, out brokenPart) && brokenPart != null)
            DropPart(dropSprite, sourceRenderer, slot);
    }

    void TriggerDeath()
    {
        if (isDead)
            return;

        isDead = true;
        DisablePlayerAfterDeath();
        StartCoroutine(LoadStartSceneRoutine());
    }

    IEnumerator LoadStartSceneRoutine()
    {
        yield return new WaitForSeconds(0.8f);
        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            Destroy(hud.gameObject);
        SceneManager.LoadScene("StartScene");
    }

    void DisablePlayerAfterDeath()
    {
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;

        PlayerAttack attack = GetComponent<PlayerAttack>();
        if (attack != null)
            attack.enabled = false;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = false;
    }

    void ShakeCamera()
    {
        if (shakeCameraOnHit)
            CameraShake.ShakeHorizontal(
                Mathf.Max(cameraShakeDuration, 0.20f),
                Mathf.Max(cameraShakeMagnitude, 0.32f));
    }

    void StartInvincibilityBlink()
    {
        if (invincibilityRoutine != null)
            StopCoroutine(invincibilityRoutine);
        if (gameObject.activeInHierarchy)
            invincibilityRoutine = StartCoroutine(InvincibilityBlinkRoutine());
    }

    IEnumerator InvincibilityBlinkRoutine()
    {
        const float totalDuration = 1.5f;
        const float blinkInterval = 0.10f;
        float elapsed = 0f;

        // Wait for red hit feedback to finish before blinking white
        while (feedbackRoutine != null)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= totalDuration) yield break;
            yield return null;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        System.Collections.Generic.Dictionary<SpriteRenderer, Color> baseColors
            = new System.Collections.Generic.Dictionary<SpriteRenderer, Color>();
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                baseColors[renderers[i]] = renderers[i].color;

        while (elapsed < totalDuration)
        {
            bool bright = (Mathf.FloorToInt(elapsed / blinkInterval) % 2 == 0);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer r = renderers[i];
                if (r == null) continue;
                Color c;
                if (!baseColors.TryGetValue(r, out c)) c = r.color;
                c = bright
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(c.r, c.g, c.b, 0.35f);
                r.color = c;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer r = renderers[i];
            if (r == null) continue;
            Color c;
            if (baseColors.TryGetValue(r, out c))
                r.color = c;
        }

        invincibilityRoutine = null;
    }

    void PlayHitFeedback()
    {
        if (!gameObject.activeInHierarchy)
            return;

        ScreenFlash.FlashRed();

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(HitFeedbackRoutine());
    }

    IEnumerator HitFeedbackRoutine()
    {
        ResolveReferences();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        rendererColors.Clear();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            rendererColors[renderers[i]] = renderers[i].color;
            renderers[i].color = hitTint;
        }

        float duration = Mathf.Max(0.01f, hitFeedbackDuration);
        float elapsed = 0f;
        Vector3 appliedOffset = Vector3.zero;
        Vector3 baseScale = transform.localScale;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            transform.position -= appliedOffset;
            appliedOffset = (Vector3)(Random.insideUnitCircle * hitShakeDistance * (1f - t));
            transform.position += appliedOffset;
            transform.localScale = Vector3.Lerp(baseScale, baseScale * hitScaleMultiplier, pulse);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !rendererColors.TryGetValue(renderer, out Color baseColor))
                    continue;

                renderer.color = Color.Lerp(hitTint, baseColor, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position -= appliedOffset;
        transform.localScale = baseScale;
        foreach (KeyValuePair<SpriteRenderer, Color> pair in rendererColors)
            if (pair.Key != null)
                pair.Key.color = pair.Value;

        rendererColors.Clear();
        feedbackRoutine = null;
    }

    void DropPart(Sprite sprite, SpriteRenderer sourceRenderer, BodySlot? slot)
    {
        if (sprite == null)
            return;

        ResolveReferences();
        SpriteRenderer sortingSource = sourceRenderer != null ? sourceRenderer : bodyRenderer;
        int sortingLayerId = sortingSource != null ? sortingSource.sortingLayerID : 0;
        Vector3 worldScale = DroppedPartWorldScale(sprite, slot);
        Vector3 start = sortingSource != null ? sortingSource.bounds.center : transform.position;
        Vector3 end = transform.position + new Vector3(Random.Range(-dropSideDistance, dropSideDistance), -dropDownDistance, 0f);
        end.z = start.z;

        GameObject dropped = new GameObject("Dropped_" + sprite.name);
        DroppedBodyPart droppedPart = dropped.AddComponent<DroppedBodyPart>();
        droppedPart.Configure(sprite, sortingLayerId, droppedPartSortingOrder, start, end, worldScale);
    }

    Vector3 DroppedPartWorldScale(Sprite sprite, BodySlot? slot)
    {
        // The whole body falls at its real on-screen size.
        if (!slot.HasValue || sprite == null)
            return bodyRenderer != null ? bodyRenderer.transform.lossyScale : transform.lossyScale;

        // If a size anchor is assigned, match the dropped part to the anchor's
        // current on-screen size so designers can tune sizes by scaling the anchor
        // object directly in the Hierarchy/Scene (WYSIWYG).
        Transform anchor = AnchorForSlot(slot.Value);
        if (anchor != null)
            return AnchorWorldScale(anchor, sprite);

        // Otherwise fall back to the per-slot world size. The part icons share one
        // 200x200 canvas where each part fills a different fraction, so normalising
        // by sprite bounds keeps the dropped pieces proportional to one another.
        float largest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float uniform = largest > 0.0001f ? SizeForSlot(slot.Value) / largest : 1f;
        return new Vector3(uniform, uniform, 1f);
    }

    Vector3 AnchorWorldScale(Transform anchor, Sprite sprite)
    {
        // Prefer matching the anchor's rendered size so the result looks exactly
        // like the (editor-only) preview, even if the anchor uses a different sprite.
        SpriteRenderer anchorRenderer = anchor.GetComponent<SpriteRenderer>();
        if (anchorRenderer != null && anchorRenderer.sprite != null && sprite != null)
        {
            float anchorWorld = Mathf.Max(anchorRenderer.bounds.size.x, anchorRenderer.bounds.size.y);
            float spriteLargest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            float uniform = spriteLargest > 0.0001f ? anchorWorld / spriteLargest : 1f;
            return new Vector3(uniform, uniform, 1f);
        }

        // No renderer: use the anchor's world scale directly.
        Vector3 lossy = anchor.lossyScale;
        float scale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y)));
        return new Vector3(scale, scale, 1f);
    }

    Transform AnchorForSlot(BodySlot slot)
    {
        switch (slot)
        {
            case BodySlot.EyeLeft:
            case BodySlot.EyeRight:
                return eyeDropSizeAnchor;
            case BodySlot.ArmLeft:
            case BodySlot.ArmRight:
                return armDropSizeAnchor;
            default:
                return legDropSizeAnchor;
        }
    }

    float SizeForSlot(BodySlot slot)
    {
        switch (slot)
        {
            case BodySlot.EyeLeft:
            case BodySlot.EyeRight:
                return eyeDropSize;
            case BodySlot.ArmLeft:
            case BodySlot.ArmRight:
                return armDropSize;
            default:
                return legDropSize;
        }
    }

    // Drop visuals come from the PlayerUI art set (body-aligned part images),
    // indexed by BodySlot order: EyeLeft, EyeRight, ArmLeft, ArmRight, LegLeft, LegRight.
    static readonly string[] PlayerUiPartNames =
    {
        "eye_L",
        "eye_R",
        "정면_왼쪽팔",
        "정면_오른팔",
        "정면_왼다리",
        "정면_오른다리"
    };

    Sprite SpriteForSlot(BodySlot slot)
    {
        Sprite uiSprite = LoadPlayerUiSprite(slot);
        if (uiSprite != null)
            return uiSprite;

        // Fallback to the inventory/character art if a PlayerUI image is missing.
        return InventoryUI.FindDisplaySpriteForSlot(slot);
    }

    Sprite LoadPlayerUiSprite(BodySlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= PlayerUiPartNames.Length)
            return null;

        string spriteName = PlayerUiPartNames[index];
        Sprite sprite = Resources.Load<Sprite>("Sprites/PlayerUI/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/PlayerUI/" + spriteName + ".png");
#else
        return null;
#endif
    }

    SpriteRenderer SourceRendererForSlot(BodySlot slot)
    {
        ResolveReferences();
        if (playerController == null)
            return null;

        switch (slot)
        {
            case BodySlot.ArmLeft:
                return playerController.LeftArmRenderer;
            case BodySlot.ArmRight:
                return playerController.RightArmRenderer;
            default:
                return null;
        }
    }

    void ResolveReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (bodyRenderer == null)
            bodyRenderer = playerController != null ? playerController.BodyRenderer : GetComponent<SpriteRenderer>();

        if (playerCollider == null)
            playerCollider = GetComponent<Collider2D>();
    }

    void EnsurePlayerCollider()
    {
        ResolveReferences();
        if (playerCollider != null)
            return;

        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        if (bodyRenderer != null && bodyRenderer.sprite != null)
        {
            Bounds bounds = bodyRenderer.sprite.bounds;
            box.offset = (Vector2)bodyRenderer.transform.localPosition + (Vector2)bounds.center;
            box.size = new Vector2(Mathf.Max(0.1f, bounds.size.x), Mathf.Max(0.1f, bounds.size.y));
        }
        else
        {
            box.size = Vector2.one;
        }

        playerCollider = box;
    }
}
