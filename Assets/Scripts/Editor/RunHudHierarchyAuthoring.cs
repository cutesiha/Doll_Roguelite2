using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RunHudHierarchyAuthoring
{
    const string RunHudPrefabPath = "Assets/Prefabs/UI/RunHudCanvas.prefab";
    const string FontPath = "Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset";
    const string OptionBackgroundPath = "Assets/Resources/Sprites/startscene/option2.png";
    const string QuestionSpritePath = "Assets/Resources/Sprites/startscene/question.png";

    [MenuItem("Tools/Run HUD/Bake Editable Panels Into Prefab")]
    public static void BakeEditablePanelsIntoPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RunHudPrefabPath);
        try
        {
            EnsurePauseMenu(root);
            EnsureMapHierarchy(root);
            PrefabUtility.SaveAsPrefabAsset(root, RunHudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CleanupLegacySceneOverrides();
        Debug.Log("[Run HUD] RunMenuPanel and TreeMap are now authored in the RunHudCanvas prefab hierarchy.");
    }

    [MenuItem("Tools/Run HUD/Apply Map Visual Tweaks Only")]
    public static void ApplyMapVisualTweaksOnly()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RunHudPrefabPath);
        try
        {
            EnsureMapHierarchy(root);
            CopyMenuCloseButtonToMap(root);
            PrefabUtility.SaveAsPrefabAsset(root, RunHudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Run HUD] Applied TreeMap visuals and copied the menu close button style to the map only.");
    }

    static void CopyMenuCloseButtonToMap(GameObject root)
    {
        Transform menuClose = FindDeep(root.transform, "RunMenuCloseButton");
        Transform mapPanel = FindDeep(root.transform, "MapPanel");
        if (menuClose == null || mapPanel == null)
            throw new MissingReferenceException("RunMenuCloseButton or MapPanel was not found.");

        Transform oldMapClose = mapPanel.Find("MapCloseButton_X");
        if (oldMapClose != null)
            Object.DestroyImmediate(oldMapClose.gameObject);

        GameObject clone = Object.Instantiate(menuClose.gameObject, mapPanel, false);
        clone.name = "MapCloseButton_X";
        clone.SetActive(true);
        RectTransform rect = clone.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -18f);
        rect.sizeDelta = new Vector2(60f, 60f);
        clone.transform.SetAsLastSibling();
    }

    static void EnsurePauseMenu(GameObject root)
    {
        Button menuButton = FindDeep(root.transform, "MenuIconButton")?.GetComponent<Button>();
        RunPauseMenuUI pauseMenu = root.GetComponent<RunPauseMenuUI>();
        if (pauseMenu == null)
            pauseMenu = root.AddComponent<RunPauseMenuUI>();

        SerializedObject serializedMenu = new SerializedObject(pauseMenu);
        serializedMenu.FindProperty("menuButton").objectReferenceValue = menuButton;
        serializedMenu.FindProperty("startOptionPanelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/StartUIPanelPrefabs/StartOptionPanel.prefab");
        serializedMenu.FindProperty("startRoadPanelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/StartUIPanelPrefabs/StartRoadPanel.prefab");
        serializedMenu.FindProperty("startExitPanelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/StartUIPanelPrefabs/StartExitPanel.prefab");
        serializedMenu.FindProperty("optionBackgroundSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(OptionBackgroundPath);
        serializedMenu.FindProperty("questionSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(QuestionSpritePath);
        serializedMenu.FindProperty("uiFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        Transform existing = root.transform.Find("RunMenuPanel");
        RectTransform panel = existing as RectTransform;
        if (panel == null)
            panel = CreateRect(root.transform, "RunMenuPanel");

        Center(panel, Vector2.zero, new Vector2(720f, 360f));
        Image panelImage = GetOrAdd<Image>(panel.gameObject);
        panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OptionBackgroundPath);
        panelImage.type = panelImage.sprite != null && panelImage.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = Color.white;
        panelImage.raycastTarget = true;

        string[] labels = { "설정", "저장", "메인으로", "나가기" };
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuestionSpritePath);
        for (int i = 0; i < labels.Length; i++)
            EnsureMenuButton(panel, i, labels[i], font, buttonSprite);

        EnsureMenuCloseButton(panel, font);

        panel.gameObject.SetActive(false);
    }

    static void EnsureMenuCloseButton(RectTransform panel, TMP_FontAsset font)
    {
        RectTransform close = panel.Find("RunMenuCloseButton") as RectTransform;
        if (close == null)
            close = CreateRect(panel, "RunMenuCloseButton");
        close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-18f, -18f);
        close.sizeDelta = new Vector2(60f, 60f);

        Image background = GetOrAdd<Image>(close.gameObject);
        background.sprite = null;
        background.color = new Color(1f, 1f, 1f, 0f);
        background.raycastTarget = true;
        Button button = GetOrAdd<Button>(close.gameObject);
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        GetOrAdd<StartPanelHoverTint>(close.gameObject);

        Color brown = new Color(0.30f, 0.18f, 0.10f, 1f);
        for (int i = 0; i < 4; i++)
        {
            float coordinate = -18f + i * 12f;
            EnsureDash(close, "BorderDash_Top_" + i, new Vector2(coordinate, 27f), new Vector2(8f, 3f), brown);
            EnsureDash(close, "BorderDash_Bottom_" + i, new Vector2(coordinate, -27f), new Vector2(8f, 3f), brown);
            EnsureDash(close, "BorderDash_Left_" + i, new Vector2(-27f, coordinate), new Vector2(3f, 8f), brown);
            EnsureDash(close, "BorderDash_Right_" + i, new Vector2(27f, coordinate), new Vector2(3f, 8f), brown);
        }

        RectTransform xRect = close.Find("CloseX") as RectTransform;
        if (xRect == null)
            xRect = CreateRect(close, "CloseX");
        Stretch(xRect);
        TextMeshProUGUI xLabel = GetOrAdd<TextMeshProUGUI>(xRect.gameObject);
        xLabel.font = font;
        xLabel.text = "X";
        xLabel.fontSize = 34f;
        xLabel.alignment = TextAlignmentOptions.Center;
        xLabel.color = brown;
        xLabel.raycastTarget = false;
        xLabel.textWrappingMode = TextWrappingModes.NoWrap;
        close.SetAsLastSibling();
    }

    static void EnsureDash(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = parent.Find(name) as RectTransform;
        if (rect == null)
            rect = CreateRect(parent, name);
        Center(rect, position, size);
        Image image = GetOrAdd<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = false;
    }

    static void EnsureMenuButton(RectTransform panel, int index, string labelText, TMP_FontAsset font, Sprite sprite)
    {
        string buttonName = "RunMenuButton_" + index;
        RectTransform buttonRect = panel.Find(buttonName) as RectTransform;
        if (buttonRect == null)
            buttonRect = CreateRect(panel, buttonName);

        Center(buttonRect, new Vector2(0f, 114f - index * 76f), new Vector2(560f, 68f));
        Image image = GetOrAdd<Image>(buttonRect.gameObject);
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        Button button = GetOrAdd<Button>(buttonRect.gameObject);
        button.targetGraphic = image;

        RectTransform labelRect = buttonRect.Find("Label") as RectTransform;
        if (labelRect == null)
            labelRect = CreateRect(buttonRect, "Label");
        Stretch(labelRect);

        TextMeshProUGUI label = GetOrAdd<TextMeshProUGUI>(labelRect.gameObject);
        label.font = font;
        label.text = labelText;
        label.fontSize = 42f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.17f, 0.11f, 0.06f, 1f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static void EnsureMapHierarchy(GameObject root)
    {
        RectTransform mapPanel = FindDeep(root.transform, "MapPanel") as RectTransform;
        if (mapPanel == null)
            throw new MissingReferenceException("MapPanel was not found in RunHudCanvas.prefab.");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Color lineColor = new Color(0.17f, 0.15f, 0.13f, 1f);

        RectTransform titleRect = mapPanel.Find("MapTitle") as RectTransform;
        if (titleRect == null)
            titleRect = CreateRect(mapPanel, "MapTitle");
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-120f, 50f);
        TextMeshProUGUI titleLabel = GetOrAdd<TextMeshProUGUI>(titleRect.gameObject);
        titleLabel.font = font;
        titleLabel.text = "RUN MAP";
        titleLabel.fontSize = 30f;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.color = lineColor;
        titleLabel.raycastTarget = false;
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleRect.SetAsFirstSibling();

        RectTransform scroll = FindDeep(mapPanel, "MapScroll") as RectTransform;
        if (scroll == null)
            scroll = CreateRect(mapPanel, "MapScroll");
        scroll.gameObject.SetActive(true);
        Stretch(scroll);
        scroll.offsetMin = new Vector2(54f, 44f);
        scroll.offsetMax = new Vector2(-54f, -88f);

        ScrollRect scrollRect = GetOrAdd<ScrollRect>(scroll.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 42f;

        RectTransform viewport = scroll.Find("MapViewport") as RectTransform;
        if (viewport == null)
            viewport = CreateRect(scroll, "MapViewport");
        viewport.gameObject.SetActive(true);
        Stretch(viewport);
        RectMask2D viewportMask = GetOrAdd<RectMask2D>(viewport.gameObject);
        viewportMask.padding = Vector4.zero;
        viewportMask.softness = Vector2Int.zero;
        Image viewportImage = GetOrAdd<Image>(viewport.gameObject);
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;
        Mask legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
            Object.DestroyImmediate(legacyMask);

        RectTransform content = viewport.Find("MapContent") as RectTransform;
        if (content == null)
            content = CreateRect(viewport, "MapContent");
        content.gameObject.SetActive(true);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1040f, 1500f);

        RectTransform tree = content.Find("TreeMap") as RectTransform;
        if (tree == null)
            tree = CreateRect(content, "TreeMap");
        tree.gameObject.SetActive(true);
        if (tree.anchorMin == tree.anchorMax)
            Stretch(tree);

        EnsureTreeMapPreview(tree);

        scrollRect.viewport = viewport;
        scrollRect.content = content;
    }

    static void EnsureTreeMapPreview(RectTransform tree)
    {
        Transform oldPreview = tree.Find("TreeMapPreview");
        if (oldPreview != null)
            Object.DestroyImmediate(oldPreview.gameObject);

        RectTransform preview = CreateRect(tree, "TreeMapPreview");
        Stretch(preview);
        Color brown = new Color(0.27f, 0.16f, 0.09f, 1f);

        Vector2 top = new Vector2(0f, 550f);
        Vector2 left = new Vector2(-220f, 170f);
        Vector2 right = new Vector2(220f, 170f);
        Vector2 lowerLeft = new Vector2(-360f, -210f);
        Vector2 lowerMiddle = new Vector2(0f, -210f);
        Vector2 lowerRight = new Vector2(360f, -210f);

        CreatePreviewDashedLine(preview, top, left, brown);
        CreatePreviewDashedLine(preview, top, right, brown);
        CreatePreviewDashedLine(preview, left, lowerLeft, brown);
        CreatePreviewDashedLine(preview, left, lowerMiddle, brown);
        CreatePreviewDashedLine(preview, right, lowerMiddle, brown);
        CreatePreviewDashedLine(preview, right, lowerRight, brown);

        Sprite nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/UI/ui_circle.png");
        CreatePreviewNode(preview, "PreviewNode_Start", top, nodeSprite, brown, 124f);
        CreatePreviewNode(preview, "PreviewNode_Left", left, nodeSprite, brown, 124f);
        CreatePreviewNode(preview, "PreviewNode_Right", right, nodeSprite, brown, 124f);
        CreatePreviewNode(preview, "PreviewNode_LowerLeft", lowerLeft, nodeSprite, brown, 124f);
        CreatePreviewNode(preview, "PreviewNode_LowerMiddle", lowerMiddle, nodeSprite, brown, 124f);
        CreatePreviewNode(preview, "PreviewNode_LowerRight", lowerRight, nodeSprite, brown, 124f);
    }

    static void CreatePreviewDashedLine(Transform parent, Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        const float dashLength = 20f;
        const float gap = 13f;
        int dashCount = Mathf.Max(1, Mathf.FloorToInt((length + gap) / (dashLength + gap)));
        Vector2 direction = delta.normalized;
        for (int i = 0; i < dashCount; i++)
        {
            float distance = Mathf.Min(length, i * (dashLength + gap) + dashLength * 0.5f);
            RectTransform dash = CreateRect(parent, "PreviewLine");
            Center(dash, from + direction * distance, new Vector2(Mathf.Min(dashLength, length - i * (dashLength + gap)), 4f));
            dash.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image image = GetOrAdd<Image>(dash.gameObject);
            image.color = color;
            image.raycastTarget = false;
        }
    }

    static void CreatePreviewNode(Transform parent, string name, Vector2 position, Sprite sprite, Color color, float size)
    {
        RectTransform node = CreateRect(parent, name);
        Center(node, position, new Vector2(size, size));
        Image image = GetOrAdd<Image>(node.gameObject);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
    }

    static void CleanupLegacySceneOverrides()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForCleanup = !scene.IsValid() || !scene.isLoaded;
            if (openedForCleanup)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            bool changed = CleanupScene(scene);
            if (changed)
                EditorSceneManager.SaveScene(scene);

            if (openedForCleanup)
                EditorSceneManager.CloseScene(scene, true);
        }

        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            SceneManager.SetActiveScene(originalActiveScene);
    }

    static bool CleanupScene(Scene scene)
    {
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            RunHudUI[] huds = root.GetComponentsInChildren<RunHudUI>(true);
            foreach (RunHudUI hud in huds)
            {
                RunPauseMenuUI[] menus = hud.GetComponents<RunPauseMenuUI>();
                foreach (RunPauseMenuUI menu in menus)
                {
                    if (!PrefabUtility.IsAddedComponentOverride(menu))
                        continue;
                    Object.DestroyImmediate(menu);
                    changed = true;
                }

                List<GameObject> legacyPanels = new List<GameObject>();
                for (int i = 0; i < hud.transform.childCount; i++)
                {
                    GameObject child = hud.transform.GetChild(i).gameObject;
                    if (child.name == "RunMenuPanel" && PrefabUtility.IsAddedGameObjectOverride(child))
                        legacyPanels.Add(child);
                }

                foreach (GameObject panel in legacyPanels)
                {
                    Object.DestroyImmediate(panel);
                    changed = true;
                }
            }
        }

        return changed;
    }

    static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null)
            return null;
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
