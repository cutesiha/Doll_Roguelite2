using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DoorTrigger : MonoBehaviour
{
    [Header("Destination Scenes")]
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField] string middleBossSceneName = "MiddleBossScene";
    [SerializeField] string finalBossSceneName = "BookBossScene";
    [SerializeField] string challengeSceneName = "ChallengeScene";
    [SerializeField] string supplySceneName = "PresentScene";
    [SerializeField] string eventSceneName = "EventScene";
    [SerializeField] string treasureSceneName = "TreasureRoomScene";
    [SerializeField] string shopSceneName = "ShopScene";

    [Header("Door Feedback")]
    [SerializeField] Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color blockedColor = new Color(0.72f, 0.20f, 0.18f, 1f);
    [SerializeField, Min(0.1f)] float doorWorldScale = 1.8f;
    [SerializeField, Min(0.1f)] float tooltipHeight = 2.25f;
    [Header("Proximity Highlight")]
    // 근처(거리)만 가면 살구색 하이라이트 + 반짝이 + Enter 상호작용 활성화.
    [SerializeField, Min(0.1f)] float proximityRadius = 2.6f;
    [SerializeField] Color nearHighlightColor = new Color(1f, 0.80f, 0.62f, 1f);  // 살구색
    // ShopScene 문이 창백해 보여서 플레이어 몸 색(푸른색)으로 보정.
    [SerializeField] bool tintShopDoorBlue = true;
    // #3: 너무 진하지 않은 연한 파란틴트.
    [SerializeField] Color shopDoorTint = new Color(0.68f, 0.84f, 1f, 1f);
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
    Transform playerTransform;
    ItemPickupSparkle doorSparkle;

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

        // 문 루트가 비균등 스케일(예: 1.5 x 0.4)이면 폴리곤 콜라이더가 왜곡되어
        // 문과 어긋난다. 루트 스케일을 1로 정규화하면 카탈로그의 폴리곤 예시가
        // 왜곡 없이 그대로(월드 좌표=로컬 좌표) 적용된다. 비주얼은 _DoorVisual 자식이
        // 카탈로그 크기로 별도 처리하므로 외형은 영향받지 않는다.
        transform.localScale = Vector3.one;

        EnsureDoorVisual();
        ApplyDoorVisual();
        EnsureInteractionCollider();
        EnsureBlockingCollider();
        DisableTooltip();
    }

    // 문을 물리적으로 막는 솔리드 콜라이더(트리거 아님). 입장 감지용 트리거는 문 루트에 그대로 두고,
    // 막는 콜라이더는 별도 자식 "_DoorBlocker"에 둔다(루트의 GetComponent 콜라이더 로직과 충돌 방지).
    // 핵심: 블로커 레이어를 '벽과 동일'하게 맞춰, 플레이어가 벽에 막히듯 문에도 반드시 막히게 한다
    // (레이어 충돌 매트릭스가 어떻든 벽이 막으면 문도 막힘).
    void EnsureBlockingCollider()
    {
        DoorSpriteCatalog layout = DoorSpriteCatalog.Load();
        float parentX = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float parentY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));

        Transform existing = transform.Find("_DoorBlocker");
        GameObject blocker = existing != null ? existing.gameObject : new GameObject("_DoorBlocker");
        blocker.transform.SetParent(transform, false);
        blocker.transform.localPosition = Vector3.zero;
        blocker.transform.localRotation = Quaternion.identity;
        blocker.transform.localScale = Vector3.one;
        blocker.layer = ResolveBlockingLayer();

        // #2: 문 모양에 맞추기 위해, 상호작용 트리거와 동일한 폴리곤 경로를 솔리드로 사용한다.
        if (layout != null && layout.doorColliderPath != null && layout.doorColliderPath.Length >= 3)
        {
            BoxCollider2D staleBox = blocker.GetComponent<BoxCollider2D>();
            if (staleBox != null)
                Destroy(staleBox);

            PolygonCollider2D polygon = blocker.GetComponent<PolygonCollider2D>();
            if (polygon == null)
                polygon = blocker.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = false;   // 솔리드 → 물리적으로 막음, 문 모양과 일치

            Vector2[] path = new Vector2[layout.doorColliderPath.Length];
            for (int i = 0; i < path.Length; i++)
                path[i] = new Vector2(layout.doorColliderPath[i].x / parentX, layout.doorColliderPath[i].y / parentY);
            polygon.pathCount = 1;
            polygon.SetPath(0, path);
            return;
        }

        // 폴백: 경로가 없으면 박스.
        PolygonCollider2D stalePolygon = blocker.GetComponent<PolygonCollider2D>();
        if (stalePolygon != null)
            Destroy(stalePolygon);

        BoxCollider2D box = blocker.GetComponent<BoxCollider2D>();
        if (box == null)
            box = blocker.AddComponent<BoxCollider2D>();
        box.isTrigger = false;

        if (layout != null && layout.doorColliderSize.x > 0.01f && layout.doorColliderSize.y > 0.01f)
        {
            box.size = new Vector2(layout.doorColliderSize.x / parentX, layout.doorColliderSize.y / parentY);
            box.offset = new Vector2(layout.doorColliderOffset.x / parentX, layout.doorColliderOffset.y / parentY);
        }
        else if (doorRenderer != null && doorRenderer.sprite != null)
        {
            Vector2 spriteSize = doorRenderer.sprite.bounds.size * doorWorldScale;
            box.size = new Vector2(spriteSize.x / parentX, spriteSize.y / parentY);
            box.offset = Vector2.zero;
        }
        else
        {
            box.size = new Vector2(2.3f / parentX, 2.7f / parentY);
            box.offset = Vector2.zero;
        }
    }

    // 방의 벽과 동일한 레이어를 찾는다. 벽이 플레이어를 막고 있으므로 같은 레이어면 문도 확실히 막힌다.
    // 손으로 배치한 방(예: ChallengeRewardScene)은 벽 오브젝트 이름이 "Wall_Left" 같은 정확한
    // 이름이 아니라 "wall", "wall (1)" 처럼 다르게 지어져 있을 수 있어, 이름이 "wall"로
    // 시작하는 모든 오브젝트를 대상으로 찾는다(대소문자 무시).
    int ResolveBlockingLayer()
    {
        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            string objectName = collider.gameObject.name;
            if (!string.IsNullOrEmpty(objectName)
                && objectName.StartsWith("wall", System.StringComparison.OrdinalIgnoreCase))
                return collider.gameObject.layer;
        }

        return gameObject.layer;
    }

    // task17: 문 위 말풍선(문구)은 표시하지 않는다. 방 종류 아이콘(도형)은 그대로 유지.
    // 하이어라키/템플릿에 미리 만들어 둔 _DoorTooltip 자식이 있으면 비활성화한다.
    void DisableTooltip()
    {
        if (tooltipRoot == null)
        {
            Transform existing = transform.Find("_DoorTooltip");
            if (existing != null)
                tooltipRoot = existing;
        }

        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);
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

        // 거리 기반 근접 판정 → 근처에 오기만 하면 살구색 하이라이트 + 반짝이 + Enter 상호작용.
        ResolvePlayer();
        ApplyDoorDepthSorting();
        float distance = playerTransform != null
            ? Vector2.Distance(playerTransform.position, transform.position)
            : float.PositiveInfinity;
        bool nearby = distance <= proximityRadius;

        if (blockedRoutine == null)
            ApplyProximityHighlight(nearby);   // 막힘 피드백 애니메이션 중엔 색 안 건드림
        SetDoorSparkle(nearby);

        if (!nearby)
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
        ApplyDoorDepthSorting();

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
        // task17: 말풍선 문구 비표시. 혹시 남아있는 툴팁이 있으면 숨긴다.
        DisableTooltip();
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
        SoundManager.PlayDoorBlocked();

        if (blockedRoutine != null)
            StopCoroutine(blockedRoutine);
        blockedRoutine = StartCoroutine(BlockedFeedbackRoutine());
    }

    void ResolvePlayer()
    {
        if (playerTransform != null)
            return;
        GameObject go = GameObject.FindWithTag("Player");
        if (go != null)
            playerTransform = go.transform;
    }

    // 예전에는 플레이어가 문보다 위(뒤)에 있으면 문을 앞으로 그려서 "문 뒤로 걸어들어가는" 느낌을
    // 냈는데, 그 경계값 근처에서 캐릭터 머리 위쪽이 문 그림에 가려지는 문제가 있었다.
    // 이제는 위치와 무관하게 캐릭터가 항상 문보다 앞에 보이도록 고정한다.
    void ApplyDoorDepthSorting()
    {
        if (doorRenderer == null)
            return;

        if (playerTransform == null)
            ResolvePlayer();

        int doorOrder = PlayerTopSortingOrder() - 2;
        doorRenderer.sortingOrder = doorOrder;
        if (iconRenderer != null)
            iconRenderer.sortingOrder = doorOrder + 1;
    }

    int PlayerTopSortingOrder()
    {
        if (playerTransform == null)
            return 80;

        SpriteRenderer[] renderers = playerTransform.GetComponentsInChildren<SpriteRenderer>(true);
        int order = 80;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                order = Mathf.Max(order, renderers[i].sortingOrder);
        return order;
    }

    // 문 기본 색: 잠김이면 회색, 열림이고 ShopScene이면 푸른색 보정(창백함 방지), 그 외 흰색.
    bool IsShopDoor()
    {
        return tintShopDoorBlue && SceneManager.GetActiveScene().name == "ShopScene";
    }

    Color BaseDoorColor()
    {
        if (!isOpen)
            return lockedColor;
        if (IsShopDoor())
            return shopDoorTint;
        return Color.white;
    }

    // 근처면 살구색 하이라이트. 단 상점 문은 근접해도 파란틴트를 유지한다(#4).
    // 색은 문 본체 + 아이콘 양쪽에 적용한다(#3: 아이콘에도 틴트).
    void ApplyProximityHighlight(bool nearby)
    {
        Color c;
        if (!isOpen)
            c = lockedColor;
        else if (IsShopDoor())
            c = shopDoorTint;                                 // #4: 상점은 근접해도 파란 유지
        else
            c = nearby ? nearHighlightColor : Color.white;    // #5: 근처면 살구 하이라이트

        if (doorRenderer != null)
            doorRenderer.color = c;
        if (iconRenderer != null)
            iconRenderer.color = c;                           // #3: 아이콘에도 틴트
    }

    // 근처면 반짝이 파티클 표시(아이템과 동일한 ItemPickupSparkle 재사용).
    void SetDoorSparkle(bool active)
    {
        if (active && doorSparkle == null)
            EnsureDoorSparkle();
        if (doorSparkle != null && doorSparkle.gameObject.activeSelf != active)
            doorSparkle.gameObject.SetActive(active);
    }

    void EnsureDoorSparkle()
    {
        Transform existing = transform.Find("_DoorSparkle");
        GameObject go = existing != null ? existing.gameObject : new GameObject("_DoorSparkle");
        go.transform.SetParent(transform, false);

        DoorSpriteCatalog catalog = DoorSpriteCatalog.Load();
        Vector2 center = catalog != null ? catalog.doorVisualOffset : Vector2.zero;
        go.transform.localPosition = new Vector3(center.x, center.y + 0.3f, -0.05f);
        go.transform.localScale = Vector3.one * 2.2f;

        doorSparkle = go.GetComponent<ItemPickupSparkle>();
        if (doorSparkle == null)
            doorSparkle = go.AddComponent<ItemPickupSparkle>();
        doorSparkle.Configure(90);
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
        Color baseColor = BaseDoorColor();
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
