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
        Vector3 target = ResolveWallBounce(from, to);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(from, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.8f;
            transform.position = pos;
            yield return null;
        }
        transform.position = target;
        basePosition = target;
    }

    // 목표 지점으로 가는 경로가 벽 콜라이더를 통과하면, 벽 위/안쪽에 드랍되는 대신
    // 벽에서 튕겨 나온 지점으로 목표를 옮긴다.
    Vector3 ResolveWallBounce(Vector3 from, Vector3 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return to;

        Vector2 dir = delta / distance;
        RaycastHit2D[] hits = Physics2D.RaycastAll(from, dir, distance);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger || hitCollider.transform == transform)
                continue;
            if (!IsWallCollider(hitCollider))
                continue;

            Vector2 reflected = Vector2.Reflect(dir, hits[i].normal);
            float remaining = Mathf.Max(0.15f, (distance - hits[i].distance) * 0.5f);
            Vector2 bounced = hits[i].point + reflected * remaining;
            return new Vector3(bounced.x, bounced.y, to.z);
        }

        return to;
    }

    static bool IsWallCollider(Collider2D collider)
    {
        string objectName = collider.transform.name;
        return !string.IsNullOrEmpty(objectName)
            && (objectName.StartsWith("Wall_") || objectName.StartsWith("wall"));
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
