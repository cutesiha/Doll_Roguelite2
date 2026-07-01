using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CoinWorldPickup : MonoBehaviour
{
    bool collected;
    Vector3 basePosition;
    float pickupImmuneUntil;

    void Start()
    {
        basePosition = transform.position;
    }

    public void Toss(Vector3 origin, float distance = 2.5f, float duration = 0.4f)
    {
        pickupImmuneUntil = Time.time + duration + 0.15f;
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.01f)
            dir = Vector2.right;
        Vector3 target = origin + (Vector3)(dir * distance);
        StartCoroutine(TossRoutine(origin, target, duration));
    }

    IEnumerator TossRoutine(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.8f;
            transform.position = pos;
            yield return null;
        }
        transform.position = to;
        basePosition = to;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.collider);
    }

    void TryCollect(Collider2D other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        if (Time.time < pickupImmuneUntil)
            return;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        BodyPart coin = new BodyPart(ItemKind.Coin);
        coin.icon = GetComponent<SpriteRenderer>()?.sprite;

        if (!inventory.TryAddPart(coin, false))
            return;

        collected = true;
        SoundManager.PlayCoinPickup();
        Destroy(gameObject);
    }
}
