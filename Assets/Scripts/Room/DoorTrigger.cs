using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoorTrigger : MonoBehaviour
{
    [Header("Destination Scenes")]
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField] string middleBossSceneName = "MiddleBossScene";
    [SerializeField] string finalBossSceneName = "BookBossScene";
    [SerializeField] string challengeSceneName = "RoomScene";
    [SerializeField] string supplySceneName = "PresentScene";
    [SerializeField] string eventSceneName = "EventScene";
    [SerializeField] string treasureSceneName = "TreasureRoomScene";
    [SerializeField] string shopSceneName = "ShopScene";

    [Header("Door Feedback")]
    [SerializeField] Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color blockedColor = new Color(0.72f, 0.20f, 0.18f, 1f);
    [SerializeField, Min(0.1f)] float doorWorldScale = 1.8f;
    [SerializeField, Min(0.1f)] float tooltipHeight = 2.25f;
    [Header("Door Authoring")]
    [SerializeField] Sprite doorSpriteOverride;
    [SerializeField] Sprite roomIconSpriteOverride;
    [SerializeField] Sprite tooltipSpriteOverride;
    [SerializeField] Vector3 iconLocalPosition = new Vector3(-0.02f, 0.25f, -0.02f);
    [SerializeField] Vector3 tooltipOffset = new Vector3(0f, 2.25f, -0.2f);
    [SerializeField] Vector3 tooltipTextLocalPosition = new Vector3(0f, 0.12f, -0.05f);
    [SerializeField] Vector3 tooltipBackgroundScale = new Vector3(1.82f, 1.35f, 1f);
    [SerializeField] Vector2 tooltipTextBoxSize = new Vector2(4.25f, 1.35f);
    [SerializeField, Min(0.1f)] float tooltipFontSize = 2.25f;

    MapNode targetNode;
    bool isOpen;
    bool playerNearby;
    bool mouseHovered;
    bool entering;
    Renderer legacyRenderer;
    [SerializeField] Transform visualRoot;
    [SerializeField] SpriteRenderer doorRenderer;
    [SerializeField] SpriteRenderer iconRenderer;
    [SerializeField] Transform tooltipRoot;
    [SerializeField] TextMeshPro tooltipText;
    Vector3 visualRestPosition;
    Coroutine blockedRoutine;

    static Sprite tooltipSprite;

    void Awake()
    {
        legacyRenderer = GetComponent<Renderer>();
        if (legacyRenderer != null)
            legacyRenderer.enabled = false;
    }

    public void Configure(MapNode node, bool open)
    {
        targetNode = node;
        isOpen = open;
        entering = false;
        gameObject.SetActive(open && node != null);

        if (!gameObject.activeSelf)
        {
            HideTooltip();
            return;
        }

        EnsureDoorVisual();
        ApplyDoorVisual();
        EnsureInteractionCollider();
        UpdateTooltip();
    }

    public void CopyPresentationFrom(DoorTrigger template)
    {
        if (template == null)
            return;

        lockedColor = template.lockedColor;
        blockedColor = template.blockedColor;
        doorWorldScale = template.doorWorldScale;
        tooltipHeight = template.tooltipHeight;
        doorSpriteOverride = template.doorSpriteOverride;
        roomIconSpriteOverride = template.roomIconSpriteOverride;
        tooltipSpriteOverride = template.tooltipSpriteOverride;
        iconLocalPosition = template.iconLocalPosition;
        tooltipOffset = template.tooltipOffset;
        tooltipTextLocalPosition = template.tooltipTextLocalPosition;
        tooltipBackgroundScale = template.tooltipBackgroundScale;
        tooltipTextBoxSize = template.tooltipTextBoxSize;
        tooltipFontSize = template.tooltipFontSize;
    }

    void Update()
    {
        if (!isOpen || targetNode == null || entering)
            return;

        bool showTooltip = playerNearby || mouseHovered;
        if (showTooltip)
            ShowTooltip();
        else
            HideTooltip();

        if (!playerNearby)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame))
            return;

        if (!BodyConditionUtility.CanPass(targetNode))
        {
            PlayBlockedFeedback();
            return;
        }

        if (!MapRunState.BeginRoom(targetNode))
        {
            PlayBlockedFeedback();
            return;
        }

        entering = true;
        BodyConditionUtility.LockRequiredMissingSlot(targetNode);
        HideTooltip();
        StartCoroutine(OpenDoorAndEnterRoutine(SceneNameFor(targetNode)));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerNearby = true;
        ShowTooltip();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerNearby = false;
        if (!mouseHovered)
            HideTooltip();
    }

    void OnMouseEnter()
    {
        mouseHovered = true;
        ShowTooltip();
    }

    void OnMouseExit()
    {
        mouseHovered = false;
        if (!playerNearby)
            HideTooltip();
    }

    void OnDisable()
    {
        playerNearby = false;
        mouseHovered = false;
        HideTooltip();
    }

    void OnDestroy()
    {
        if (tooltipRoot != null)
            Destroy(tooltipRoot.gameObject);
    }

    void EnsureDoorVisual()
    {
        if (legacyRenderer == null)
            legacyRenderer = GetComponent<Renderer>();
        if (legacyRenderer != null)
            legacyRenderer.enabled = false;

        if (visualRoot == null)
        {
            Transform existing = transform.Find("_DoorVisual");
            if (existing != null)
                visualRoot = existing;
        }

        if (visualRoot == null)
        {
            GameObject visualObject = new GameObject("_DoorVisual");
            visualRoot = visualObject.transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        doorRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (doorRenderer == null)
            doorRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        doorRenderer.sortingOrder = 80;

        Transform iconTransform = visualRoot.Find("RoomIcon");
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("RoomIcon");
            iconTransform = iconObject.transform;
            iconTransform.SetParent(visualRoot, false);
        }

        iconRenderer = iconTransform.GetComponent<SpriteRenderer>();
        if (iconRenderer == null)
            iconRenderer = iconTransform.gameObject.AddComponent<SpriteRenderer>();
        iconRenderer.sortingOrder = 81;

        DoorSpriteCatalog layout = DoorSpriteCatalog.Load();
        float visualScale = layout != null ? layout.doorVisualScale : doorWorldScale;
        Vector2 visualOffset = layout != null ? layout.doorVisualOffset : Vector2.zero;
        iconTransform.localPosition = layout != null ? layout.iconLocalOffset : iconLocalPosition;

        // The door root carries a non-uniform scale; compensate so the visual ends up at the
        // authored world size/position regardless of that root scale.
        float parentX = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float parentY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
        visualRoot.localScale = new Vector3(visualScale / parentX, visualScale / parentY, 1f);
        visualRoot.localPosition = new Vector3(visualOffset.x / parentX, visualOffset.y / parentY, 0f);
        visualRestPosition = visualRoot.localPosition;
    }

    void ApplyDoorVisual()
    {
        DoorSpriteCatalog catalog = DoorSpriteCatalog.Load();
        Sprite doorSprite = doorSpriteOverride != null ? doorSpriteOverride : (catalog != null ? catalog.DoorFor(targetNode) : null);
        Sprite iconSprite = roomIconSpriteOverride != null ? roomIconSpriteOverride : (catalog != null ? catalog.IconFor(targetNode) : null);

        if (doorRenderer != null)
        {
            doorRenderer.sprite = doorSprite;
            doorRenderer.color = isOpen ? Color.white : lockedColor;
            doorRenderer.enabled = doorSprite != null;
        }

        if (iconRenderer != null)
        {
            iconRenderer.sprite = iconSprite;
            iconRenderer.color = Color.white;
            iconRenderer.enabled = iconSprite != null;
            if (iconSprite != null)
            {
                float iconHeight = Mathf.Max(0.01f, iconSprite.bounds.size.y);
                float targetHeight = catalog != null ? catalog.iconLocalHeight : 0.62f;
                float scale = targetHeight / iconHeight;
                iconRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    void EnsureInteractionCollider()
    {
        DoorSpriteCatalog layout = DoorSpriteCatalog.Load();
        float parentX = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float parentY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));

        // Authored polygon trigger (preferred). Points are stored as world-offset from the
        // door root, so divide by the root scale to express them in collider-local space.
        if (layout != null && layout.doorColliderPath != null && layout.doorColliderPath.Length >= 3)
        {
            BoxCollider2D legacyBox = GetComponent<BoxCollider2D>();
            if (legacyBox != null)
                Destroy(legacyBox);

            PolygonCollider2D polygon = GetComponent<PolygonCollider2D>();
            if (polygon == null)
                polygon = gameObject.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = true;

            Vector2[] path = new Vector2[layout.doorColliderPath.Length];
            for (int i = 0; i < path.Length; i++)
                path[i] = new Vector2(layout.doorColliderPath[i].x / parentX, layout.doorColliderPath[i].y / parentY);
            polygon.pathCount = 1;
            polygon.SetPath(0, path);
            return;
        }

        // Box fallback.
        PolygonCollider2D stalePolygon = GetComponent<PolygonCollider2D>();
        if (stalePolygon != null)
            Destroy(stalePolygon);

        BoxCollider2D interactionCollider = GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider2D>();
        interactionCollider.isTrigger = true;

        if (layout != null)
        {
            interactionCollider.size = new Vector2(layout.doorColliderSize.x / parentX, layout.doorColliderSize.y / parentY);
            interactionCollider.offset = new Vector2(layout.doorColliderOffset.x / parentX, layout.doorColliderOffset.y / parentY);
            return;
        }

        if (doorRenderer == null || doorRenderer.sprite == null)
            return;

        Vector2 spriteSize = doorRenderer.sprite.bounds.size * doorWorldScale;
        interactionCollider.size = new Vector2(spriteSize.x / parentX, spriteSize.y / parentY);
        interactionCollider.offset = Vector2.zero;
    }

    void EnsureTooltip()
    {
        if (tooltipRoot == null)
        {
            Transform existingTooltip = transform.Find("_DoorTooltip");
            if (existingTooltip != null)
                tooltipRoot = existingTooltip;
        }

        // 하이어라키에 직접 만들어 둔 툴팁(BalloonBackground + 텍스트 자식이 있는 경우)이면
        // 코드가 위치·크기·폰트를 덮어쓰지 않는다. 에디터에서 직접 조정한 값을 그대로 유지.
        bool authored = tooltipRoot != null
            && tooltipRoot.Find("BalloonBackground") != null
            && tooltipRoot.GetComponentInChildren<TextMeshPro>(true) != null;

        if (tooltipRoot == null)
        {
            GameObject rootObject = new GameObject("_DoorTooltip");
            tooltipRoot = rootObject.transform;
            tooltipRoot.SetParent(transform, false);
        }

        tooltipRoot.rotation = Quaternion.identity;
        if (!authored)
            tooltipRoot.localScale = Vector3.one;

        SpriteRenderer background = tooltipRoot.GetComponentInChildren<SpriteRenderer>(true);
        if (background == null)
        {
            GameObject backgroundObject = new GameObject("BalloonBackground");
            backgroundObject.transform.SetParent(tooltipRoot, false);
            background = backgroundObject.AddComponent<SpriteRenderer>();
        }
        if (background.sprite == null || !authored)
            background.sprite = tooltipSpriteOverride != null ? tooltipSpriteOverride : TooltipSprite();
        background.color = Color.white;
        background.sortingOrder = 300;
        if (!authored)
            background.transform.localScale = tooltipBackgroundScale;

        if (tooltipText == null)
            tooltipText = tooltipRoot.GetComponentInChildren<TextMeshPro>(true);
        if (tooltipText == null)
        {
            GameObject textObject = new GameObject("BalloonText");
            textObject.transform.SetParent(tooltipRoot, false);
            tooltipText = textObject.AddComponent<TextMeshPro>();
        }
        tooltipText.font = UIThinDungFont.Get();
        tooltipText.sortingOrder = 301;
        if (!authored)
        {
            tooltipText.transform.localPosition = tooltipTextLocalPosition;
            tooltipText.fontSize = tooltipFontSize;
            tooltipText.fontStyle = FontStyles.Bold;
            tooltipText.alignment = TextAlignmentOptions.Center;
            tooltipText.color = new Color(0.24f, 0.14f, 0.09f, 1f);
            tooltipText.sortingOrder = 301;
            tooltipText.textWrappingMode = TextWrappingModes.NoWrap;
            tooltipText.rectTransform.sizeDelta = tooltipTextBoxSize;
        }

        tooltipRoot.gameObject.SetActive(false);
    }

    void ShowTooltip()
    {
        if (!isOpen || targetNode == null || entering)
            return;

        EnsureTooltip();
        UpdateTooltip();
        tooltipRoot.position = transform.position + tooltipOffset;
        tooltipRoot.gameObject.SetActive(true);
    }

    void HideTooltip()
    {
        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);
    }

    void UpdateTooltip()
    {
        if (tooltipText == null || targetNode == null)
            return;

        bool canEnter = BodyConditionUtility.CanPass(targetNode);
        string secondLine;
        if (canEnter)
            secondLine = "[Enter] 입장";
        else if (targetNode.roomType == RoomType.ConditionCombat)
            secondLine = ConditionLabel(targetNode.conditionType) + " 제거 후 입장";
        else
            secondLine = "입장 불가";

        tooltipText.text = DoorTitle(targetNode) + "\n" + secondLine;
        tooltipText.color = canEnter
            ? new Color(0.24f, 0.14f, 0.09f, 1f)
            : new Color(0.55f, 0.16f, 0.13f, 1f);
    }

    void PlayBlockedFeedback()
    {
        if (blockedRoutine != null)
            StopCoroutine(blockedRoutine);
        blockedRoutine = StartCoroutine(BlockedFeedbackRoutine());
    }

    IEnumerator OpenDoorAndEnterRoutine(string sceneName)
    {
        DoorSpriteCatalog catalog = DoorSpriteCatalog.Load();
        AudioClip openClip = catalog != null ? catalog.doorOpenSfx : null;
        if (openClip != null)
        {
            SoundManager.PlaySfx(openClip, 0f, 1f);
            yield return new WaitForSecondsRealtime(openClip.length);
        }

        RoomPageTransition.LoadScene(sceneName);
    }

    IEnumerator BlockedFeedbackRoutine()
    {
        EnsureDoorVisual();
        float duration = 0.38f;
        float elapsed = 0f;
        Color baseColor = Color.white;
        Color rejectColor = Color.Lerp(Color.white, blockedColor, 0.48f);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float damping = 1f - t;
            float offset = Mathf.Sin(t * Mathf.PI * 8f) * 0.13f * damping;
            visualRoot.localPosition = visualRestPosition + Vector3.right * offset;
            doorRenderer.color = Color.Lerp(rejectColor, baseColor, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        visualRoot.localPosition = visualRestPosition;
        doorRenderer.color = baseColor;
        blockedRoutine = null;
        ShowTooltip();
    }

    string SceneNameFor(MapNode node)
    {
        switch (node.roomType)
        {
            case RoomType.Boss: return bossSceneName;
            case RoomType.MiddleBoss: return middleBossSceneName;
            case RoomType.FinalBoss: return finalBossSceneName;
            case RoomType.Treasure: return treasureSceneName;
            case RoomType.Shop: return shopSceneName;
            case RoomType.Supply: return supplySceneName;
            case RoomType.Event: return eventSceneName;
            case RoomType.Challenge: return challengeSceneName;
            default: return roomSceneName;
        }
    }

    static string DoorTitle(MapNode node)
    {
        if (node == null)
            return "잠긴 문";

        if (node.roomType == RoomType.ConditionCombat)
            return ConditionLabel(node.conditionType) + " 봉인 전투방";

        switch (node.roomType)
        {
            case RoomType.Boss: return "보스방";
            case RoomType.MiddleBoss: return "중간보스";
            case RoomType.FinalBoss: return "최종보스";
            case RoomType.Treasure: return "신체방";
            case RoomType.Shop: return "상점";
            case RoomType.Challenge: return "도전방";
            case RoomType.Supply: return "신체방";
            case RoomType.Event: return "도전방";
            case RoomType.Start: return "시작방";
            default: return "전투방";
        }
    }

    static string ConditionLabel(NodeConditionType condition)
    {
        switch (condition)
        {
            case NodeConditionType.NoLeftArm: return "왼쪽 팔";
            case NodeConditionType.NoRightArm: return "오른쪽 팔";
            case NodeConditionType.NoLeftEye: return "왼쪽 눈";
            case NodeConditionType.NoRightEye: return "오른쪽 눈";
            case NodeConditionType.NoLeftLeg: return "왼쪽 다리";
            case NodeConditionType.NoRightLeg: return "오른쪽 다리";
            default: return "조건";
        }
    }

    static Sprite TooltipSprite()
    {
        if (tooltipSprite != null)
            return tooltipSprite;

        const int width = 256;
        const int height = 112;
        const int radius = 15;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "DoorTooltipBalloon_Runtime";
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color border = new Color(0.32f, 0.19f, 0.12f, 0.96f);
        Color fill = new Color(0.96f, 0.89f, 0.77f, 0.97f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inTail = y < 18 && x >= 112 - y / 2 && x <= 144 + y / 2;
                int bodyY = y - 14;
                bool inBody = bodyY >= 0 && bodyY < height - 14 && InRoundedRect(x, bodyY, width, height - 14, radius);
                bool inInner = bodyY >= 3 && bodyY < height - 17 && InRoundedRect(x - 3, bodyY - 3, width - 6, height - 20, radius - 3);
                texture.SetPixel(x, y, inTail || inBody ? (inInner && !inTail ? fill : border) : clear);
            }
        }

        texture.Apply();
        tooltipSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.08f), 100f);
        tooltipSprite.name = "DoorTooltipBalloon_Runtime";
        return tooltipSprite;
    }

    static bool InRoundedRect(int x, int y, int width, int height, int radius)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return false;

        int nearestX = Mathf.Clamp(x, radius, width - radius - 1);
        int nearestY = Mathf.Clamp(y, radius, height - radius - 1);
        int dx = x - nearestX;
        int dy = y - nearestY;
        return dx * dx + dy * dy <= radius * radius;
    }
}
