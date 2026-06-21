using UnityEngine;

// 월드에 떨어진 드랍 아이템. 플레이어가 닿으면 인벤토리(보관함)에 들어간다.
[RequireComponent(typeof(Collider2D))]
public class DropPickup : MonoBehaviour
{
    [SerializeField] ItemKind kind = ItemKind.Coin;

    bool collected;

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // 보관함이 가득 차 있다가 비면 머무는 동안 다시 시도
        TryCollect(other);
    }

    void TryCollect(Collider2D other)
    {
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        if (inv.AddItem(kind))
        {
            collected = true;
            Destroy(gameObject);
        }
    }
}
