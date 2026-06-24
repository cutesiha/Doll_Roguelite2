using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class ItemWorldPickup : MonoBehaviour
{
    ItemData item;
    bool shopItem;
    int price;
    bool collected;
    bool pointerOver;
    Vector3 basePosition;
    float bobPhase;
    ItemSystemSettings settings;
    GameObject tooltipRoot;
    TextMeshPro tooltipText;
    Transform player;
    float nextFailureNoticeTime;

    public ItemData Item => item;
    public bool IsShopItem => shopItem;

    public void Configure(ItemData data, bool isShopItem, int shopPrice)
    {
        item = data;
        shopItem = isShopItem;
        price = Mathf.Max(0, shopPrice);
        name = (shopItem ? "ShopItem_" : "ItemDrop_") + (item != null ? item.ItemId : "Missing");
    }

    void Awake()
    {
        settings = ItemSystemSettings.Load();
        basePosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.isTrigger = true;
    }

    void Start()
    {
        basePosition = transform.position;
        ResolvePlayer();
        EnsureTooltip();
    }

    void Update()
    {
        if (item == null || collected)
            return;

        float bob = Mathf.Sin(Time.unscaledTime * settings.floatSpeed + bobPhase) * settings.floatHeight;
        transform.position = basePosition + Vector3.up * bob;

        ResolvePlayer();
        float distance = player != null ? Vector2.Distance(player.position, transform.position) : float.PositiveInfinity;
        bool nearby = distance <= settings.tooltipRadius;
        SetTooltipVisible(pointerOver || nearby);

        if (shopItem)
        {
            if (nearby && WasInteractPressed())
                TryPurchase();
        }
        else if (distance <= settings.pickupRadius)
        {
            TryCollectPlayer();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!shopItem)
            TryCollect(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!shopItem)
            TryCollect(other);
    }

    void OnMouseEnter()
    {
        pointerOver = true;
    }

    void OnMouseExit()
    {
        pointerOver = false;
    }

    void TryCollect(Collider2D other)
    {
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        player = other.transform;
        TryCollectPlayer();
    }

    void TryCollectPlayer()
    {
        if (collected || player == null)
            return;

        ItemInventoryManager inventory = ItemInventoryManager.Instance;
        if (inventory == null)
            return;

        if (!inventory.TryAcquire(item, out string message))
        {
            if (Time.unscaledTime >= nextFailureNoticeTime)
            {
                nextFailureNoticeTime = Time.unscaledTime + 1f;
                Announce(message);
            }
            return;
        }

        collected = true;
        Announce(message);
        Destroy(gameObject);
    }

    void TryPurchase()
    {
        ItemInventoryManager inventory = ItemInventoryManager.Instance;
        if (inventory == null)
            return;

        if (!inventory.TryPurchase(item, price, out string message))
        {
            Announce(message);
            return;
        }

        collected = true;
        Announce(message);
        Destroy(gameObject);
    }

    void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    void EnsureTooltip()
    {
        if (tooltipRoot != null)
            return;

        tooltipRoot = new GameObject("ItemTooltip");
        tooltipRoot.transform.SetParent(transform, false);
        tooltipRoot.transform.localPosition = new Vector3(0f, 1.45f, -0.1f);
        tooltipRoot.transform.localScale = Vector3.one / Mathf.Max(0.01f, transform.localScale.x);

        GameObject background = new GameObject("Background");
        background.transform.SetParent(tooltipRoot.transform, false);
        background.transform.localScale = new Vector3(5.2f, 1.65f, 1f);
        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = BossVisuals.SquareSprite();
        backgroundRenderer.color = new Color(0.10f, 0.07f, 0.05f, 0.92f);
        backgroundRenderer.sortingOrder = 79;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(tooltipRoot.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        tooltipText = textObject.AddComponent<TextMeshPro>();
        tooltipText.font = UIThinDungFont.Get();
        tooltipText.fontSize = 2.0f;
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.color = Color.white;
        tooltipText.sortingOrder = 80;
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        tooltipText.rectTransform.sizeDelta = new Vector2(4.85f, 1.35f);
        tooltipText.text = TooltipText();

        tooltipRoot.SetActive(false);
    }

    string TooltipText()
    {
        if (item == null)
            return "아이템 데이터 없음";

        string suffix = shopItem
            ? "\n[E] 구매  " + price + " 코인"
            : "\n가까이 가면 자동 획득";
        return "<b>" + item.ItemName + "</b>\n<size=70%>" + item.Description + suffix + "</size>";
    }

    void SetTooltipVisible(bool visible)
    {
        EnsureTooltip();
        if (tooltipRoot != null)
            tooltipRoot.SetActive(visible);
    }

    static bool WasInteractPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.eKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame);
    }

    static void Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText(message);
        Debug.Log("[Item] " + message);
    }
}
