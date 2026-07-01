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
    ItemPickupSparkle sparkle;
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
    [Header("Authoring (에디터 배치용)")]
    [SerializeField] ItemData itemAsset;
    [Header("Shop Feedback")]
    [SerializeField, Min(0f)] float purchaseFailureShakeDistance = 0.22f;
    [SerializeField, Min(0.02f)] float purchaseFailureShakeDuration = 0.34f;
    [SerializeField, Min(2)] int purchaseFailureShakeSteps = 12;
    [SerializeField] Color purchaseFailureColor = new Color(1f, 0.12f, 0.12f, 1f);

    float pickupImmuneUntil;

    public ItemData Item => item;
    public bool IsShopItem => shopItem;
    public bool StoreWithoutEquip => storeWithoutEquip;

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

        tooltipBackgroundSprite = template.tooltipBackgroundSprite;
        tooltipLocalPosition = template.tooltipLocalPosition;
        tooltipBackgroundScale = template.tooltipBackgroundScale;
        tooltipTextLocalPosition = template.tooltipTextLocalPosition;
        tooltipTextBoxSize = template.tooltipTextBoxSize;
        tooltipFontSize = template.tooltipFontSize;
        tooltipBackgroundColor = template.tooltipBackgroundColor;
        tooltipTextColor = template.tooltipTextColor;
    }

    void Awake()
    {
        settings = ItemSystemSettings.Load();
        basePosition = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        itemRenderer = GetComponent<SpriteRenderer>();
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.isTrigger = true;

        if (itemAsset != null && item == null)
            Configure(itemAsset, false, 0, false);
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
        SetSparkleActive(nearby);   // task5: 근처에 가면 반짝반짝 파티클

        if (shopItem)
        {
            // task6: 상점은 닿아도 안 사지고, 근처 + E 로 구매 확인창을 띄운다.
            if (nearby && !ShopPurchaseConfirmUI.IsOpen && WasInteractPressed())
                OpenPurchaseConfirm();
        }
        // task5: 일반 아이템은 거리 기반 자동획득을 제거했다.
        // 실제 획득은 플레이어 캡슐 콜라이더가 아이템에 닿는 OnTrigger 콜백에서만 일어난다.
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

    // task6/7: 구매 확인창(StartExitPanel 복제)을 띄운다. 예 → 동전 차감(TryPurchase), 아니오 → 취소.
    void OpenPurchaseConfirm()
    {
        if (ItemInventoryManager.Instance == null || item == null)
            return;

        string question = "이 아이템을 구매하시겠습니까?\n<size=55%>" + item.ItemName + "  -  " + price + " 코인</size>";
        ShopPurchaseConfirmUI.Present(question, TryPurchase);
    }

    // task5: 근처에서만 켜지는 반짝이 파티클
    void SetSparkleActive(bool active)
    {
        if (active && sparkle == null)
            EnsureSparkle();

        if (sparkle != null && sparkle.gameObject.activeSelf != active)
            sparkle.gameObject.SetActive(active);
    }

    void EnsureSparkle()
    {
        Transform existing = transform.Find("ItemSparkle");
        GameObject go = existing != null ? existing.gameObject : new GameObject("ItemSparkle");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one / Mathf.Max(0.01f, transform.localScale.x);

        sparkle = go.GetComponent<ItemPickupSparkle>();
        if (sparkle == null)
            sparkle = go.AddComponent<ItemPickupSparkle>();
        sparkle.Configure(itemRenderer != null ? itemRenderer.sortingOrder + 1 : 60);
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
        if (collected || other == null || !other.CompareTag("Player"))
            return;

        // task5: 플레이어의 캡슐 콜라이더(몸통)가 아이템에 실제로 닿았을 때만 획득.
        // (원 콜라이더는 물리 차단용이라 접촉 판정에서 제외한다.)
        if (!(other is CapsuleCollider2D))
            return;

        if (Time.time < pickupImmuneUntil)
            return;

        player = other.transform;
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
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    void EnsureTooltip()
    {
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

    string TooltipText()
    {
        if (item == null)
            return "아이템 데이터 없음";

        // task10: 신체부위 아이템이면 이름 아래에 "부위: 00" 표기
        string partLabel = item.BodyPartLabel();
        string partLine = string.IsNullOrEmpty(partLabel) ? "" : "\n<size=70%>부위: " + partLabel + "</size>";

        string suffix = shopItem
            ? "\n[E] 구매  " + price + " 코인"
            : "\n닿으면 획득";
        return "<b>" + item.ItemName + "</b>" + partLine + "\n<size=70%>" + item.Description + suffix + "</size>";
    }

    void SetTooltipVisible(bool visible)
    {
        EnsureTooltip();
        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(visible);
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
