using System.Collections;
using UnityEngine;

// 미로 출구 앞 보물상자: 플레이어가 닿으면 열리고 아이템 3종 중 1개가 뿅 튀어나옴
[RequireComponent(typeof(Collider2D))]
public class MazeTreasureChest : MonoBehaviour
{
    [SerializeField] string[] rewardItemIds = { "wood_plank", "wooden_leg", "spool" };
    [SerializeField] float popForce = 4.5f;

    SpriteRenderer lid;
    bool opened;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        // 뚜껑 자식 오브젝트 찾기
        Transform lidTr = transform.Find("Lid");
        if (lidTr != null) lid = lidTr.GetComponent<SpriteRenderer>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (opened || !other.CompareTag("Player")) return;
        opened = true;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        // 뚜껑 열기 애니메이션
        if (lid != null)
        {
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                lid.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -60f, t / 0.25f));
                yield return null;
            }
        }

        // 아이템 스폰
        SpawnRewardItem();
    }

    void SpawnRewardItem()
    {
        if (rewardItemIds == null || rewardItemIds.Length == 0) return;

        string itemId = rewardItemIds[Random.Range(0, rewardItemIds.Length)];
        ItemData[] allItems = Resources.LoadAll<ItemData>("Items");
        ItemData chosen = null;
        for (int i = 0; i < allItems.Length; i++)
            if (allItems[i] != null && allItems[i].ItemId == itemId) { chosen = allItems[i]; break; }

        if (chosen == null) return;

        // 부유하는 아이템 오브젝트 생성
        GameObject go = new GameObject("ChestReward_" + itemId);
        go.transform.position = transform.position + Vector3.up * 0.5f;
        // task23: 하드코딩 0.9 대신 아이템별 테스트룸 크기(worldScale)를 사용해 다른 보상들과 크기를 통일한다.
        go.transform.localScale = Vector3.one * chosen.ResolveWorldScale();

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = chosen.Sprite;
        sr.sortingOrder = 50;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.55f;

        ItemWorldPickup pickup = go.AddComponent<ItemWorldPickup>();
        // itemAsset 필드 리플렉션으로 설정 (직렬화 필드이므로 런타임에도 가능)
        var field = typeof(ItemWorldPickup).GetField("itemAsset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public);
        field?.SetValue(pickup, chosen);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.up * popForce;

        go.AddComponent<MazeChestItemFloat>();
    }
}

// 뿅 나온 아이템이 공중에서 둥둥 떠다님
public class MazeChestItemFloat : MonoBehaviour
{
    float amplitude = 0.25f;
    float speed = 1.6f;
    Vector3 origin;
    Rigidbody2D rb;
    bool settling;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        origin = transform.position;
        StartCoroutine(SettleRoutine());
    }

    IEnumerator SettleRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        settling = true;
        float t = 0f;
        Vector3 startVel = rb != null ? (Vector3)(rb.linearVelocity * 0.016f) : Vector3.zero;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            if (rb != null) rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, t / 0.4f);
            yield return null;
        }
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }
        origin = transform.position;
    }

    void Update()
    {
        if (!settling) return;
        transform.position = origin + Vector3.up * (Mathf.Sin(Time.time * speed) * amplitude);
    }
}
