using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class ItemWorldPickup : MonoBehaviour
{
    ItemData item;
    bool shopItem;
    bool storeWithoutEquip;
    int price;
    bool collected;
    bool pointerOver;
    Vector3 basePosition;
    float bobPhase;
    ItemSystemSettings settings;
    Transform player;
    float nextFailureNoticeTime;
    Vector3 shakeOffset;
    Coroutine purchaseFailureRoutine;
    SpriteRenderer itemRenderer;
    Collider2D pickupCollider;
    CircleCollider2D playerCircle;
    [Header("Tooltip Authoring")]
    [SerializeField] Transform tooltipRoot;
    [SerializeField] SpriteRenderer tooltipBackground;
    [SerializeField] TextMeshPro tooltipText;
    [SerializeField] Sprite tooltipBackgroundSprite;
    [SerializeField] Vector3 tooltipLocalPosition = new Vector3(0f, 1.45f, -0.1f);
    [SerializeField] Vector3 tooltipBackgroundScale = new Vector3(5.2f, 1.65f, 1f);
    [SerializeField] Vector3 tooltipTextLocalPosition = new Vector3(0f, 0f, -0.05f);
    [SerializeField] Vector2 tooltipTextBoxSize = new Vector2(4.85f, 1.35f);
    [SerializeField, Min(0.1f)] float tooltipFontSize = 2.0f;
    [SerializeField] Color tooltipBackgroundColor = new Color(0.10f, 0.07f, 0.05f, 0.92f);
    [SerializeField] Color tooltipTextColor = Color.white;
    [SerializeField] bool useAsGlobalTooltipTemplate;
    [Header("Authoring (에디터 배치용)")]
    [SerializeField] ItemData itemAsset;
    [Header("Shop Feedback")]
    [SerializeField, Min(0f)] float purchaseFailureShakeDistance = 0.09f;
    [SerializeField, Min(0.02f)] float purchaseFailureShakeDuration = 0.28f;
    [SerializeField, Min(2)] int purchaseFailureShakeSteps = 8;
    [SerializeField] Color purchaseFailureColor = new Color(1f, 0.52f, 0.52f, 1f);

    float pickupImmuneUntil;

    public ItemData Item => item;
    public bool IsShopItem => shopItem;
    public bool StoreWithoutEquip => storeWithoutEquip;

    static ItemWorldPickup globalTooltipTemplate;

    public void Configure(ItemData data, bool isShopItem, int shopPrice, bool storeOnly = false)
    {
        item = data;
        shopItem = isShopItem;
        storeWithoutEquip = storeOnly;
        price = Mathf.Max(0, shopPrice);
        name = (shopItem ? "ShopItem_" : "ItemDrop_") + (item != null ? item.ItemId : "Missing");
    }

    public void CopyPresentationFrom(ItemWorldPickup template)
    {
        if (template == null)
            return;

        template.CaptureTooltipAuthoringFromHierarchy();
        tooltipBackgroundSprite = template.tooltipBackgroundSprite;
        tooltipLocalPosition = template.tooltipLocalPosition;
        tooltipBackgroundScale = template.tooltipBackgroundScale;
        tooltipTextLocalPosition = template.tooltipTextLocalPosition;
        tooltipTextBoxSize = template.tooltipTextBoxSize;
        tooltipFontSize = template.tooltipFontSize;
        tooltipBackgroundColor = template.tooltipBackgroundColor;
        tooltipTextColor = template.tooltipTextColor;
    }

    public void UseAsGlobalTooltipTemplate()
    {
        useAsGlobalTooltipTemplate = true;
        globalTooltipTemplate = this;
    }

    void Awake()
    {
        settings = ItemSystemSettings.Load();
        basePosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        itemRenderer = GetComponent<SpriteRenderer>();
        pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;

        if (itemAsset != null && item == null)
            Configure(itemAsset, false, 0, false);

        if (useAsGlobalTooltipTemplate)
            globalTooltipTemplate = this;
    }

    void OnDestroy()
    {
        if (globalTooltipTemplate == this)
            globalTooltipTemplate = null;
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
        transform.position = basePosition + Vector3.up * bob + shakeOffset;

        ResolvePlayer();
        float distance = player != null ? Vector2.Distance(player.position, transform.position) : float.PositiveInfinity;
        bool nearby = distance <= settings.tooltipRadius;
        SetTooltipVisible(pointerOver || nearby);

        if (shopItem)
        {
            if (nearby && WasInteractPressed())
                TryPurchase();
        }
        else if (IsTouchingPlayerCircle())
        {
            TryCollectPlayer();
        }
    }

    // 아이템 획득 기준을 플레이어의 서클 콜라이더(몸체) 접촉으로 통일한다.
    // 예전에는 settings.pickupRadius(추상 거리) 기준이라 캡슐/박스 등 다른 콜라이더와
    // 어긋나 획득 범위가 애매했다.
    bool IsTouchingPlayerCircle()
    {
        if (playerCircle == null)
        {
            ResolvePlayer();
            if (playerCircle == null)
                return false;
        }

        Bounds circleBounds = playerCircle.bounds;
        float circleRadius = Mathf.Max(circleBounds.extents.x, circleBounds.extents.y);

        float itemRadius = 0f;
        if (pickupCollider != null)
        {
            Bounds itemBounds = pickupCollider.bounds;
            itemRadius = Mathf.Max(itemBounds.extents.x, itemBounds.extents.y);
        }

        return Vector2.Distance(transform.position, circleBounds.center) <= circleRadius + itemRadius;
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

    void TryCollect(Collider2D other)
    {
        if (collected || other == null)
            return;

        if (Time.time < pickupImmuneUntil)
            return;

        // 플레이어의 콜라이더가 아니면 무시. 서클 콜라이더에 실제로 닿았을 때만 획득한다
        // (캡슐/박스 트리거에 스쳐도 몸체에 닿기 전엔 안 먹도록 통일).
        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == null)
            return;

        ResolvePlayer();
        if (!IsTouchingPlayerCircle())
            return;

        TryCollectPlayer();
    }

    void TryCollectPlayer()
    {
        if (collected || player == null)
            return;

        if (Time.time < pickupImmuneUntil)
            return;

        ItemInventoryManager inventory = ItemInventoryManager.Instance;
        if (inventory == null)
            return;

        bool acquired = storeWithoutEquip
            ? inventory.TryStoreWithoutEquip(item, out string message)
            : inventory.TryAcquire(item, out message);

        if (!acquired)
        {
            if (Time.unscaledTime >= nextFailureNoticeTime)
            {
                nextFailureNoticeTime = Time.unscaledTime + 1f;
                Announce(message);
            }
            return;
        }

        collected = true;
        SoundManager.PlayItemPickup();
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
            PlayShopFeedbackSound();
            StartPurchaseFailureFeedback();
            Announce(message);
            return;
        }

        collected = true;
        PlayShopFeedbackSound();
        Announce(message);
        Destroy(gameObject);
    }

    void StartPurchaseFailureFeedback()
    {
        if (!isActiveAndEnabled)
            return;

        if (purchaseFailureRoutine != null)
            StopCoroutine(purchaseFailureRoutine);

        purchaseFailureRoutine = StartCoroutine(PurchaseFailureFeedbackRoutine());
    }

    IEnumerator PurchaseFailureFeedbackRoutine()
    {
        if (itemRenderer == null)
            itemRenderer = GetComponent<SpriteRenderer>();

        Color originalColor = itemRenderer != null ? itemRenderer.color : Color.white;
        if (itemRenderer != null)
            itemRenderer.color = purchaseFailureColor;

        int steps = Mathf.Max(2, purchaseFailureShakeSteps);
        float delay = Mathf.Max(0.02f, purchaseFailureShakeDuration) / steps;
        for (int i = 0; i < steps; i++)
        {
            float direction = i % 2 == 0 ? 1f : -1f;
            shakeOffset = Vector3.right * (purchaseFailureShakeDistance * direction);
            yield return new WaitForSecondsRealtime(delay);
        }

        shakeOffset = Vector3.zero;
        if (itemRenderer != null)
            itemRenderer.color = originalColor;
        purchaseFailureRoutine = null;
    }

    static void PlayShopFeedbackSound()
    {
        SoundManager.PlayClick(0f);
    }

    void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (playerCircle == null && player != null)
            playerCircle = player.GetComponent<CircleCollider2D>();
    }

    void EnsureTooltip()
    {
        ItemWorldPickup template = ResolveGlobalTooltipTemplate();
        if (template != null && template != this)
            CopyPresentationFrom(template);
        else if (useAsGlobalTooltipTemplate)
            CaptureTooltipAuthoringFromHierarchy();

        if (tooltipRoot == null)
        {
            Transform existingTooltip = transform.Find("ItemTooltip");
            if (existingTooltip != null)
                tooltipRoot = existingTooltip;
        }

        if (tooltipRoot == null)
        {
            GameObject tooltipObject = new GameObject("ItemTooltip");
            tooltipRoot = tooltipObject.transform;
            tooltipRoot.SetParent(transform, false);
        }

        tooltipRoot.localPosition = tooltipLocalPosition;
        tooltipRoot.localScale = Vector3.one / Mathf.Max(0.01f, transform.localScale.x);

        if (tooltipBackground == null)
            tooltipBackground = tooltipRoot.GetComponentInChildren<SpriteRenderer>(true);

        if (tooltipBackground == null)
        {
            GameObject background = new GameObject("Background");
            background.transform.SetParent(tooltipRoot, false);
            tooltipBackground = background.AddComponent<SpriteRenderer>();
        }

        tooltipBackground.transform.localScale = tooltipBackgroundScale;
        tooltipBackground.sprite = tooltipBackgroundSprite != null ? tooltipBackgroundSprite : BossVisuals.SquareSprite();
        tooltipBackground.color = tooltipBackgroundColor;
        tooltipBackground.sortingOrder = 79;

        if (tooltipText == null)
            tooltipText = tooltipRoot.GetComponentInChildren<TextMeshPro>(true);

        if (tooltipText == null)
        {
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(tooltipRoot, false);
            tooltipText = textObject.AddComponent<TextMeshPro>();
        }

        tooltipText.transform.localPosition = tooltipTextLocalPosition;
        tooltipText.font = UIThinDungFont.Get();
        tooltipText.fontSize = tooltipFontSize;
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.color = tooltipTextColor;
        tooltipText.sortingOrder = 80;
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        tooltipText.rectTransform.sizeDelta = tooltipTextBoxSize;
        tooltipText.text = TooltipText();

        tooltipRoot.gameObject.SetActive(false);
    }

    void CaptureTooltipAuthoringFromHierarchy()
    {
        if (tooltipRoot == null)
        {
            Transform existingTooltip = transform.Find("ItemTooltip");
            if (existingTooltip != null)
                tooltipRoot = existingTooltip;
        }

        if (tooltipRoot != null)
            tooltipLocalPosition = tooltipRoot.localPosition;

        if (tooltipBackground == null && tooltipRoot != null)
            tooltipBackground = tooltipRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (tooltipBackground != null)
        {
            tooltipBackgroundSprite = tooltipBackground.sprite;
            tooltipBackgroundScale = tooltipBackground.transform.localScale;
            tooltipBackgroundColor = tooltipBackground.color;
        }

        if (tooltipText == null && tooltipRoot != null)
            tooltipText = tooltipRoot.GetComponentInChildren<TextMeshPro>(true);
        if (tooltipText != null)
        {
            tooltipTextLocalPosition = tooltipText.transform.localPosition;
            tooltipFontSize = tooltipText.fontSize;
            tooltipTextColor = tooltipText.color;
            tooltipTextBoxSize = tooltipText.rectTransform.sizeDelta;
        }
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
            tooltipRoot.gameObject.SetActive(visible);
    }

    ItemWorldPickup ResolveGlobalTooltipTemplate()
    {
        if (globalTooltipTemplate != null)
            return globalTooltipTemplate;

        ItemWorldPickup[] pickups = FindObjectsByType<ItemWorldPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] == null || !pickups[i].useAsGlobalTooltipTemplate)
                continue;

            globalTooltipTemplate = pickups[i];
            return globalTooltipTemplate;
        }

        return null;
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
