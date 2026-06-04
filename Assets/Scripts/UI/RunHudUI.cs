using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RunHudUI : MonoBehaviour
{
    [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] TMP_FontAsset uiFont;

    Canvas canvas;
    RectTransform rootRect;
    Button mapButton;
    Button inventoryButton;
    GameObject mapOverlay;
    RectTransform mapPanel;
    RectTransform mapContent;
    bool suppressInventoryOutsideClick;

    static RunHudUI instance;

    static readonly Color PanelColor = new Color(0.075f, 0.07f, 0.085f, 0.96f);
    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.42f);
    static readonly Color LineColor = new Color(0.45f, 0.42f, 0.48f, 1f);
    static readonly Color TextColor = new Color(0.94f, 0.90f, 0.82f, 1f);
    static readonly Color AccentColor = new Color(0.84f, 0.64f, 0.32f, 1f);

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
        if (!Application.isPlaying)
            return;

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

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

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

        mapButton = BuildIconButton(transform, "MapIconButton", "MAP", Anchor.TopRight, new Vector2(-38f, -38f));
        mapButton.onClick.AddListener(OpenMap);

        inventoryButton = BuildIconButton(transform, "InventoryIconButton", "BAG", Anchor.BottomLeft, new Vector2(38f, 38f));
        inventoryButton.onClick.AddListener(ToggleInventory);

        BuildMapOverlay();
    }

    void NormalizeRootCanvas()
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.one;

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

    void EnsureBuilt()
    {
        canvas = GetComponent<Canvas>();
        rootRect = transform as RectTransform;

        BindExistingPrefabUi();

        if (mapButton == null || inventoryButton == null || mapOverlay == null || mapPanel == null || mapContent == null)
            Rebuild();
        else
            WireControlEvents();
    }

    void BindExistingPrefabUi()
    {
        if (mapButton == null)
            mapButton = FindChildComponent<Button>("MapIconButton");

        if (inventoryButton == null)
            inventoryButton = FindChildComponent<Button>("InventoryIconButton");

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

    void OpenMap()
    {
        MapRunState.EnsureRun();
        BuildMapTree();
        mapOverlay.SetActive(true);
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
        panelImage.color = PanelColor;
        Outline panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = LineColor;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI title = Text(panelGO.transform, "MapTitle", "RUN MAP", 30f, AccentColor, TextAlignmentOptions.Center);
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
            Destroy(mapContent.GetChild(i).gameObject);

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

    Button BuildIconButton(Transform parent, string name, string label, Anchor anchor, Vector2 offset)
    {
        GameObject go = Rect(parent, name, anchor, offset, new Vector2(104f, 76f));
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.15f, 0.92f);
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.20f, 0.18f, 0.17f, 0.96f);
        colors.pressedColor = new Color(0.30f, 0.22f, 0.13f, 1f);
        button.colors = colors;

        TextMeshProUGUI text = Text(go.transform, name + "_Label", label, 24f, TextColor, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    Button BuildCloseButton(Transform parent)
    {
        GameObject closeGO = Rect(parent, "MapCloseButton_X", Anchor.TopRight, new Vector2(-22f, -22f), new Vector2(72f, 72f));
        Image image = closeGO.AddComponent<Image>();
        image.color = new Color(0.78f, 0.08f, 0.08f, 1f);
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
        tmp.enableWordWrapping = false;
        TMP_FontAsset font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
        return tmp;
    }

    void ApplyAnchor(RectTransform rt, Anchor anchor)
    {
        switch (anchor)
        {
            case Anchor.TopRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                break;
            case Anchor.BottomLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
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
        InventoryUI[] inventories = FindObjectsOfType<InventoryUI>(true);
        return inventories.Length > 0 ? inventories[0] : null;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
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

    enum Anchor
    {
        Center,
        TopRight,
        BottomLeft,
        Stretch
    }
}
