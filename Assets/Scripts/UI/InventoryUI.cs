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
    // RunHudCanvas.prefab owns the in-game inventory panel.
    // Keep this bootstrap empty so an older standalone InventoryCanvas prefab
    // cannot override the layout edited inside RunHudCanvas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
    }

    // ── Inspector 연결 필드 ────────────────────────────────────────────
    [Header("패널 루트 (Tab/I 로 토글)")]
    [SerializeField] GameObject _panel;
    [SerializeField] Button _closeButton;

    [Header("보관 슬롯 ×9")]
    [SerializeField] Image[]           _storageImg  = new Image[InventoryManager.StorageSlotCount];
    [SerializeField] Button[]          _storageBtn  = new Button[InventoryManager.StorageSlotCount];
    [SerializeField] TextMeshProUGUI[] _storageName = new TextMeshProUGUI[InventoryManager.StorageSlotCount];
    [SerializeField] TextMeshProUGUI[] _storageHp   = new TextMeshProUGUI[InventoryManager.StorageSlotCount];

    [Header("캐릭터 부위 (EyeLeft=0 ~ LegRight=5)")]
    [SerializeField] Image[]  _charImg = new Image[6];
    [SerializeField] Button[] _charBtn = new Button[6];
    [SerializeField, Range(0f, 1f)] float partAlphaHitThreshold = 0.1f;
    readonly GameObject[] _charLockBadge = new GameObject[6];

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
    RectTransform _panelRect;
    Coroutine _panelAnimationRoutine;
    Vector2 _panelShownPosition;
    bool _panelPositionCaptured;
    const float PanelHiddenOffsetY = 980f;
    const float PanelOvershootY = 36f;

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
        EnsureStorageSlots();
        ForceClosePanelImmediate();
        NormalizeCanvasTransform();
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        WireClicks();
        EnsureToggleHotspot();
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
        RunUiPauseManager.SetPaused("Inventory", false);
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    void Update()
    {
        // Tab/I 키는 RunHudUI.HandleInventoryHotkey 에서 일괄 처리.
        // 여기서 중복 처리하면 같은 프레임에 열기→닫기가 발생해 아무것도 안 됨.
    }

    void WireClicks()
    {
        _closeButton?.onClick.AddListener(ClosePanel);

        // Equip/unequip is intentionally drag-and-drop only.
    }

    public void ClosePanel()
    {
        EnsurePanelReference();
        if (_panel == null)
        {
            SetToggleHotspotVisible(false);
            RunUiPauseManager.SetPaused("Inventory", false);
            return;
        }

        CapturePanelShownPosition();
        if (!_panel.activeSelf)
        {
            SetToggleHotspotVisible(false);
            RunUiPauseManager.SetPaused("Inventory", false);
            return;
        }

        RunUiPauseManager.SetPaused("Inventory", false);
        SoundManager.PlayPanel();
        PlayPanelAnimation(false);
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        EnsurePanelReference();
        NormalizeCanvasTransform();
        EnsureToggleHotspot();
        ApplyInventoryHitTesting();
        if (_panel == null) return;
        CapturePanelShownPosition();
        bool wasOpen = _panel.activeSelf;
        _panel.SetActive(true);
        RunUiPauseManager.SetPaused("Inventory", true);
        SetToggleHotspotVisible(true);
        RefreshUI();
        if (!wasOpen)
            SoundManager.PlayPanel();
        PlayPanelAnimation(true);
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
        {
            if (_panelRect == null)
                _panelRect = _panel.transform as RectTransform;
            return;
        }

        Transform panel = FindChildRecursive(transform, "InventoryPanel");
        if (panel != null)
        {
            _panel = panel.gameObject;
            _panelRect = panel as RectTransform;
        }
    }

    void EnsureStorageSlots()
    {
        EnsurePanelReference();
        EnsureStorageArrays();

        Transform parent = StorageSlotsParent();
        if (parent == null)
            return;

        for (int i = 0; i < InventoryManager.StorageSlotCount; i++)
        {
            GameObject slot = StorageSlotRoot(i);
            if (slot == null)
                slot = FindExistingStorageSlot(parent, i);
            if (slot == null)
                slot = new GameObject("StorageSlot_" + (i + 1));

            ConfigureStorageSlot(slot, parent, i);
        }
    }

    void EnsureStorageArrays()
    {
        _storageImg = EnsureArrayLength(_storageImg, InventoryManager.StorageSlotCount);
        _storageBtn = EnsureArrayLength(_storageBtn, InventoryManager.StorageSlotCount);
        _storageName = EnsureArrayLength(_storageName, InventoryManager.StorageSlotCount);
        _storageHp = EnsureArrayLength(_storageHp, InventoryManager.StorageSlotCount);
    }

    static T[] EnsureArrayLength<T>(T[] source, int length)
    {
        if (source != null && source.Length == length)
            return source;

        T[] result = new T[length];
        if (source == null)
            return result;

        int count = Mathf.Min(source.Length, length);
        for (int i = 0; i < count; i++)
            result[i] = source[i];
        return result;
    }

    Transform StorageSlotsParent()
    {
        GameObject existing = StorageSlotRoot(0);
        if (existing != null && existing.transform.parent != null)
            return existing.transform.parent;

        if (_panel != null)
        {
            Transform left = FindChildRecursive(_panel.transform, "Left_InventorySlots");
            if (left != null)
                return left;

            left = FindChildRecursive(_panel.transform, "LeftSection");
            if (left != null)
                return left;
        }

        return _panel != null ? _panel.transform : transform;
    }

    GameObject FindExistingStorageSlot(Transform parent, int index)
    {
        string[] names =
        {
            "StorageSlot_" + (index + 1),
            "StorageSlot" + (index + 1),
            "StorageSlot" + index
        };

        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindChildRecursive(parent, names[i]);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    void ConfigureStorageSlot(GameObject slot, Transform parent, int index)
    {
        slot.name = "StorageSlot_" + (index + 1);
        slot.transform.SetParent(parent, false);
        slot.SetActive(true);

        RectTransform rect = slot.GetComponent<RectTransform>();
        if (rect == null)
            rect = slot.AddComponent<RectTransform>();

        const float slotWidth = 148f;
        const float slotHeight = 92f;
        const float gapX = 16f;
        const float gapY = 14f;
        int col = index % 3;
        int row = index / 3;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((col - 1) * (slotWidth + gapX), -96f - row * (slotHeight + gapY));
        rect.sizeDelta = new Vector2(slotWidth, slotHeight);
        rect.localScale = Vector3.one;

        Image image = slot.GetComponent<Image>();
        if (image == null)
            image = slot.AddComponent<Image>();
        image.color = CEmpty;
        image.raycastTarget = true;
        _storageImg[index] = image;

        Button button = slot.GetComponent<Button>();
        if (button == null)
            button = slot.AddComponent<Button>();
        button.targetGraphic = image;
        _storageBtn[index] = button;

        InventoryStorageDragSource dragSource = slot.GetComponent<InventoryStorageDragSource>();
        if (dragSource == null)
            dragSource = slot.AddComponent<InventoryStorageDragSource>();
        dragSource.SetStorageIndex(index);

        InventoryStorageDropTarget dropTarget = slot.GetComponent<InventoryStorageDropTarget>();
        if (dropTarget == null)
            dropTarget = slot.AddComponent<InventoryStorageDropTarget>();
        dropTarget.SetStorageIndex(index);

        EnsureStorageSlotLabel(slot.transform, "SlotLabel", "슬롯 " + (index + 1), 18f, new Vector2(0f, -8f), new Vector2(slotWidth, 28f), TextAlignmentOptions.Center);
        _storageName[index] = EnsureStorageSlotLabel(slot.transform, "SlotName", "빈 슬롯", 14f, new Vector2(0f, -38f), new Vector2(slotWidth - 12f, 28f), TextAlignmentOptions.Center);
        _storageHp[index] = EnsureStorageSlotLabel(slot.transform, "SlotHP", "", 16f, new Vector2(0f, -66f), new Vector2(slotWidth - 12f, 24f), TextAlignmentOptions.Center);
    }

    TextMeshProUGUI EnsureStorageSlotLabel(Transform parent, string name, string value, float fontSize, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
            rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (label == null)
            label = go.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = value;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = TextColorForStorage(name);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    Color TextColorForStorage(string name)
    {
        if (name == "SlotHP")
            return new Color(0.88f, 0.48f, 0.24f, 1f);
        if (name == "SlotName")
            return new Color(0.17f, 0.15f, 0.13f, 0.84f);
        return new Color(0.17f, 0.15f, 0.13f, 1f);
    }

    void ForceClosePanelImmediate()
    {
        EnsurePanelReference();
        if (_panelAnimationRoutine != null)
        {
            StopCoroutine(_panelAnimationRoutine);
            _panelAnimationRoutine = null;
        }

        CapturePanelShownPosition();
        if (_panel != null)
        {
            if (_panelRect != null)
                _panelRect.anchoredPosition = _panelShownPosition;
            _panel.SetActive(false);
        }
        SetToggleHotspotVisible(false);
        RunUiPauseManager.SetPaused("Inventory", false);
    }

    void CapturePanelShownPosition()
    {
        EnsurePanelReference();
        if (_panelRect == null || _panelPositionCaptured)
            return;

        _panelShownPosition = _panelRect.anchoredPosition;
        _panelPositionCaptured = true;
    }

    void PlayPanelAnimation(bool show)
    {
        if (_panelRect == null)
        {
            if (_panel != null)
                _panel.SetActive(show);
            SetToggleHotspotVisible(show);
            return;
        }

        if (_panelAnimationRoutine != null)
            StopCoroutine(_panelAnimationRoutine);

        _panelAnimationRoutine = StartCoroutine(PanelAnimationRoutine(show));
    }

    System.Collections.IEnumerator PanelAnimationRoutine(bool show)
    {
        Vector2 shown = _panelShownPosition;
        Vector2 hidden = shown + Vector2.down * PanelHiddenOffsetY;
        Vector2 overshoot = shown + Vector2.up * PanelOvershootY;

        if (show)
        {
            _panelRect.anchoredPosition = hidden;
            yield return StartCoroutine(AnimatePanelSegment(hidden, overshoot, 0.25f));
            yield return StartCoroutine(AnimatePanelSegment(overshoot, shown, 0.10f));
            _panelRect.anchoredPosition = shown;
        }
        else
        {
            Vector2 from = _panelRect.anchoredPosition;
            yield return StartCoroutine(AnimatePanelSegment(from, overshoot, 0.09f));
            yield return StartCoroutine(AnimatePanelSegment(overshoot, hidden, 0.24f));
            _panelRect.anchoredPosition = shown;
            if (_panel != null)
                _panel.SetActive(false);
            SetToggleHotspotVisible(false);
        }

        _panelAnimationRoutine = null;
    }

    System.Collections.IEnumerator AnimatePanelSegment(Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            _panelRect.anchoredPosition = Vector2.Lerp(from, to, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _panelRect.anchoredPosition = to;
    }

    void NormalizeCanvasTransform()
    {
        bool isTopLevelCanvas = transform.parent == null;
        if (isTopLevelCanvas)
            transform.localScale = Vector3.one;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        if (scaler != null)
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
        EnsureStorageSlots();
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
                // 보석 등 아이콘이 지정된 아이템은 그 스프라이트를, 아니면 부위 기본 스프라이트를 표시.
                Sprite slotSprite = p == null ? null : (p.icon != null ? p.icon : (p.IsEquippable ? DisplaySpriteForSlot(p.slot) : null));
                SetImageSpriteSafely(_storageImg[i], slotSprite);
                _storageImg[i].preserveAspect = true;
                _storageImg[i].type = Image.Type.Simple;
                _storageImg[i].color = p != null
                    ? (_storageImg[i].sprite != null ? Color.white : CSlot)
                    : CEmpty;
                ApplyAlphaHitTest(_storageImg[i], _storageImg[i].sprite != null ? partAlphaHitThreshold : 0f);
            }
            if (i < _storageBtn.Length && _storageBtn[i] != null && _storageImg[i] != null)
                _storageBtn[i].targetGraphic = _storageImg[i];
            if (_storageName[i] != null) _storageName[i].text   = p != null ? p.DisplayName() : "빈 슬롯";
            if (_storageHp[i]   != null) _storageHp[i].text     = p != null && p.IsEquippable ? Dots(p) : "";
        }

        // 캐릭터 부위 — sprite/color/type 은 프리팹 설정을 그대로 유지.
        // 드래그 충돌 감지(AlphaHitTest)만 적용.
        for (int i = 0; i < 6; i++)
        {
            var p = inv.equipped[i];
            if (_charImg[i] != null)
            {
                _charImg[i].preserveAspect = true;
                _charImg[i].type = Image.Type.Simple;
                _charImg[i].color = p != null ? Color.white : CUnequippedPart;
                ApplyAlphaHitTest(_charImg[i], partAlphaHitThreshold);
            }

            SetCharacterSlotLocked(i, inv.IsSlotLocked((BodySlot)i));
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
        ApplyInventoryHitTesting();
    }

    void ApplyInventoryHitTesting()
    {
        for (int i = 0; i < _charImg.Length; i++)
        {
            if (_charImg[i] == null)
                continue;

            ApplyAlphaHitTest(_charImg[i], partAlphaHitThreshold);
        }

        for (int i = 0; i < _storageImg.Length; i++)
            if (_storageImg[i] != null)
                ApplyAlphaHitTest(_storageImg[i], _storageImg[i].sprite != null ? partAlphaHitThreshold : 0f);
    }

    void SetCharacterSlotLocked(int index, bool locked)
    {
        if (index < 0 || index >= _charImg.Length || _charImg[index] == null)
            return;

        GameObject badge = EnsureCharacterLockBadge(index);
        if (badge != null)
            badge.SetActive(locked);
    }

    GameObject EnsureCharacterLockBadge(int index)
    {
        if (index < 0 || index >= _charLockBadge.Length)
            return null;

        if (_charLockBadge[index] != null)
            return _charLockBadge[index];

        Image parentImage = _charImg[index];
        if (parentImage == null)
            return null;

        Transform existing = parentImage.transform.Find("SlotLockBadge");
        if (existing != null)
        {
            _charLockBadge[index] = existing.gameObject;
            return _charLockBadge[index];
        }

        GameObject badge = new GameObject("SlotLockBadge");
        badge.transform.SetParent(parentImage.transform, false);
        RectTransform rect = badge.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = badge.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.46f);
        background.raycastTarget = false;

        GameObject labelGO = new GameObject("SlotLockLabel");
        labelGO.transform.SetParent(badge.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = "LOCK";
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        _charLockBadge[index] = badge;
        return badge;
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
                SetAlphaHitThresholdSafely(image, safeThreshold);
            }
            catch (System.Exception)
            {
                // Leave the default rectangular hit area for sprites Unity cannot alpha-test.
            }
        }
        else
        {
            SetAlphaHitThresholdSafely(image, 0f);
        }

        Button button = image.GetComponent<Button>();
        if (button != null)
            button.targetGraphic = image;
    }

    public Sprite DisplaySpriteForSlot(BodySlot slot)
    {
        int index = (int)slot;
        if (_charImg != null && index >= 0 && index < _charImg.Length && _charImg[index] != null && _charImg[index].sprite != null)
            return _charImg[index].sprite;

        return GetPartSprite(slot);
    }

    public static Sprite FindDisplaySpriteForSlot(BodySlot slot)
    {
        InventoryUI[] inventories = FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < inventories.Length; i++)
        {
            Sprite sprite = inventories[i].DisplaySpriteForSlot(slot);
            if (sprite != null)
                return sprite;
        }

        return GetPartSprite(slot);
    }

    static void SetImageSpriteSafely(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        SetAlphaHitThresholdSafely(image, 0f);
        image.sprite = sprite;
    }

    static void SetAlphaHitThresholdSafely(Image image, float value)
    {
        if (image == null)
            return;

        try
        {
            image.alphaHitTestMinimumThreshold = value;
        }
        catch (System.InvalidOperationException)
        {
            // Some UI sprites are intentionally not read/write. Rectangular hit testing is fine for them.
        }
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
