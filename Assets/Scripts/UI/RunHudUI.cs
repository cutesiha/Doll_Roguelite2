using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RunHudUI : MonoBehaviour
{
    public static bool ShowControlHintsOnNextRoom { get; set; }

    public System.Func<bool> mapKeyAllowed;
    public System.Func<bool> inventoryKeyAllowed;
    public System.Func<bool> menuKeyAllowed;

    [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] TMP_FontAsset uiFont;
    [SerializeField] Sprite roundedPanelSprite;
    [SerializeField] Sprite roundedButtonSprite;
    [SerializeField] Sprite roundedPipSprite;
    [SerializeField] Sprite circleSprite;
    [SerializeField] Sprite hpPipSprite;
    [Header("Run Map Icons")]
    [SerializeField] Sprite startRoomMapIcon;
    [SerializeField] Sprite treasureMapIcon;
    [SerializeField] Sprite challengeMapIcon;
    [SerializeField] Sprite middleBossMapIcon;
    [SerializeField] Sprite mainBossMapIcon;
    [SerializeField] Sprite shopMapIcon;
    [SerializeField] Sprite noLeftArmMapIcon;
    [SerializeField] Sprite noRightArmMapIcon;
    [SerializeField] Sprite noLeftEyeMapIcon;
    [SerializeField] Sprite noRightEyeMapIcon;
    [SerializeField] Sprite noLeftLegMapIcon;
    [SerializeField] Sprite noRightLegMapIcon;
    [Header("Run Map Scroll")]
    [SerializeField, Min(0.01f)] float mapWheelStep = 0.14f;
    [SerializeField, Min(1f)] float mapWheelLerpSpeed = 16f;

    Canvas canvas;
    RectTransform rootRect;
    Button mapButton;
    Button inventoryButton;
    Button menuButton;
    GameObject mapOverlay;
    RectTransform mapPanel;
    ScrollRect mapScrollRect;
    RectTransform mapViewport;
    RectTransform mapContent;
    RectTransform mapTree;
    RectTransform miniMapContent;
    GameObject waveHud;
    TextMeshProUGUI waveLabel;
    TextMeshProUGUI waveClearLabel;
    TextMeshProUGUI diaryLabel;
    RectTransform jewelPopupHud;
    Image jewelPopupIcon;
    TextMeshProUGUI jewelPopupName;
    CanvasGroup jewelPopupCanvasGroup;
    Coroutine jewelPopupRoutine;
    RectTransform bossHpHud;
    Image bossHpFill;
    TextMeshProUGUI bossNameLabel;
    TextMeshProUGUI bossHpText;
    const float BossHpTrackWidth = 864f;
    const float BossHpTrackHeight = 30f;
    RectTransform judgementTimerHud;
    Image judgementTimerFill;
    TextMeshProUGUI judgementTimerSeconds;
    TextMeshProUGUI judgementTimerCaption;
    Color judgementTimerBaseColor = new Color(1f, 0.36f, 0.12f, 1f);
    // req: 좌측 상단 캐릭터 HP UI 바로 아래(TopLeft 기준).
    static readonly Vector2 JudgementTimerAnchoredPos = new Vector2(28f, -214f);
    static readonly Color JudgementTimerHigh = new Color(1f, 0.36f, 0.12f, 1f);
    static readonly Color JudgementTimerLow = new Color(0.88f, 0.08f, 0.06f, 1f);
    RectTransform bossPartsHud;
    readonly RectTransform[] bossPartRows = new RectTransform[3];
    readonly Image[] bossPartFills = new Image[3];
    readonly TextMeshProUGUI[] bossPartNames = new TextMeshProUGUI[3];
    readonly TextMeshProUGUI[] bossPartHpTexts = new TextMeshProUGUI[3];
    RectTransform collectedWordsPanel;
    TextMeshProUGUI collectedWordsLabel;
    const float BossPartTrackWidth = 820f;
    const float BossPartTrackHeight = 28f;
    static readonly Color[] BossPartColors =
    {
        new Color(0.85f, 0.20f, 0.20f, 1f),
        new Color(0.92f, 0.58f, 0.20f, 1f),
        new Color(0.30f, 0.62f, 0.92f, 1f)
    };
    GameObject mapControlHint;
    GameObject menuControlHint;
    readonly List<Image> waveDots = new List<Image>();
    readonly List<HudPipGroup> hudPipGroups = new List<HudPipGroup>();
    Coroutine waveClearRoutine;
    // task8: 장착 아이템 표시 패널
    EquippedItemHudPanel equippedItemPanel;
    Coroutine mapAnimationRoutine;
    bool suppressInventoryOutsideClick;
    int lastMiniMapCurrentId = -999;
    Vector2 miniMapTargetOffset;
    Vector2 authoredMapPanelPosition;
    bool hasAuthoredMapPanelPosition;

    static RunHudUI instance;
    static bool sceneHookRegistered;

    static readonly Color PanelColor = new Color(0.91f, 0.86f, 0.78f, 0.98f);
    static readonly Color HudPanelColor = new Color(0.91f, 0.86f, 0.78f, 0.96f);
    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.42f);
    static readonly Color LineColor = new Color(0.17f, 0.15f, 0.13f, 1f);
    static readonly Color SoftLineColor = new Color(0.17f, 0.15f, 0.13f, 0.82f);
    static readonly Color TextColor = new Color(0.17f, 0.15f, 0.13f, 1f);
    static readonly Color MutedTextColor = new Color(0.35f, 0.31f, 0.28f, 1f);
    static readonly Color AccentColor = new Color(0.88f, 0.48f, 0.24f, 1f);
    static readonly Color EmptyPipColor = new Color(0.17f, 0.15f, 0.13f, 0.28f);
    static readonly Color MapBrown = new Color(0.27f, 0.16f, 0.09f, 1f);
    static readonly Color HiddenMapNodeColor = new Color(0.29f, 0.25f, 0.22f, 1f);

    static readonly Color PipYellow = new Color(1.00f, 0.85f, 0.23f, 1f);
    static readonly Color PipLightOrange = new Color(0.96f, 0.65f, 0.14f, 1f);
    static readonly Color PipOrange = new Color(0.94f, 0.62f, 0.15f, 1f);
    static readonly Color PipRed = new Color(0.89f, 0.29f, 0.29f, 1f);
    static readonly Color PipScarlet = new Color(0.75f, 0.08f, 0.06f, 1f);

    static readonly Color ColCurrent = new Color(1.00f, 0.65f, 0.10f, 1f);
    static readonly Color ColCleared = new Color(0.20f, 0.20f, 0.20f, 1f);
    static readonly Color ColFree = new Color(0.25f, 0.80f, 0.35f, 1f);
    static readonly Color ColNoLeftArm = new Color(0.85f, 0.20f, 0.15f, 1f);
    static readonly Color ColNoRightEye = new Color(0.65f, 0.20f, 0.85f, 1f);
    static readonly Color ColNoLeftLeg = new Color(1.00f, 0.50f, 0.20f, 1f);
    static readonly Color ColNoRightLeg = new Color(0.20f, 0.60f, 1.00f, 1f);
    static readonly Color ColBoss = new Color(0.90f, 0.75f, 0.10f, 1f);
    static readonly Color ColSupply = new Color(0.20f, 0.85f, 0.90f, 1f);
    static readonly Color ColEvent = new Color(0.90f, 0.45f, 0.80f, 1f);
    static readonly Color ColTreasure = new Color(1.00f, 0.78f, 0.16f, 1f);
    static readonly Color ColShop = new Color(0.42f, 0.72f, 0.92f, 1f);
    static readonly Color ColRouteOnly = new Color(0.45f, 0.45f, 0.45f, 1f);
    static readonly Color ColHidden = new Color(0.22f, 0.22f, 0.22f, 1f);
    const float MapScrollLeftRightMargin = 54f;
    const float MapScrollBottomMargin = 130f;
    const float MapScrollTopMargin = 124f;
    const float MapViewportTopGuard = 80f;
    const float MapViewportBottomGuard = 120f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BootstrapRunHud()
    {
        if (sceneHookRegistered)
            return;

        SceneManager.sceneLoaded += EnsureRunHudForScene;
        sceneHookRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapActiveSceneRunHud()
    {
        EnsureRunHudForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void EnsureRunHudForScene(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

        RunHudUI[] existing = FindObjectsByType<RunHudUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (IsRunHudExcludedScene(scene.name))
        {
            for (int i = 0; i < existing.Length; i++)
                if (existing[i] != null)
                    Destroy(existing[i].gameObject);

            return;
        }

        if (existing.Length > 0)
            return;

        RunHudUI prefab = Resources.Load<RunHudUI>("RunHudCanvas");
        if (prefab != null)
        {
            GameObject hudInstance = Instantiate(prefab.gameObject);
            hudInstance.name = "RunHudCanvas";
            return;
        }

        GameObject hud = new GameObject("RunHudCanvas");
        hud.AddComponent<RectTransform>();
        hud.AddComponent<RunHudUI>();
    }

    static bool IsRunHudExcludedScene(string sceneName)
    {
        return sceneName == "StartScene";
    }

void Awake()
    {
        EnsureRootCanvasComponents(false);

        if (Application.isPlaying)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // RoomScene에서 편집한 HUD 계층을 그대로 확인할 수 있게 현재 씬 소속으로 유지한다.
            instance = this;
        }

        EnsureEventSystem();
        EnsureBuilt();
        CloseMapImmediate();
        ShowPendingControlHintsIfNeeded();

        // InventoryCanvas가 비활성 상태면 OpenPanel 직후 Start가 늦게 실행되어 다시 닫히는 문제를 막는다.
        InventoryUI inventoryUI = GetComponentInChildren<InventoryUI>(true);
        if (inventoryUI != null && !inventoryUI.gameObject.activeSelf)
            inventoryUI.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        RunUiPauseManager.SetPaused("Map", false);
    }

    void Update()
    {
        UpdateHudState();
        UpdateMiniMap();

        if (!Application.isPlaying)
            return;

        HandleMapHotkey();
        HandleMenuHotkey();
        HandleInventoryHotkey();
        HandleInventoryOutsideClick();
    }

    public void Rebuild()
    {
        EnsureRootCanvasComponents(true);

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "InventoryCanvas")
                continue;

            DestroyUiObject(child.gameObject);
        }

        hudPipGroups.Clear();
        mapButton = null;
        inventoryButton = null;
        menuButton = null;
        mapOverlay = null;
        mapPanel = null;
        mapScrollRect = null;
        mapViewport = null;
        mapContent = null;
        mapTree = null;
        miniMapContent = null;
        hasAuthoredMapPanelPosition = false;
        waveLabel = null;
        waveClearLabel = null;
        diaryLabel = null;
        bossHpHud = null;
        bossHpFill = null;
        bossNameLabel = null;
        bossHpText = null;
        bossPartsHud = null;
        collectedWordsPanel = null;
        collectedWordsLabel = null;
        for (int i = 0; i < 3; i++)
        {
            bossPartRows[i] = null;
            bossPartFills[i] = null;
            bossPartNames[i] = null;
            bossPartHpTexts[i] = null;
        }
        mapControlHint = null;
        menuControlHint = null;
        waveDots.Clear();

        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        rootRect = transform as RectTransform;
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

        BuildBodyHud();
        BuildEquippedItemPanel();
        BuildWaveHud();
        BuildTopRightMapButton();
        BuildDiaryText();
        BuildBottomRightButtons();
        BuildControlHints();
        BuildMapOverlay();
        CloseMapImmediate();
        UpdateHudState();
    }

    void EnsureRootCanvasComponents(bool forceGeneratedLayout)
    {
        gameObject.SetActive(true);
        EnsureUIAssets();

        RectTransform rect = transform as RectTransform;
        bool addedRect = false;
        if (rect == null)
        {
            rect = gameObject.AddComponent<RectTransform>();
            addedRect = true;
        }

        if (forceGeneratedLayout || addedRect)
        {
            transform.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        Canvas rootCanvas = GetComponent<Canvas>();
        bool addedCanvas = false;
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
            addedCanvas = true;
        }

        if (forceGeneratedLayout || addedCanvas)
        {
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 80;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void EnsureUIAssets()
    {
#if UNITY_EDITOR
        if (uiFont == null)
            uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset");
        if (roundedPanelSprite == null)
            roundedPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/ui_round_rect_20.png");
        if (roundedButtonSprite == null)
            roundedButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/ui_round_rect_10.png");
        if (roundedPipSprite == null)
            roundedPipSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/ui_round_pip_6.png");
        if (circleSprite == null)
            circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/ui_circle.png");
        if (hpPipSprite == null)
            hpPipSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/room/hp.png");
        if (startRoomMapIcon == null)
            startRoomMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/startroom.png");
        if (treasureMapIcon == null)
            treasureMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/treasure.png");
        if (challengeMapIcon == null)
            challengeMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/challenge.png");
        if (middleBossMapIcon == null)
            middleBossMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/middleboss.png");
        if (mainBossMapIcon == null)
            mainBossMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/mainboss.png");
        if (shopMapIcon == null)
            shopMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/shop.png");
        if (noLeftArmMapIcon == null)
            noLeftArmMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/noleftarm.png");
        if (noRightArmMapIcon == null)
            noRightArmMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/norightarm.png");
        if (noLeftEyeMapIcon == null)
            noLeftEyeMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/nolefteye.png");
        if (noRightEyeMapIcon == null)
            noRightEyeMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/norighteye.png");
        if (noLeftLegMapIcon == null)
            noLeftLegMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/noleftleg.png");
        if (noRightLegMapIcon == null)
            noRightLegMapIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/mapicon/norightleg.png");
#endif
    }

    void EnsureBuilt()
    {
        canvas = GetComponent<Canvas>();
        rootRect = transform as RectTransform;

        BindExistingPrefabUi();

        bool hasCoreHud = inventoryButton != null
            && menuButton != null
            && diaryLabel != null
            && waveLabel != null
            && waveClearLabel != null
            && waveDots.Count >= 3
            && HasExistingBodyHud();

        if (!hasCoreHud)
        {
            Rebuild();
            return;
        }

        EnsureMapUiPieces();
        BindExistingPrefabUi();
        BindExistingWaveUi();
        BindExistingPipGroups();
        WireControlEvents();
        EnsureControlHints();
        UpdateHudState();
    }

    void BindExistingPrefabUi()
    {
        if (mapButton == null)
            mapButton = FindChildComponent<Button>("MapIconButton");

        if (inventoryButton == null)
            inventoryButton = FindChildComponent<Button>("InventoryIconButton");

        if (menuButton == null)
            menuButton = FindChildComponent<Button>("MenuIconButton");

        if (waveLabel == null)
            waveLabel = FindChildComponent<TextMeshProUGUI>("WaveLabel");

        if (waveClearLabel == null)
            waveClearLabel = FindChildComponent<TextMeshProUGUI>("WaveClearLabel");

        BindExistingWaveUi();

        if (diaryLabel == null)
            diaryLabel = FindChildComponent<TextMeshProUGUI>("DiaryMemoryText");

        if (mapOverlay == null)
        {
            Transform overlay = FindChildRecursive(transform, "MapOverlay");
            if (overlay != null)
                mapOverlay = overlay.gameObject;
        }

        if (mapPanel == null)
            mapPanel = FindChildComponent<RectTransform>("MapPanel");

        if (mapPanel != null && !hasAuthoredMapPanelPosition)
        {
            authoredMapPanelPosition = mapPanel.anchoredPosition;
            hasAuthoredMapPanelPosition = true;
        }

        if (mapContent == null)
            mapContent = FindChildComponent<RectTransform>("MapContent");

        if (mapTree == null)
            mapTree = FindChildComponent<RectTransform>("TreeMap");

        if (mapViewport == null)
            mapViewport = FindChildComponent<RectTransform>("MapViewport");

        if (mapScrollRect == null && mapPanel != null)
            mapScrollRect = mapPanel.GetComponentInChildren<ScrollRect>(true);

        miniMapContent = null;
    }

    void EnsureMapUiPieces()
    {
        if (mapButton == null)
        {
            BuildTopRightMapButton();
        }
        else
        {
            ConfigureMapButtonForMapIcon();
        }

        if (mapOverlay == null)
        {
            BuildMapOverlay();
            return;
        }

        EnsureMapScrollHierarchy();
    }

    void EnsureMapScrollHierarchy()
    {
        if (mapPanel == null)
            mapPanel = FindChildComponent<RectTransform>("MapPanel");
        if (mapPanel == null)
            return;

        Transform scrollTransform = FindChildRecursive(mapPanel, "MapScroll");
        if (scrollTransform == null)
        {
            GameObject scrollObject = Rect(mapPanel, "MapScroll", Anchor.Stretch, Vector2.zero, Vector2.zero);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollTransform = scrollObject.transform;
            scrollTransform.SetAsFirstSibling();
        }
        scrollTransform.gameObject.SetActive(true);
        ApplyMapScrollLayout(scrollTransform as RectTransform);

        mapScrollRect = scrollTransform.GetComponent<ScrollRect>();
        if (mapScrollRect == null)
            mapScrollRect = scrollTransform.gameObject.AddComponent<ScrollRect>();

        Transform viewportTransform = FindChildRecursive(scrollTransform, "MapViewport");
        if (viewportTransform == null)
        {
            GameObject viewportObject = Rect(scrollTransform, "MapViewport", Anchor.Stretch, Vector2.zero, Vector2.zero);
            viewportTransform = viewportObject.transform;
        }
        viewportTransform.gameObject.SetActive(true);

        mapViewport = viewportTransform as RectTransform;
        RectMask2D viewportMask = viewportTransform.GetComponent<RectMask2D>();
        if (viewportMask == null)
            viewportMask = viewportTransform.gameObject.AddComponent<RectMask2D>();
        viewportMask.padding = Vector4.zero;
        viewportMask.softness = new Vector2Int(0, 80);

        Image viewportImage = viewportTransform.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = viewportTransform.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;

        Mask legacyViewportMask = viewportTransform.GetComponent<Mask>();
        if (legacyViewportMask != null)
            DestroyUiObject(legacyViewportMask);

        if (mapContent == null)
            mapContent = FindChildComponent<RectTransform>("MapContent");
        if (mapContent == null)
        {
            GameObject contentObject = Rect(viewportTransform, "MapContent", Anchor.TopCenter, Vector2.zero, new Vector2(1040f, 1500f));
            mapContent = contentObject.GetComponent<RectTransform>();
        }
        mapContent.gameObject.SetActive(true);

        if (mapContent.parent != viewportTransform)
        {
            Vector2 contentSize = mapContent.sizeDelta;
            mapContent.SetParent(viewportTransform, false);
            mapContent.anchorMin = mapContent.anchorMax = new Vector2(0.5f, 1f);
            mapContent.pivot = new Vector2(0.5f, 1f);
            mapContent.anchoredPosition = Vector2.zero;
            mapContent.sizeDelta = contentSize;
        }

        if (mapTree == null)
        {
            Transform authoredTree = mapContent.Find("TreeMap");
            if (authoredTree != null)
                mapTree = authoredTree as RectTransform;
        }
        if (mapTree == null)
        {
            GameObject treeObject = Rect(mapContent, "TreeMap", Anchor.Stretch, Vector2.zero, Vector2.zero);
            mapTree = treeObject.GetComponent<RectTransform>();
        }
        mapTree.gameObject.SetActive(true);

        mapScrollRect.horizontal = false;
        mapScrollRect.vertical = true;
        ConfigureMapScrollRect();
        mapScrollRect.viewport = mapViewport;
        mapScrollRect.content = mapContent;
        mapScrollRect.onValueChanged.RemoveListener(RefreshMapViewportVisibility);
        mapScrollRect.onValueChanged.AddListener(RefreshMapViewportVisibility);
    }

    void ApplyMapScrollLayout(RectTransform scrollRectTransform)
    {
        if (scrollRectTransform == null)
            return;

        scrollRectTransform.offsetMin = new Vector2(MapScrollLeftRightMargin, MapScrollBottomMargin);
        scrollRectTransform.offsetMax = new Vector2(-MapScrollLeftRightMargin, -MapScrollTopMargin);
    }

    void ConfigureMapScrollRect()
    {
        if (mapScrollRect == null)
            return;

        mapScrollRect.horizontal = false;
        mapScrollRect.vertical = true;
        mapScrollRect.movementType = ScrollRect.MovementType.Clamped;
        mapScrollRect.inertia = true;
        mapScrollRect.decelerationRate = 0.03f;
        mapScrollRect.scrollSensitivity = 0f;

        SmoothMapScrollRect smoothScroll = mapScrollRect.GetComponent<SmoothMapScrollRect>();
        if (smoothScroll == null)
            smoothScroll = mapScrollRect.gameObject.AddComponent<SmoothMapScrollRect>();
        smoothScroll.Configure(mapScrollRect, Mathf.Min(mapWheelStep, 0.012f), mapWheelLerpSpeed);
    }

    void EnsureMiniMapOnExistingButton()
    {
        if (mapButton == null)
            return;

        Transform existingContent = FindChildRecursive(mapButton.transform, "MiniMapContent");
        if (existingContent != null)
        {
            miniMapContent = existingContent as RectTransform;
            ConfigureMapButtonForMiniMap();
            BuildMiniMap();
            return;
        }

        DestroyDirectChild(mapButton.transform, "TreeMapLineIcon");
        DestroyDirectChild(mapButton.transform, "MapButtonLabel");
        ConfigureMapButtonForMiniMap();

        GameObject viewport = Rect(mapButton.transform, "MiniMapViewport", Anchor.Stretch, Vector2.zero, Vector2.zero);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = Rect(viewport.transform, "MiniMapContent", Anchor.Center, Vector2.zero, new Vector2(440f, 440f));
        miniMapContent = content.GetComponent<RectTransform>();
        BuildMiniMap();
    }

    void ConfigureMapButtonForMiniMap()
    {
        if (mapButton == null)
            return;

        Image image = mapButton.GetComponent<Image>();
        if (image == null)
            return;

        SetRoundedImage(image, roundedPanelSprite);
        image.color = PanelColor;
        image.raycastTarget = true;

        if (mapButton.GetComponent<Outline>() == null)
        {
            Outline outline = mapButton.gameObject.AddComponent<Outline>();
            outline.effectColor = LineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    void DestroyDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return;

        Transform child = parent.Find(childName);
        if (child != null)
            DestroyUiObject(child.gameObject);
    }

    bool HasExistingBodyHud()
    {
        return FindChildRecursive(transform, "BodyPipHud") != null
            && FindChildRecursive(transform, "EyesRow_L_Pip_0") != null
            && FindChildRecursive(transform, "LegsRow_R_Pip_0") != null;
    }

    void BindExistingWaveUi()
    {
        if (waveHud == null)
        {
            Transform hud = FindChildRecursive(transform, "WaveHud");
            if (hud != null)
                waveHud = hud.gameObject;
        }

        waveDots.Clear();
        for (int i = 0; i < 3; i++)
        {
            Image image = FindChildComponent<Image>("WaveDot_" + i);
            if (image != null)
                waveDots.Add(image);
        }
    }

    void BindExistingPipGroups()
    {
        hudPipGroups.Clear();

        AddExistingPipGroup(BodySlot.EyeLeft, "EyesRow_L_Pip_", 2);
        AddExistingPipGroup(BodySlot.EyeRight, "EyesRow_R_Pip_", 2);
        AddExistingPipGroup(BodySlot.ArmLeft, "ArmsRow_L_Pip_", 3);
        AddExistingPipGroup(BodySlot.ArmRight, "ArmsRow_R_Pip_", 3);
        AddExistingPipGroup(BodySlot.LegLeft, "LegsRow_L_Pip_", 3);
        AddExistingPipGroup(BodySlot.LegRight, "LegsRow_R_Pip_", 3);
        Transform bodyRow = FindChildRecursive(transform, "BodyRow");
        if (bodyRow != null)
            bodyRow.gameObject.SetActive(false);
    }

    HudPipGroup AddExistingPipGroup(BodySlot? slot, string prefix, int count)
    {
        Image[] pips = new Image[count];
        for (int i = 0; i < count; i++)
        {
            Transform pip = FindChildRecursive(transform, prefix + i);
            if (pip == null)
                return null;

            pips[i] = pip.GetComponent<Image>();
            if (pips[i] == null)
                return null;
        }

        HudPipGroup group = new HudPipGroup(slot, pips, count);
        hudPipGroups.Add(group);
        return group;
    }

    void WireControlEvents()
    {
        if (mapButton != null)
        {
            mapButton.onClick.RemoveListener(PlayClickSound);
            mapButton.onClick.RemoveListener(OpenMap);
            mapButton.onClick.RemoveListener(ToggleMap);
            mapButton.onClick.AddListener(PlayClickSound);
            mapButton.onClick.AddListener(ToggleMap);
            ConfigureMapButtonForMapIcon();
            AttachHudTooltip(mapButton, "지도");
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(PlayClickSound);
            inventoryButton.onClick.RemoveListener(ToggleInventory);
            inventoryButton.onClick.AddListener(PlayClickSound);
            inventoryButton.onClick.AddListener(ToggleInventory);
            AttachHudTooltip(inventoryButton, "인벤토리");
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveListener(DismissMenuControlHint);
            menuButton.onClick.AddListener(DismissMenuControlHint);
            AttachHudTooltip(menuButton, "메뉴");
        }

        Button backdropButton = FindChildComponent<Button>("MapBackdrop");
        if (backdropButton != null)
        {
            backdropButton.onClick.RemoveListener(PlayClickSound);
            backdropButton.onClick.RemoveListener(CloseMap);
            backdropButton.onClick.AddListener(PlayClickSound);
            backdropButton.onClick.AddListener(CloseMap);
        }

        Button closeButton = FindChildComponent<Button>("MapCloseButton_X");
        if (closeButton != null)
        {
            // 씬에 배치된 X 버튼이므로 하이어라키의 위치/크기를 유지한다.
            ConfigureMapCloseButtonStyle(closeButton, false);
            closeButton.onClick.RemoveListener(PlayClickSound);
            closeButton.onClick.RemoveListener(CloseMap);
            closeButton.onClick.AddListener(PlayClickSound);
            closeButton.onClick.AddListener(CloseMap);
        }

        WirePauseMenu();
    }

    void WirePauseMenu()
    {
        if (menuButton == null)
            return;

        RunPauseMenuUI pauseMenu = GetComponent<RunPauseMenuUI>();
        if (pauseMenu == null)
            pauseMenu = gameObject.AddComponent<RunPauseMenuUI>();

        pauseMenu.SetMenuButton(menuButton);
        // 다른 패널이 열려 있으면 메뉴를 열지 못하게 한다 (패널 겹침 방지)
        pauseMenu.CanOpenMenu = () => !IsMapPanelOpen() && !IsInventoryPanelOpen();
    }

    void AttachHudTooltip(Button button, string text)
    {
        if (button == null)
            return;

        HudIconTooltip tooltip = button.GetComponent<HudIconTooltip>();
        if (tooltip == null)
            tooltip = button.gameObject.AddComponent<HudIconTooltip>();

        tooltip.SetText(text);
    }

    T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = FindChildRecursive(transform, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    void BuildBodyHud()
    {
        GameObject group = Rect(transform, "BodyPipHud", Anchor.TopLeft, new Vector2(30f, -30f), new Vector2(470f, 176f));
        Image backing = group.AddComponent<Image>();
        SetRoundedImage(backing, roundedPanelSprite);
        backing.color = new Color(0.17f, 0.15f, 0.13f, 0.10f);
        backing.raycastTarget = false;

        BuildPixelDoll(group.transform);

        float rowX = 114f;
        BuildPartRow(group.transform, "EyesRow", HudPartIcon.Eye, BodySlot.EyeLeft, 2, BodySlot.EyeRight, 2, new Vector2(rowX, -10f), false);
        BuildPartRow(group.transform, "ArmsRow", HudPartIcon.Arm, BodySlot.ArmLeft, 3, BodySlot.ArmRight, 3, new Vector2(rowX, -46f), false);
        BuildPartRow(group.transform, "LegsRow", HudPartIcon.Leg, BodySlot.LegLeft, 3, BodySlot.LegRight, 3, new Vector2(rowX, -82f), false);
    }

// task8: HP 섹션 아래에 장착된 Q보석 아이템 표시 패널
    void BuildEquippedItemPanel()
    {
        // BodyPipHud 아래 (y=-30-176=-206, 여백 10 추가)
        const float panelY = -216f;
        const float panelH = 48f;
        const float panelW = 260f;

        GameObject existing = null;
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i).name == "EquippedItemPanel") { existing = transform.GetChild(i).gameObject; break; }

        GameObject panel = existing ?? Rect(transform, "EquippedItemPanel", Anchor.TopLeft, new Vector2(30f, panelY), new Vector2(panelW, panelH));
        panel.SetActive(false);

        Image backing = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        SetRoundedImage(backing, roundedPanelSprite);
        backing.color = HudPanelColor;
        backing.raycastTarget = false;

        // 아이콘
        GameObject iconGo = panel.transform.Find("ItemIcon")?.gameObject ?? Rect(panel.transform, "ItemIcon", Anchor.TopLeft, new Vector2(6f, -6f), new Vector2(36f, 36f));
        Image iconImg = iconGo.GetComponent<Image>() ?? iconGo.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // 이름 레이블
        GameObject labelGo = panel.transform.Find("ItemName")?.gameObject ?? Rect(panel.transform, "ItemName", Anchor.TopLeft, new Vector2(48f, -8f), new Vector2(panelW - 56f, 32f));
        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>() ?? labelGo.AddComponent<TextMeshProUGUI>();
        label.font = uiFont != null ? uiFont : UIThinDungFont.Get();
        label.fontSize = 18f;
        label.color = TextColor;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;

        EquippedItemHudPanel comp = panel.GetComponent<EquippedItemHudPanel>() ?? panel.AddComponent<EquippedItemHudPanel>();
        comp.SetReferences(iconImg, label);
        equippedItemPanel = comp;
        comp.Refresh();
    }

    void BuildPixelDoll(Transform parent)
    {
        GameObject frame = Rect(parent, "PlayerDollFrame", Anchor.TopLeft, new Vector2(0f, -3f), new Vector2(82f, 116f));
        Image image = frame.AddComponent<Image>();
        SetRoundedImage(image, roundedButtonSprite);
        image.color = HudPanelColor;
        image.raycastTarget = false;
        Outline outline = frame.AddComponent<Outline>();
        outline.effectColor = LineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        AddPixel(frame.transform, "Head", new Vector2(24f, -13f), new Vector2(34f, 26f), new Color(0.78f, 0.65f, 0.68f, 1f));
        AddPixel(frame.transform, "Body", new Vector2(20f, -42f), new Vector2(42f, 38f), new Color(0.62f, 0.47f, 0.55f, 1f));
        AddPixel(frame.transform, "LeftArm", new Vector2(12f, -46f), new Vector2(10f, 32f), new Color(0.55f, 0.41f, 0.49f, 1f));
        AddPixel(frame.transform, "RightArm", new Vector2(60f, -46f), new Vector2(10f, 32f), new Color(0.55f, 0.41f, 0.49f, 1f));
        AddPixel(frame.transform, "LeftLeg", new Vector2(27f, -80f), new Vector2(10f, 22f), new Color(0.49f, 0.36f, 0.42f, 1f));
        AddPixel(frame.transform, "RightLeg", new Vector2(45f, -80f), new Vector2(10f, 22f), new Color(0.49f, 0.36f, 0.42f, 1f));
        AddPixel(frame.transform, "ButtonLeft", new Vector2(32f, -51f), new Vector2(5f, 5f), new Color(0.18f, 0.13f, 0.16f, 1f));
        AddPixel(frame.transform, "ButtonRight", new Vector2(46f, -51f), new Vector2(5f, 5f), new Color(0.18f, 0.13f, 0.16f, 1f));
        AddLine(frame.transform, "Stitch", new Vector2(41f, -61f), new Vector2(3f, 24f), SoftLineColor);

        TextMeshProUGUI label = Text(frame.transform, "DollLabel", "인형", 16f, TextColor, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        label.rectTransform.sizeDelta = new Vector2(-8f, 24f);
    }

    void AddPixel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = Rect(parent, name, Anchor.TopLeft, position, size);
        Image image = go.AddComponent<Image>();
        SetRoundedImage(image, size.x == size.y ? circleSprite : roundedButtonSprite);
        image.color = color;
        image.raycastTarget = false;
    }

    void BuildPartRow(Transform parent, string name, HudPartIcon icon, BodySlot? leftSlot, int leftCount, BodySlot? rightSlot, int rightCount, Vector2 offset, bool bodyRow)
    {
        float pipWidth = 30f;
        float pipHeight = 30f;
        float pipGap = 6f;
        float separatorGap = 13f;

        int totalPips = leftCount + rightCount;
        float rowWidth = 34f + (pipWidth + pipGap) * totalPips + (leftCount > 0 ? separatorGap + 7f : 0f) + 24f;
        GameObject row = Rect(parent, name, Anchor.TopLeft, offset, new Vector2(rowWidth, 38f));

        BuildPartIcon(row.transform, icon, new Vector2(0f, -2f));

        float x = 48f;
        HudPipGroup leftGroup = null;
        if (leftCount > 0)
        {
            Image[] leftPips = BuildPips(row.transform, name + "_L", leftCount, new Vector2(x, -4f), new Vector2(pipWidth, pipHeight), pipGap);
            leftGroup = new HudPipGroup(leftSlot, leftPips, leftCount);
            hudPipGroups.Add(leftGroup);
            x += leftCount * (pipWidth + pipGap) - pipGap + separatorGap;

            AddLine(row.transform, name + "_Separator", new Vector2(x, -2f), new Vector2(3f, bodyRow ? 28f : 25f), new Color(0.82f, 0.72f, 0.78f, 0.84f));
            x += 16f;
        }

        Image[] rightPips = BuildPips(row.transform, name + (bodyRow ? "_Body" : "_R"), rightCount, new Vector2(x, -4f), new Vector2(pipWidth, pipHeight), pipGap);
        HudPipGroup rightGroup = new HudPipGroup(rightSlot, rightPips, rightCount);
        hudPipGroups.Add(rightGroup);
    }

    Image[] BuildPips(Transform parent, string prefix, int count, Vector2 start, Vector2 size, float gap)
    {
        Image[] pips = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject pip = Rect(parent, prefix + "_Pip_" + i, Anchor.TopLeft, new Vector2(start.x + i * (size.x + gap), start.y), size);
            Image image = pip.AddComponent<Image>();
            SetRoundedImage(image, hpPipSprite != null ? hpPipSprite : roundedPipSprite);
            image.preserveAspect = hpPipSprite != null;
            image.color = EmptyPipColor;
            image.raycastTarget = false;
            Outline outline = pip.AddComponent<Outline>();
            outline.effectColor = LineColor;
            outline.effectDistance = new Vector2(1f, -1f);
            pips[i] = image;
        }

        return pips;
    }

    void BuildPartIcon(Transform parent, HudPartIcon icon, Vector2 offset)
    {
        GameObject holder = Rect(parent, icon + "Icon", Anchor.TopLeft, offset, new Vector2(34f, 30f));

        switch (icon)
        {
            case HudPartIcon.Eye:
                AddLine(holder.transform, "EyeLine", new Vector2(5f, -14f), new Vector2(24f, 3f), SoftLineColor);
                AddLine(holder.transform, "EyePupil", new Vector2(15f, -10f), new Vector2(6f, 10f), SoftLineColor);
                break;
            case HudPartIcon.Arm:
                AddLine(holder.transform, "ArmUpper", new Vector2(10f, -8f), new Vector2(4f, 17f), SoftLineColor, -22f);
                AddLine(holder.transform, "ArmLower", new Vector2(16f, -19f), new Vector2(4f, 14f), SoftLineColor, 35f);
                AddLine(holder.transform, "Hand", new Vector2(21f, -25f), new Vector2(10f, 3f), SoftLineColor);
                break;
            case HudPartIcon.Leg:
                AddLine(holder.transform, "LegThigh", new Vector2(12f, -7f), new Vector2(4f, 18f), SoftLineColor);
                AddLine(holder.transform, "LegShin", new Vector2(17f, -22f), new Vector2(4f, 13f), SoftLineColor, -15f);
                AddLine(holder.transform, "Foot", new Vector2(16f, -27f), new Vector2(13f, 3f), SoftLineColor);
                break;
            case HudPartIcon.Body:
                AddLine(holder.transform, "BodyTop", new Vector2(9f, -7f), new Vector2(17f, 3f), SoftLineColor);
                AddLine(holder.transform, "BodyBottom", new Vector2(7f, -25f), new Vector2(21f, 3f), SoftLineColor);
                AddLine(holder.transform, "BodyLeft", new Vector2(8f, -8f), new Vector2(3f, 18f), SoftLineColor);
                AddLine(holder.transform, "BodyRight", new Vector2(25f, -8f), new Vector2(3f, 18f), SoftLineColor);
                AddLine(holder.transform, "StitchA", new Vector2(15f, -13f), new Vector2(3f, 5f), SoftLineColor, 35f);
                AddLine(holder.transform, "StitchB", new Vector2(19f, -18f), new Vector2(3f, 5f), SoftLineColor, -35f);
                break;
        }
    }

    void BuildWaveHud()
    {
        GameObject wave = Rect(transform, "WaveHud", Anchor.TopCenter, new Vector2(0f, -38f), new Vector2(238f, 44f));
        waveHud = wave;
        Image bg = wave.AddComponent<Image>();
        SetRoundedImage(bg, roundedButtonSprite);
        bg.color = new Color(0.08f, 0.06f, 0.07f, 0.86f);
        bg.raycastTarget = false;

        waveDots.Clear();
        for (int i = 0; i < 3; i++)
        {
            GameObject dot = Rect(wave.transform, "WaveDot_" + i, Anchor.Left, new Vector2(27f + i * 28f, 0f), new Vector2(14f, 14f));
            Image image = dot.AddComponent<Image>();
            SetRoundedImage(image, circleSprite);
            image.color = new Color(0.18f, 0.16f, 0.18f, 0.92f);
            image.raycastTarget = false;
            Outline outline = dot.AddComponent<Outline>();
            outline.effectColor = new Color(0.91f, 0.86f, 0.78f, 0.70f);
            outline.effectDistance = new Vector2(1f, -1f);
            waveDots.Add(image);
        }

        waveLabel = Text(wave.transform, "WaveLabel", "WAVE 1/3", 21f, PanelColor, TextAlignmentOptions.MidlineLeft);
        waveLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        waveLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        waveLabel.rectTransform.offsetMin = new Vector2(104f, 0f);
        waveLabel.rectTransform.offsetMax = new Vector2(-16f, 0f);

        waveClearLabel = Text(transform, "WaveClearLabel", "Wave Clear!", 74f, Color.white, TextAlignmentOptions.Center);
        waveClearLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        waveClearLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        waveClearLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        waveClearLabel.rectTransform.anchoredPosition = new Vector2(0f, -92f);
        waveClearLabel.rectTransform.sizeDelta = new Vector2(720f, 96f);
        waveClearLabel.raycastTarget = false;
        SetTextAlpha(waveClearLabel, 0f);

        ApplyWave(1, 3);
    }

    void EnsureBossHpBar()
    {
        if (bossHpHud != null)
            return;

        Transform existing = FindChildRecursive(transform, "BossHpHud");
        if (existing != null)
        {
            bossHpHud = existing as RectTransform;
            bossHpFill = FindChildComponent<Image>("BossHpFill");
            bossNameLabel = FindChildComponent<TextMeshProUGUI>("BossNameLabel");
            bossHpText = FindChildComponent<TextMeshProUGUI>("BossHpText");
            if (bossHpHud != null && bossHpFill != null)
                return;

            DestroyUiObject(bossHpHud.gameObject);
            bossHpHud = null;
        }

        BuildBossHpBar();
    }

    void BuildBossHpBar()
    {
        GameObject hud = Rect(transform, "BossHpHud", Anchor.TopCenter, new Vector2(0f, -54f), new Vector2(908f, 84f));
        bossHpHud = hud.GetComponent<RectTransform>();
        Image bg = hud.AddComponent<Image>();
        SetRoundedImage(bg, roundedButtonSprite);
        bg.color = new Color(0.08f, 0.05f, 0.06f, 0.90f);
        bg.raycastTarget = false;
        Outline outline = hud.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.16f, 0.16f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        bossNameLabel = Text(hud.transform, "BossNameLabel", "미노타우로스", 26f, new Color(0.96f, 0.84f, 0.62f, 1f), TextAlignmentOptions.Center);
        bossNameLabel.fontStyle = FontStyles.Bold;
        bossNameLabel.raycastTarget = false;
        bossNameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        bossNameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        bossNameLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        bossNameLabel.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        bossNameLabel.rectTransform.sizeDelta = new Vector2(-24f, 32f);

        GameObject track = Rect(hud.transform, "BossHpTrack", Anchor.TopCenter, new Vector2(0f, -44f), new Vector2(BossHpTrackWidth, BossHpTrackHeight));
        Image trackImage = track.AddComponent<Image>();
        SetRoundedImage(trackImage, roundedButtonSprite);
        trackImage.color = new Color(0.16f, 0.10f, 0.11f, 1f);
        trackImage.raycastTarget = false;

        GameObject fill = Rect(track.transform, "BossHpFill", Anchor.Left, Vector2.zero, new Vector2(BossHpTrackWidth, BossHpTrackHeight));
        bossHpFill = fill.AddComponent<Image>();
        SetRoundedImage(bossHpFill, roundedButtonSprite);
        bossHpFill.color = new Color(0.85f, 0.18f, 0.18f, 1f);
        bossHpFill.raycastTarget = false;

        bossHpText = Text(track.transform, "BossHpText", "", 18f, Color.white, TextAlignmentOptions.Center);
        bossHpText.fontStyle = FontStyles.Bold;
        bossHpText.raycastTarget = false;
        bossHpText.rectTransform.anchorMin = Vector2.zero;
        bossHpText.rectTransform.anchorMax = Vector2.one;
        bossHpText.rectTransform.offsetMin = Vector2.zero;
        bossHpText.rectTransform.offsetMax = Vector2.zero;

        hud.SetActive(false);
    }

    void ApplyBossHealth(string bossName, int current, int max)
    {
        EnsureBossHpBar();
        if (bossHpHud == null)
            return;

        bossHpHud.gameObject.SetActive(true);
        bossHpHud.anchoredPosition = new Vector2(0f, -54f);
        bossHpHud.SetAsLastSibling();

        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        float ratio = (float)safeCurrent / safeMax;

        if (bossHpFill != null)
            bossHpFill.rectTransform.sizeDelta = new Vector2(BossHpTrackWidth * ratio, BossHpTrackHeight);

        if (bossNameLabel != null && !string.IsNullOrEmpty(bossName))
            bossNameLabel.text = bossName;

        if (bossHpText != null)
            bossHpText.text = safeCurrent + " / " + safeMax;
    }

    void HideBossHealthBar()
    {
        if (bossHpHud != null)
            bossHpHud.gameObject.SetActive(false);
    }

    // ---- judgement / design-match circular timer --------------------------

    void EnsureJudgementTimer()
    {
        if (judgementTimerHud != null)
            return;

        Transform existing = FindChildRecursive(transform, "JudgementTimerGauge");
        if (existing != null)
        {
            judgementTimerHud = existing as RectTransform;
            judgementTimerFill = FindChildComponent<Image>("TimerFill");
            judgementTimerSeconds = FindChildComponent<TextMeshProUGUI>("TimerSeconds");
            judgementTimerCaption = FindChildComponent<TextMeshProUGUI>("TimerCaption");
            if (judgementTimerHud != null
                && judgementTimerFill != null
                && judgementTimerFill.fillMethod == Image.FillMethod.Horizontal
                && judgementTimerHud.sizeDelta.x > judgementTimerHud.sizeDelta.y)
                return;

            DestroyUiObject(judgementTimerHud.gameObject);
            judgementTimerHud = null;
        }

        BuildJudgementTimer();
    }

    void BuildJudgementTimer()
    {
        // req: 좌측 상단 '캐릭터 HP UI'(BodyPipHud, TopLeft 30,-30 크기 470x176 → 바닥 ≈ y-206) 바로 아래에 배치.
        GameObject hud = Rect(transform, "JudgementTimerGauge", Anchor.TopLeft, JudgementTimerAnchoredPos, new Vector2(620f, 100f));
        judgementTimerHud = hud.GetComponent<RectTransform>();

        Image background = hud.AddComponent<Image>();
        SetRoundedImage(background, roundedButtonSprite);
        background.color = new Color(0.08f, 0.05f, 0.055f, 0.88f);
        background.raycastTarget = false;

        Outline outline = hud.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.34f, 0.12f, 0.74f);
        outline.effectDistance = new Vector2(2f, -2f);

        judgementTimerCaption = Text(hud.transform, "TimerCaption", "", 22f, new Color(1f, 0.86f, 0.66f, 1f), TextAlignmentOptions.Center);
        judgementTimerCaption.raycastTarget = false;
        judgementTimerCaption.rectTransform.anchorMin = new Vector2(0f, 1f);
        judgementTimerCaption.rectTransform.anchorMax = new Vector2(1f, 1f);
        judgementTimerCaption.rectTransform.pivot = new Vector2(0.5f, 1f);
        judgementTimerCaption.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        judgementTimerCaption.rectTransform.sizeDelta = new Vector2(-28f, 28f);

        GameObject track = Rect(hud.transform, "TimerTrack", Anchor.TopCenter, new Vector2(0f, -58f), new Vector2(584f, 26f));
        Image trackImage = track.AddComponent<Image>();
        SetRoundedImage(trackImage, roundedButtonSprite);
        trackImage.color = new Color(0.16f, 0.08f, 0.07f, 0.96f);
        trackImage.raycastTarget = false;

        GameObject fill = Rect(track.transform, "TimerFill", Anchor.Stretch, Vector2.zero, Vector2.zero);
        judgementTimerFill = fill.AddComponent<Image>();
        SetRoundedImage(judgementTimerFill, roundedButtonSprite);
        judgementTimerFill.type = Image.Type.Filled;
        judgementTimerFill.fillMethod = Image.FillMethod.Horizontal;
        judgementTimerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        judgementTimerFill.fillAmount = 1f;
        judgementTimerFill.color = JudgementTimerHigh;
        judgementTimerFill.raycastTarget = false;

        judgementTimerSeconds = Text(track.transform, "TimerSeconds", "0.0", 24f, Color.white, TextAlignmentOptions.Center);
        judgementTimerSeconds.fontStyle = FontStyles.Bold;
        judgementTimerSeconds.raycastTarget = false;
        judgementTimerSeconds.rectTransform.anchorMin = Vector2.zero;
        judgementTimerSeconds.rectTransform.anchorMax = Vector2.one;
        judgementTimerSeconds.rectTransform.offsetMin = Vector2.zero;
        judgementTimerSeconds.rectTransform.offsetMax = Vector2.zero;

        hud.SetActive(false);
    }

    void ApplyShowJudgementTimer(string caption, Color color)
    {
        EnsureJudgementTimer();
        if (judgementTimerHud == null)
            return;

        judgementTimerHud.gameObject.SetActive(true);
        judgementTimerHud.SetAsLastSibling();
        // req: 표시할 때마다 좌측 상단 캐릭터 HP UI 아래로 위치 재확정(과거 위치가 남아있어도 교정).
        bool challengeTimer = string.Equals(caption, "CHALLENGE", System.StringComparison.OrdinalIgnoreCase);
        if (challengeTimer)
        {
            judgementTimerHud.anchorMin = judgementTimerHud.anchorMax = new Vector2(0.5f, 1f);
            judgementTimerHud.pivot = new Vector2(0.5f, 1f);
            judgementTimerHud.anchoredPosition = new Vector2(0f, -36f);
            judgementTimerHud.sizeDelta = new Vector2(520f, 84f);
        }
        else
        {
            judgementTimerHud.anchorMin = judgementTimerHud.anchorMax = new Vector2(0f, 1f);
            judgementTimerHud.pivot = new Vector2(0f, 1f);
            judgementTimerHud.anchoredPosition = JudgementTimerAnchoredPos;
            judgementTimerHud.sizeDelta = new Vector2(620f, 100f);
        }
        judgementTimerBaseColor = JudgementTimerHigh;

        if (judgementTimerFill != null)
        {
            judgementTimerFill.color = judgementTimerBaseColor;
            judgementTimerFill.fillAmount = 1f;
        }

        if (judgementTimerSeconds != null)
            judgementTimerSeconds.text = "0.0";

        if (judgementTimerCaption != null)
        {
            judgementTimerCaption.text = caption;
            judgementTimerCaption.color = color;
        }
    }

    void ApplySetJudgementTimer(float remaining, float duration)
    {
        if (judgementTimerHud == null)
            return;

        float fraction = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        if (judgementTimerFill != null)
        {
            judgementTimerFill.fillAmount = fraction;
            // Stay the base colour for most of the timer, then bleed to red near the end.
            judgementTimerFill.color = Color.Lerp(JudgementTimerLow, judgementTimerBaseColor, Mathf.InverseLerp(0f, 0.35f, fraction));
        }

        if (judgementTimerSeconds != null)
            judgementTimerSeconds.text = Mathf.Max(0f, remaining).ToString("0.0");
    }

    void HideJudgementTimerHud()
    {
        if (judgementTimerHud != null)
            judgementTimerHud.gameObject.SetActive(false);
    }

    void EnsureBossPartsHud()
    {
        if (bossPartsHud != null)
            return;

        Transform existing = FindChildRecursive(transform, "BossPartsHud");
        if (existing != null)
            DestroyUiObject(existing.gameObject);

        GameObject hud = Rect(transform, "BossPartsHud", Anchor.TopCenter, new Vector2(0f, -38f), new Vector2(860f, 150f));
        bossPartsHud = hud.GetComponent<RectTransform>();

        string[] defaultNames = { "몸통(책)", "왼팔", "오른팔" };
        for (int i = 0; i < 3; i++)
        {
            GameObject row = Rect(hud.transform, "BossPartBar_" + i, Anchor.TopCenter, new Vector2(0f, -10f - i * 46f), new Vector2(BossPartTrackWidth, BossPartTrackHeight + 14f));

            GameObject track = Rect(row.transform, "Track", Anchor.TopCenter, new Vector2(0f, -14f), new Vector2(BossPartTrackWidth, BossPartTrackHeight));
            Image trackImage = track.AddComponent<Image>();
            SetRoundedImage(trackImage, roundedButtonSprite);
            trackImage.color = new Color(0.13f, 0.10f, 0.11f, 0.95f);
            trackImage.raycastTarget = false;

            GameObject fill = Rect(track.transform, "Fill", Anchor.Left, Vector2.zero, new Vector2(BossPartTrackWidth, BossPartTrackHeight));
            bossPartFills[i] = fill.AddComponent<Image>();
            SetRoundedImage(bossPartFills[i], roundedButtonSprite);
            bossPartFills[i].color = BossPartColors[i];
            bossPartFills[i].raycastTarget = false;

            bossPartNames[i] = Text(track.transform, "Name", defaultNames[i], 16f, Color.white, TextAlignmentOptions.MidlineLeft);
            bossPartNames[i].fontStyle = FontStyles.Bold;
            bossPartNames[i].raycastTarget = false;
            bossPartNames[i].rectTransform.anchorMin = Vector2.zero;
            bossPartNames[i].rectTransform.anchorMax = Vector2.one;
            bossPartNames[i].rectTransform.offsetMin = new Vector2(14f, 0f);
            bossPartNames[i].rectTransform.offsetMax = new Vector2(-14f, 0f);

            bossPartHpTexts[i] = Text(track.transform, "Hp", "", 15f, Color.white, TextAlignmentOptions.MidlineRight);
            bossPartHpTexts[i].raycastTarget = false;
            bossPartHpTexts[i].rectTransform.anchorMin = Vector2.zero;
            bossPartHpTexts[i].rectTransform.anchorMax = Vector2.one;
            bossPartHpTexts[i].rectTransform.offsetMin = new Vector2(14f, 0f);
            bossPartHpTexts[i].rectTransform.offsetMax = new Vector2(-14f, 0f);

            bossPartRows[i] = row.GetComponent<RectTransform>();
        }

        hud.SetActive(false);
    }

    void ApplyBossParts(string[] names, int[] current, int[] max)
    {
        ApplyBossParts(names, current, max, null);
    }

    void ApplyBossParts(string[] names, int[] current, int[] max, bool[] locked)
    {
        EnsureBossPartsHud();
        if (bossPartsHud == null)
            return;

        bossPartsHud.gameObject.SetActive(true);
        bossPartsHud.anchoredPosition = new Vector2(0f, -38f);
        bossPartsHud.SetAsLastSibling();

        int count = names != null ? Mathf.Min(3, names.Length) : 0;
        for (int i = 0; i < 3; i++)
        {
            bool active = i < count;
            if (bossPartRows[i] != null)
                bossPartRows[i].gameObject.SetActive(active);
            if (!active)
                continue;

            int safeMax = Mathf.Max(1, max[i]);
            int safeCurrent = Mathf.Clamp(current[i], 0, safeMax);
            float ratio = (float)safeCurrent / safeMax;
            bool isLocked = locked != null && i < locked.Length && locked[i];

            if (bossPartFills[i] != null)
            {
                bossPartFills[i].rectTransform.sizeDelta = new Vector2(BossPartTrackWidth * ratio, BossPartTrackHeight);
                bossPartFills[i].color = isLocked
                    ? new Color(0.22f, 0.18f, 0.16f, 1f)
                    : safeCurrent <= 0 ? new Color(0.3f, 0.3f, 0.3f, 1f) : BossPartColors[i];
            }
            if (bossPartNames[i] != null)
            {
                bossPartNames[i].text = names[i];
                bossPartNames[i].color = isLocked ? new Color(1f, 0.72f, 0.38f, 1f) : Color.white;
            }
            if (bossPartHpTexts[i] != null)
            {
                bossPartHpTexts[i].text = isLocked ? "LOCKED" : safeCurrent + " / " + safeMax;
                bossPartHpTexts[i].color = isLocked ? new Color(1f, 0.72f, 0.38f, 1f) : Color.white;
            }
        }
    }

    void HideBossPartsHud()
    {
        if (bossPartsHud != null)
            bossPartsHud.gameObject.SetActive(false);
    }

    void EnsureCollectedWords()
    {
        if (collectedWordsPanel != null)
            return;

        Transform existing = FindChildRecursive(transform, "CollectedWordsPanel");
        if (existing != null)
            DestroyUiObject(existing.gameObject);

        GameObject panel = Rect(transform, "CollectedWordsPanel", Anchor.TopLeft, new Vector2(30f, -214f), new Vector2(470f, 150f));
        collectedWordsPanel = panel.GetComponent<RectTransform>();
        Image bg = panel.AddComponent<Image>();
        SetRoundedImage(bg, roundedPanelSprite);
        bg.color = new Color(0.10f, 0.08f, 0.07f, 0.55f);
        bg.raycastTarget = false;

        TextMeshProUGUI title = Text(panel.transform, "Title", "획득한 글자", 18f, new Color(1f, 0.9f, 0.5f, 1f), TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.raycastTarget = false;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        title.rectTransform.sizeDelta = new Vector2(-24f, 26f);

        collectedWordsLabel = Text(panel.transform, "Words", "", 22f, new Color(1f, 0.95f, 0.72f, 1f), TextAlignmentOptions.TopLeft);
        collectedWordsLabel.raycastTarget = false;
        collectedWordsLabel.textWrappingMode = TextWrappingModes.Normal;
        collectedWordsLabel.rectTransform.anchorMin = Vector2.zero;
        collectedWordsLabel.rectTransform.anchorMax = Vector2.one;
        collectedWordsLabel.rectTransform.offsetMin = new Vector2(14f, 12f);
        collectedWordsLabel.rectTransform.offsetMax = new Vector2(-14f, -36f);

        panel.SetActive(false);
    }

    void ApplyCollectedWords(IList<string> words)
    {
        EnsureCollectedWords();
        if (collectedWordsPanel == null)
            return;

        bool any = words != null && words.Count > 0;
        collectedWordsPanel.gameObject.SetActive(any);
        if (any && collectedWordsLabel != null)
            collectedWordsLabel.text = string.Join("  ", words);
    }

    void BuildTopRightMapButton()
    {
        mapButton = BuildHudButton(transform, "MapIconButton", Anchor.TopRight, new Vector2(-38f, -38f), new Vector2(154f, 154f));
        mapButton.onClick.AddListener(PlayClickSound);
        mapButton.onClick.AddListener(ToggleMap);
        ConfigureMapButtonForMapIcon();
        AttachHudTooltip(mapButton, "지도");
    }

    void ConfigureMapButtonForMapIcon()
    {
        if (mapButton == null)
            return;

        Image image = mapButton.GetComponent<Image>();
        if (image != null)
        {
            SetRoundedImage(image, roundedButtonSprite);
            image.color = HudPanelColor;
            image.raycastTarget = true;
        }

        DestroyDirectChild(mapButton.transform, "MiniMapViewport");
        DestroyDirectChild(mapButton.transform, "MiniMapContent");
        miniMapContent = null;
        lastMiniMapCurrentId = -999;

        Transform icon = mapButton.transform.Find("TreeMapLineIcon");
        if (icon == null)
            BuildMapButtonIcon(mapButton.transform);
        else
            icon.gameObject.SetActive(true);
    }

    void BuildMapButtonIcon(Transform parent)
    {
        GameObject icon = Rect(parent, "TreeMapLineIcon", Anchor.Center, Vector2.zero, new Vector2(106f, 96f));
        Color line = TextColor;
        Color node = AccentColor;

        AddLine(icon.transform, "BranchA", new Vector2(51f, -27f), new Vector2(4f, 48f), line);
        AddLine(icon.transform, "BranchB", new Vector2(27f, -48f), new Vector2(52f, 4f), line);
        AddLine(icon.transform, "BranchC", new Vector2(27f, -48f), new Vector2(4f, 24f), line);
        AddLine(icon.transform, "BranchD", new Vector2(75f, -48f), new Vector2(4f, 24f), line);
        AddLine(icon.transform, "BranchE", new Vector2(51f, -72f), new Vector2(4f, 16f), line);

        AddPixel(icon.transform, "NodeTop", new Vector2(43f, -15f), new Vector2(20f, 20f), node);
        AddPixel(icon.transform, "NodeLeft", new Vector2(19f, -62f), new Vector2(20f, 20f), PanelColor);
        AddPixel(icon.transform, "NodeRight", new Vector2(67f, -62f), new Vector2(20f, 20f), PanelColor);
        AddPixel(icon.transform, "NodeBottom", new Vector2(43f, -75f), new Vector2(20f, 20f), PanelColor);
    }

    void ConfigureMiniMapButtonHover(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.76f, 0.66f, 0.55f, 1f);
        colors.pressedColor = new Color(0.58f, 0.47f, 0.36f, 1f);
        button.colors = colors;
    }

    void UpdateMiniMap()
    {
        if (miniMapContent == null)
        {
            if (mapButton != null && MapRunState.Root != null)
                EnsureMiniMapOnExistingButton();
            return;
        }

        MapRunState.EnsureRun();
        MapNode current = MapRunState.CurrentNode;
        if (current == null)
            return;

        if (current.id != lastMiniMapCurrentId || miniMapContent.childCount == 0)
            BuildMiniMap();

        miniMapContent.anchoredPosition = Vector2.Lerp(
            miniMapContent.anchoredPosition,
            miniMapTargetOffset,
            Application.isPlaying ? Time.deltaTime * 8f : 1f);
    }

    void BuildMiniMap()
    {
        if (miniMapContent == null)
            return;

        for (int i = miniMapContent.childCount - 1; i >= 0; i--)
            DestroyUiObject(miniMapContent.GetChild(i).gameObject);

        MapRunState.EnsureRun();
        MapNode current = MapRunState.CurrentNode;
        if (current == null)
            return;

        // 트리맵과 동일한 170×170 노드를 사용하고, 버튼 크기에 맞게 축소
        // 버튼 실제 크기: ~178px, viewport: ~158px (half=79)
        // scale=0.31 → 170*0.31≈53px 노드, 3개까지 가로로 맞음
        const float miniScale = 0.31f;
        const float nodeHalfGap = 95f;
        const float maxChildHalfX = 170f;

        List<MapNode> children = new List<MapNode>(current.children);
        Dictionary<MapNode, Vector2> positions = new Dictionary<MapNode, Vector2>();
        positions[current] = new Vector2(0f, nodeHalfGap);

        if (children.Count > 0)
        {
            float xStep = children.Count <= 1 ? 0f
                : Mathf.Min(maxChildHalfX * 2f / (children.Count - 1), maxChildHalfX);
            float startX = children.Count <= 1 ? 0f : -(children.Count - 1) * xStep * 0.5f;
            for (int i = 0; i < children.Count; i++)
                positions[children[i]] = new Vector2(startX + i * xStep, -nodeHalfGap);
        }

        miniMapContent.localScale = new Vector3(miniScale, miniScale, 1f);
        miniMapTargetOffset = Vector2.zero;
        if (lastMiniMapCurrentId == -999)
            miniMapContent.anchoredPosition = Vector2.zero;
        lastMiniMapCurrentId = current.id;

        // 연결선 (노드 뒤에)
        Color lineColor = MapBrown;
        lineColor.a = 1f;
        foreach (MapNode child in children)
            BuildDashedLine(miniMapContent, "MiniLine", positions[current], positions[child], 22f, 13f, 5f, lineColor);

        // 노드 (트리맵과 동일 스타일)
        foreach (KeyValuePair<MapNode, Vector2> kvp in positions)
            BuildMiniMapTreeNode(kvp.Key, kvp.Value, kvp.Key == current);
    }

    void BuildMiniMapTreeNode(MapNode node, Vector2 position, bool isCurrent)
    {
        bool revealRoom = ShouldRevealRoom(node);
        GameObject nodeGO = Rect(miniMapContent, "MiniNode_" + node.id, Anchor.Center, position, new Vector2(170f, 170f));
        Image nodeImage = nodeGO.AddComponent<Image>();
        Sprite icon = revealRoom ? MapIcon(node) : circleSprite;
        nodeImage.sprite = icon != null ? icon : circleSprite;
        nodeImage.preserveAspect = true;
        nodeImage.color = revealRoom ? Color.white : HiddenMapNodeColor;
        nodeImage.raycastTarget = false;

        if (isCurrent)
        {
            Outline outline = nodeGO.AddComponent<Outline>();
            outline.effectColor = MapBrown;
            outline.effectDistance = new Vector2(4f, -4f);
        }
    }

    void BuildDiaryText()
    {
        diaryLabel = Text(transform, "DiaryMemoryText", "\"창 너머를 만지고 싶었어.\"", 21f, new Color(0.72f, 0.67f, 0.70f, 0.88f), TextAlignmentOptions.Center);
        diaryLabel.fontStyle = FontStyles.Italic;
        diaryLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        diaryLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        diaryLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        diaryLabel.rectTransform.anchoredPosition = new Vector2(0f, 78f);
        diaryLabel.rectTransform.sizeDelta = new Vector2(820f, 36f);
    }

    void BuildBottomRightButtons()
    {
        inventoryButton = BuildHudButton(transform, "InventoryIconButton", Anchor.BottomRight, new Vector2(-116f, 36f), new Vector2(66f, 66f));
        inventoryButton.onClick.AddListener(PlayClickSound);
        inventoryButton.onClick.AddListener(ToggleInventory);
        BuildInventoryIcon(inventoryButton.transform);
        AttachHudTooltip(inventoryButton, "인벤토리");

        menuButton = BuildHudButton(transform, "MenuIconButton", Anchor.BottomRight, new Vector2(-100f, 100f), new Vector2(170f, 170f));
        menuButton.onClick.AddListener(DismissMenuControlHint);
        BuildMenuIcon(menuButton.transform);
        AttachHudTooltip(menuButton, "메뉴");
        WirePauseMenu();
    }

    void BuildControlHints()
    {
        mapControlHint = BuildControlHint(
            "MapControlHint",
            Anchor.TopRight,
            new Vector2(-38f, -212f),
            new Vector2(430f, 76f),
            "[M] 키를 눌러 지도 열기");

        menuControlHint = BuildControlHint(
            "MenuControlHint",
            Anchor.BottomRight,
            new Vector2(-100f, 286f),
            new Vector2(470f, 76f),
            "[ESC] 를 눌러 메뉴 열기");

        SetControlHintsVisible(false);
    }

    void EnsureControlHints()
    {
        if (mapControlHint == null)
        {
            Transform existing = FindChildRecursive(transform, "MapControlHint");
            if (existing != null)
                mapControlHint = existing.gameObject;
        }

        if (menuControlHint == null)
        {
            Transform existing = FindChildRecursive(transform, "MenuControlHint");
            if (existing != null)
                menuControlHint = existing.gameObject;
        }

        if (mapControlHint == null || menuControlHint == null)
            BuildControlHints();
    }

    GameObject BuildControlHint(string objectName, Anchor anchor, Vector2 offset, Vector2 size, string textValue)
    {
        GameObject hint = Rect(transform, objectName, anchor, offset, size);
        Image background = hint.AddComponent<Image>();
        SetRoundedImage(background, roundedButtonSprite);
        background.color = new Color(0.98f, 0.94f, 0.82f, 0.94f);
        background.raycastTarget = false;

        AddDashedBorder(hint.GetComponent<RectTransform>(), size, LineColor);

        TextMeshProUGUI label = Text(hint.transform, "Label", textValue, 28f, TextColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(18f, 10f);
        label.rectTransform.offsetMax = new Vector2(-18f, -10f);
        hint.SetActive(false);
        return hint;
    }

    void ShowPendingControlHintsIfNeeded()
    {
        ShowControlHintsOnNextRoom = false;
        SetControlHintsVisible(false);
    }

    void SetControlHintsVisible(bool visible)
    {
        if (mapControlHint != null)
            mapControlHint.SetActive(visible);
        if (menuControlHint != null)
            menuControlHint.SetActive(visible);
    }

    void DismissMapControlHint()
    {
        if (mapControlHint != null)
            mapControlHint.SetActive(false);
    }

    void DismissMenuControlHint()
    {
        if (menuControlHint != null)
            menuControlHint.SetActive(false);
    }

    void BuildInventoryIcon(Transform parent)
    {
        GameObject icon = Rect(parent, "InventoryLineIcon", Anchor.Center, Vector2.zero, new Vector2(44f, 44f));
        AddLine(icon.transform, "Needle", new Vector2(8f, -22f), new Vector2(32f, 4f), TextColor, -35f);
        AddLine(icon.transform, "ThreadA", new Vector2(13f, -13f), new Vector2(18f, 3f), TextColor, 35f);
        AddLine(icon.transform, "ThreadB", new Vector2(14f, -29f), new Vector2(14f, 3f), TextColor, -35f);
        AddPixel(icon.transform, "Button", new Vector2(27f, -14f), new Vector2(9f, 9f), AccentColor);
    }

    void BuildMenuIcon(Transform parent)
    {
        GameObject icon = Rect(parent, "MenuLineIcon", Anchor.Center, Vector2.zero, new Vector2(112f, 96f));
        AddLine(icon.transform, "MenuA", new Vector2(20f, -23f), new Vector2(72f, 11f), TextColor);
        AddLine(icon.transform, "MenuB", new Vector2(20f, -48f), new Vector2(72f, 11f), TextColor);
        AddLine(icon.transform, "MenuC", new Vector2(20f, -73f), new Vector2(72f, 11f), TextColor);
    }

    Button BuildHudButton(Transform parent, string name, Anchor anchor, Vector2 offset, Vector2 size)
    {
        GameObject go = Rect(parent, name, anchor, offset, size);
        Image image = go.AddComponent<Image>();
        SetRoundedImage(image, roundedButtonSprite);
        image.color = HudPanelColor;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = LineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.90f, 0.78f, 1f);
        colors.pressedColor = new Color(1f, 0.72f, 0.42f, 1f);
        button.colors = colors;
        return button;
    }

    void AddLine(Transform parent, string name, Vector2 position, Vector2 size, Color color, float rotation = 0f)
    {
        GameObject go = Rect(parent, name, Anchor.TopLeft, position, size);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    void AddDashedBorder(RectTransform parent, Vector2 size, Color color)
    {
        if (parent == null)
            return;

        const float dash = 28f;
        const float gap = 14f;
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, size.y * 0.5f), Vector2.right, size.x, dash, gap, color);
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.right, size.x, dash, gap, color);
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dash, gap, color);
        AddDashedEdge(parent, new Vector2(size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dash, gap, color);
    }

    void AddDashedEdge(RectTransform parent, Vector2 start, Vector2 direction, float length, float dash, float gap, Color color)
    {
        float offset = 0f;
        int index = 0;
        while (offset < length)
        {
            float segment = Mathf.Min(dash, length - offset);
            Vector2 size = Mathf.Abs(direction.x) > 0f ? new Vector2(segment, 4f) : new Vector2(4f, segment);
            GameObject go = Rect(parent, "HintDash_" + index, Anchor.Center, start + direction * (offset + segment * 0.5f), size);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            offset += dash + gap;
            index++;
        }
    }

    public static void SetWave(int currentWave, int totalWaves)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyWave(currentWave, totalWaves);
    }

    public static void ShowWaveClear()
    {
        SoundManager.PlayWaveClear();

        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.PlayWaveClear();
    }

    public static void SetWaveHudVisible(bool visible)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyWaveHudVisible(visible);
    }

    void ApplyWaveHudVisible(bool visible)
    {
        if (waveHud == null)
        {
            Transform found = FindChildRecursive(transform, "WaveHud");
            if (found != null)
                waveHud = found.gameObject;
        }

        if (waveHud != null)
            waveHud.SetActive(visible);
    }

    public static void ShowJudgementTimer(string caption, Color color)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyShowJudgementTimer(caption, color);
    }

    public static void SetJudgementTimer(float remaining, float duration)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplySetJudgementTimer(remaining, duration);
    }

    public static void HideJudgementTimer()
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.HideJudgementTimerHud();
    }

    public static void SetBossHealth(string bossName, int current, int max)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyBossHealth(bossName, current, max);
    }

    public static void HideBossHealth()
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.HideBossHealthBar();
    }

    public static void SetBossParts(string[] names, int[] current, int[] max)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyBossParts(names, current, max);
    }

    public static void SetBossParts(string[] names, int[] current, int[] max, bool[] locked)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyBossParts(names, current, max, locked);
    }

    public static void HideBossParts()
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.HideBossPartsHud();
    }

    public static void SetCollectedWords(IList<string> words)
    {
        RunHudUI hud = ActiveInstance();
        if (hud != null)
            hud.ApplyCollectedWords(words);
    }

    static RunHudUI ActiveInstance()
    {
        if (instance != null)
            return instance;

        return FindFirstObjectByType<RunHudUI>();
    }

    void ApplyWave(int currentWave, int totalWaves)
    {
        int safeTotal = Mathf.Max(1, totalWaves);
        int safeCurrent = Mathf.Clamp(currentWave, 1, safeTotal);

        if (waveLabel != null)
            waveLabel.text = "WAVE " + safeCurrent + "/" + safeTotal;

        for (int i = 0; i < waveDots.Count; i++)
        {
            Image dot = waveDots[i];
            if (dot == null)
                continue;

            bool filled = i < safeCurrent;
            dot.color = filled ? AccentColor : new Color(0.18f, 0.16f, 0.18f, 0.92f);
            Outline outline = dot.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = filled ? Color.white : new Color(0.91f, 0.86f, 0.78f, 0.70f);
        }
    }

    void PlayWaveClear()
    {
        if (waveClearLabel == null)
            return;

        if (waveClearRoutine != null)
            StopCoroutine(waveClearRoutine);

        waveClearRoutine = StartCoroutine(WaveClearRoutine());
    }

    System.Collections.IEnumerator WaveClearRoutine()
    {
        const int flashes = 8;
        const float interval = 0.14f;

        for (int i = 0; i < flashes; i++)
        {
            SetTextAlpha(waveClearLabel, i % 2 == 0 ? 1f : 0f);
            yield return new WaitForSeconds(interval);
        }

        SetTextAlpha(waveClearLabel, 0f);
        waveClearRoutine = null;
    }

    static void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    void UpdateHudState()
    {
        for (int i = 0; i < hudPipGroups.Count; i++)
            UpdatePipGroup(hudPipGroups[i]);

    }

    void UpdatePipGroup(HudPipGroup group)
    {
        if (group == null || group.pips == null || !group.slot.HasValue)
            return;

        BodyPart part = null;
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            ApplyPipColors(group.pips, group.maxPips, group.maxPips);
            return;
        }

        BodySlot slot = group.slot.Value;
        int index = (int)slot;
        if (inventory.equipped != null && index >= 0 && index < inventory.equipped.Length)
            part = inventory.equipped[index];

        int remaining;
        if (part != null)
        {
            remaining = HpToPips(part.currentHp, part.maxHp, group.maxPips);
        }
        else if (ItemInventoryManager.Instance != null
            && ItemInventoryManager.Instance.TryGetEquippedItemHp(slot, out int itemCurrentHp, out int itemMaxHp))
        {
            // 신규 아이템 시스템(기본 파츠 포함)으로 장착된 경우도 HP 점에 반영한다.
            remaining = HpToPips(itemCurrentHp, itemMaxHp, group.maxPips);
        }
        else
        {
            remaining = 0;
        }

        ApplyPipColors(group.pips, remaining, group.maxPips);
    }

    static int HpToPips(int currentHp, int maxHp, int maxPips)
    {
        if (maxHp <= 0 || currentHp <= 0)
            return 0;

        return Mathf.Clamp(Mathf.CeilToInt((float)currentHp / maxHp * maxPips), 1, maxPips);
    }

    static void ApplyPipColors(Image[] pips, int remaining, int maxPips)
    {
        Color filled = PipColor(remaining, maxPips);
        for (int i = 0; i < pips.Length; i++)
            if (pips[i] != null)
                pips[i].color = i < remaining ? filled : EmptyPipColor;
    }

    static Color PipColor(int remaining, int maxPips)
    {
        if (remaining <= 0)
            return EmptyPipColor;

        if (remaining == 1)
            return PipScarlet;

        float t = maxPips <= 1 ? 1f : (float)remaining / maxPips;
        if (t >= 0.95f) return PipYellow;
        if (t >= 0.72f) return PipLightOrange;
        if (t >= 0.50f) return PipOrange;
        return PipRed;
    }

    public void ShowDiaryText(string text, float duration = 2f)
    {
        if (diaryLabel == null)
            return;

        StopAllCoroutines();
        StartCoroutine(ShowDiaryRoutine(text, duration));
    }

    System.Collections.IEnumerator ShowDiaryRoutine(string text, float duration)
    {
        diaryLabel.text = text;
        Color color = diaryLabel.color;
        color.a = 0.95f;
        diaryLabel.color = color;

        yield return new WaitForSeconds(duration);

        float fade = 0.35f;
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            color.a = Mathf.Lerp(0.95f, 0f, t / fade);
            diaryLabel.color = color;
            yield return null;
        }

        color.a = 0f;
        diaryLabel.color = color;
    }

    // 보석 발동 시 아이콘 + 이름을 페이드 인/아웃으로 띄운다.
    public void ShowJewelPopup(Sprite icon, string jewelName, float duration = 1.4f)
    {
        EnsureJewelPopup();
        if (jewelPopupHud == null)
            return;

        jewelPopupHud.gameObject.SetActive(true);
        jewelPopupHud.SetAsLastSibling();

        if (jewelPopupIcon != null)
        {
            jewelPopupIcon.sprite = icon;
            jewelPopupIcon.enabled = icon != null;
            jewelPopupIcon.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (jewelPopupName != null)
            jewelPopupName.text = jewelName;

        if (jewelPopupCanvasGroup != null)
            jewelPopupCanvasGroup.alpha = 0f;

        if (jewelPopupRoutine != null)
            StopCoroutine(jewelPopupRoutine);
        jewelPopupRoutine = StartCoroutine(JewelPopupRoutine(duration));
    }

    System.Collections.IEnumerator JewelPopupRoutine(float duration)
    {
        const float fadeInTime = 0.25f;
        const float fadeOutTime = 0.4f;
        float holdTime = Mathf.Max(0f, duration - fadeInTime - fadeOutTime);

        // 페이드 인
        if (jewelPopupCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeInTime)
            {
                elapsed += Time.unscaledDeltaTime;
                jewelPopupCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInTime);
                yield return null;
            }
            jewelPopupCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(holdTime);

        // 페이드 아웃
        if (jewelPopupCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                jewelPopupCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutTime);
                yield return null;
            }
            jewelPopupCanvasGroup.alpha = 0f;
        }

        if (jewelPopupHud != null)
            jewelPopupHud.gameObject.SetActive(false);
        jewelPopupRoutine = null;
    }

    void EnsureJewelPopup()
    {
        if (jewelPopupHud != null)
            return;

        Transform existing = FindChildRecursive(transform, "JewelPopup");
        if (existing != null)
        {
            jewelPopupHud = existing as RectTransform;
            jewelPopupIcon = FindChildComponent<Image>("JewelPopupIcon");
            jewelPopupName = FindChildComponent<TextMeshProUGUI>("JewelPopupName");
            jewelPopupCanvasGroup = jewelPopupHud != null ? jewelPopupHud.GetComponent<CanvasGroup>() : null;
            if (jewelPopupHud != null && jewelPopupIcon != null && jewelPopupName != null)
            {
                if (jewelPopupCanvasGroup == null)
                    jewelPopupCanvasGroup = jewelPopupHud.gameObject.AddComponent<CanvasGroup>();
                return;
            }

            DestroyUiObject(jewelPopupHud != null ? jewelPopupHud.gameObject : null);
            jewelPopupHud = null;
        }

        GameObject hud = Rect(transform, "JewelPopup", Anchor.TopCenter, new Vector2(0f, -60f), new Vector2(320f, 240f));
        jewelPopupHud = hud.GetComponent<RectTransform>();

        jewelPopupCanvasGroup = hud.AddComponent<CanvasGroup>();
        jewelPopupCanvasGroup.alpha = 0f;

        Image bg = hud.AddComponent<Image>();
        SetRoundedImage(bg, roundedPanelSprite);
        bg.color = new Color(0.08f, 0.06f, 0.08f, 0.82f);
        bg.raycastTarget = false;

        GameObject iconGO = Rect(hud.transform, "JewelPopupIcon", Anchor.TopCenter, new Vector2(0f, -18f), new Vector2(140f, 140f));
        jewelPopupIcon = iconGO.AddComponent<Image>();
        jewelPopupIcon.preserveAspect = true;
        jewelPopupIcon.raycastTarget = false;

        jewelPopupName = Text(hud.transform, "JewelPopupName", "", 28f, new Color(0.96f, 0.86f, 0.62f, 1f), TextAlignmentOptions.Center);
        jewelPopupName.fontStyle = FontStyles.Bold;
        jewelPopupName.raycastTarget = false;
        jewelPopupName.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        jewelPopupName.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        jewelPopupName.rectTransform.pivot = new Vector2(0.5f, 0f);
        jewelPopupName.rectTransform.anchoredPosition = new Vector2(0f, 16f);
        jewelPopupName.rectTransform.sizeDelta = new Vector2(300f, 44f);

        hud.SetActive(false);
    }

    void OpenMap()
    {
        DismissMapControlHint();

        InventoryUI inv = FindInventory();
        if (inv != null && inv.IsOpen)
        {
            inv.ClosePanel();
            suppressInventoryOutsideClick = false;
        }

        MapRunState.EnsureRun();
        EnsureMapScrollHierarchy();
        BuildMapTree();

        if (mapOverlay == null)
            return;

        if (mapPanel != null)
            mapPanel.anchoredPosition = MapPanelShownPosition() + new Vector2(0f, -980f);

        mapOverlay.SetActive(true);

        Canvas.ForceUpdateCanvases();
        if (mapScrollRect != null)
        {
            mapScrollRect.StopMovement();
            mapScrollRect.verticalNormalizedPosition = 1f;
            ResetMapSmoothScroll();
        }
        Canvas.ForceUpdateCanvases();
        RectMask2D viewportMask = mapViewport != null ? mapViewport.GetComponent<RectMask2D>() : null;
        if (viewportMask != null)
            viewportMask.PerformClipping();
        RefreshMapViewportVisibility(Vector2.zero);
        Canvas.ForceUpdateCanvases();

        SoundManager.PlayTutorialPaperOpen(0f);
        PlayMapPanelAnimation(true);
        RunUiPauseManager.SetPaused("Map", true);
    }

    void ToggleMap()
    {
        if (mapOverlay != null && mapOverlay.activeSelf)
            CloseMap();
        else
        {
            // 다른 패널이 열려 있으면 지도를 열지 않는다 (패널 겹침 방지)
            if (IsInventoryPanelOpen() || IsMenuPanelOpen())
                return;
            OpenMap();
        }
    }

    // ── 패널 상호 배제: 한 번에 하나의 패널만 ──────────────────────────────
    bool IsMapPanelOpen()
    {
        return mapOverlay != null && mapOverlay.activeSelf;
    }

    bool IsInventoryPanelOpen()
    {
        InventoryUI inv = FindInventory();
        return inv != null && inv.IsOpen;
    }

    bool IsMenuPanelOpen()
    {
        RunPauseMenuUI pauseMenu = GetComponent<RunPauseMenuUI>();
        return pauseMenu != null && pauseMenu.IsAnyOpen;
    }

    void HandleMapHotkey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.mKey.wasPressedThisFrame)
            return;

        bool isOpen = mapOverlay != null && mapOverlay.activeSelf;
        if (!isOpen && mapKeyAllowed != null && !mapKeyAllowed())
            return;

        DismissMapControlHint();
        ToggleMap();
    }

    void HandleMenuHotkey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        RunPauseMenuUI pauseMenu = GetComponent<RunPauseMenuUI>();
        bool isOpen = pauseMenu != null && pauseMenu.IsAnyOpen;
        if (!isOpen && menuKeyAllowed != null && !menuKeyAllowed())
            return;

        DismissMenuControlHint();
        if (pauseMenu != null)
            pauseMenu.ToggleMenu();
    }

    void PlayClickSound()
    {
        SoundManager.PlayClick();
    }

    void HandleInventoryHotkey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.tabKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)
        {
            InventoryUI inv = FindInventory();
            bool isOpen = inv != null && inv.IsOpen;
            if (!isOpen && inventoryKeyAllowed != null && !inventoryKeyAllowed())
                return;
            ToggleInventory();
        }
    }

    public void CloseMap()
    {
        if (mapOverlay == null)
            return;

        RunUiPauseManager.SetPaused("Map", false);
        if (Application.isPlaying && mapOverlay.activeSelf)
        {
            SoundManager.PlayTutorialPaperClose(0f);
            PlayMapPanelAnimation(false);
            return;
        }

        mapOverlay.SetActive(false);
    }

    void CloseMapImmediate()
    {
        RunUiPauseManager.SetPaused("Map", false);

        if (mapAnimationRoutine != null)
        {
            StopCoroutine(mapAnimationRoutine);
            mapAnimationRoutine = null;
        }

        if (mapPanel != null)
        {
            Vector2 shown = MapPanelShownPosition();
            mapPanel.anchoredPosition = shown + new Vector2(0f, -980f);
        }

        if (mapOverlay != null)
            mapOverlay.SetActive(false);
    }

    void ResetMapSmoothScroll()
    {
        if (mapScrollRect == null)
            return;

        SmoothMapScrollRect smoothScroll = mapScrollRect.GetComponent<SmoothMapScrollRect>();
        if (smoothScroll != null)
            smoothScroll.Configure(mapScrollRect, Mathf.Min(mapWheelStep, 0.012f), mapWheelLerpSpeed);
    }

    void PlayMapPanelAnimation(bool show)
    {
        if (mapPanel == null)
        {
            mapOverlay.SetActive(show);
            return;
        }

        if (mapAnimationRoutine != null)
            StopCoroutine(mapAnimationRoutine);

        mapAnimationRoutine = StartCoroutine(MapPanelAnimationRoutine(show));
    }

    System.Collections.IEnumerator MapPanelAnimationRoutine(bool show)
    {
        Vector2 shown = MapPanelShownPosition();
        Vector2 hidden = shown + new Vector2(0f, -980f);
        Vector2 overshoot = shown + new Vector2(0f, 36f);
        if (show)
        {
            mapPanel.anchoredPosition = hidden;
            yield return StartCoroutine(AnimateMapPanelSegment(hidden, overshoot, 0.25f));
            yield return StartCoroutine(AnimateMapPanelSegment(overshoot, shown, 0.10f));
        }
        else
        {
            Vector2 from = mapPanel.anchoredPosition;
            yield return StartCoroutine(AnimateMapPanelSegment(from, overshoot, 0.09f));
            yield return StartCoroutine(AnimateMapPanelSegment(overshoot, hidden, 0.24f));
        }

        mapPanel.anchoredPosition = show ? shown : hidden;
        if (!show && mapOverlay != null)
            mapOverlay.SetActive(false);
        mapAnimationRoutine = null;
    }

    Vector2 MapPanelShownPosition()
    {
        if (!hasAuthoredMapPanelPosition && mapPanel != null)
        {
            authoredMapPanelPosition = mapPanel.anchoredPosition;
            hasAuthoredMapPanelPosition = true;
        }

        return hasAuthoredMapPanelPosition ? authoredMapPanelPosition : Vector2.zero;
    }

    System.Collections.IEnumerator AnimateMapPanelSegment(Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            mapPanel.anchoredPosition = Vector2.Lerp(from, to, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        mapPanel.anchoredPosition = to;
    }

    void ToggleInventory()
    {
        InventoryUI inventory = FindInventory();
        if (inventory == null)
            return;

        if (inventory.IsOpen)
        {
            inventory.ClosePanel();
            suppressInventoryOutsideClick = false;
        }
        else
        {
            // 다른 패널이 열려 있으면 인벤토리를 열지 않는다 (패널 겹침 방지)
            if (IsMapPanelOpen() || IsMenuPanelOpen())
                return;

            inventory.OpenPanel();
            suppressInventoryOutsideClick = true;
        }
    }

    void HandleInventoryOutsideClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (suppressInventoryOutsideClick)
        {
            if (!mouse.leftButton.isPressed)
                suppressInventoryOutsideClick = false;
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame)
            return;

        InventoryUI inventory = FindInventory();
        if (inventory == null || !inventory.IsOpen)
            return;

        Vector2 screenPoint = mouse.position.ReadValue();
        if (IsScreenPointInsideButton(inventoryButton, screenPoint))
        {
            inventory.ClosePanel();
            suppressInventoryOutsideClick = true;
            return;
        }

        if (!inventory.IsScreenPointInsidePanel(screenPoint))
            inventory.ClosePanel();
    }

    bool IsScreenPointInsideButton(Button button, Vector2 screenPoint)
    {
        if (button == null)
            return false;

        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);
    }

    void BuildMapOverlay()
    {
        mapOverlay = Rect(transform, "MapOverlay", Anchor.Stretch, Vector2.zero, Vector2.zero);

        GameObject backdrop = Rect(mapOverlay.transform, "MapBackdrop", Anchor.Stretch, Vector2.zero, Vector2.zero);
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = BackdropColor;
        Button backdropButton = backdrop.AddComponent<Button>();
        backdropButton.targetGraphic = backdropImage;
        backdropButton.onClick.AddListener(CloseMap);

        GameObject panelGO = Rect(mapOverlay.transform, "MapPanel", Anchor.Center, Vector2.zero, new Vector2(1160f, 860f));
        mapPanel = panelGO.GetComponent<RectTransform>();
        authoredMapPanelPosition = mapPanel.anchoredPosition;
        hasAuthoredMapPanelPosition = true;
        Image panelImage = panelGO.AddComponent<Image>();
        SetRoundedImage(panelImage, roundedPanelSprite);
        panelImage.color = PanelColor;
        Outline panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = LineColor;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI title = Text(panelGO.transform, "MapTitle", "RUN MAP", 30f, LineColor, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-120f, 50f);

        Button closeButton = BuildCloseButton(panelGO.transform);
        closeButton.onClick.AddListener(CloseMap);
        closeButton.transform.SetAsLastSibling();

        GameObject scrollGO = Rect(panelGO.transform, "MapScroll", Anchor.Stretch, Vector2.zero, Vector2.zero);
        RectTransform scrollRectTransform = scrollGO.GetComponent<RectTransform>();
        ApplyMapScrollLayout(scrollRectTransform);
        mapScrollRect = scrollGO.AddComponent<ScrollRect>();
        ConfigureMapScrollRect();

        GameObject viewport = Rect(scrollGO.transform, "MapViewport", Anchor.Stretch, Vector2.zero, Vector2.zero);
        mapViewport = viewport.GetComponent<RectTransform>();
        RectMask2D mapViewportMask = viewport.AddComponent<RectMask2D>();
        mapViewportMask.softness = new Vector2Int(0, 80);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;
        mapScrollRect.viewport = mapViewport;

        GameObject content = Rect(viewport.transform, "MapContent", Anchor.TopCenter, Vector2.zero, new Vector2(1040f, 1500f));
        mapContent = content.GetComponent<RectTransform>();
        mapContent.pivot = new Vector2(0.5f, 1f);
        mapScrollRect.content = mapContent;

        GameObject tree = Rect(mapContent, "TreeMap", Anchor.Stretch, Vector2.zero, Vector2.zero);
        mapTree = tree.GetComponent<RectTransform>();
    }

    void BuildMapTree()
    {
        if (mapContent == null)
            return;

        if (mapTree == null)
        {
            Transform authoredTree = mapContent.Find("TreeMap");
            mapTree = authoredTree as RectTransform;
        }

        MapNode root = MapRunState.Root;
        if (root == null)
            return;

        Transform treeRoot = mapTree != null ? mapTree : mapContent;
        for (int i = treeRoot.childCount - 1; i >= 0; i--)
            DestroyUiObject(treeRoot.GetChild(i).gameObject);

        List<List<MapNode>> layers = CollectLayers(root);
        Dictionary<MapNode, Vector2> positions = new Dictionary<MapNode, Vector2>();
        float width = 1040f;
        const float verticalInset = 240f;
        List<float> layerYOffsets = BuildLayerYOffsets(layers);
        float totalLayerSpan = layerYOffsets.Count > 0 ? layerYOffsets[layerYOffsets.Count - 1] : 0f;
        float height = Mathf.Max(1200f, verticalInset * 2f + totalLayerSpan);
        mapContent.sizeDelta = new Vector2(width, height);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            List<MapNode> layer = layers[layerIndex];
            float y = height * 0.5f - verticalInset - layerYOffsets[layerIndex];
            float xGap = layer.Count <= 1 ? 0f : (width - 340f) / (layer.Count - 1);

            for (int nodeIndex = 0; nodeIndex < layer.Count; nodeIndex++)
            {
                float x = layer.Count <= 1 ? 0f : -width * 0.5f + 170f + xGap * nodeIndex;
                positions[layer[nodeIndex]] = new Vector2(x, y);
            }
        }

        foreach (KeyValuePair<MapNode, Vector2> kvp in positions)
        {
            MapNode from = kvp.Key;
            foreach (MapNode child in from.children)
                if (positions.ContainsKey(child))
                    BuildLine(positions[from], positions[child], from.state != NodeState.Hidden && child.state != NodeState.Hidden);
        }

        foreach (KeyValuePair<MapNode, Vector2> kvp in positions)
            BuildNode(kvp.Key, kvp.Value);
    }

    List<float> BuildLayerYOffsets(List<List<MapNode>> layers)
    {
        const float baseGap = 270f;
        const float wideBranchExtraGap = 110f;

        List<float> offsets = new List<float>();
        float current = 0f;
        for (int i = 0; i < layers.Count; i++)
        {
            offsets.Add(current);
            if (i >= layers.Count - 1)
                continue;

            int fromCount = layers[i] != null ? layers[i].Count : 0;
            int toCount = layers[i + 1] != null ? layers[i + 1].Count : 0;
            bool wideBranch = Mathf.Min(fromCount, toCount) <= 2 && Mathf.Max(fromCount, toCount) >= 4;
            current += baseGap + (wideBranch ? wideBranchExtraGap : 0f);
        }

        return offsets;
    }

    void BuildLine(Vector2 from, Vector2 to, bool visibleRoute)
    {
        Color color = MapBrown;
        color.a = 1f;
        BuildDashedLine(mapTree != null ? mapTree : mapContent, "MapLine", from, to, 22f, 13f, visibleRoute ? 5f : 3f, color);
    }

    void RefreshMapViewportVisibility(Vector2 _)
    {
        if (mapViewport == null || mapTree == null)
            return;

        const float keepActiveTolerance = 160f;
        Rect viewportRect = mapViewport.rect;
        Vector3[] corners = new Vector3[4];

        for (int childIndex = 0; childIndex < mapTree.childCount; childIndex++)
        {
            RectTransform child = mapTree.GetChild(childIndex) as RectTransform;
            if (child == null)
                continue;

            child.GetWorldCorners(corners);
            float minY = float.MaxValue, maxY = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                Vector3 local = mapViewport.InverseTransformPoint(corners[c]);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
            }

            bool active = maxY >= viewportRect.yMin - keepActiveTolerance
                && minY <= viewportRect.yMax + keepActiveTolerance
                && maxX >= viewportRect.xMin - keepActiveTolerance
                && minX <= viewportRect.xMax + keepActiveTolerance;

            child.gameObject.SetActive(active);

            if (active)
            {
                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
            }
        }
    }

    void BuildNode(MapNode node, Vector2 position)
    {
        bool revealRoom = ShouldRevealRoom(node);
        Vector2 nodeSize = new Vector2(170f, 170f);
        GameObject nodeGO = Rect(mapTree != null ? mapTree : mapContent, "MapNode_" + node.id, Anchor.Center, position, nodeSize);
        Image nodeImage = nodeGO.AddComponent<Image>();
        Sprite icon = revealRoom ? MapIcon(node) : circleSprite;
        nodeImage.sprite = icon != null ? icon : circleSprite;
        nodeImage.preserveAspect = true;
        nodeImage.color = revealRoom ? Color.white : HiddenMapNodeColor;
        nodeImage.raycastTarget = false;

        if (revealRoom)
        {
            GameObject labelBox = Rect(nodeGO.transform, "RoomLabelBox", Anchor.Center, new Vector2(0f, 120f), new Vector2(200f, 54f));
            Image labelBackground = labelBox.AddComponent<Image>();
            SetRoundedImage(labelBackground, roundedButtonSprite);
            labelBackground.color = PanelColor;
            labelBackground.raycastTarget = false;
            BuildDashedBorder(labelBox.GetComponent<RectTransform>(), new Vector2(200f, 54f), 16f, 9f, 3f, MapBrown);

            TextMeshProUGUI label = Text(labelBox.transform, "RoomLabel", RoomTypeLabel(node), 26f, MapBrown, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 17f;
            label.fontSizeMax = 26f;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 4f);
            label.rectTransform.offsetMax = new Vector2(-8f, -4f);
        }

        if (IsCurrentOrPending(node))
        {
            TextMeshProUGUI marker = Text(nodeGO.transform, "CurrentLocationMarker", "\u25C0 \uD604\uC7AC \uC704\uCE58", 38f, MapBrown, TextAlignmentOptions.Center);
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.anchoredPosition = new Vector2(205f, 0f);
            marker.rectTransform.sizeDelta = new Vector2(240f, 68f);
        }
    }

    static bool ShouldRevealRoom(MapNode node)
    {
        return node != null && (node.state != NodeState.Hidden || IsCurrentOrPending(node));
    }

    void BuildDashedLine(Transform parent, string name, Vector2 from, Vector2 to, float dashLength, float gap, float thickness, Color color)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length <= 0.01f)
            return;

        Vector2 direction = delta / length;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        int index = 0;
        for (float distance = 0f; distance < length; distance += dashLength + gap)
        {
            float segmentLength = Mathf.Min(dashLength, length - distance);
            Vector2 center = from + direction * (distance + segmentLength * 0.5f);
            GameObject segment = Rect(parent, name + "_Dash_" + index++, Anchor.Center, center, new Vector2(segmentLength, thickness));
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image image = segment.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }

    void BuildDashedBorder(RectTransform parent, Vector2 size, float dashLength, float gap, float thickness, Color color)
    {
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        BuildDashedLine(parent, "BorderTop", new Vector2(-halfWidth, halfHeight), new Vector2(halfWidth, halfHeight), dashLength, gap, thickness, color);
        BuildDashedLine(parent, "BorderBottom", new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight), dashLength, gap, thickness, color);
        BuildDashedLine(parent, "BorderLeft", new Vector2(-halfWidth, -halfHeight), new Vector2(-halfWidth, halfHeight), dashLength, gap, thickness, color);
        BuildDashedLine(parent, "BorderRight", new Vector2(halfWidth, -halfHeight), new Vector2(halfWidth, halfHeight), dashLength, gap, thickness, color);
    }

    Sprite MapIcon(MapNode node)
    {
        if (node == null)
            return circleSprite;

        switch (node.roomType)
        {
            case RoomType.Start: return startRoomMapIcon;
            case RoomType.Treasure: return treasureMapIcon;
            case RoomType.Shop: return shopMapIcon;
            case RoomType.Challenge: return challengeMapIcon;
            case RoomType.MiddleBoss: return middleBossMapIcon;
            case RoomType.FinalBoss:
            case RoomType.Boss: return mainBossMapIcon;
            case RoomType.ConditionCombat:
                switch (node.conditionType)
                {
                    case NodeConditionType.NoLeftArm: return noLeftArmMapIcon;
                    case NodeConditionType.NoRightArm: return noRightArmMapIcon;
                    case NodeConditionType.NoLeftEye: return noLeftEyeMapIcon;
                    case NodeConditionType.NoRightEye: return noRightEyeMapIcon;
                    case NodeConditionType.NoLeftLeg: return noLeftLegMapIcon;
                    case NodeConditionType.NoRightLeg: return noRightLegMapIcon;
                }
                break;
        }

        return circleSprite;
    }

    static string RoomTypeLabel(MapNode node)
    {
        if (node == null)
            return "";

        switch (node.roomType)
        {
            case RoomType.Start: return "\uC2DC\uC791";
            case RoomType.ConditionCombat:
            case RoomType.NormalCombat: return "\uC804\uD22C\uBC29";
            case RoomType.Treasure:
            case RoomType.Supply: return "\uC2E0\uCCB4\uBC29";
            case RoomType.Shop: return "\uC0C1\uC810";
            case RoomType.Challenge:
            case RoomType.Event: return "\uB3C4\uC804\uBC29";
            case RoomType.MiddleBoss: return "\uC911\uAC04\uBCF4\uC2A4";
            case RoomType.FinalBoss:
            case RoomType.Boss: return "\uCD5C\uC885\uBCF4\uC2A4";
            default: return "";
        }
    }

    Button BuildCloseButton(Transform parent)
    {
        GameObject closeGO = Rect(parent, "MapCloseButton_X", Anchor.TopRight, new Vector2(-18f, -18f), new Vector2(60f, 60f));
        Image image = closeGO.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        Button button = closeGO.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureMapCloseButtonStyle(button);
        return button;
    }

    void ConfigureMapCloseButtonStyle(Button button, bool applyLayout = true)
    {
        if (button == null)
            return;

        // applyLayout=false: 씬에 직접 배치한 X 버튼의 앵커/피벗/위치/크기를 그대로 유지한다.
        // (런타임에 강제로 우상단으로 옮기던 동작이 하이어라키 배치를 무시하는 문제 수정)
        if (applyLayout)
        {
            RectTransform rect = button.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -18f);
            rect.sizeDelta = new Vector2(60f, 60f);
        }

        Color closeBrown = new Color(0.30f, 0.18f, 0.10f, 1f);
        Image background = button.GetComponent<Image>();
        if (background == null)
            background = button.gameObject.AddComponent<Image>();
        background.sprite = null;
        background.color = Color.clear;
        background.raycastTarget = true;
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        StartPanelHoverTint hover = button.GetComponent<StartPanelHoverTint>();
        if (hover == null)
            hover = button.gameObject.AddComponent<StartPanelHoverTint>();
        hover.Configure(background, Color.clear, new Color(closeBrown.r, closeBrown.g, closeBrown.b, 0.20f), new Color(closeBrown.r, closeBrown.g, closeBrown.b, 0.34f));

        Transform legacyLabel = button.transform.Find("MapCloseButton_X_Label");
        if (legacyLabel != null)
            DestroyUiObject(legacyLabel.gameObject);

        for (int i = 0; i < 4; i++)
        {
            float coordinate = -18f + i * 12f;
            ConfigureMapCloseDash(button.transform, "BorderDash_Top_" + i, new Vector2(coordinate, 27f), new Vector2(8f, 3f), closeBrown);
            ConfigureMapCloseDash(button.transform, "BorderDash_Bottom_" + i, new Vector2(coordinate, -27f), new Vector2(8f, 3f), closeBrown);
            ConfigureMapCloseDash(button.transform, "BorderDash_Left_" + i, new Vector2(-27f, coordinate), new Vector2(3f, 8f), closeBrown);
            ConfigureMapCloseDash(button.transform, "BorderDash_Right_" + i, new Vector2(27f, coordinate), new Vector2(3f, 8f), closeBrown);
        }

        Transform xTransform = button.transform.Find("CloseX");
        if (xTransform == null)
        {
            GameObject xObject = new GameObject("CloseX");
            xObject.transform.SetParent(button.transform, false);
            xTransform = xObject.AddComponent<RectTransform>();
        }

        RectTransform xRect = xTransform as RectTransform;
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.offsetMin = Vector2.zero;
        xRect.offsetMax = Vector2.zero;
        xRect.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI xLabel = xTransform.GetComponent<TextMeshProUGUI>();
        if (xLabel == null)
            xLabel = xTransform.gameObject.AddComponent<TextMeshProUGUI>();
        xLabel.font = UIThinDungFont.Get(uiFont);
        xLabel.text = "X";
        xLabel.fontSize = 34f;
        xLabel.alignment = TextAlignmentOptions.Center;
        xLabel.color = closeBrown;
        xLabel.raycastTarget = false;
        xLabel.textWrappingMode = TextWrappingModes.NoWrap;
        button.transform.SetAsLastSibling();
    }

    void ConfigureMapCloseDash(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Transform dashTransform = parent.Find(name);
        if (dashTransform == null)
        {
            GameObject dashObject = new GameObject(name);
            dashObject.transform.SetParent(parent, false);
            dashTransform = dashObject.AddComponent<RectTransform>();
        }

        RectTransform dashRect = dashTransform as RectTransform;
        dashRect.anchorMin = dashRect.anchorMax = new Vector2(0.5f, 0.5f);
        dashRect.pivot = new Vector2(0.5f, 0.5f);
        dashRect.anchoredPosition = position;
        dashRect.sizeDelta = size;

        Image dash = dashTransform.GetComponent<Image>();
        if (dash == null)
            dash = dashTransform.gameObject.AddComponent<Image>();
        dash.color = color;
        dash.raycastTarget = false;
    }

    GameObject Rect(Transform parent, string name, Anchor anchor, Vector2 offset, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        ApplyAnchor(rt, anchor);
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        return go;
    }

    TextMeshProUGUI Text(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_FontAsset font = UIThinDungFont.Get(uiFont);
        if (font != null) tmp.font = font;
        return tmp;
    }

    static void SetRoundedImage(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
    }

    void ApplyAnchor(RectTransform rt, Anchor anchor)
    {
        switch (anchor)
        {
            case Anchor.TopLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                break;
            case Anchor.TopCenter:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                break;
            case Anchor.TopRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                break;
            case Anchor.BottomCenter:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                break;
            case Anchor.BottomRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                break;
            case Anchor.Left:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                break;
            case Anchor.Stretch:
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                break;
            default:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                break;
        }
    }

    InventoryUI FindInventory()
    {
        InventoryUI ownInventory = GetComponentInChildren<InventoryUI>(true);
        if (ownInventory != null)
            return ownInventory;

        InventoryUI[] inventories = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return inventories.Length > 0 ? inventories[0] : null;
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventGO = new GameObject("RuntimeEventSystem");
        eventGO.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(eventGO);
        eventGO.AddComponent<EventSystem>();

        System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            eventGO.AddComponent(inputModuleType);
        else
            eventGO.AddComponent<StandaloneInputModule>();
    }

    static void DestroyUiObject(Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    static List<List<MapNode>> CollectLayers(MapNode root)
    {
        List<List<MapNode>> result = new List<List<MapNode>>();
        if (root == null)
            return result;

        HashSet<MapNode> visited = new HashSet<MapNode>();
        Queue<MapNode> queue = new Queue<MapNode>();
        queue.Enqueue(root);
        visited.Add(root);

        while (queue.Count > 0)
        {
            MapNode node = queue.Dequeue();
            while (result.Count <= node.layer)
                result.Add(new List<MapNode>());
            result[node.layer].Add(node);

            foreach (MapNode child in node.children)
                if (visited.Add(child))
                    queue.Enqueue(child);
        }

        return result;
    }

    static string NodeLabel(MapNode node)
    {
        if (node.state == NodeState.Hidden) return "?";
        if (node.state == NodeState.RouteOnly) return "?";

        switch (node.roomType)
        {
            case RoomType.NormalCombat: return "COMBAT";
            case RoomType.Supply: return "SUPPLY";
            case RoomType.Event: return "EVENT";
            case RoomType.Treasure: return "TREASURE";
            case RoomType.Shop: return "SHOP";
            case RoomType.Boss: return "BOSS";
            case RoomType.Start: return "START";
            case RoomType.Challenge: return "CHALLENGE";
            case RoomType.MiddleBoss: return "MIDDLE BOSS";
            case RoomType.FinalBoss: return "FINAL BOSS";
            case RoomType.ConditionCombat: return ConditionLabel(node.conditionType);
            default: return "";
        }
    }

    static string ConditionLabel(NodeConditionType condition)
    {
        switch (condition)
        {
            case NodeConditionType.NoLeftArm: return "NO\nL ARM";
            case NodeConditionType.NoRightArm: return "NO\nR ARM";
            case NodeConditionType.NoLeftEye: return "NO\nL EYE";
            case NodeConditionType.NoRightEye: return "NO\nR EYE";
            case NodeConditionType.NoLeftLeg: return "NO\nL LEG";
            case NodeConditionType.NoRightLeg: return "NO\nR LEG";
            default: return "COND";
        }
    }

    static Color GetColor(MapNode node)
    {
        if (MapRunState.PendingNode == node) return ColCurrent;
        if (node.state == NodeState.Current) return ColCurrent;
        if (node.state == NodeState.Cleared) return ColCleared;
        if (node.state == NodeState.RouteOnly) return ColRouteOnly;
        if (node.state == NodeState.Hidden) return ColHidden;

        if (node.state == NodeState.Visible)
        {
            switch (node.roomType)
            {
                case RoomType.NormalCombat: return ColFree;
                case RoomType.Supply: return ColSupply;
                case RoomType.Event: return ColEvent;
                case RoomType.Treasure: return ColTreasure;
                case RoomType.Shop: return ColShop;
                case RoomType.Boss: return ColBoss;
                case RoomType.Start: return ColEvent;
                case RoomType.Challenge: return ColEvent;
                case RoomType.MiddleBoss: return ColBoss;
                case RoomType.FinalBoss: return ColBoss;
                case RoomType.ConditionCombat:
                    switch (node.conditionType)
                    {
                        case NodeConditionType.NoLeftArm: return ColNoLeftArm;
                        case NodeConditionType.NoRightArm: return ColNoLeftArm;
                        case NodeConditionType.NoLeftEye: return ColNoRightEye;
                        case NodeConditionType.NoRightEye: return ColNoRightEye;
                        case NodeConditionType.NoLeftLeg: return ColNoLeftLeg;
                        case NodeConditionType.NoRightLeg: return ColNoRightLeg;
                    }
                    break;
            }
        }

        return Color.white;
    }

    static bool IsCurrentOrPending(MapNode node)
    {
        return node != null && (node.state == NodeState.Current || MapRunState.PendingNode == node);
    }

    class HudPipGroup
    {
        public readonly BodySlot? slot;
        public readonly Image[] pips;
        public readonly int maxPips;

        public HudPipGroup(BodySlot? slot, Image[] pips, int maxPips)
        {
            this.slot = slot;
            this.pips = pips;
            this.maxPips = maxPips;
        }
    }

    enum HudPartIcon
    {
        Eye,
        Arm,
        Leg,
        Body
    }

    enum Anchor
    {
        Center,
        TopLeft,
        TopCenter,
        TopRight,
        BottomCenter,
        BottomRight,
        Left,
        Stretch
    }
}
