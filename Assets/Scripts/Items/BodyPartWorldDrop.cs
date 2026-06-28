using System.Collections;
using UnityEngine;

// 인벤토리 "버리는 칸"에 드롭한 부위/아이템을 월드에 다시 떨어뜨린 오브젝트.
// 플레이어가 닿으면 원래 BodyPart 인스턴스(체력·아이콘 그대로)가 보관함으로 되돌아온다.
[RequireComponent(typeof(CircleCollider2D))]
public class BodyPartWorldDrop : MonoBehaviour
{
    BodyPart part;
    bool collected;
    float pickupImmuneUntil;

    public static BodyPartWorldDrop Spawn(BodyPart part, Vector3 position, Sprite sprite)
    {
        if (part == null)
            return null;

        GameObject go = new GameObject("DroppedItem_" + part.DisplayName());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.9f;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 35;

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.62f;

        BodyPartWorldDrop drop = go.AddComponent<BodyPartWorldDrop>();
        drop.part = part;
        return drop;
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
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void TryCollect(Collider2D other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        if (Time.time < pickupImmuneUntil)
            return;

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null)
            return;

        if (!inv.TryAddPart(part, false))
            return;

        collected = true;
        SoundManager.PlayClick();
        Destroy(gameObject);
    }
}
