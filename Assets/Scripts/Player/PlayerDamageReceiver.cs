using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Contact Damage")]
    [SerializeField, Min(1)] int contactDamage = 25;
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

    [Header("Dropped Parts")]
    [SerializeField, Min(0f)] float dropDownDistance = 0.52f;
    [SerializeField, Min(0f)] float dropSideDistance = 0.16f;
    [SerializeField] int droppedPartSortingOrder = 4;

    static readonly BodySlot[] DamageOrder =
    {
        BodySlot.ArmLeft,
        BodySlot.ArmRight,
        BodySlot.LegLeft,
        BodySlot.LegRight,
        BodySlot.EyeLeft,
        BodySlot.EyeRight
    };

    readonly Dictionary<SpriteRenderer, Color> rendererColors = new Dictionary<SpriteRenderer, Color>();

    PlayerController playerController;
    SpriteRenderer bodyRenderer;
    Collider2D playerCollider;
    Coroutine feedbackRoutine;
    float nextDamageTime;
    bool bodyDropped;

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
        if (enemyCollider == null || Time.time < nextDamageTime)
            return;

        if (((1 << enemyCollider.gameObject.layer) & enemyLayers.value) == 0)
            return;

        EnemyBase enemy = enemyCollider.GetComponentInParent<EnemyBase>();
        if (enemy == null)
            return;

        nextDamageTime = Time.time + damageCooldown;
        DamageNextBodyTarget(contactDamage);
        PlayHitFeedback();
        SoundManager.PlayPlayerHit();
    }

    void DamageNextBodyTarget(int damage)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            for (int i = 0; i < DamageOrder.Length; i++)
            {
                BodySlot slot = DamageOrder[i];
                BodyPart part = inventory.GetEquippedPart(slot);
                if (part == null)
                    continue;

                Sprite dropSprite = SpriteForSlot(slot);
                SpriteRenderer sourceRenderer = SourceRendererForSlot(slot);
                BodyPart brokenPart;
                if (inventory.TryDamageEquippedPart(slot, damage, out brokenPart) && brokenPart != null)
                    DropPart(dropSprite, sourceRenderer);
                return;
            }
        }

        bodyCurrentHp = Mathf.Clamp(bodyCurrentHp - damage, 0, bodyMaxHp);
        PlayerManager.Instance?.TakeDamage(damage);
        if (bodyCurrentHp <= 0 && !bodyDropped)
        {
            bodyDropped = true;
            DropPart(bodyRenderer != null ? bodyRenderer.sprite : null, bodyRenderer);
        }
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

    void DropPart(Sprite sprite, SpriteRenderer sourceRenderer)
    {
        if (sprite == null)
            return;

        ResolveReferences();
        SpriteRenderer sortingSource = sourceRenderer != null ? sourceRenderer : bodyRenderer;
        int sortingLayerId = sortingSource != null ? sortingSource.sortingLayerID : 0;
        Vector3 sourceScale = sortingSource != null ? sortingSource.transform.lossyScale : transform.lossyScale;
        Vector3 start = sortingSource != null ? sortingSource.bounds.center : transform.position;
        Vector3 end = transform.position + new Vector3(Random.Range(-dropSideDistance, dropSideDistance), -dropDownDistance, 0f);
        end.z = start.z;

        GameObject dropped = new GameObject("Dropped_" + sprite.name);
        DroppedBodyPart droppedPart = dropped.AddComponent<DroppedBodyPart>();
        droppedPart.Configure(sprite, sortingLayerId, droppedPartSortingOrder, start, end, sourceScale);
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
