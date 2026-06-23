using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField, Min(1)] int contactDamage = 1;
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
    [SerializeField, Range(0.1f, 2f)] float fallbackDroppedPartScaleRatio = 0.72f;
    [SerializeField] int droppedPartSortingOrder = 4;

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
    float nextDamageTime;
    bool bodyDropped;
    bool isDead;

    void Awake()
    {
        ResolveReferences();
        EnsurePlayerCollider();
        bodyCurrentHp = Mathf.Clamp(bodyCurrentHp, 0, bodyMaxHp);
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

    public void TryTakeHit(Collider2D enemyCollider)
    {
        if (isDead || enemyCollider == null || Time.time < nextDamageTime)
            return;

        if (((1 << enemyCollider.gameObject.layer) & enemyLayers.value) == 0)
            return;

        EnemyBase enemy = enemyCollider.GetComponentInParent<EnemyBase>();
        if (enemy == null)
            return;

        nextDamageTime = Time.time + damageCooldown;
        DamageNextBodyTarget(contactDamage);
        PlayHitFeedback();
        ShakeCamera();
        SoundManager.PlayPlayerHit();
    }

    public bool TryTakePatternDamage(int damage, float cooldownOverride = -1f)
    {
        if (isDead || Time.time < nextDamageTime)
            return false;

        int finalDamage = Mathf.Max(1, damage);
        float cooldown = cooldownOverride >= 0f ? cooldownOverride : damageCooldown;
        nextDamageTime = Time.time + Mathf.Max(0.01f, cooldown);
        DamageNextBodyTarget(finalDamage);
        PlayHitFeedback();
        ShakeCamera();
        SoundManager.PlayPlayerHit();
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
        InventoryManager inventory = InventoryManager.Instance;
        damageCandidates.Clear();
        if (inventory != null)
            for (int i = 0; i < DamageSlots.Length; i++)
                if (inventory.GetEquippedPart(DamageSlots[i]) != null)
                    damageCandidates.Add(DamageSlots[i]);

        int equippedCount = damageCandidates.Count;
        // 부위 3개가 영구히 떨어진 뒤부터 몸도 랜덤 데미지 풀에 포함
        bool bodyInPool = inventory == null || CountFallenParts(inventory) >= 3;
        int total = equippedCount + (bodyInPool ? 1 : 0);

        if (total == 0)
        {
            DamageBody(damage);
            return;
        }

        int pick = Random.Range(0, total);
        if (pick < equippedCount)
        {
            BodySlot slot = damageCandidates[pick];
            Sprite dropSprite = SpriteForSlot(slot);
            SpriteRenderer sourceRenderer = SourceRendererForSlot(slot);
            BodyPart brokenPart;
            if (inventory.TryDamageEquippedPart(slot, damage, out brokenPart) && brokenPart != null)
                DropPart(dropSprite, sourceRenderer, slot);
        }
        else
        {
            DamageBody(damage);
        }
    }

    // 인벤토리(장착+보관) 어디에도 없는 = 영구히 떨어진 부위 수
    int CountFallenParts(InventoryManager inventory)
    {
        int fallen = 0;
        for (int i = 0; i < DamageSlots.Length; i++)
        {
            BodySlot slot = DamageSlots[i];
            bool present = inventory.GetEquippedPart(slot) != null;
            if (!present && inventory.storage != null)
            {
                for (int s = 0; s < inventory.storage.Length; s++)
                {
                    BodyPart item = inventory.storage[s];
                    if (item != null && item.IsEquippable && item.slot == slot)
                    {
                        present = true;
                        break;
                    }
                }
            }
            if (!present)
                fallen++;
        }
        return fallen;
    }

    // 몸 체력 감소 (HUD엔 PlayerManager.CurrentHp 로 표시됨). 0이면 사망.
    void DamageBody(int damage)
    {
        PlayerManager pm = PlayerManager.Instance;
        if (pm == null)
        {
            DieBodyOnly();
            return;
        }

        pm.TakeDamage(Mathf.Max(1, damage));
        if (pm.CurrentHp <= 0)
            DieBodyOnly();
    }

    void DieBodyOnly()
    {
        if (isDead)
            return;

        isDead = true;
        bodyCurrentHp = 0;
        PlayerManager.Instance?.SetCurrentHp(0);

        if (BodyManager.Instance != null && BodyManager.Instance.State != null)
            BodyManager.Instance.State.body = false;

        if (!bodyDropped)
        {
            bodyDropped = true;
            DropPart(bodyRenderer != null ? bodyRenderer.sprite : null, bodyRenderer, null);
        }

        DisablePlayerAfterDeath();
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

    bool TryPickRandomDamageSlot(InventoryManager inventory, out BodySlot slot)
    {
        slot = BodySlot.ArmLeft;
        damageCandidates.Clear();

        for (int i = 0; i < DamageSlots.Length; i++)
        {
            BodySlot candidate = DamageSlots[i];
            if (inventory.GetEquippedPart(candidate) != null)
                damageCandidates.Add(candidate);
        }

        if (damageCandidates.Count == 0)
            return false;

        slot = damageCandidates[Random.Range(0, damageCandidates.Count)];
        return true;
    }

    void ShakeCamera()
    {
        if (shakeCameraOnHit)
            CameraShake.ShakeHorizontal(cameraShakeDuration, cameraShakeMagnitude);
    }

    void PlayHitFeedback()
    {
        if (!gameObject.activeInHierarchy)
            return;

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
        Vector3 sourceScale = DroppedPartWorldScale(sprite, sourceRenderer, slot);
        Vector3 start = sortingSource != null ? sortingSource.bounds.center : transform.position;
        Vector3 end = transform.position + new Vector3(Random.Range(-dropSideDistance, dropSideDistance), -dropDownDistance, 0f);
        end.z = start.z;

        GameObject dropped = new GameObject("Dropped_" + sprite.name);
        DroppedBodyPart droppedPart = dropped.AddComponent<DroppedBodyPart>();
        droppedPart.Configure(sprite, sortingLayerId, droppedPartSortingOrder, start, end, sourceScale);
    }

    Vector3 DroppedPartWorldScale(Sprite sprite, SpriteRenderer sourceRenderer, BodySlot? slot)
    {
        if (sourceRenderer != null && sourceRenderer.sprite != null)
            return sourceRenderer.transform.lossyScale;

        Vector3 baseScale = bodyRenderer != null ? bodyRenderer.transform.lossyScale : transform.lossyScale;
        if (bodyRenderer == null || bodyRenderer.sprite == null || sprite == null)
            return baseScale;

        float bodyLargest = Mathf.Max(bodyRenderer.sprite.bounds.size.x, bodyRenderer.sprite.bounds.size.y);
        float partLargest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (bodyLargest <= 0f || partLargest <= 0f)
            return baseScale;

        float ratio = partLargest / bodyLargest;
        if (!slot.HasValue)
            ratio = 1f;
        else
            ratio = Mathf.Clamp(ratio, 0.35f, fallbackDroppedPartScaleRatio);

        return new Vector3(baseScale.x * ratio, baseScale.y * ratio, 1f);
    }

    Sprite SpriteForSlot(BodySlot slot)
    {
        SpriteRenderer sourceRenderer = SourceRendererForSlot(slot);
        if (sourceRenderer != null && sourceRenderer.sprite != null)
            return sourceRenderer.sprite;

        return InventoryUI.FindDisplaySpriteForSlot(slot);
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
