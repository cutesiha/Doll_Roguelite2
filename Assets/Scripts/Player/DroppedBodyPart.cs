using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DroppedBodyPart : MonoBehaviour
{
    [SerializeField, Range(0.05f, 1f)] float fallDuration = 0.42f;
    [SerializeField, Min(0f)] float bounceHeight = 0.18f;
    [SerializeField] float spinDegrees = 90f;

    SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Configure(
        Sprite sprite,
        int sortingLayerId,
        int sortingOrder,
        Vector3 startPosition,
        Vector3 endPosition,
        Vector3 worldScale)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingLayerID = sortingLayerId;
        spriteRenderer.sortingOrder = sortingOrder;

        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = KeepReadableScale(worldScale);
        StartCoroutine(FallRoutine(startPosition, endPosition));
    }

    IEnumerator FallRoutine(Vector3 startPosition, Vector3 endPosition)
    {
        float duration = Mathf.Max(0.01f, fallDuration);
        float direction = Random.value < 0.5f ? -1f : 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * bounceHeight;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, direction * spinDegrees * eased);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPosition;
    }

    Vector3 KeepReadableScale(Vector3 sourceScale)
    {
        float x = Mathf.Abs(sourceScale.x);
        float y = Mathf.Abs(sourceScale.y);
        float uniform = Mathf.Max(0.01f, Mathf.Max(x, y));
        return new Vector3(
            Mathf.Sign(sourceScale.x == 0f ? 1f : sourceScale.x) * uniform,
            Mathf.Sign(sourceScale.y == 0f ? 1f : sourceScale.y) * uniform,
            1f);
    }
}
