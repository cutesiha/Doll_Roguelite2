using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단 메뉴 Game > 인벤토리 UI 생성 으로 씬에 인벤토리 Canvas를 만들어줍니다.
/// 조정 후 Game > 인벤토리 UI 프리팹 저장 으로 Resources에 저장하세요.
/// </summary>
public static class InventoryUICreator
{
    // ── 레이아웃 상수 (여기서 크기/비율 조정) ─────────────────────────
    const float RefW = 1920f;
    const float RefH = 1080f;
    const float PW   = 1700f;
    const float PH   = 940f;
    const float TopH    = 48f;
    const float BottomH = 72f;
    const int StorageSlotCount = 9;
    const float LeftRatio   = 0.30f;
    const float CenterRatio = 0.38f;
    const float RightRatio  = 0.32f;

    // ── 색상 ───────────────────────────────────────────────────────────
    static Color CPanel    = new Color(0.07f, 0.07f, 0.09f, 0.97f);
    static Color CSection  = new Color(0.11f, 0.11f, 0.14f, 1f);
    static Color CSlot     = new Color(0.20f, 0.20f, 0.27f, 1f);
    static Color CEmpty    = new Color(0.13f, 0.13f, 0.17f, 1f);
    static Color CFixed    = new Color(0.10f, 0.18f, 0.10f, 1f);
    static Color CDivider  = new Color(0.25f, 0.25f, 0.30f, 1f);
    static Color CHeader   = new Color(0.70f, 0.85f, 1.00f, 1f);
    static Color CDiary    = new Color(0.12f, 0.10f, 0.08f, 1f);
    static Color CDiaryTxt = new Color(0.80f, 0.70f, 0.55f, 1f);
    static Color CClose    = new Color(0.55f, 0.12f, 0.12f, 1f);
    static Color CHighlight = new Color(0.40f, 0.65f, 1.00f, 1f);
    static Color CUnequip   = new Color(1.00f, 0.35f, 0.35f, 1f);

    static TMP_FontAsset _font;
    static TMP_FontAsset Font()
    {
        if (_font != null) return _font;
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset");
        return _font;
    }

    // ── 메뉴 ───────────────────────────────────────────────────────────
    [MenuItem("Game/인벤토리 UI 생성 (씬에 추가)")]
    static void Create()
    {
        // 기존에 있으면 제거
        var existing = GameObject.Find("InventoryCanvas");
        if (existing != null)
        {
            bool ok = EditorUtility.DisplayDialog("기존 UI 삭제",
                "씬에 InventoryCanvas가 이미 있습니다. 삭제하고 새로 만들까요?", "삭제 후 재생성", "취소");
            if (!ok) return;
            Undo.DestroyObjectImmediate(existing);
        }

        var root = BuildCanvas();
        Undo.RegisterCreatedObjectUndo(root, "Create InventoryCanvas");
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("[InventoryUI] 씬에 생성 완료. 조정 후 상단 메뉴 Game > 인벤토리 UI 프리팹 저장을 눌러주세요.");
    }

    [MenuItem("Game/인벤토리 UI 프리팹 저장 (Resources)")]
    static void SavePrefab()
    {
        var root = GameObject.Find("InventoryCanvas");
        if (root == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 InventoryCanvas가 없습니다. 먼저 '인벤토리 UI 생성'을 실행하세요.", "확인");
            return;
        }

        // Resources 폴더 없으면 생성
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var path = "Assets/Resources/InventoryCanvas.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
        AssetDatabase.Refresh();
        Debug.Log($"[InventoryUI] 저장 완료: {path}");
    }

    // ── 전체 계층 생성 ─────────────────────────────────────────────────
    static GameObject BuildCanvas()
    {
        // Canvas
        var canvasGO = new GameObject("InventoryCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // InventoryUI 컴포넌트
        var ui = canvasGO.AddComponent<InventoryUI>();

        // 패널
        var panel = MakeRect(canvasGO.transform, "InventoryPanel", V2(0, 0), V2(PW, PH));
        Img(panel, CPanel);

        // 닫기 버튼
        var closeGO = MakeRect(panel.transform, "CloseBtn", V2(PW * 0.5f - 30f, PH * 0.5f - 30f), V2(44f, 44f));
        var closeImg = Img(closeGO, CClose);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        SetHover(closeBtn, new Color(0.8f, 0.2f, 0.2f, 1f));
        Txt(closeGO.transform, "✕", 20f, Color.white, TextAlignmentOptions.Center);

        // 타이틀
        var titleGO = MakeRect(panel.transform, "TitleBar", V2(0, PH * 0.5f - TopH * 0.5f), V2(PW, TopH));
        Img(titleGO, new Color(0.10f, 0.10f, 0.14f, 1f));
        Txt(titleGO.transform, "인  벤  토  리", 22f, CHeader, TextAlignmentOptions.Center);

        // 3분할 영역
        float contentH = PH - TopH - BottomH;
        float contentY = -(TopH * 0.5f);
        float lW = PW * LeftRatio;
        float cW = PW * CenterRatio;
        float rW = PW * RightRatio;
        float lX = -(cW * 0.5f + lW * 0.5f);
        float cX = 0f;
        float rX =  cW * 0.5f + rW * 0.5f;

        var leftBg   = BuildLeft  (panel.transform, lX, contentY, lW, contentH, ui);
        var centerBg = BuildCenter(panel.transform, cX, contentY, cW, contentH, ui);
        var rightBg  = BuildRight (panel.transform, rX, contentY, rW, contentH, ui);

        // 구분선
        Divider(panel.transform, lX + lW * 0.5f, contentY, contentH);
        Divider(panel.transform, rX - rW * 0.5f, contentY, contentH);

        // 하단 일기 바
        BuildBottom(panel.transform);

        // SerializeField 연결
        WireReferences(ui, panel, closeBtn);

        return canvasGO;
    }

    // ── 좌측: 인벤토리 슬롯 ───────────────────────────────────────────
    static GameObject BuildLeft(Transform parent, float x, float y, float w, float h,
                                InventoryUI ui)
    {
        var bg = MakeRect(parent, "LeftSection", V2(x, y), V2(w, h));
        Img(bg, CSection);
        Txt(bg.transform, "[ 인벤토리 슬롯 ]", 16f, CHeader, TextAlignmentOptions.Center,
            V2(0, h * 0.5f - 22f), V2(w - 16f, 34f));

        float slotW = w * 0.28f;
        float slotH = (h - 150f) / 3f;
        float gapX = w * 0.04f;
        float gapY = 12f;

        var so = new SerializedObject(ui);
        so.FindProperty("_storageImg").arraySize = StorageSlotCount;
        so.FindProperty("_storageBtn").arraySize = StorageSlotCount;
        so.FindProperty("_storageName").arraySize = StorageSlotCount;
        so.FindProperty("_storageHp").arraySize = StorageSlotCount;

        for (int i = 0; i < StorageSlotCount; i++)
        {
            int col = i % 3;
            int row = i / 3;
            float sx = (col - 1) * (slotW + gapX);
            float sy = h * 0.5f - 76f - slotH * 0.5f - row * (slotH + gapY);
            var slotGO = MakeRect(bg.transform, $"StorageSlot_{i + 1}", V2(sx, sy), V2(slotW, slotH));
            var slotImg = Img(slotGO, CEmpty);
            var btn     = slotGO.AddComponent<Button>();
            btn.targetGraphic = slotImg;
            SetHover(btn, CHighlight);

            // 번호 배지
            Txt(slotGO.transform, $"{i + 1}", 12f, new Color(0.5f, 0.5f, 0.5f),
                TextAlignmentOptions.Center,
                V2(-slotW * 0.5f + 14f, slotH * 0.5f - 14f), V2(22f, 22f));

            // 부위 이름
            var nameTxt = Txt(slotGO.transform, "빈 슬롯", 16f, Color.white,
                TextAlignmentOptions.Center,
                V2(0, slotH * 0.15f), V2(slotW - 12f, slotH * 0.38f));

            // HP 도트
            var hpTxt = Txt(slotGO.transform, "", 18f, new Color(0.85f, 0.60f, 0.20f),
                TextAlignmentOptions.Center,
                V2(0, -slotH * 0.20f), V2(slotW - 12f, slotH * 0.28f));

            // InventoryUI 참조 연결
            so.FindProperty("_storageImg").GetArrayElementAtIndex(i).objectReferenceValue  = slotImg;
            so.FindProperty("_storageBtn").GetArrayElementAtIndex(i).objectReferenceValue  = btn;
            so.FindProperty("_storageName").GetArrayElementAtIndex(i).objectReferenceValue = nameTxt;
            so.FindProperty("_storageHp").GetArrayElementAtIndex(i).objectReferenceValue   = hpTxt;
        }

        so.ApplyModifiedProperties();

        Txt(bg.transform, "클릭하여 장착", 13f, new Color(0.45f, 0.45f, 0.50f),
            TextAlignmentOptions.Center,
            V2(0, -h * 0.5f + 22f), V2(w - 16f, 26f));

        return bg;
    }

    // ── 중앙: 캐릭터 ──────────────────────────────────────────────────
    static GameObject BuildCenter(Transform parent, float x, float y, float w, float h,
                                  InventoryUI ui)
    {
        var bg = MakeRect(parent, "CenterSection", V2(x, y), V2(w, h));
        Img(bg, CSection);
        Txt(bg.transform, "[ 캐릭터 ]", 16f, CHeader, TextAlignmentOptions.Center,
            V2(0, h * 0.5f - 22f), V2(w - 16f, 34f));

        float dw = w - 40f;
        float dh = h - 80f;

        // 몸통 (고정)
        var bodyGO = MakeRect(bg.transform, "Body_Fixed", V2(0, dh * 0.04f), V2(dw * 0.38f, dh * 0.42f));
        Img(bodyGO, CFixed);
        Txt(bodyGO.transform, "몸\n(고정)", 14f, new Color(0.7f, 0.9f, 0.7f), TextAlignmentOptions.Center);

        // 6개 부위
        var parts = new (string name, BodySlot slot, float rx, float ry, float pw, float ph, string lbl)[]
        {
            ("Part_EyeLeft",  BodySlot.EyeLeft,  -0.19f,  0.37f, 0.22f, 0.12f, "눈 (왼)"),
            ("Part_EyeRight", BodySlot.EyeRight,  0.19f,  0.37f, 0.22f, 0.12f, "눈 (오)"),
            ("Part_ArmLeft",  BodySlot.ArmLeft,  -0.43f,  0.06f, 0.19f, 0.32f, "팔\n(왼)"),
            ("Part_ArmRight", BodySlot.ArmRight,  0.43f,  0.06f, 0.19f, 0.32f, "팔\n(오)"),
            ("Part_LegLeft",  BodySlot.LegLeft,  -0.14f, -0.36f, 0.22f, 0.28f, "다리\n(왼)"),
            ("Part_LegRight", BodySlot.LegRight,  0.14f, -0.36f, 0.22f, 0.28f, "다리\n(오)"),
        };

        for (int i = 0; i < parts.Length; i++)
        {
            var p     = parts[i];
            var partGO = MakeRect(bg.transform, p.name,
                V2(p.rx * dw, p.ry * dh), V2(p.pw * dw, p.ph * dh));
            var partImg = Img(partGO, CSlot);
            var btn     = partGO.AddComponent<Button>();
            btn.targetGraphic = partImg;
            SetHover(btn, CUnequip);
            Txt(partGO.transform, p.lbl, 12f, Color.white, TextAlignmentOptions.Center);

            var so = new SerializedObject(ui);
            so.FindProperty("_charImg").GetArrayElementAtIndex(i).objectReferenceValue = partImg;
            so.FindProperty("_charBtn").GetArrayElementAtIndex(i).objectReferenceValue = btn;
            so.ApplyModifiedProperties();
        }

        Txt(bg.transform, "부위 클릭 → 보관함으로", 12f, new Color(0.45f, 0.45f, 0.50f),
            TextAlignmentOptions.Center,
            V2(0, -h * 0.5f + 22f), V2(w - 16f, 26f));

        return bg;
    }

    // ── 우측: 부위 상태 ───────────────────────────────────────────────
    static GameObject BuildRight(Transform parent, float x, float y, float w, float h,
                                 InventoryUI ui)
    {
        var bg = MakeRect(parent, "RightSection", V2(x, y), V2(w, h));
        Img(bg, CSection);
        Txt(bg.transform, "[ 부위 상태 ]", 16f, CHeader, TextAlignmentOptions.Center,
            V2(0, h * 0.5f - 22f), V2(w - 16f, 34f));

        string[] labels = { "눈 (왼)", "눈 (오)", "팔 (왼)", "팔 (오)", "다리 (왼)", "다리 (오)", "몸" };
        float rowH   = (h - 60f) / 7f;
        float startY = h * 0.5f - 50f - rowH * 0.5f;

        for (int i = 0; i < 7; i++)
        {
            float ry  = startY - i * rowH;
            var row   = MakeRect(bg.transform, $"Row_{labels[i]}", V2(0, ry), V2(w - 16f, rowH - 3f));
            Img(row, i % 2 == 0 ? new Color(0.17f, 0.17f, 0.21f) : new Color(0.14f, 0.14f, 0.18f));

            float lw  = (w - 16f) * 0.42f;
            float rw2 = (w - 16f) * 0.55f;
            float lx  = -(w - 16f) * 0.5f + lw * 0.5f + 6f;
            float rx  =  (w - 16f) * 0.5f - rw2 * 0.5f - 6f;

            Txt(row.transform, labels[i], 13f, CHeader,
                TextAlignmentOptions.MidlineLeft,
                V2(lx, 0), V2(lw, rowH - 3f));

            var nameTxt = Txt(row.transform, "-", 12f, Color.white,
                TextAlignmentOptions.MidlineLeft,
                V2(rx, (rowH - 3f) * 0.22f), V2(rw2, (rowH - 3f) * 0.48f));

            var hpTxt = Txt(row.transform, "", 15f, new Color(0.85f, 0.60f, 0.20f),
                TextAlignmentOptions.MidlineLeft,
                V2(rx, -(rowH - 3f) * 0.22f), V2(rw2, (rowH - 3f) * 0.42f));

            var so = new SerializedObject(ui);
            so.FindProperty("_statName").GetArrayElementAtIndex(i).objectReferenceValue = nameTxt;
            so.FindProperty("_statHp").GetArrayElementAtIndex(i).objectReferenceValue   = hpTxt;
            so.ApplyModifiedProperties();
        }

        return bg;
    }

    // ── 하단: 인형의 일기 ─────────────────────────────────────────────
    static void BuildBottom(Transform parent)
    {
        float by = -(PH * 0.5f - BottomH * 0.5f);

        var line = MakeRect(parent, "DiaryDivider", V2(0, by + BottomH * 0.5f), V2(PW, 2f));
        Img(line, CDivider);

        var bar = MakeRect(parent, "DiaryBar", V2(0, by), V2(PW, BottomH));
        Img(bar, CDiary);

        Txt(bar.transform, "≡",                       26f, CDiaryTxt, TextAlignmentOptions.Center,
            V2(-PW * 0.5f + 38f,  0), V2(36f, BottomH));
        Txt(bar.transform, "[ 인형의 일기 ]",          15f, CDiaryTxt, TextAlignmentOptions.MidlineLeft,
            V2(-PW * 0.5f + 130f, 0), V2(200f, BottomH));
        Txt(bar.transform, "→  지금까지 모은 회상 페이지", 13f, new Color(0.55f, 0.48f, 0.38f),
            TextAlignmentOptions.MidlineLeft,
            V2(0, 0), V2(PW * 0.5f, BottomH));
    }

    // ── 패널/닫기 버튼 참조 연결 ──────────────────────────────────────
    static void WireReferences(InventoryUI ui, GameObject panel, Button closeBtn)
    {
        var so = new SerializedObject(ui);
        so.FindProperty("_panel").objectReferenceValue = panel;
        so.ApplyModifiedProperties();

        // 닫기 버튼: Panel을 끄는 동작은 런타임에 InventoryUI.Update()가 처리.
        // 닫기 버튼 onClick은 별도 PanelToggle로 연결 (에디터에서 직접 연결 권장)
        // 프리팹 저장 후 Inspector에서 CloseBtn > onClick에 패널.SetActive(false) 연결하세요.
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────
    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static GameObject MakeRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = V2(0.5f, 0.5f);
        rt.pivot = V2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    static Image Img(GameObject go, Color color)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // 별도 위치/크기를 가진 텍스트 GO 생성
    static TextMeshProUGUI Txt(Transform parent, string text, float size, Color color,
                               TextAlignmentOptions align, Vector2 pos, Vector2 sz)
    {
        var go = MakeRect(parent, "Txt_" + text.Replace("\n",""), pos, sz);
        return ApplyTxt(go, text, size, color, align);
    }

    // 부모를 꽉 채우는 텍스트 GO 생성
    static TextMeshProUGUI Txt(Transform parent, string text, float size, Color color,
                               TextAlignmentOptions align)
    {
        var go = new GameObject("Txt_" + text.Replace("\n",""));
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return ApplyTxt(go, text, size, color, align);
    }

    static TextMeshProUGUI ApplyTxt(GameObject go, string text, float size,
                                    Color color, TextAlignmentOptions align)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        var f = Font();
        if (f != null) tmp.font = f;
        return tmp;
    }

    static void SetHover(Button btn, Color highlight)
    {
        var c = btn.colors;
        c.highlightedColor = highlight;
        c.pressedColor     = new Color(highlight.r * 0.65f, highlight.g * 0.65f, highlight.b * 0.65f, 1f);
        btn.colors = c;
    }

    static void Divider(Transform parent, float x, float y, float h)
    {
        var go = MakeRect(parent, "Divider", V2(x, y), V2(2f, h));
        Img(go, CDivider);
    }
}
