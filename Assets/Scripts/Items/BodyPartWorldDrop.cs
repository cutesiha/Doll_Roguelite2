using UnityEngine;

// 인벤토리 "버리는 칸"에 드롭한 부위/아이템을 월드에 다시 떨어뜨린 오브젝트.
// 플레이어가 닿으면 원래 BodyPart 인스턴스(체력·아이콘 그대로)가 보관함으로 되돌아온다.
// 버린 직후 플레이어와 겹친 상태에서 곧바로 다시 줍히지 않도록, 플레이어와 떨어진
// 위치에 생성하고 OnTriggerEnter 로만 회수한다.
[RequireComponent(typeof(CircleCollider2D))]
public class BodyPartWorldDrop : MonoBehaviour
{
    BodyPart part;
    bool collected;

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

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void TryCollect(Collider2D other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null)
            return;

        // 보관함이 가득 차 있으면 줍지 않고 월드에 그대로 둔다.
        if (!inv.TryAddPart(part, false))
            return;

        collected = true;
        SoundManager.PlayClick();
        Destroy(gameObject);
    }
}
