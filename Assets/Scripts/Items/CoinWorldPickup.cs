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
        Vector3 target = DropWallBounce.ResolveTarget(from, to, transform);

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

        // 상점(ItemInventoryManager)이 실제로 확인/차감하는 동전 풀과 같은 곳에 넣어야 한다.
        // 예전엔 레거시 InventoryManager(BodyPart Kind.Coin)에 쌓여서, 인벤토리엔 코인이
        // 보여도 상점에서는 그 돈을 전혀 인식하지 못하는 별개의 풀이 되어 있었다.
        ItemInventoryManager itemInventory = ItemInventoryManager.Instance;
        if (itemInventory == null)
            return;

        ItemData coinItem = ItemCatalog.Find("coin");
        if (coinItem == null || !itemInventory.TryAcquire(coinItem, out _))
            return;

        collected = true;
        SoundManager.PlayCoinPickup();
        Destroy(gameObject);
    }
}
