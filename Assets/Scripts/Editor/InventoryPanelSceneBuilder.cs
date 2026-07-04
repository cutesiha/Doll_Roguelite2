using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InventoryPanelSceneBuilder
{
    const int StorageSlotCount = 9;
    static TMP_FontAsset font;

    [MenuItem("Game/Inventory/Rebuild Scene Inventory Panel")]
    public static void Rebuild()
    {
        GameObject canvasGO = GameObject.Find("InventoryCanvas");
        if (canvasGO == null)
            canvasGO = new GameObject("InventoryCanvas");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            canvasGO.AddComponent<GraphicRaycaster>();

        InventoryUI ui = canvasGO.GetComponent<InventoryUI>();
        if (ui == null) ui = canvasGO.AddComponent<InventoryUI>();

        for (int i = canvasGO.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasGO.transform.GetChild(i);
            if (child.name == "InventoryPanel")
                Object.DestroyImmediate(child.gameObject);
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset");

        Color panelColor = new Color(0.075f, 0.07f, 0.085f, 0.97f);
        Color sectionColor = new Color(0.135f, 0.125f, 0.145f, 1f);
        Color slotColor = new Color(0.23f, 0.22f, 0.28f, 1f);
        Color emptyColor = new Color(0.105f, 0.10f, 0.12f, 1f);
        Color lineColor = new Color(0.46f, 0.43f, 0.49f, 1f);
        Color textColor = new Color(0.94f, 0.90f, 0.82f, 1f);
        Color accentColor = new Color(0.84f, 0.64f, 0.32f, 1f);
        Color redColor = new Color(0.78f, 0.08f, 0.08f, 1f);

        GameObject panel = Rect(canvasGO.transform, "InventoryPanel", V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), Vector2.zero, V2(1680f, 900f));
        Image(panel, panelColor);
        AddBorder(panel.transform, lineColor);

        Button closeButton = BuildCloseButton(panel.transform, redColor);

        Text(panel.transform, "Title", "인벤토리", 30f, accentColor, TextAlignmentOptions.Center,
            V2(0f, 1f), V2(1f, 1f), V2(0.5f, 1f), V2(0f, -22f), V2(0f, 50f));
        Text(panel.transform, "ToggleHint", "Tab / I", 18f, new Color(0.72f, 0.68f, 0.72f, 1f), TextAlignmentOptions.MidlineLeft,
            V2(0f, 1f), V2(0f, 1f), V2(0f, 1f), V2(24f, -24f), V2(160f, 42f));

        GameObject topArea = Rect(panel.transform, "TopArea", V2(0f, 0.16f), V2(1f, 0.92f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI sewingStatus = BuildSewingStatusBar(panel.transform, lineColor, textColor, accentColor);

        GameObject left = Rect(topArea.transform, "Left_InventorySlots", V2(0f, 0f), V2(0.32f, 1f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image(left, sectionColor);
        GameObject center = Rect(topArea.transform, "Center_Character", V2(0.32f, 0f), V2(0.70f, 1f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image(center, new Color(0.115f, 0.105f, 0.13f, 1f));
        GameObject right = Rect(topArea.transform, "Right_BodyStatus", V2(0.70f, 0f), V2(1f, 1f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image(right, sectionColor);

        Image(Rect(topArea.transform, "Divider_LeftCenter", V2(0.32f, 0f), V2(0.32f, 1f), V2(0.5f, 0.5f), Vector2.zero, V2(3f, 0f)), lineColor);
        Image(Rect(topArea.transform, "Divider_CenterRight", V2(0.70f, 0f), V2(0.70f, 1f), V2(0.5f, 0.5f), Vector2.zero, V2(3f, 0f)), lineColor);

        Image[] storageImgs;
        Button[] storageBtns;
        TextMeshProUGUI[] storageNames;
        TextMeshProUGUI[] storageHps;
        BuildStorage(left.transform, emptyColor, textColor, accentColor, out storageImgs, out storageBtns, out storageNames, out storageHps);

        Image[] charImgs;
        Button[] charBtns;
        BuildCharacter(center.transform, slotColor, textColor, accentColor, out charImgs, out charBtns);

        TextMeshProUGUI[] statNames;
        TextMeshProUGUI[] statHps;
        BuildStatus(right.transform, textColor, accentColor, out statNames, out statHps);

        closeButton.transform.SetAsLastSibling();
        Wire(ui, panel, closeButton, sewingStatus, storageImgs, storageBtns, storageNames, storageHps, charImgs, charBtns, statNames, statHps);

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);
    }

    [MenuItem("Game/Inventory/Patch Character Base Images")]
    public static void PatchCharacterBaseImages()
    {
        PatchCharacterBaseImages("Assets/Prefabs/UI/InventoryCanvas.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void PatchCharacterBaseImages(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform frame = FindChildRecursive(root.transform, "CharacterImageFrame");
            if (frame == null)
                return;

            Image body = EnsureCharacterBaseImage(frame, "BodyBaseImage", V2(0f, -38f), V2(250f, 340f), new Color(0.32f, 0.29f, 0.36f, 0.55f));
            Image face = EnsureCharacterBaseImage(frame, "FaceBaseImage", V2(0f, 118f), V2(190f, 170f), new Color(0.42f, 0.37f, 0.45f, 0.55f));

            body.transform.SetSiblingIndex(0);
            face.transform.SetSiblingIndex(1);

            InventoryUI ui = root.GetComponent<InventoryUI>();
            if (ui != null)
            {
                SerializedObject so = new SerializedObject(ui);
                SerializedProperty bodyProp = so.FindProperty("_baseBodyImg");
                if (bodyProp != null) bodyProp.objectReferenceValue = body;
                SerializedProperty faceProp = so.FindProperty("_baseFaceImg");
                if (faceProp != null) faceProp.objectReferenceValue = face;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Image EnsureCharacterBaseImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
            rect = go.AddComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = V2(0.5f, 0.5f);
        rect.pivot = V2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
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

    static Button BuildCloseButton(Transform parent, Color redColor)
    {
        GameObject closeGO = Rect(parent, "CloseButton_X", V2(1f, 1f), V2(1f, 1f), V2(1f, 1f), V2(-22f, -22f), V2(72f, 72f));
        Image closeImg = Image(closeGO, redColor);
        Button closeButton = AddButton(closeGO, closeImg, new Color(1f, 0.20f, 0.16f, 1f));
        Text(closeGO.transform, "CloseButton_X_Label", "X", 42f, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return closeButton;
    }

    static TextMeshProUGUI BuildSewingStatusBar(Transform parent, Color lineColor, Color textColor, Color accentColor)
    {
        GameObject bottomBar = Rect(parent, "SewingStatusBar", V2(0f, 0f), V2(1f, 0.16f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image(bottomBar, new Color(0.12f, 0.13f, 0.11f, 1f));
        Image(Rect(parent, "SewingStatusDivider", V2(0f, 0.16f), V2(1f, 0.16f), V2(0.5f, 0.5f), Vector2.zero, V2(0f, 3f)), lineColor);
        return Text(bottomBar.transform, "SewingStatusSummary", "[재봉 상태] 빈 슬롯 0개 · 안정적", 22f, textColor, TextAlignmentOptions.MidlineLeft,
            V2(0f, 0f), V2(1f, 1f), V2(0f, 0.5f), V2(42f, 0f), V2(-84f, 0f));
    }

    static void BuildStorage(Transform parent, Color emptyColor, Color textColor, Color accentColor,
        out Image[] storageImgs, out Button[] storageBtns, out TextMeshProUGUI[] storageNames, out TextMeshProUGUI[] storageHps)
    {
        Text(parent, "LeftHeader", "[인벤토리 슬롯]", 24f, accentColor, TextAlignmentOptions.Center,
            V2(0f, 1f), V2(1f, 1f), V2(0.5f, 1f), V2(0f, -54f), V2(0f, 48f));

        storageImgs = new Image[StorageSlotCount];
        storageBtns = new Button[StorageSlotCount];
        storageNames = new TextMeshProUGUI[StorageSlotCount];
        storageHps = new TextMeshProUGUI[StorageSlotCount];

        for (int i = 0; i < StorageSlotCount; i++)
        {
            int col = i % 3;
            int row = i / 3;
            float x = (col - 1) * 164f;
            float y = -96f - row * 106f;
            GameObject slot = Rect(parent, "StorageSlot_" + (i + 1), V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(x, y), V2(148f, 92f));
            storageImgs[i] = Image(slot, emptyColor);
            storageBtns[i] = AddButton(slot, storageImgs[i], new Color(0.42f, 0.38f, 0.48f, 1f));
            AddStorageDragSource(slot, i);
            AddStorageDropTarget(slot, i);
            Text(slot.transform, "SlotLabel", "슬롯 " + (i + 1), 22f, textColor, TextAlignmentOptions.Center,
                V2(0f, 0.43f), V2(1f, 1f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            storageNames[i] = Text(slot.transform, "SlotName", "빈 슬롯", 17f, new Color(0.78f, 0.74f, 0.80f, 1f), TextAlignmentOptions.Center,
                V2(0f, 0.12f), V2(1f, 0.48f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            storageHps[i] = Text(slot.transform, "SlotHP", "", 19f, accentColor, TextAlignmentOptions.Center,
                V2(0f, 0f), V2(1f, 0.20f), V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        Text(parent, "DragHint", "[부위를 끌어 놓기]", 21f, textColor, TextAlignmentOptions.Center,
            V2(0f, 0f), V2(1f, 0f), V2(0.5f, 0f), V2(0f, 92f), V2(0f, 42f));
        Text(parent, "DropHint", "몸 ↔ 슬롯 드래그 앤 드롭", 15f, new Color(0.62f, 0.58f, 0.64f, 1f), TextAlignmentOptions.Center,
            V2(0f, 0f), V2(1f, 0f), V2(0.5f, 0f), V2(0f, 58f), V2(0f, 30f));
    }

    static void BuildCharacter(Transform parent, Color slotColor, Color textColor, Color accentColor, out Image[] charImgs, out Button[] charBtns)
    {
        Text(parent, "CenterHeader", "[내 캐릭터]", 24f, accentColor, TextAlignmentOptions.Center,
            V2(0f, 1f), V2(1f, 1f), V2(0.5f, 1f), V2(0f, -54f), V2(0f, 48f));

        GameObject frame = Rect(parent, "CharacterImageFrame", V2(0.5f, 0.50f), V2(0.5f, 0.50f), V2(0.5f, 0.5f), Vector2.zero, V2(400f, 520f));
        Image(frame, new Color(0.08f, 0.075f, 0.095f, 1f));
        Outline outline = frame.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = V2(3f, -3f);

        Text(frame.transform, "CharacterImageText", "캐릭터\n이미지", 36f, new Color(0.70f, 0.66f, 0.72f, 1f), TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image bodyBase = Image(Rect(frame.transform, "BodyBaseImage", V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, -38f), V2(250f, 340f)),
            new Color(0.32f, 0.29f, 0.36f, 0.55f));
        bodyBase.raycastTarget = false;
        bodyBase.preserveAspect = true;

        Image faceBase = Image(Rect(frame.transform, "FaceBaseImage", V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 118f), V2(190f, 170f)),
            new Color(0.42f, 0.37f, 0.45f, 0.55f));
        faceBase.raycastTarget = false;
        faceBase.preserveAspect = true;

        bodyBase.transform.SetSiblingIndex(0);
        faceBase.transform.SetSiblingIndex(1);

        charImgs = new Image[6];
        charBtns = new Button[6];
        string[] partNames = { "EyeLeft", "EyeRight", "ArmLeft", "ArmRight", "LegLeft", "LegRight" };
        string[] partLabels = { "눈 L", "눈 R", "팔 L", "팔 R", "다리 L", "다리 R" };
        Vector2[] partPos = { V2(-74f, 154f), V2(74f, 154f), V2(-190f, 22f), V2(190f, 22f), V2(-76f, -192f), V2(76f, -192f) };
        Vector2[] partSize = { V2(102f, 58f), V2(102f, 58f), V2(96f, 190f), V2(96f, 190f), V2(106f, 150f), V2(106f, 150f) };

        for (int i = 0; i < 6; i++)
        {
            GameObject part = Rect(frame.transform, "EquipPart_" + partNames[i], V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), partPos[i], partSize[i]);
            charImgs[i] = Image(part, slotColor);
            charBtns[i] = AddButton(part, charImgs[i], new Color(0.68f, 0.24f, 0.24f, 1f));
            AddEquippedDragSource(part, (BodySlot)i);
            AddEquipDropTarget(part, (BodySlot)i);
            Text(part.transform, "PartLabel", partLabels[i], 16f, Color.white, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, V2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        Text(parent, "UnequipHint", "부위를 슬롯으로 끌면 보관, 슬롯에서 몸으로 끌면 장착", 16f, new Color(0.66f, 0.62f, 0.68f, 1f), TextAlignmentOptions.Center,
            V2(0f, 0f), V2(1f, 0f), V2(0.5f, 0f), V2(0f, 42f), V2(0f, 34f));
    }

    static void BuildStatus(Transform parent, Color textColor, Color accentColor, out TextMeshProUGUI[] statNames, out TextMeshProUGUI[] statHps)
    {
        Text(parent, "RightHeader", "[부위 상태]", 24f, accentColor, TextAlignmentOptions.Center,
            V2(0f, 1f), V2(1f, 1f), V2(0.5f, 1f), V2(0f, -54f), V2(0f, 48f));

        statNames = new TextMeshProUGUI[7];
        statHps = new TextMeshProUGUI[7];
        string[] rowLabels = { "눈 L", "눈 R", "팔 L", "팔 R", "다리 L", "다리 R", "몸" };
        string[] rowItems = { "단추", "단추", "헝겊", "헝겊", "천", "천", "천" };

        for (int i = 0; i < 7; i++)
        {
            GameObject row = Rect(parent, "StatusRow_" + rowLabels[i].Replace(" ", ""), V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, -116f - i * 80f), V2(380f, 68f));
            Image(row, i % 2 == 0 ? new Color(0.18f, 0.17f, 0.21f, 1f) : new Color(0.14f, 0.135f, 0.165f, 1f));
            Text(row.transform, "PartType", rowLabels[i] + ":", 20f, accentColor, TextAlignmentOptions.MidlineLeft,
                V2(0f, 0.45f), V2(0.34f, 1f), V2(0f, 0.5f), V2(20f, 0f), Vector2.zero);
            statNames[i] = Text(row.transform, "PartName", rowItems[i], 20f, textColor, TextAlignmentOptions.MidlineLeft,
                V2(0.32f, 0.45f), V2(1f, 1f), V2(0f, 0.5f), Vector2.zero, Vector2.zero);
            Text(row.transform, "HPLabel", "HP:", 18f, new Color(0.72f, 0.68f, 0.72f, 1f), TextAlignmentOptions.MidlineLeft,
                V2(0f, 0f), V2(0.34f, 0.52f), V2(0f, 0.5f), V2(20f, 0f), Vector2.zero);
            statHps[i] = Text(row.transform, "HPDots", "●●●○", 20f, accentColor, TextAlignmentOptions.MidlineLeft,
                V2(0.32f, 0f), V2(1f, 0.52f), V2(0f, 0.5f), Vector2.zero, Vector2.zero);
        }
    }

    static void Wire(InventoryUI ui, GameObject panel, Button closeButton, TextMeshProUGUI sewingStatus, Image[] storageImgs, Button[] storageBtns,
        TextMeshProUGUI[] storageNames, TextMeshProUGUI[] storageHps, Image[] charImgs, Button[] charBtns,
        TextMeshProUGUI[] statNames, TextMeshProUGUI[] statHps)
    {
        SerializedObject so = new SerializedObject(ui);
        SerializedProperty panelProp = so.FindProperty("_panel");
        if (panelProp != null) panelProp.objectReferenceValue = panel;
        SerializedProperty closeProp = so.FindProperty("_closeButton");
        if (closeProp != null) closeProp.objectReferenceValue = closeButton;
        SerializedProperty sewingStatusProp = so.FindProperty("_sewingStatus");
        if (sewingStatusProp != null) sewingStatusProp.objectReferenceValue = sewingStatus;

        AssignArray(so, "_storageImg", storageImgs);
        AssignArray(so, "_storageBtn", storageBtns);
        AssignArray(so, "_storageName", storageNames);
        AssignArray(so, "_storageHp", storageHps);
        AssignArray(so, "_charImg", charImgs);
        AssignArray(so, "_charBtn", charBtns);
        AssignArray(so, "_statName", statNames);
        AssignArray(so, "_statHp", statHps);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignArray(SerializedObject so, string propertyName, Object[] values)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null) return;
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void AddStorageDragSource(GameObject target, int storageIndex)
    {
        System.Type type = System.Type.GetType("InventoryStorageDragSource, Assembly-CSharp");
        if (type == null) return;

        Component component = target.AddComponent(type);
        System.Reflection.MethodInfo setter = type.GetMethod("SetStorageIndex");
        if (setter != null)
            setter.Invoke(component, new object[] { storageIndex });
    }

    static void AddStorageDropTarget(GameObject target, int storageIndex)
    {
        System.Type type = System.Type.GetType("InventoryStorageDropTarget, Assembly-CSharp");
        if (type == null) return;

        Component component = target.AddComponent(type);
        System.Reflection.MethodInfo setter = type.GetMethod("SetStorageIndex");
        if (setter != null)
            setter.Invoke(component, new object[] { storageIndex });
    }

    static void AddEquippedDragSource(GameObject target, BodySlot bodySlot)
    {
        System.Type type = System.Type.GetType("InventoryEquippedDragSource, Assembly-CSharp");
        if (type == null) return;

        Component component = target.AddComponent(type);
        System.Reflection.MethodInfo setter = type.GetMethod("SetBodySlot");
        if (setter != null)
            setter.Invoke(component, new object[] { bodySlot });
    }

    static void AddEquipDropTarget(GameObject target, BodySlot acceptedSlot)
    {
        System.Type type = System.Type.GetType("InventoryEquipDropTarget, Assembly-CSharp");
        if (type == null) return;

        Component component = target.AddComponent(type);
        System.Reflection.MethodInfo setter = type.GetMethod("SetAcceptedSlot");
        if (setter != null)
            setter.Invoke(component, new object[] { acceptedSlot });
    }

    static void AddBorder(Transform parent, Color lineColor)
    {
        Image(Rect(parent, "Border_Top", V2(0f, 1f), V2(1f, 1f), V2(0.5f, 1f), Vector2.zero, V2(0f, 4f)), lineColor);
        Image(Rect(parent, "Border_Bottom", V2(0f, 0f), V2(1f, 0f), V2(0.5f, 0f), Vector2.zero, V2(0f, 4f)), lineColor);
        Image(Rect(parent, "Border_Left", V2(0f, 0f), V2(0f, 1f), V2(0f, 0.5f), Vector2.zero, V2(4f, 0f)), lineColor);
        Image(Rect(parent, "Border_Right", V2(1f, 0f), V2(1f, 1f), V2(1f, 0.5f), Vector2.zero, V2(4f, 0f)), lineColor);
    }

    static GameObject Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return go;
    }

    static Image Image(GameObject go, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 rectSize)
    {
        GameObject go = Rect(parent, name, anchorMin, anchorMax, pivot, anchoredPos, rectSize);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    static Button AddButton(GameObject go, Graphic targetGraphic, Color highlight)
    {
        Button button = go.AddComponent<Button>();
        button.targetGraphic = targetGraphic;
        ColorBlock colors = button.colors;
        colors.highlightedColor = highlight;
        colors.pressedColor = new Color(highlight.r * 0.75f, highlight.g * 0.75f, highlight.b * 0.75f, 1f);
        colors.selectedColor = highlight;
        button.colors = colors;
        return button;
    }

    static Vector2 V2(float x, float y)
    {
        return new Vector2(x, y);
    }
}
