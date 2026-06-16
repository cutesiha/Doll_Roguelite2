using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class InventoryUI : MonoBehaviour
{
    const string EditorInventoryPrefabPath = "Assets/Prefabs/UI/InventoryCanvas.prefab";

    // Bootstrap: Resources/InventoryCanvas 프리팹을 로드
    // 씬에 이미 있으면 아무것도 안 함
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) return;
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorInventoryPrefabPath);
#endif
        if (prefab == null)
            prefab = Resources.Load<GameObject>("InventoryCanvas");

        if (prefab == null)
        {
            Debug.LogWarning("[InventoryUI] Resources/InventoryCanvas 프리팹 없음. 에디터 메뉴 Game > 인벤토리 UI 생성 후 저장하세요.");
            return;
        }
        var go = Instantiate(prefab);
        DontDestroyOnLoad(go);
    }

    // ── Inspector 연결 필드 ────────────────────────────────────────────
    [Header("패널 루트 (Tab/I 로 토글)")]
    [SerializeField] GameObject _panel;
    [SerializeField] Button _closeButton;

    [Header("보관 슬롯 ×2")]
    [SerializeField] Image[]           _storageImg  = new Image[2];
    [SerializeField] Button[]          _storageBtn  = new Button[2];
    [SerializeField] TextMeshProUGUI[] _storageName = new TextMeshProUGUI[2];
    [SerializeField] TextMeshProUGUI[] _storageHp   = new TextMeshProUGUI[2];

    [Header("캐릭터 부위 (EyeLeft=0 ~ LegRight=5)")]
    [SerializeField] Image[]  _charImg = new Image[6];
    [SerializeField] Button[] _charBtn = new Button[6];
    [SerializeField, Range(0f, 1f)] float partAlphaHitThreshold = 0.1f;

    [Header("Character Base Images")]
    [SerializeField] Sprite _baseBodySprite;
    [SerializeField] Sprite _baseFaceSprite;
    [SerializeField] Image _baseBodyImg;
    [SerializeField] Image _baseFaceImg;

    [Header("부위 상태 텍스트 (0~5=슬롯, 6=몸)")]
    [SerializeField] TextMeshProUGUI[] _statName = new TextMeshProUGUI[7];
    [SerializeField] TextMeshProUGUI[] _statHp   = new TextMeshProUGUI[7];

    [Header("재봉 상태")]
    [SerializeField] TextMeshProUGUI _sewingStatus;

    GameObject _toggleHotspot;
    Button _toggleHotspotButton;

    // ── 색상 ───────────────────────────────────────────────────────────
    static readonly Color CSlot  = new Color(0.88f, 0.48f, 0.24f, 1f);
    static readonly Color CEmpty = new Color(0.17f, 0.15f, 0.13f, 0.20f);
    static readonly Color CUnequippedPart = new Color(0.04f, 0.035f, 0.03f, 0.48f);
    static readonly string[] PartSpriteNames =
    {
        "eye_left",
        "eye_right",
        "arm_left",
        "arm_right",
        "leg_left",
        "leg_right"
    };

    static Color HpColor(BodyPart p)
    {
        float r = (float)p.currentHp / p.maxHp;
        if (r > 0.66f) return new Color(1.00f, 0.85f, 0.23f, 1f);
        if (r > 0.33f) return new Color(0.94f, 0.62f, 0.15f, 1f);
        return new Color(0.75f, 0.08f, 0.06f, 1f);
    }

    // ── Unity 수명 ─────────────────────────────────────────────────────
    void Awake()
    {
        EnsurePanelReference();
        ForceClosePanelImmediate();
        NormalizeCanvasTransform();
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        WireClicks();
        EnsureToggleHotspot();
        EnsureCharacterBaseImages();
        ApplyInventoryHitTesting();
        DisableTextRaycasts();
    }

    void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;
        ForceClosePanelImmediate();
        RefreshUI();
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || _panel == null) return;
        if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
        {
            TogglePanel();
        }
    }

    void WireClicks()
    {
        _closeButton?.onClick.AddListener(ClosePanel);

        // Equip/unequip is intentionally drag-and-drop only.
    }

    public void ClosePanel()
    {
        EnsurePanelReference();
        if (_panel != null) _panel.SetActive(false);
        SetToggleHotspotVisible(false);
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        EnsurePanelReference();
        NormalizeCanvasTransform();
        EnsureToggleHotspot();
        EnsureCharacterBaseImages();
        ApplyInventoryHitTesting();
        if (_panel == null) return;
        _panel.SetActive(true);
        SetToggleHotspotVisible(true);
        RefreshUI();
    }

    void DisableTextRaycasts()
    {
        SetRaycastTargets(_storageName, false);
        SetRaycastTargets(_storageHp, false);
        SetRaycastTargets(_statName, false);
        SetRaycastTargets(_statHp, false);
        if (_sewingStatus != null)
            _sewingStatus.raycastTarget = false;
    }

    void SetRaycastTargets(TextMeshProUGUI[] labels, bool value)
    {
        if (labels == null)
            return;

        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null)
                labels[i].raycastTarget = value;
    }

    public void TogglePanel()
    {
        EnsurePanelReference();
        if (_panel == null) return;
        if (_panel.activeSelf) ClosePanel();
        else OpenPanel();
    }

    public bool IsOpen
    {
        get
        {
            EnsurePanelReference();
            return _panel != null && _panel.activeSelf;
        }
    }

    public bool IsScreenPointInsidePanel(Vector2 screenPoint)
    {
        EnsurePanelReference();
        if (_panel == null || !_panel.activeSelf) return false;
        RectTransform rect = _panel.transform as RectTransform;
        if (rect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);
    }

    void EnsurePanelReference()
    {
        if (_panel != null)
            return;

        Transform panel = FindChildRecursive(transform, "InventoryPanel");
        if (panel != null)
            _panel = panel.gameObject;
    }

    void ForceClosePanelImmediate()
    {
        EnsurePanelReference();
        if (_panel != null)
            _panel.SetActive(false);
        SetToggleHotspotVisible(false);
    }

    void NormalizeCanvasTransform()
    {
        bool isTopLevelCanvas = transform.parent == null;
        if (isTopLevelCanvas)
            transform.localScale = Vector3.one;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null && isTopLevelCanvas)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null && isTopLevelCanvas)
        {
            scaler.enabled = true;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = true;
    }

    void EnsureToggleHotspot()
    {
        if (_toggleHotspot != null)
            return;

        Transform existing = transform.Find("InventoryToggleHotspot");
        if (existing != null)
        {
            _toggleHotspot = existing.gameObject;
            _toggleHotspotButton = _toggleHotspot.GetComponent<Button>();
        }
        else
        {
            _toggleHotspot = new GameObject("InventoryToggleHotspot");
            _toggleHotspot.transform.SetParent(transform, false);

            RectTransform rect = _toggleHotspot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(32f, 28f);
            rect.sizeDelta = new Vector2(132f, 100f);

            Image image = _toggleHotspot.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;

            _toggleHotspotButton = _toggleHotspot.AddComponent<Button>();
            _toggleHotspotButton.targetGraphic = image;
        }

        if (_toggleHotspotButton != null)
        {
            _toggleHotspotButton.onClick.RemoveListener(ClosePanel);
            _toggleHotspotButton.onClick.AddListener(ClosePanel);
        }

        SetToggleHotspotVisible(false);
    }

    void SetToggleHotspotVisible(bool visible)
    {
        if (_toggleHotspot == null)
            return;

        if (visible)
            _toggleHotspot.transform.SetAsLastSibling();
        _toggleHotspot.SetActive(visible);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        var eventGO = new GameObject("RuntimeEventSystem");
        eventGO.hideFlags = HideFlags.HideInHierarchy;
        eventGO.transform.position = new Vector3(99999f, 99999f, 99999f);
        DontDestroyOnLoad(eventGO);
        eventGO.AddComponent<UnityEngine.EventSystems.EventSystem>();

        var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            eventGO.AddComponent(inputModuleType);
        else
            eventGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ── RefreshUI ─────────────────────────────────────────────────────
    public void RefreshUI()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // 보관 슬롯
        int storageCount = Mathf.Min(inv.storage.Length, _storageImg.Length, _storageName.Length, _storageHp.Length);
        SetExtraStorageSlotsVisible(storageCount);
        for (int i = 0; i < storageCount; i++)
        {
            var p = inv.storage[i];
            if (_storageImg[i] != null)
            {
                _storageImg[i].sprite = p != null ? GetPartSprite(p.slot) : null;
                _storageImg[i].preserveAspect = true;
                _storageImg[i].color = p != null
                    ? (_storageImg[i].sprite != null ? Color.white : CSlot)
                    : CEmpty;
                ApplyAlphaHitTest(_storageImg[i], _storageImg[i].sprite != null ? partAlphaHitThreshold : 0f);
            }
            if (i < _storageBtn.Length && _storageBtn[i] != null && _storageImg[i] != null)
                _storageBtn[i].targetGraphic = _storageImg[i];
            if (_storageName[i] != null) _storageName[i].text   = p != null ? p.SlotName() : "빈 슬롯";
            if (_storageHp[i]   != null) _storageHp[i].text     = p != null ? Dots(p) : "";
        }

        // 캐릭터 부위
        for (int i = 0; i < 6; i++)
        {
            var p = inv.equipped[i];
            if (_charImg[i] != null)
            {
                if (_charImg[i].sprite == null)
                    _charImg[i].sprite = GetPartSprite((BodySlot)i);
                _charImg[i].preserveAspect = true;
                _charImg[i].type = Image.Type.Simple;
                _charImg[i].color = p != null ? Color.white : CUnequippedPart;
                ApplyAlphaHitTest(_charImg[i], partAlphaHitThreshold);
            }
        }

        // 우측 상태
        for (int i = 0; i < 6; i++)
        {
            var p = inv.equipped[i];
            if (_statName[i] != null) _statName[i].text  = p != null ? "장착됨" : "없음";
            if (_statHp[i]   != null)
            {
                _statHp[i].text  = p != null ? Dots(p) : new string('○', 5);
                _statHp[i].color = p != null
                    ? new Color(0.88f, 0.48f, 0.24f, 1f)
                    : new Color(0.17f, 0.15f, 0.13f, 0.42f);
            }
        }
        if (_statName[6] != null) _statName[6].text  = "장착됨 (고정)";
        if (_statHp[6]   != null)
        {
            _statHp[6].text  = new string('●', 5);
            _statHp[6].color = new Color(0.88f, 0.48f, 0.24f, 1f);
        }

        RefreshSewingStatus(inv);
        ApplyCharacterBaseSprites();
        ApplyInventoryHitTesting();
    }

    void ApplyInventoryHitTesting()
    {
        for (int i = 0; i < _charImg.Length; i++)
        {
            if (_charImg[i] == null)
                continue;

            if (_charImg[i].sprite == null)
                _charImg[i].sprite = GetPartSprite((BodySlot)i);
            ApplyAlphaHitTest(_charImg[i], partAlphaHitThreshold);
        }

        for (int i = 0; i < _storageImg.Length; i++)
            if (_storageImg[i] != null)
                ApplyAlphaHitTest(_storageImg[i], _storageImg[i].sprite != null ? partAlphaHitThreshold : 0f);
    }

    public static void ApplyAlphaHitTest(Image image, float threshold)
    {
        if (image == null)
            return;

        image.raycastTarget = true;

        float safeThreshold = 0f;
        if (image.sprite != null && threshold > 0f)
        {
            Texture2D texture = image.sprite.texture;
            if (texture != null && texture.isReadable)
                safeThreshold = threshold;
        }

        if (safeThreshold > 0f)
        {
            try
            {
                image.alphaHitTestMinimumThreshold = safeThreshold;
            }
            catch (System.Exception)
            {
                // Leave the default rectangular hit area for sprites Unity cannot alpha-test.
            }
        }

        Button button = image.GetComponent<Button>();
        if (button != null)
            button.targetGraphic = image;
    }

    public static Sprite GetPartSprite(BodySlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= PartSpriteNames.Length)
            return null;

        string spriteName = PartSpriteNames[index];
        Sprite sprite = LoadResourceSprite("Sprites/Player/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Player/" + spriteName + ".png");
        for (int i = 0; i < assets.Length; i++)
            if (assets[i] is Sprite editorSprite)
                return editorSprite;

        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Player/" + spriteName + ".png");
#else
        return null;
#endif
    }

    static Sprite LoadResourceSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        return sprites.Length > 0 ? sprites[0] : null;
    }

    void SetExtraStorageSlotsVisible(int visibleCount)
    {
        int maxCount = Mathf.Max(_storageImg.Length, _storageBtn.Length, _storageName.Length, _storageHp.Length);
        for (int i = 0; i < maxCount; i++)
        {
            GameObject slot = StorageSlotRoot(i);
            if (slot != null)
                slot.SetActive(i < visibleCount);
        }
    }

    GameObject StorageSlotRoot(int index)
    {
        if (index < _storageImg.Length && _storageImg[index] != null)
            return _storageImg[index].gameObject;

        if (index < _storageBtn.Length && _storageBtn[index] != null)
            return _storageBtn[index].gameObject;

        if (index < _storageName.Length && _storageName[index] != null)
            return _storageName[index].transform.parent != null ? _storageName[index].transform.parent.gameObject : _storageName[index].gameObject;

        if (index < _storageHp.Length && _storageHp[index] != null)
            return _storageHp[index].transform.parent != null ? _storageHp[index].transform.parent.gameObject : _storageHp[index].gameObject;

        return null;
    }

    void EnsureCharacterBaseImages()
    {
        Transform frame = FindChildRecursive(transform, "CharacterImageFrame");
        if (frame == null)
            return;

        _baseBodyImg = EnsureBaseImage(frame, "BodyBaseImage", new Vector2(0f, -38f), new Vector2(250f, 340f), new Color(0.17f, 0.15f, 0.13f, 0.20f));
        _baseFaceImg = EnsureBaseImage(frame, "FaceBaseImage", new Vector2(0f, 118f), new Vector2(190f, 170f), new Color(0.17f, 0.15f, 0.13f, 0.16f));

        HideCharacterPartLabels();
        ApplyCharacterBaseSprites();
    }

    Image EnsureBaseImage(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, Color placeholderColor)
    {
        Transform existing = parent.Find(objectName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
        go.transform.SetParent(parent, false);
        bool isNew = existing == null;

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = go.AddComponent<RectTransform>();
            isNew = true;
        }

        if (isNew)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
            isNew = true;
        }

        image.raycastTarget = false;
        image.preserveAspect = true;
        if (isNew || image.sprite == null)
            image.color = image.sprite == null ? placeholderColor : Color.white;
        return image;
    }

    void ApplyCharacterBaseSprites()
    {
        if (_baseBodyImg != null)
        {
            if (_baseBodySprite != null)
                _baseBodyImg.sprite = _baseBodySprite;
            _baseBodyImg.color = _baseBodyImg.sprite == null ? new Color(0.17f, 0.15f, 0.13f, 0.20f) : Color.white;
        }

        if (_baseFaceImg != null)
        {
            if (_baseFaceSprite != null)
                _baseFaceImg.sprite = _baseFaceSprite;
            _baseFaceImg.color = _baseFaceImg.sprite == null ? new Color(0.17f, 0.15f, 0.13f, 0.16f) : Color.white;
        }

        Transform hint = FindChildRecursive(transform, "CharacterImageText");
        if (hint != null)
            hint.gameObject.SetActive((_baseBodyImg == null || _baseBodyImg.sprite == null) && (_baseFaceImg == null || _baseFaceImg.sprite == null));
    }

    void HideCharacterPartLabels()
    {
        for (int i = 0; i < _charImg.Length; i++)
        {
            if (_charImg[i] == null)
                continue;

            TextMeshProUGUI[] labels = _charImg[i].GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int j = 0; j < labels.Length; j++)
                labels[j].gameObject.SetActive(false);
        }
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

    void RefreshSewingStatus(InventoryManager inv)
    {
        if (_sewingStatus == null) return;

        int emptyCount = 0;
        var missingParts = new List<string>();

        for (int i = 0; i < inv.equipped.Length; i++)
        {
            if (inv.equipped[i] != null) continue;

            emptyCount++;
            missingParts.Add(BodySlotLabel((BodySlot)i) + " 없음");
        }

        if (emptyCount == 0)
        {
            _sewingStatus.text = "[재봉 상태] 빈 슬롯 0개 · 안정적";
            _sewingStatus.color = new Color(0.17f, 0.15f, 0.13f, 1f);
        }
        else if (emptyCount >= 3)
        {
            _sewingStatus.text = "[재봉 상태] 빈 슬롯 3개 이상 · 몸이 공격받을 수 있음!";
            _sewingStatus.color = new Color(0.75f, 0.08f, 0.06f, 1f);
        }
        else
        {
            _sewingStatus.text = "[재봉 상태] "
                + string.Join(" · ", missingParts)
                + $" · 빈 슬롯 {emptyCount}개 · 몸 안전";
            _sewingStatus.color = new Color(0.17f, 0.15f, 0.13f, 1f);
        }
    }

    static string BodySlotLabel(BodySlot slot)
    {
        switch (slot)
        {
            case BodySlot.EyeLeft: return "왼쪽 눈";
            case BodySlot.EyeRight: return "오른쪽 눈";
            case BodySlot.ArmLeft: return "왼팔";
            case BodySlot.ArmRight: return "오른팔";
            case BodySlot.LegLeft: return "왼다리";
            case BodySlot.LegRight: return "오른다리";
            default: return "알 수 없는 부위";
        }
    }

    static string Dots(BodyPart p)
    {
        int f = Mathf.Clamp(Mathf.RoundToInt((float)p.currentHp / p.maxHp * 5f), 0, 5);
        return new string('●', f) + new string('○', 5 - f);
    }
}
