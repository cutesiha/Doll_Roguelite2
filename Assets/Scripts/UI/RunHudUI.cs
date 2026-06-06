using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RunHudUI : MonoBehaviour
{
    [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] TMP_FontAsset uiFont;
    [SerializeField] Sprite roundedPanelSprite;
    [SerializeField] Sprite roundedButtonSprite;
    [SerializeField] Sprite roundedPipSprite;
    [SerializeField] Sprite circleSprite;

    Canvas canvas;
    RectTransform rootRect;
    Button mapButton;
    Button inventoryButton;
    Button menuButton;
    GameObject mapOverlay;
    RectTransform mapPanel;
    RectTransform mapContent;
    TextMeshProUGUI waveLabel;
    TextMeshProUGUI diaryLabel;
    readonly List<HudPipGroup> hudPipGroups = new List<HudPipGroup>();
    HudPipGroup bodyPips;
    bool suppressInventoryOutsideClick;

    static RunHudUI instance;

    static readonly Color PanelColor = new Color(0.91f, 0.86f, 0.78f, 0.98f);
    static readonly Color HudPanelColor = new Color(0.91f, 0.86f, 0.78f, 0.96f);
    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.42f);
    static readonly Color LineColor = new Color(0.17f, 0.15f, 0.13f, 1f);
    static readonly Color SoftLineColor = new Color(0.17f, 0.15f, 0.13f, 0.82f);
    static readonly Color TextColor = new Color(0.17f, 0.15f, 0.13f, 1f);
    static readonly Color MutedTextColor = new Color(0.35f, 0.31f, 0.28f, 1f);
    static readonly Color AccentColor = new Color(0.88f, 0.48f, 0.24f, 1f);
    static readonly Color EmptyPipColor = new Color(0.17f, 0.15f, 0.13f, 0.28f);
    static readonly Color BodyDangerColor = new Color(0.84f, 0.22f, 0.24f, 1f);

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
    static readonly Color ColRouteOnly = new Color(0.45f, 0.45f, 0.45f, 1f);
    static readonly Color ColHidden = new Color(0.22f, 0.22f, 0.22f, 1f);

    void Awake()
    {
        NormalizeRootCanvas();

        if (Application.isPlaying)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        EnsureEventSystem();
        EnsureBuilt();
        CloseMap();
    }

    void Update()
    {
        UpdateHudState();

        if (!Application.isPlaying)
            return;

        HandleMapHotkey();
        HandleInventoryOutsideClick();
    }

    public void Rebuild()
    {
        NormalizeRootCanvas();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "InventoryCanvas")
                continue;

            DestroyUiObject(child.gameObject);
        }

        hudPipGroups.Clear();
        bodyPips = null;
        mapButton = null;
        inventoryButton = null;
        menuButton = null;
        mapOverlay = null;
        mapPanel = null;
        mapContent = null;
        waveLabel = null;
        diaryLabel = null;

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
        BuildWaveHud();
        BuildTopRightMapButton();
        BuildDiaryText();
        BuildBottomRightButtons();
        BuildMapOverlay();
        CloseMap();
        UpdateHudState();
    }

    void NormalizeRootCanvas()
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.one;
        EnsureUIAssets();

        RectTransform rect = transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        Canvas rootCanvas = GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.enabled = true;
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 80;
        }
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
#endif
    }

    void EnsureBuilt()
    {
        canvas = GetComponent<Canvas>();
        rootRect = transform as RectTransform;

        BindExistingPrefabUi();

        if (mapButton == null || inventoryButton == null || menuButton == null || mapOverlay == null || mapPanel == null || mapContent == null || diaryLabel == null || waveLabel == null || !HasExistingBodyHud())
            Rebuild();
        else
        {
            BindExistingPipGroups();
            WireControlEvents();
            UpdateHudState();
        }
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

        if (mapContent == null)
            mapContent = FindChildComponent<RectTransform>("MapContent");
    }

    bool HasExistingBodyHud()
    {
        return FindChildRecursive(transform, "BodyPipHud") != null
            && FindChildRecursive(transform, "EyesRow_L_Pip_0") != null
            && FindChildRecursive(transform, "BodyRow_Body_Pip_0") != null;
    }

    void BindExistingPipGroups()
    {
        hudPipGroups.Clear();
        bodyPips = null;

        AddExistingPipGroup(BodySlot.EyeLeft, "EyesRow_L_Pip_", 2);
        AddExistingPipGroup(BodySlot.EyeRight, "EyesRow_R_Pip_", 2);
        AddExistingPipGroup(BodySlot.ArmLeft, "ArmsRow_L_Pip_", 3);
        AddExistingPipGroup(BodySlot.ArmRight, "ArmsRow_R_Pip_", 3);
        AddExistingPipGroup(BodySlot.LegLeft, "LegsRow_L_Pip_", 3);
        AddExistingPipGroup(BodySlot.LegRight, "LegsRow_R_Pip_", 3);
        bodyPips = AddExistingPipGroup(null, "BodyRow_Body_Pip_", 5);
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
            mapButton.onClick.RemoveListener(OpenMap);
            mapButton.onClick.AddListener(OpenMap);
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveListener(ToggleInventory);
            inventoryButton.onClick.AddListener(ToggleInventory);
        }

        Button backdropButton = FindChildComponent<Button>("MapBackdrop");
        if (backdropButton != null)
        {
            backdropButton.onClick.RemoveListener(CloseMap);
            backdropButton.onClick.AddListener(CloseMap);
        }

        Button closeButton = FindChildComponent<Button>("MapCloseButton_X");
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseMap);
            closeButton.onClick.AddListener(CloseMap);
        }
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
        GameObject group = Rect(transform, "BodyPipHud", Anchor.TopLeft, new Vector2(30f, -30f), new Vector2(470f, 188f));
        Image backing = group.AddComponent<Image>();
        SetRoundedImage(backing, roundedPanelSprite);
        backing.color = new Color(0.17f, 0.15f, 0.13f, 0.10f);
        backing.raycastTarget = false;

        BuildPixelDoll(group.transform);

        float rowX = 114f;
        BuildPartRow(group.transform, "EyesRow", HudPartIcon.Eye, BodySlot.EyeLeft, 2, BodySlot.EyeRight, 2, new Vector2(rowX, -12f), false);
        BuildPartRow(group.transform, "ArmsRow", HudPartIcon.Arm, BodySlot.ArmLeft, 3, BodySlot.ArmRight, 3, new Vector2(rowX, -44f), false);
        BuildPartRow(group.transform, "LegsRow", HudPartIcon.Leg, BodySlot.LegLeft, 3, BodySlot.LegRight, 3, new Vector2(rowX, -76f), false);
        BuildPartRow(group.transform, "BodyRow", HudPartIcon.Body, null, 0, null, 5, new Vector2(rowX, -110f), true);
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
        float pipWidth = bodyRow ? 34f : 26f;
        float pipHeight = bodyRow ? 24f : 21f;
        float pipGap = 8f;
        float separatorGap = 13f;

        int totalPips = leftCount + rightCount;
        float rowWidth = 34f + (pipWidth + pipGap) * totalPips + (leftCount > 0 ? separatorGap + 7f : 0f) + 24f;
        GameObject row = Rect(parent, name, Anchor.TopLeft, offset, new Vector2(rowWidth, bodyRow ? 42f : 30f));

        if (bodyRow)
            BuildBodyDangerFrame(row.transform, new Vector2(35f, -3f), new Vector2((pipWidth + pipGap) * rightCount - pipGap + 16f, 34f));

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

        if (bodyRow)
            bodyPips = rightGroup;
    }

    Image[] BuildPips(Transform parent, string prefix, int count, Vector2 start, Vector2 size, float gap)
    {
        Image[] pips = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject pip = Rect(parent, prefix + "_Pip_" + i, Anchor.TopLeft, new Vector2(start.x + i * (size.x + gap), start.y), size);
            Image image = pip.AddComponent<Image>();
            SetRoundedImage(image, roundedPipSprite);
            image.color = EmptyPipColor;
            image.raycastTarget = false;
            Outline outline = pip.AddComponent<Outline>();
            outline.effectColor = LineColor;
            outline.effectDistance = new Vector2(1f, -1f);
            pips[i] = image;
        }

        return pips;
    }

    void BuildBodyDangerFrame(Transform parent, Vector2 position, Vector2 size)
    {
        AddLine(parent, "BodyFrame_T", position, new Vector2(size.x, 3f), BodyDangerColor);
        AddLine(parent, "BodyFrame_B", new Vector2(position.x, position.y - size.y), new Vector2(size.x, 3f), BodyDangerColor);
        AddLine(parent, "BodyFrame_L", position, new Vector2(3f, size.y), BodyDangerColor);
        AddLine(parent, "BodyFrame_R", new Vector2(position.x + size.x, position.y), new Vector2(3f, size.y), BodyDangerColor);
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
        Image bg = wave.AddComponent<Image>();
        SetRoundedImage(bg, roundedButtonSprite);
        bg.color = new Color(0.08f, 0.06f, 0.07f, 0.86f);
        bg.raycastTarget = false;

        for (int i = 0; i < 3; i++)
        {
            GameObject dot = Rect(wave.transform, "WaveDot_" + i, Anchor.Left, new Vector2(27f + i * 28f, 0f), new Vector2(14f, 14f));
            Image image = dot.AddComponent<Image>();
            SetRoundedImage(image, circleSprite);
            image.color = i == 0 ? AccentColor : new Color(0.18f, 0.16f, 0.18f, 0.92f);
            image.raycastTarget = false;
            Outline outline = dot.AddComponent<Outline>();
            outline.effectColor = i == 0 ? AccentColor : new Color(0.91f, 0.86f, 0.78f, 0.70f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        waveLabel = Text(wave.transform, "WaveLabel", "WAVE 1/3", 21f, PanelColor, TextAlignmentOptions.MidlineLeft);
        waveLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        waveLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        waveLabel.rectTransform.offsetMin = new Vector2(104f, 0f);
        waveLabel.rectTransform.offsetMax = new Vector2(-16f, 0f);
    }

    void BuildTopRightMapButton()
    {
        mapButton = BuildHudButton(transform, "MapIconButton", Anchor.TopRight, new Vector2(-38f, -38f), new Vector2(184f, 104f));
        mapButton.onClick.AddListener(OpenMap);

        BuildTreeMapIcon(mapButton.transform, new Vector2(25f, -24f));
        TextMeshProUGUI label = Text(mapButton.transform, "MapButtonLabel", "트리 맵", 20f, TextColor, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(54f, 8f);
        label.rectTransform.offsetMax = new Vector2(-14f, -8f);
    }

    void BuildTreeMapIcon(Transform parent, Vector2 offset)
    {
        GameObject icon = Rect(parent, "TreeMapLineIcon", Anchor.TopLeft, offset, new Vector2(44f, 54f));
        AddLine(icon.transform, "Stem", new Vector2(20f, -11f), new Vector2(4f, 29f), SoftLineColor);
        AddLine(icon.transform, "BranchL", new Vector2(11f, -25f), new Vector2(21f, 3f), SoftLineColor, 27f);
        AddLine(icon.transform, "BranchR", new Vector2(21f, -25f), new Vector2(20f, 3f), SoftLineColor, -27f);
        AddPixel(icon.transform, "NodeA", new Vector2(16f, -4f), new Vector2(10f, 10f), AccentColor);
        AddPixel(icon.transform, "NodeB", new Vector2(3f, -36f), new Vector2(10f, 10f), SoftLineColor);
        AddPixel(icon.transform, "NodeC", new Vector2(31f, -36f), new Vector2(10f, 10f), SoftLineColor);
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
        inventoryButton.onClick.AddListener(ToggleInventory);
        BuildInventoryIcon(inventoryButton.transform);

        menuButton = BuildHudButton(transform, "MenuIconButton", Anchor.BottomRight, new Vector2(-38f, 36f), new Vector2(66f, 66f));
        BuildMenuIcon(menuButton.transform);
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
        GameObject icon = Rect(parent, "MenuLineIcon", Anchor.Center, Vector2.zero, new Vector2(38f, 34f));
        AddLine(icon.transform, "MenuA", new Vector2(7f, -8f), new Vector2(24f, 4f), TextColor);
        AddLine(icon.transform, "MenuB", new Vector2(7f, -16f), new Vector2(24f, 4f), TextColor);
        AddLine(icon.transform, "MenuC", new Vector2(7f, -24f), new Vector2(24f, 4f), TextColor);
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

    void UpdateHudState()
    {
        for (int i = 0; i < hudPipGroups.Count; i++)
            UpdatePipGroup(hudPipGroups[i]);

        if (bodyPips != null)
        {
            BodyState state = BodyManager.Instance != null ? BodyManager.Instance.State : null;
            int remaining = state == null || state.body ? bodyPips.maxPips : 0;
            ApplyPipColors(bodyPips.pips, remaining, bodyPips.maxPips);
        }
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

        int index = (int)group.slot.Value;
        if (inventory.equipped != null && index >= 0 && index < inventory.equipped.Length)
            part = inventory.equipped[index];

        int remaining = part != null ? HpToPips(part.currentHp, part.maxHp, group.maxPips) : 0;
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

    void OpenMap()
    {
        MapRunState.EnsureRun();
        BuildMapTree();
        mapOverlay.SetActive(true);
    }

    void ToggleMap()
    {
        if (mapOverlay != null && mapOverlay.activeSelf)
            CloseMap();
        else
            OpenMap();
    }

    void HandleMapHotkey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            ToggleMap();
    }

    public void CloseMap()
    {
        if (mapOverlay != null)
            mapOverlay.SetActive(false);
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

        GameObject panelGO = Rect(mapOverlay.transform, "MapPanel", Anchor.Center, Vector2.zero, new Vector2(1260f, 780f));
        mapPanel = panelGO.GetComponent<RectTransform>();
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

        GameObject content = Rect(panelGO.transform, "MapContent", Anchor.Stretch, Vector2.zero, Vector2.zero);
        mapContent = content.GetComponent<RectTransform>();
        mapContent.offsetMin = new Vector2(54f, 44f);
        mapContent.offsetMax = new Vector2(-54f, -88f);
    }

    void BuildMapTree()
    {
        if (mapContent == null)
            return;

        for (int i = mapContent.childCount - 1; i >= 0; i--)
            DestroyUiObject(mapContent.GetChild(i).gameObject);

        MapNode root = MapRunState.Root;
        if (root == null)
            return;

        List<List<MapNode>> layers = CollectLayers(root);
        Dictionary<MapNode, Vector2> positions = new Dictionary<MapNode, Vector2>();
        Rect rect = mapContent.rect;
        float width = Mathf.Max(100f, rect.width);
        float height = Mathf.Max(100f, rect.height);
        float yGap = layers.Count <= 1 ? 0f : (height - 120f) / (layers.Count - 1);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            List<MapNode> layer = layers[layerIndex];
            float y = height * 0.5f - 60f - yGap * layerIndex;
            float xGap = layer.Count <= 1 ? 0f : (width - 160f) / (layer.Count - 1);

            for (int nodeIndex = 0; nodeIndex < layer.Count; nodeIndex++)
            {
                float x = layer.Count <= 1 ? 0f : -width * 0.5f + 80f + xGap * nodeIndex;
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

    void BuildLine(Vector2 from, Vector2 to, bool visibleRoute)
    {
        GameObject line = Rect(mapContent, "MapLine", Anchor.Center, Vector2.zero, Vector2.zero);
        Image image = line.AddComponent<Image>();
        Color color = LineColor;
        color.a = visibleRoute ? 0.85f : 0.25f;
        image.color = color;

        RectTransform rt = line.GetComponent<RectTransform>();
        Vector2 delta = to - from;
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.sizeDelta = new Vector2(delta.magnitude, visibleRoute ? 4f : 2f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    void BuildNode(MapNode node, Vector2 position)
    {
        GameObject nodeGO = Rect(mapContent, "MapNode_" + node.id, Anchor.Center, position, new Vector2(92f, 92f));
        Image nodeImage = nodeGO.AddComponent<Image>();
        nodeImage.color = GetColor(node);
        Button button = nodeGO.AddComponent<Button>();
        button.targetGraphic = nodeImage;
        button.interactable = false;

        Outline outline = nodeGO.AddComponent<Outline>();
        outline.effectColor = node.state == NodeState.Current ? Color.white : new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI label = Text(nodeGO.transform, "NodeLabel", NodeLabel(node), 16f, Color.white, TextAlignmentOptions.Center);
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 16f;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(5f, 5f);
        label.rectTransform.offsetMax = new Vector2(-5f, -5f);
    }

    Button BuildCloseButton(Transform parent)
    {
        GameObject closeGO = Rect(parent, "MapCloseButton_X", Anchor.TopRight, new Vector2(-22f, -22f), new Vector2(72f, 72f));
        Image image = closeGO.AddComponent<Image>();
        SetRoundedImage(image, roundedButtonSprite);
        image.color = AccentColor;
        Button button = closeGO.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = Text(closeGO.transform, "MapCloseButton_X_Label", "X", 42f, Color.white, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
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
        TMP_FontAsset font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
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
            case RoomType.Boss: return "BOSS";
            case RoomType.ConditionCombat: return ConditionLabel(node.conditionType);
            default: return "";
        }
    }

    static string ConditionLabel(NodeConditionType condition)
    {
        switch (condition)
        {
            case NodeConditionType.NoLeftArm: return "NO\nL ARM";
            case NodeConditionType.NoRightEye: return "NO\nR EYE";
            case NodeConditionType.NoLeftLeg: return "NO\nL LEG";
            case NodeConditionType.NoRightLeg: return "NO\nR LEG";
            default: return "COND";
        }
    }

    static Color GetColor(MapNode node)
    {
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
                case RoomType.Boss: return ColBoss;
                case RoomType.ConditionCombat:
                    switch (node.conditionType)
                    {
                        case NodeConditionType.NoLeftArm: return ColNoLeftArm;
                        case NodeConditionType.NoRightEye: return ColNoRightEye;
                        case NodeConditionType.NoLeftLeg: return ColNoLeftLeg;
                        case NodeConditionType.NoRightLeg: return ColNoRightLeg;
                    }
                    break;
            }
        }

        return Color.white;
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
        BottomRight,
        Left,
        Stretch
    }
}
