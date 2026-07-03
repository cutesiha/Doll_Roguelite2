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
    // A안: 프리팹에서 지정한 슬롯 배경색/빈 슬롯 텍스트를 그대로 보존한다.
    readonly Color[] _storageEmptyColor = new Color[InventoryManager.StorageSlotCount];
    readonly string[] _storageEmptyName = new string[InventoryManager.StorageSlotCount];
    // A안: Q 보석 칸의 프리팹 배경(스프라이트/색)을 기억해 비었을 때 되돌린다.
    Sprite _qGemBaseSprite;
    Color _qGemBaseColor = Color.white;
    bool _qGemBaseCaptured;

    [Header("캐릭터 부위 (EyeLeft=0 ~ LegRight=5)")]
    [SerializeField] Image[]  _charImg = new Image[InventoryManager.BodySlotCount];
    [SerializeField] Button[] _charBtn = new Button[InventoryManager.BodySlotCount];
    [SerializeField, Range(0f, 1f)] float partAlphaHitThreshold = 0.1f;
    readonly GameObject[] _charLockBadge = new GameObject[InventoryManager.BodySlotCount];

    [Header("Character Base Images")]
    [SerializeField] Sprite _baseBodySprite;
    [SerializeField] Sprite _baseFaceSprite;
    [SerializeField] Image _baseBodyImg;
    [SerializeField] Image _baseFaceImg;

    [Header("부위 상태 텍스트 (0~5=슬롯, 6=몸)")]
    [SerializeField] TextMeshProUGUI[] _statName = new TextMeshProUGUI[7];
    [SerializeField] TextMeshProUGUI[] _statHp   = new TextMeshProUGUI[7];
    // #3: 프리팹에서 지정한 HP dots(●) 색을 기억해 장착 상태에 그대로 쓴다.
    readonly Color[] _statHpAuthoredColor = new Color[7];
    bool _statHpColorsCaptured;

    [Header("재봉 상태")]
    [SerializeField] TextMeshProUGUI _sewingStatus;

    [Header("특수 슬롯 (버리기 / Q 보석)")]
    Image _trashImg;
    Image _qGemImg;
    TextMeshProUGUI _qGemName;
    Button _qGemBtn;

    GameObject _toggleHotspot;
    Button _toggleHotspotButton;
    RectTransform _panelRect;
    Coroutine _panelAnimationRoutine;
    Vector2 _panelShownPosition;
    bool _panelPositionCaptured;
    bool _panelOpenStateCaptured;
    bool _panelWasAuthoredOpen;
    const float PanelHiddenOffsetY = 980f;
    const float PanelOvershootY = 36f;
    // #2: 제목 텍스트 박스를 아주 조금 아래로 내리는 오프셋(px). 더 내리려면 값을 더 음수로.
    const float TitleBoxYOffset = -8f;

    // ── 색상 ───────────────────────────────────────────────────────────
    static readonly Color ClearColor = new Color(0f, 0f, 0f, 0f);
    static readonly Color CSlot  = new Color(0.88f, 0.48f, 0.24f, 1f);
    static readonly Color CEmpty = new Color(0.17f, 0.15f, 0.13f, 0.20f);
    static readonly Color CTrash = new Color(0.55f, 0.12f, 0.10f, 0.55f);
    static readonly Color CUnequippedPart = new Color(0.04f, 0.035f, 0.03f, 0.48f);
    static readonly Color CDarkBrown = new Color(0.22f, 0.12f, 0.06f, 1f);
    static readonly Color CSoftDarkBrown = new Color(0.22f, 0.12f, 0.06f, 0.22f);
    static readonly string[] PartSpriteNames =
    {
        "eye_left",
        "eye_right",
        "arm_left",
        "arm_right",
        "leg_left",
        "leg_right",
        "body"
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
        CaptureAuthoredPanelOpenState();
        EnsureStorageSlots();
        EnsureSpecialSlots();
        EnsureCharacterSlots();
        EnsureCharacterBaseImages();
        EnsureInventoryDecorations();
        ApplyAuthoredPanelOpenState();
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
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed += RefreshUI;
        ApplyAuthoredPanelOpenState();
        RefreshUI();
    }

    void OnDestroy()
    {
        RunUiPauseManager.SetPaused("Inventory", false);
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
        if (ItemInventoryManager.Instance != null)
            ItemInventoryManager.Instance.Changed -= RefreshUI;
    }

    void Update()
    {
        // Tab/I 키는 RunHudUI.HandleInventoryHotkey 에서 일괄 처리.
        // 여기서 중복 처리하면 같은 프레임에 열기→닫기가 발생해 아무것도 안 됨.
    }

    void WireClicks()
    {
        _closeButton?.onClick.AddListener(ClosePanel);

        // task5/task19: 보관 슬롯 클릭 시 또잉 애니메이션
        for (int i = 0; i < _storageBtn.Length; i++)
        {
            int captured = i;
            _storageBtn[i]?.onClick.AddListener(() => PlaySlotBoing(captured));
        }

        // task19: 장착된 신체 부위(캐릭터) 슬롯도 클릭하면 또잉
        // 몸(Body)은 그림이 훨씬 커서 이 효과가 너무 크게 보이므로 뺀다.
        for (int i = 0; i < _charBtn.Length; i++)
        {
            if (_charBtn[i] == null || _charImg == null || i >= _charImg.Length || _charImg[i] == null)
                continue;
            if ((BodySlot)i == BodySlot.Body)
                continue;

            InventoryBoingEffect boing = _charImg[i].GetComponent<InventoryBoingEffect>();
            if (boing == null)
                boing = _charImg[i].gameObject.AddComponent<InventoryBoingEffect>();

            InventoryBoingEffect captured = boing;
            _charBtn[i].onClick.AddListener(() => captured.PlayBoing());
        }
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
            CenterInventoryPanel();
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

    void EnsureCharacterSlots()
    {
        _charImg = EnsureArrayLength(_charImg, InventoryManager.BodySlotCount);
        _charBtn = EnsureArrayLength(_charBtn, InventoryManager.BodySlotCount);

        TryBindCharacterSlot(BodySlot.EyeLeft, "EquipPart_EyeLeft");
        TryBindCharacterSlot(BodySlot.EyeRight, "EquipPart_EyeRight");
        TryBindCharacterSlot(BodySlot.ArmLeft, "EquipPart_ArmLeft");
        TryBindCharacterSlot(BodySlot.ArmRight, "EquipPart_ArmRight");
        TryBindCharacterSlot(BodySlot.LegLeft, "EquipPart_LegLeft");
        TryBindCharacterSlot(BodySlot.LegRight, "EquipPart_LegRight");
        TryBindCharacterSlot(BodySlot.Body, "EquipPart_Body");

        NormalizeCharacterSlotButtons();
    }

    // #2: 몸 슬롯 버튼도 다른 부위와 동일한 호버 틴트를 갖게 하고,
    // #3: 클릭 후에도 남는 selected(어두운) 틴트를 마우스를 떼면 사라지도록 제거한다.
    void NormalizeCharacterSlotButtons()
    {
        // 프리팹에 authored된 부위 버튼(눈/팔/다리)의 색상 블록을 기준으로 삼는다.
        Button colorRef = null;
        for (int i = 0; i < _charBtn.Length; i++)
        {
            if (i == (int)BodySlot.Body)
                continue;
            if (_charBtn[i] != null) { colorRef = _charBtn[i]; break; }
        }

        for (int i = 0; i < _charBtn.Length; i++)
        {
            if (_charBtn[i] == null)
                continue;

            // 몸 슬롯은 코드로 추가돼 기본색이므로 다른 부위 색을 그대로 복사한다.
            if (colorRef != null && _charBtn[i] != colorRef)
                _charBtn[i].colors = colorRef.colors;

            ClearButtonSelectedTint(_charBtn[i]);
        }
    }

    // 클릭 후 selected 상태로 남아 틴트가 유지되는 것을 막는다(selected = normal).
    static void ClearButtonSelectedTint(Button button)
    {
        if (button == null)
            return;

        ColorBlock cb = button.colors;
        cb.selectedColor = cb.normalColor;
        button.colors = cb;
    }

    // 몸통(Body) 슬롯은 히트박스보다 스프라이트를 더 크게 보여주기 위해
    // 실제 그림은 히트박스 안의 별도 자식 이미지(BodyVisual)에 그린다.
    static readonly Vector2 BodyVisualSize = new Vector2(579.1426f, 641.98f);

    void TryBindCharacterSlot(BodySlot slot, string objectName)
    {
        int index = (int)slot;
        if (index < 0 || index >= _charImg.Length)
            return;

        Transform part = FindChildRecursive(transform, objectName);
        if (part == null)
            return;

        Image image = part.GetComponent<Image>();
        if (slot == BodySlot.Body)
        {
            Sprite existingSprite = image != null ? image.sprite : null;
            if (image != null)
            {
                // 부모(히트박스) 자체는 화면에 보이지 않게 비우고, 클릭/드래그는 계속 받도록 둔다.
                image.sprite = null;
                image.color = Color.clear;
                image.preserveAspect = true;
                image.type = Image.Type.Simple;
                image.raycastTarget = true;
            }
            image = EnsureBodyVisual(part, existingSprite);
            // 몸통이 팔/다리/머리보다 앞(위)에 그려지도록 형제 순서를 맨 뒤로 보낸다.
            part.SetAsLastSibling();
        }
        else if (image != null)
        {
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
        }

        _charImg[index] = image;

        Button button = part.GetComponent<Button>();
        if (button == null)
            button = part.gameObject.AddComponent<Button>();
        _charBtn[index] = button;
        if (image != null)
            button.targetGraphic = image;

        InventoryEquippedDragSource dragSource = part.GetComponent<InventoryEquippedDragSource>();
        if (dragSource == null)
            dragSource = part.gameObject.AddComponent<InventoryEquippedDragSource>();
        dragSource.SetBodySlot(slot);

        InventoryEquipDropTarget dropTarget = part.GetComponent<InventoryEquipDropTarget>();
        if (dropTarget == null)
            dropTarget = part.gameObject.AddComponent<InventoryEquipDropTarget>();
        dropTarget.SetAcceptedSlot(slot);

        InventoryItemTooltip tooltip = part.GetComponent<InventoryItemTooltip>();
        if (tooltip == null)
            tooltip = part.gameObject.AddComponent<InventoryItemTooltip>();
        tooltip.SetBodySlot(slot);
    }

    // 히트박스(part)보다 큰 시각적 이미지를 자식으로 만들어, 클릭/드래그 영역은 그대로 두고
    // 그림만 더 크게 보이도록 한다.
    static Image EnsureBodyVisual(Transform part, Sprite fallbackSprite)
    {
        Transform existing = part.Find("BodyVisual");
        GameObject go = existing != null ? existing.gameObject : new GameObject("BodyVisual");
        go.transform.SetParent(part, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
            rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        // 히트박스(part)가 (0,4)에서 (-6,-33)으로 옮겨진 만큼(-6,-37) 상쇄해
        // 화면에 보이는 그림 위치는 원래 자리(12,20 상당)를 유지한다.
        rect.anchoredPosition = new Vector2(18f, 53f);
        rect.sizeDelta = BodyVisualSize;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;
        if (image.sprite == null)
            image.sprite = fallbackSprite != null ? fallbackSprite : LoadInterfaceSprite("body_real");
        return image;
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
        bool existedInHierarchy = slot.transform.parent != null;
        slot.name = "StorageSlot_" + (index + 1);
        slot.transform.SetParent(parent, false);
        slot.SetActive(true);

        RectTransform rect = slot.GetComponent<RectTransform>();
        bool createdRect = rect == null;
        if (createdRect)
            rect = slot.AddComponent<RectTransform>();

        const float slotWidth = 148f;
        const float slotHeight = 92f;
        const float gapX = 16f;
        const float gapY = 14f;
        if (!existedInHierarchy || createdRect)
        {
            int col = index % 3;
            int row = index / 3;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2((col - 1) * (slotWidth + gapX), -96f - row * (slotHeight + gapY));
            rect.sizeDelta = new Vector2(slotWidth, slotHeight);
            rect.localScale = Vector3.one;
        }

        Image image = slot.GetComponent<Image>();
        bool createdImage = image == null;
        if (createdImage)
            image = slot.AddComponent<Image>();
        // A안: 이미지가 코드로 새로 붙었을 때만 CEmpty 기본색을 쓰고,
        // 프리팹에서 손으로 지정한 슬롯 배경색/모양은 그대로 보존한다.
        if (index >= 0 && index < _storageEmptyColor.Length)
            _storageEmptyColor[index] = createdImage ? CEmpty : image.color;
        image.color = createdImage ? CEmpty : image.color;
        image.raycastTarget = true;
        _storageImg[index] = image;

        Button button = slot.GetComponent<Button>();
        if (button == null)
            button = slot.AddComponent<Button>();
        button.targetGraphic = image;
        // #3: 클릭 후 어두운 틴트가 남지 않도록 selected = normal 로 맞춘다.
        ClearButtonSelectedTint(button);
        _storageBtn[index] = button;

        InventoryStorageDragSource dragSource = slot.GetComponent<InventoryStorageDragSource>();
        if (dragSource == null)
            dragSource = slot.AddComponent<InventoryStorageDragSource>();
        dragSource.SetStorageIndex(index);

        InventoryStorageDropTarget dropTarget = slot.GetComponent<InventoryStorageDropTarget>();
        if (dropTarget == null)
            dropTarget = slot.AddComponent<InventoryStorageDropTarget>();
        dropTarget.SetStorageIndex(index);

        // task6: 툴팁
        InventoryItemTooltip tooltip = slot.GetComponent<InventoryItemTooltip>();
        if (tooltip == null)
            tooltip = slot.AddComponent<InventoryItemTooltip>();
        tooltip.SetStorageIndex(index);

        // task4: 아이템 아이콘 (원본 크기, 슬롯 배경과 분리)
        EnsureSlotItemIcon(slot.transform, slotWidth, slotHeight);

        // task7: 동전 3x3 그리드 컨테이너
        EnsureCoinGrid(slot.transform, slotWidth, slotHeight);

        // 월드 동전 프리팹(BodyPart 동전 더미) 표시용 — 비스듬히 쌓인 동전더미
        EnsureCoinPile(slot.transform, slotWidth, slotHeight);

        // A안: "슬롯 N" 오버레이는 프리팹에 실제로 있을 때만 유지하고,
        // 없으면 코드가 새로 만들지 않는다(사용자 디자인엔 없는 더미 라벨 제거).
        if (slot.transform.Find("SlotLabel") != null)
            EnsureStorageSlotLabel(slot.transform, "SlotLabel", "슬롯 " + (index + 1), 18f, new Vector2(0f, -6f), new Vector2(slotWidth, 24f), TextAlignmentOptions.Center);
        // task21: 아이콘을 키웠으므로 이름은 슬롯 하단으로 이동
        _storageName[index] = EnsureStorageSlotLabel(slot.transform, "SlotName", "빈 슬롯", 14f, new Vector2(0f, -68f), new Vector2(slotWidth - 12f, 24f), TextAlignmentOptions.Center);
        // A안: 프리팹에서 지정한 빈 슬롯 이름을 기억해 RefreshStorage 에서 되돌릴 때 사용.
        if (index >= 0 && index < _storageEmptyName.Length)
            _storageEmptyName[index] = _storageName[index] != null ? _storageName[index].text : "빈 슬롯";
        // task20: 부위 저장 시 하단 체력 동그라미(●○) 표시 제거 — 라벨은 남기되 항상 비운다
        _storageHp[index] = EnsureStorageSlotLabel(slot.transform, "SlotHP", "", 16f, new Vector2(0f, -66f), new Vector2(slotWidth - 12f, 24f), TextAlignmentOptions.Center);
    }

    // 슬롯 안 아이템 아이콘이 차지하는 정사각 박스 크기 (비율 유지하며 이 안에 맞춤)
    // task21: 보관 슬롯에 넣은 부위/아이템이 너무 작아 보이지 않도록 확대.
    const float SlotIconBox = 100f;

    // 슬롯 내 아이템 아이콘 — 슬롯 크기에 맞춰 비율 유지하며 표시
    static void EnsureSlotItemIcon(Transform slotRoot, float slotW, float slotH)
    {
        if (slotRoot.Find("ItemIcon") != null)
            return;

        GameObject go = new GameObject("ItemIcon");
        go.transform.SetParent(slotRoot, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(SlotIconBox, SlotIconBox);

        Image img = go.AddComponent<Image>();
        img.preserveAspect = true; // 비율 유지하며 박스 안에 맞춤
        img.raycastTarget = false;
        img.color = Color.clear;

        // task5: 또잉은 슬롯이 아닌 아이템 아이콘에
        go.AddComponent<InventoryBoingEffect>();
    }

    // 아이콘을 슬롯 박스에 맞춰 비율 유지 (SetNativeSize 대체 — 너무 커지지 않도록)
    static void FitSlotIcon(Image icon)
    {
        if (icon == null) return;
        icon.preserveAspect = true;
        icon.type = Image.Type.Simple;
        RectTransform rt = icon.transform as RectTransform;
        if (rt != null) rt.sizeDelta = new Vector2(SlotIconBox, SlotIconBox);
    }

    // task7: 동전 3x3 그리드 컨테이너
    static void EnsureCoinGrid(Transform slotRoot, float slotW, float slotH)
    {
        if (slotRoot.Find("CoinGrid") != null)
            return;

        GameObject grid = new GameObject("CoinGrid");
        grid.transform.SetParent(slotRoot, false);
        RectTransform gridRt = grid.AddComponent<RectTransform>();
        gridRt.anchorMin = gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.pivot = new Vector2(0.5f, 0.5f);
        gridRt.anchoredPosition = Vector2.zero;
        gridRt.sizeDelta = new Vector2(slotW * 0.85f, slotH * 0.85f);

        // 3x3=9개 동전 셀 생성
        float cellW = gridRt.sizeDelta.x / 3f;
        float cellH = gridRt.sizeDelta.y / 3f;
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                GameObject cell = new GameObject("Coin_" + (r * 3 + c));
                cell.transform.SetParent(grid.transform, false);
                RectTransform cellRt = cell.AddComponent<RectTransform>();
                cellRt.anchorMin = cellRt.anchorMax = new Vector2(0f, 1f);
                cellRt.pivot = new Vector2(0.5f, 0.5f);
                cellRt.anchoredPosition = new Vector2((c + 0.5f) * cellW - gridRt.sizeDelta.x * 0.5f,
                                                      -((r + 0.5f) * cellH));
                cellRt.sizeDelta = new Vector2(cellW * 0.8f, cellH * 0.8f);

                Image coinImg = cell.AddComponent<Image>();
                coinImg.raycastTarget = false;
                coinImg.color = Color.clear;
                coinImg.preserveAspect = true;
            }
        }
        grid.SetActive(false);
    }

    // 월드 동전 프리팹(BodyPart 동전 더미) 표시 — 개별 아이콘을 살짝 어긋난 위치/각도로 겹쳐 쌓은 더미처럼 보이게 한다.
    const float CoinPileIconSize = SlotIconBox * 0.55f;
    static readonly Vector2[] CoinPileOffsets =
    {
        new Vector2(0f, 0f),
        new Vector2(9f, -6f),
        new Vector2(-10f, -4f),
        new Vector2(4f, 9f),
        new Vector2(-7f, 8f),
        new Vector2(13f, 4f),
        new Vector2(-13f, -9f),
        new Vector2(2f, -13f),
        new Vector2(-3f, 13f)
    };
    static readonly float[] CoinPileRotations = { 0f, 12f, -10f, 18f, -16f, 8f, -14f, 20f, -6f };

    static void EnsureCoinPile(Transform slotRoot, float slotW, float slotH)
    {
        if (slotRoot.Find("CoinPile") != null)
            return;

        GameObject pile = new GameObject("CoinPile");
        pile.transform.SetParent(slotRoot, false);
        RectTransform pileRt = pile.AddComponent<RectTransform>();
        pileRt.anchorMin = pileRt.anchorMax = new Vector2(0.5f, 0.5f);
        pileRt.pivot = new Vector2(0.5f, 0.5f);
        pileRt.anchoredPosition = Vector2.zero;
        pileRt.sizeDelta = new Vector2(slotW, slotH);

        for (int i = 0; i < InventoryManager.MaxCoinStackCount; i++)
        {
            GameObject coin = new GameObject("Coin_" + i);
            coin.transform.SetParent(pile.transform, false);
            RectTransform coinRt = coin.AddComponent<RectTransform>();
            coinRt.anchorMin = coinRt.anchorMax = new Vector2(0.5f, 0.5f);
            coinRt.pivot = new Vector2(0.5f, 0.5f);
            coinRt.anchoredPosition = CoinPileOffsets[i];
            coinRt.sizeDelta = new Vector2(CoinPileIconSize, CoinPileIconSize);
            coinRt.localRotation = Quaternion.Euler(0f, 0f, CoinPileRotations[i]);

            Image coinImg = coin.AddComponent<Image>();
            coinImg.raycastTarget = false;
            coinImg.preserveAspect = true;
            coinImg.color = Color.clear;
        }

        pile.SetActive(false);
    }

    // 동전 더미 아이콘 개수만큼 활성화 (겹쳐 쌓인 모양)
    static void SetCoinPile(Image slotBackground, int count, Sprite coinSprite)
    {
        if (slotBackground == null) return;
        Transform pileTr = slotBackground.transform.Find("CoinPile");
        if (pileTr == null) return;

        bool show = count > 0;
        pileTr.gameObject.SetActive(show);
        if (!show) return;

        int visible = Mathf.Clamp(count, 0, InventoryManager.MaxCoinStackCount);
        for (int i = 0; i < InventoryManager.MaxCoinStackCount; i++)
        {
            Transform coinTr = pileTr.Find("Coin_" + i);
            if (coinTr == null) continue;
            Image coinImg = coinTr.GetComponent<Image>();
            if (coinImg == null) continue;

            bool filled = i < visible;
            coinImg.sprite = filled ? coinSprite : null;
            coinImg.color = filled ? Color.white : Color.clear;
        }
    }

TextMeshProUGUI EnsureStorageSlotLabel(Transform parent, string name, string value, float fontSize, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        bool createdRect = rect == null;
        if (createdRect)
            rect = go.AddComponent<RectTransform>();
        if (existing == null || createdRect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        bool createdLabel = label == null;
        if (createdLabel)
            label = go.AddComponent<TextMeshProUGUI>();

        label.raycastTarget = false;
        // A안: 프리팹에서 직접 편집한 라벨(텍스트/폰트/색/정렬)은 그대로 두고,
        // 코드가 새로 만든 라벨에만 기본값을 채운다.
        if (createdLabel)
        {
            label.text = value;
            label.font = UIThinDungFont.Get();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = TextColorForStorage(name);
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return label;
    }

    // ── 특수 슬롯 (버리기 / Q 보석) ───────────────────────────────────
    void EnsureSpecialSlots()
    {
        EnsurePanelReference();
        Transform parent = StorageSlotsParent();
        if (parent == null)
            return;

        const float slotWidth = 148f;
        const float slotHeight = 92f;
        const float y = -430f;

        // 버리는 칸 (왼쪽) — 아이템을 끌어다 놓으면 월드에 다시 떨어뜨린다.
        GameObject trash = EnsureSpecialSlotRoot(parent, "TrashSlot", new Vector2(-82f, y), new Vector2(slotWidth, slotHeight));
        _trashImg = trash.GetComponent<Image>();
        // A안: 프리팹에서 이미지를 지정했으면 CTrash 틴트로 덮지 않는다.
        if (_trashImg.sprite == null)
            _trashImg.color = CTrash;
        _trashImg.raycastTarget = true;
        if (trash.GetComponent<InventoryTrashDropTarget>() == null)
            trash.AddComponent<InventoryTrashDropTarget>();
        EnsureStorageSlotLabel(trash.transform, "SlotLabel", "버리기", 18f, new Vector2(0f, -8f), new Vector2(slotWidth, 28f), TextAlignmentOptions.Center);
        EnsureStorageSlotLabel(trash.transform, "SlotHint", "여기에 끌어다 놓기", 13f, new Vector2(0f, -50f), new Vector2(slotWidth - 12f, 24f), TextAlignmentOptions.Center);

        // Q 보석 칸 (오른쪽) — 기존 Q 소모품 시스템(ItemInventoryManager)을 표시·사용.
        GameObject qgem = EnsureSpecialSlotRoot(parent, "QGemSlot", new Vector2(82f, y), new Vector2(slotWidth, slotHeight));
        _qGemImg = qgem.GetComponent<Image>();
        // A안: 프리팹에서 지정한 빈 칸 배경(스프라이트/색)을 기억해 둔다.
        if (!_qGemBaseCaptured)
        {
            _qGemBaseSprite = _qGemImg.sprite;
            _qGemBaseColor = _qGemImg.color;
            _qGemBaseCaptured = true;
        }
        _qGemImg.preserveAspect = true;
        _qGemImg.type = Image.Type.Simple;
        _qGemImg.raycastTarget = true;
        _qGemBtn = qgem.GetComponent<Button>();
        if (_qGemBtn == null)
            _qGemBtn = qgem.AddComponent<Button>();
        _qGemBtn.targetGraphic = _qGemImg;
        ClearButtonSelectedTint(_qGemBtn);
        _qGemBtn.onClick.RemoveListener(UseQGem);
        _qGemBtn.onClick.AddListener(UseQGem);
        if (qgem.GetComponent<ItemConsumableDragSource>() == null)
            qgem.AddComponent<ItemConsumableDragSource>();
        if (qgem.GetComponent<QGemDropTarget>() == null)
            qgem.AddComponent<QGemDropTarget>();
        EnsureStorageSlotLabel(qgem.transform, "SlotLabel", "Q 보석", 18f, new Vector2(0f, -8f), new Vector2(slotWidth, 28f), TextAlignmentOptions.Center);
        _qGemName = EnsureStorageSlotLabel(qgem.transform, "SlotName", "없음", 14f, new Vector2(0f, -50f), new Vector2(slotWidth - 12f, 24f), TextAlignmentOptions.Center);
    }

    GameObject EnsureSpecialSlotRoot(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = FindChildRecursive(parent, name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.SetActive(true);

        RectTransform rect = go.GetComponent<RectTransform>();
        bool createdRect = rect == null;
        if (createdRect)
            rect = go.AddComponent<RectTransform>();
        if (existing == null || createdRect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        return go;
    }

    void EnsureInventoryDecorations()
    {
        EnsureBodyStatusDashedBorders();
        EnsureInventoryTitleBackground();
    }

    void EnsureBodyStatusDashedBorders()
    {
        HashSet<RectTransform> decorated = new HashSet<RectTransform>();
        AddStatusBorders(_statName, decorated);
        AddStatusBorders(_statHp, decorated);
    }

    void AddStatusBorders(TextMeshProUGUI[] labels, HashSet<RectTransform> decorated)
    {
        if (labels == null)
            return;

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;

            RectTransform target = labels[i].transform.parent as RectTransform;
            if (target == null)
                target = labels[i].rectTransform;

            if (target == null || !decorated.Add(target))
                continue;

            EnsureDashedBorder(target, "BodyStatusDashedBorder", CSlotRectSize(target), 14f, 8f, 3f, CDarkBrown);
        }
    }

    void EnsureInventoryTitleBackground()
    {
        Transform title = FindChildRecursive(transform, "Title");
        if (title == null)
            title = FindChildRecursive(transform, "InventoryTitle");
        if (title == null)
            title = FindChildRecursive(transform, "TitleText");
        if (title == null)
            return;

        RectTransform titleRect = title as RectTransform;
        if (titleRect == null)
            titleRect = title.GetComponent<RectTransform>();
        if (titleRect == null)
            return;

        Transform existing = title.parent.Find("TitleBackgroundBox");
        GameObject box = existing != null ? existing.gameObject : new GameObject("TitleBackgroundBox");
        box.transform.SetParent(title.parent, false);
        box.transform.SetSiblingIndex(Mathf.Max(0, title.GetSiblingIndex()));

        RectTransform boxRect = box.GetComponent<RectTransform>();
        bool createdRect = boxRect == null;
        if (createdRect)
            boxRect = box.AddComponent<RectTransform>();

        // #4: 제목 RectTransform이 가로로 stretch돼 있어 그 폭(패널 전체)을 쓰면
        // 파선 박스가 지나치게 넓어진다. 실제 글자 폭(preferredWidth)에 맞춘다.
        TextMeshProUGUI titleLabel = title.GetComponent<TextMeshProUGUI>();
        float textWidth = titleLabel != null && titleLabel.preferredWidth > 1f
            ? titleLabel.preferredWidth
            : CSlotRectSize(titleRect).x;
        float textHeight = titleLabel != null && titleLabel.preferredHeight > 1f
            ? titleLabel.preferredHeight
            : CSlotRectSize(titleRect).y;
        Vector2 boxSize = new Vector2(textWidth + 36f, textHeight + 18f);

        // 가로 중앙 고정 앵커로 바꿔 sizeDelta 를 절대 폭으로 사용(제목 중앙 위치는 유지).
        boxRect.anchorMin = new Vector2(0.5f, titleRect.anchorMax.y);
        boxRect.anchorMax = new Vector2(0.5f, titleRect.anchorMax.y);
        boxRect.pivot = titleRect.pivot;
        // #2: 제목 텍스트 박스를 아주 조금 아래로 내린다.
        boxRect.anchoredPosition = titleRect.anchoredPosition + new Vector2(0f, TitleBoxYOffset);
        boxRect.sizeDelta = boxSize;

        Image background = box.GetComponent<Image>();
        if (background == null)
            background = box.AddComponent<Image>();
        background.color = CSoftDarkBrown;
        background.raycastTarget = false;

        // rect.size 가 아직 갱신 안 됐을 수 있어 계산한 boxSize 를 직접 넘긴다.
        EnsureDashedBorder(boxRect, "TitleDashedBorder", boxSize, 18f, 9f, 3f, CDarkBrown);
        title.SetAsLastSibling();
    }

    static Vector2 CSlotRectSize(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector2 size = rect.rect.size;
        if (size.x <= 0.01f || size.y <= 0.01f)
            size = rect.sizeDelta;
        size.x = Mathf.Max(24f, Mathf.Abs(size.x));
        size.y = Mathf.Max(18f, Mathf.Abs(size.y));
        return size;
    }

    static void EnsureDashedBorder(RectTransform parent, string rootName, Vector2 size, float dashLength, float gap, float thickness, Color color)
    {
        if (parent == null)
            return;

        Transform existing = parent.Find(rootName);
        GameObject root = existing != null ? existing.gameObject : new GameObject(rootName);
        root.transform.SetParent(parent, false);
        root.transform.SetAsLastSibling();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        EnsureDashedLine(rootRect, "Top", new Vector2(-size.x * 0.5f, size.y * 0.5f), Vector2.right, size.x, dashLength, gap, thickness, color);
        EnsureDashedLine(rootRect, "Bottom", new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.right, size.x, dashLength, gap, thickness, color);
        EnsureDashedLine(rootRect, "Left", new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dashLength, gap, thickness, color);
        EnsureDashedLine(rootRect, "Right", new Vector2(size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dashLength, gap, thickness, color);
    }

    static void EnsureDashedLine(RectTransform parent, string edgeName, Vector2 start, Vector2 direction, float length, float dash, float gap, float thickness, Color color)
    {
        Transform old = parent.Find(edgeName);
        if (old != null)
        {
            if (Application.isPlaying)
                Destroy(old.gameObject);
            else
                DestroyImmediate(old.gameObject);
        }

        GameObject edge = new GameObject(edgeName);
        edge.transform.SetParent(parent, false);

        for (float offset = 0f; offset < length; offset += dash + gap)
        {
            float segment = Mathf.Min(dash, length - offset);
            GameObject piece = new GameObject("Dash");
            piece.transform.SetParent(edge.transform, false);
            RectTransform rect = piece.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = start + direction * (offset + segment * 0.5f);
            rect.sizeDelta = Mathf.Abs(direction.x) > 0.5f
                ? new Vector2(segment, thickness)
                : new Vector2(thickness, segment);

            Image image = piece.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }

    void RefreshSpecialSlots()
    {
        // A안: 프리팹에서 이미지를 지정한 버리기 칸은 CTrash 틴트로 덮지 않는다.
        if (_trashImg != null && _trashImg.sprite == null)
            _trashImg.color = CTrash;

        if (_qGemImg == null)
            return;

        ItemData consumable = ItemInventoryManager.Instance != null ? ItemInventoryManager.Instance.Consumable : null;
        if (consumable != null)
        {
            SetImageSpriteSafely(_qGemImg, consumable.Sprite);
            _qGemImg.color = _qGemImg.sprite != null ? Color.white : CSlot;
        }
        else
        {
            // 소모품이 없으면 프리팹에서 지정한 빈 칸 배경을 그대로 보여준다.
            SetImageSpriteSafely(_qGemImg, _qGemBaseSprite);
            _qGemImg.color = _qGemBaseColor;
        }
        _qGemImg.preserveAspect = true;
        _qGemImg.type = Image.Type.Simple;

        if (_qGemName != null)
            _qGemName.text = consumable != null ? consumable.ItemName : "없음";
    }

    // Q 보석 칸 클릭 시: 기존 Q 키와 동일하게 장착된 소모품을 발동한다.
    void UseQGem()
    {
        var items = ItemInventoryManager.Instance;
        if (items == null || items.Consumable == null)
            return;

        if (items.TryUseEquippedConsumable())
            SoundManager.PlayClick();
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

void CaptureAuthoredPanelOpenState()
    {
        EnsurePanelReference();
        if (_panelOpenStateCaptured)
            return;

        _panelWasAuthoredOpen = _panel != null && _panel.activeSelf;
        _panelOpenStateCaptured = true;
    }

    void ApplyAuthoredPanelOpenState()
    {
        EnsurePanelReference();
        CapturePanelShownPosition();
        if (_panel == null)
        {
            SetToggleHotspotVisible(false);
            RunUiPauseManager.SetPaused("Inventory", false);
            return;
        }

        if (_panelRect != null)
            _panelRect.anchoredPosition = _panelShownPosition;

        // 씬/방 이동 시 인벤토리는 항상 닫힌 상태로 시작한다.
        // (프리팹엔 편집 편의를 위해 열린 상태로 authored 되어 있어도 무시)
        _panel.SetActive(false);
        SetToggleHotspotVisible(false);
        RunUiPauseManager.SetPaused("Inventory", false);
    }


    void CapturePanelShownPosition()
    {
        EnsurePanelReference();
        if (_panelRect == null || _panelPositionCaptured)
            return;

        CenterInventoryPanel();
        _panelShownPosition = _panelRect.anchoredPosition;
        _panelPositionCaptured = true;
    }

    void CenterInventoryPanel()
    {
        if (_panelRect == null)
            return;

        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _panelRect.anchoredPosition = Vector2.zero;
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
        bool addedCanvas = canvas == null;
        if (addedCanvas)
            canvas = gameObject.AddComponent<Canvas>();

        if (canvas != null)
        {
            canvas.enabled = true;
            if (isTopLevelCanvas || addedCanvas)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 500;
            }
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        bool addedScaler = scaler == null;
        if (addedScaler)
            scaler = gameObject.AddComponent<CanvasScaler>();

        if (scaler != null)
        {
            scaler.enabled = true;
            if (isTopLevelCanvas || addedScaler)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
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
        EnsureSpecialSlots();
        EnsureCharacterSlots();
        EnsureCharacterBaseImages();
        RefreshSpecialSlots();
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // 보관 슬롯
        int storageCount = Mathf.Min(inv.storage.Length, _storageImg.Length, _storageName.Length, _storageHp.Length);
        SetExtraStorageSlotsVisible(storageCount);

        // 각 슬롯 드래그 소스 초기화 + 툴팁 초기화
        for (int i = 0; i < storageCount; i++)
        {
            InventoryStorageDragSource dragSource = _storageImg[i] != null ? _storageImg[i].GetComponent<InventoryStorageDragSource>() : null;
            if (dragSource != null)
            {
                dragSource.SetItemData(null);
                dragSource.SetItemStorageIndex(-1);
            }

            InventoryItemTooltip tooltip = _storageImg[i] != null ? _storageImg[i].GetComponent<InventoryItemTooltip>() : null;
            if (tooltip != null)
                tooltip.SetItemData(null);
        }

        // ─ InventoryManager BodyPart 슬롯 ─
        for (int i = 0; i < storageCount; i++)
        {
            var p = inv.storage[i];
            // task4: 배경 Image는 빈/찬 색상만; 실제 스프라이트는 ItemIcon 자식에 표시
            if (_storageImg[i] != null)
            {
                // 슬롯 배경 sprite는 에디터 지정값을 유지한다.
                // A안: 빈 슬롯은 프리팹에서 지정한 배경색을 그대로 보여준다.
                Color emptyColor = i < _storageEmptyColor.Length ? _storageEmptyColor[i] : CEmpty;
                _storageImg[i].color = p != null ? new Color(0.12f, 0.09f, 0.06f, 0.35f) : emptyColor;
                ApplyAlphaHitTest(_storageImg[i], 0f);
            }

            bool isCoinStack = p != null && p.kind == ItemKind.Coin;

            // ItemIcon 자식에 스프라이트 표시 (task4: 원본 크기). 동전 더미는 CoinPile로 대신 표시.
            Image itemIcon = GetSlotItemIcon(_storageImg[i]);
            if (itemIcon != null)
            {
                if (isCoinStack)
                {
                    itemIcon.sprite = null;
                    itemIcon.color = Color.clear;
                }
                else
                {
                    Sprite slotSprite = p == null ? null : (p.icon != null ? p.icon : (p.IsEquippable ? DisplaySpriteForSlot(p.slot) : null));
                    itemIcon.sprite = slotSprite;
                    itemIcon.color = slotSprite != null ? Color.white : Color.clear;
                    if (slotSprite != null) FitSlotIcon(itemIcon);
                    else (itemIcon.transform as RectTransform).sizeDelta = Vector2.zero;
                }
            }

            // 동전 그리드 숨기기 (ItemInventoryManager 전용, BodyPart 슬롯에서는 미사용)
            SetCoinGrid(_storageImg[i], 0, null);
            // 동전 더미는 비스듬히 겹쳐진 아이콘들로 표시
            SetCoinPile(_storageImg[i], isCoinStack ? p.count : 0, isCoinStack ? p.icon : null);

            if (i < _storageBtn.Length && _storageBtn[i] != null && _storageImg[i] != null)
                _storageBtn[i].targetGraphic = _storageImg[i];
            if (_storageName[i] != null) _storageName[i].text = p != null ? p.DisplayName() : (i < _storageEmptyName.Length ? _storageEmptyName[i] : "빈 슬롯");
            // task20: 보관 슬롯에 저장된 부위의 체력 동그라미 표시 안 함
            if (_storageHp[i]   != null) _storageHp[i].text  = "";
        }

        // ─ 빈 슬롯에 ItemInventoryManager 아이템 + 동전 스택 표시 ─
        var itemInv = ItemInventoryManager.Instance;
        int itemIndex = 0;
        int coinIndex = 0;
        int itemStorageCount = itemInv != null ? itemInv.Storage.Count : 0;
        int coinStackCount   = itemInv != null ? itemInv.CoinStacks.Count : 0;

        for (int i = 0; i < storageCount; i++)
        {
            if (inv.storage[i] != null)
                continue; // BodyPart가 이미 사용 중

            if (itemIndex < itemStorageCount)
            {
                // task4: ItemData 아이템 표시
                int usedItemIndex = itemIndex;
                ItemData item = itemInv.Storage[itemIndex++];
                if (_storageImg[i] != null)
                {
                    _storageImg[i].color = new Color(0.12f, 0.09f, 0.06f, 0.35f);
                    // 슬롯 배경 sprite는 에디터 지정값을 유지한다.
                }
                Image icon = GetSlotItemIcon(_storageImg[i]);
                if (icon != null)
                {
                    icon.sprite = item.Sprite;
                    icon.color  = item.Sprite != null ? Color.white : new Color(0.88f, 0.48f, 0.24f, 0.8f);
                    if (item.Sprite != null) FitSlotIcon(icon);
                    else (icon.transform as RectTransform).sizeDelta = Vector2.zero;
                }
                SetCoinGrid(_storageImg[i], 0, null);
                if (_storageName[i] != null) _storageName[i].text = item.ItemName;
                if (_storageHp[i]   != null) _storageHp[i].text  = "";

                InventoryStorageDragSource dragSource = _storageImg[i] != null ? _storageImg[i].GetComponent<InventoryStorageDragSource>() : null;
                if (dragSource != null)
                {
                    dragSource.SetItemData(item);
                    dragSource.SetItemStorageIndex(usedItemIndex);
                }

                InventoryItemTooltip tooltip = _storageImg[i] != null ? _storageImg[i].GetComponent<InventoryItemTooltip>() : null;
                if (tooltip != null)
                    tooltip.SetItemData(item);
            }
            else if (coinIndex < coinStackCount)
            {
                // task7: 동전 3x3 표시
                int count = itemInv.CoinStacks[coinIndex++];
                if (_storageImg[i] != null)
                {
                    _storageImg[i].color = new Color(0.18f, 0.13f, 0.04f, 0.45f);
                    // 슬롯 배경 sprite는 에디터 지정값을 유지한다.
                }
                Image icon = GetSlotItemIcon(_storageImg[i]);
                if (icon != null) icon.color = Color.clear;

                SetCoinGrid(_storageImg[i], count, itemInv.CoinItemRef);
                if (_storageName[i] != null) _storageName[i].text = "동전 ×" + count;
                if (_storageHp[i]   != null) _storageHp[i].text  = "";
            }
            else
            {
                // 완전히 빈 칸: 여기로 아이템을 드롭하면 보관함 리스트 맨 뒤로 옮긴다.
                InventoryStorageDragSource dragSource = _storageImg[i] != null ? _storageImg[i].GetComponent<InventoryStorageDragSource>() : null;
                if (dragSource != null)
                    dragSource.SetItemStorageIndex(itemStorageCount);
            }
        }

        // ─ task3: 캐릭터 부위 슬롯 — InventoryManager + ItemInventoryManager 장착 표시 ─
        int bodySlotCount = Mathf.Min(inv.equipped.Length, _charImg.Length);
        for (int i = 0; i < bodySlotCount; i++)
        {
            var p = inv.equipped[i];
            BodySlot bodySlot = (BodySlot)i;
            ItemData bodyItem = itemInv != null ? itemInv.GetEquippedByBodySlot(bodySlot) : null;

            if (_charImg[i] != null)
            {
                _charImg[i].preserveAspect = true;
                _charImg[i].type = Image.Type.Simple;

                if (bodyItem != null)
                {
                    // ItemData 신체부위 아이템이 장착됨
                    SetImageSpriteSafely(_charImg[i], bodyItem.Sprite);
                    _charImg[i].color = bodyItem.Sprite != null ? Color.white : CSlot;
                }
                else
                {
                    // 기존 BodyPart 상태. 팔·다리·눈과 동일하게, 장착되어 있으면 원래 색,
                    // 장착 해제되어 있으면 어둡게 틴트한다.
                    _charImg[i].color = p != null ? Color.white : CUnequippedPart;
                }
                // Body는 _charImg가 히트박스가 아닌 확대 표시용 자식(BodyVisual)을 가리키므로
                // 여기서 알파 히트테스트를 적용하면 안 된다(실제 히트박스는 항상 클릭 가능한 상태로 둔다).
                if (bodySlot != BodySlot.Body)
                    ApplyAlphaHitTest(_charImg[i], partAlphaHitThreshold);
            }

            SetCharacterSlotLocked(i, inv.IsSlotLocked(bodySlot));
        }

        // 우측 상태
        // #3: 코드가 색을 덮기 전에 프리팹에서 지정한 HP dots 색을 한 번 캡처한다.
        if (!_statHpColorsCaptured)
        {
            for (int i = 0; i < _statHp.Length && i < _statHpAuthoredColor.Length; i++)
                _statHpAuthoredColor[i] = _statHp[i] != null ? _statHp[i].color : new Color(0.88f, 0.48f, 0.24f, 1f);
            _statHpColorsCaptured = true;
        }

        int statCount = Mathf.Min(inv.equipped.Length, _statName.Length, _statHp.Length);
        for (int i = 0; i < statCount; i++)
        {
            var p = inv.equipped[i];
            if (_statName[i] != null) _statName[i].text  = p != null ? "장착됨" : "없음";
            if (_statHp[i]   != null)
            {
                _statHp[i].text  = p != null ? Dots(p) : new string('○', 5);
                // 장착 상태: 프리팹에서 지정한 색 유지 / 미장착: 어둡게.
                _statHp[i].color = p != null
                    ? (i < _statHpAuthoredColor.Length ? _statHpAuthoredColor[i] : new Color(0.88f, 0.48f, 0.24f, 1f))
                    : new Color(0.17f, 0.15f, 0.13f, 0.42f);
            }
        }
        RefreshSewingStatus(inv);
        ApplyInventoryHitTesting();
    }

    void ApplyInventoryHitTesting()
    {
        for (int i = 0; i < _charImg.Length; i++)
        {
            if (_charImg[i] == null || (BodySlot)i == BodySlot.Body)
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
        background.color = new Color(0.04f, 0.03f, 0.02f, 0.55f);
        background.raycastTarget = false;

        BuildLockChainVisual(badge.transform);

        _charLockBadge[index] = badge;
        return badge;
    }

    // 조건방에서 잠긴 부위를 "쇠사슬로 묶인 자물쇠" 도형으로 표현한다.
    // 스프라이트가 없어도 동작하도록 둥근 사각형/원 스프라이트가 있으면 사용하고
    // 없으면 단색 사각형으로 그린다. 만들어진 오브젝트는 하이어라키에 그대로 남아
    // 에디터에서 위치/크기/색을 직접 조정할 수 있다.
    void BuildLockChainVisual(Transform parent)
    {
        Sprite round = LoadUiSprite("ui_round_rect_10");
        Sprite circle = LoadUiSprite("ui_circle");

        Color chainColor = new Color(0.74f, 0.75f, 0.80f, 1f);
        Color chainShade = new Color(0.45f, 0.46f, 0.52f, 1f);
        Color lockBody = new Color(0.91f, 0.73f, 0.30f, 1f);
        Color lockEdge = new Color(0.55f, 0.40f, 0.13f, 1f);
        Color lockHole = new Color(0.30f, 0.20f, 0.06f, 1f);

        GameObject chains = MakeLockPiece(parent, "Chains", Vector2.zero, new Vector2(0f, 0f), 0f, ClearColor, null);
        RectTransform chainsRect = chains.GetComponent<RectTransform>();
        chainsRect.anchorMin = Vector2.zero;
        chainsRect.anchorMax = Vector2.one;
        chainsRect.offsetMin = Vector2.zero;
        chainsRect.offsetMax = Vector2.zero;

        // 대각선으로 가로지르는 두 갈래 쇠사슬 (X 자)
        BuildChainStrand(chains.transform, 45f, round, chainColor, chainShade);
        BuildChainStrand(chains.transform, -45f, round, chainColor, chainShade);

        // 중앙 자물쇠
        GameObject lockRoot = MakeLockPiece(parent, "Padlock", new Vector2(0f, -4f), new Vector2(54f, 54f), 0f, ClearColor, null);

        // 고리(shackle): ∩ 자 — 좌/우 세로 + 위 가로
        MakeLockPiece(lockRoot.transform, "ShackleL", new Vector2(-13f, 17f), new Vector2(7f, 26f), 0f, lockEdge, round);
        MakeLockPiece(lockRoot.transform, "ShackleR", new Vector2(13f, 17f), new Vector2(7f, 26f), 0f, lockEdge, round);
        MakeLockPiece(lockRoot.transform, "ShackleTop", new Vector2(0f, 28f), new Vector2(33f, 7f), 0f, lockEdge, round);

        // 자물쇠 몸통
        MakeLockPiece(lockRoot.transform, "BodyEdge", new Vector2(0f, -8f), new Vector2(46f, 40f), 0f, lockEdge, round);
        MakeLockPiece(lockRoot.transform, "Body", new Vector2(0f, -8f), new Vector2(38f, 32f), 0f, lockBody, round);

        // 열쇠구멍
        MakeLockPiece(lockRoot.transform, "KeyholeDot", new Vector2(0f, -4f), new Vector2(9f, 9f), 0f, lockHole, circle);
        MakeLockPiece(lockRoot.transform, "KeyholeSlit", new Vector2(0f, -13f), new Vector2(5f, 11f), 0f, lockHole, round);
    }

    void BuildChainStrand(Transform parent, float angle, Sprite linkSprite, Color a, Color b)
    {
        GameObject strand = MakeLockPiece(parent, "ChainStrand", Vector2.zero, new Vector2(2f, 2f), angle, ClearColor, null);
        const int linkCount = 7;
        const float spacing = 13f;
        float start = -(linkCount - 1) * spacing * 0.5f;
        for (int i = 0; i < linkCount; i++)
        {
            Color c = (i % 2 == 0) ? a : b;
            MakeLockPiece(strand.transform, "Link" + i, new Vector2(start + i * spacing, 0f), new Vector2(11f, 8f), 0f, c, linkSprite);
        }
    }

    GameObject MakeLockPiece(Transform parent, string name, Vector2 pos, Vector2 size, float rot, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, rot);

        if (color.a > 0f)
        {
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
            }
        }
        return go;
    }

    static Sprite LoadUiSprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/UI/" + spriteName);
        if (sprite != null)
            return sprite;
#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/" + spriteName + ".png");
        if (sprite != null)
            return sprite;
#endif
        // 런타임에서 못 찾으면 씬에 이미 로드된 같은 스프라이트를 재사용한다.
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
            if (images[i] != null && images[i].sprite != null && images[i].sprite.name == spriteName)
                return images[i].sprite;
        return null;
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

    static Sprite LoadInterfaceSprite(string spriteName)
    {
        Sprite sprite = LoadResourceSprite("Sprites/interface/" + spriteName);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/interface/" + spriteName + ".png");
        for (int i = 0; i < assets.Length; i++)
            if (assets[i] is Sprite editorSprite)
                return editorSprite;

        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/interface/" + spriteName + ".png");
#else
        return null;
#endif
    }

    // task4: 슬롯의 ItemIcon 자식 Image 반환
    static Image GetSlotItemIcon(Image slotBackground)
    {
        if (slotBackground == null) return null;
        Transform iconTr = slotBackground.transform.Find("ItemIcon");
        return iconTr != null ? iconTr.GetComponent<Image>() : null;
    }

    // task7: 동전 3x3 그리드 업데이트
    static void SetCoinGrid(Image slotBackground, int count, ItemData coinItem)
    {
        if (slotBackground == null) return;
        Transform gridTr = slotBackground.transform.Find("CoinGrid");
        if (gridTr == null) return;

        bool show = count > 0;
        gridTr.gameObject.SetActive(show);
        if (!show) return;

        Sprite coinSprite = coinItem != null ? coinItem.Sprite : null;
        for (int k = 0; k < 9; k++)
        {
            Transform cellTr = gridTr.Find("Coin_" + k);
            if (cellTr == null) continue;
            Image cellImg = cellTr.GetComponent<Image>();
            if (cellImg == null) continue;

            bool filled = k < count;
            cellImg.sprite = filled ? coinSprite : null;
            cellImg.color  = filled
                ? (coinSprite != null ? Color.white : new Color(1f, 0.84f, 0.12f, 1f))
                : Color.clear;
        }
    }

    // task5: 지정 슬롯 아이템 아이콘에 또잉 애니메이션
    void PlaySlotBoing(int index)
    {
        if (index < 0 || index >= _storageImg.Length || _storageImg[index] == null)
            return;
        Transform iconTr = _storageImg[index].transform.Find("ItemIcon");
        if (iconTr == null) return;
        InventoryBoingEffect boing = iconTr.GetComponent<InventoryBoingEffect>();
        boing?.PlayBoing();
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
        // A안: 프리팹에 실제로 배치한 베이스 이미지에만 바인딩하고,
        // 없는 것은 런타임에 새로 만들지 않는다(캐릭터 위 유령 이미지 방지).
        Transform existing = parent.Find(objectName);
        if (existing == null)
            return null;

        GameObject go = existing.gameObject;

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        image.raycastTarget = false;
        if (image.sprite == null)
            image.color = placeholderColor;
        return image;
    }

    void ApplyCharacterBaseSprites()
    {
        if (_baseBodySprite == null)
            _baseBodySprite = LoadInterfaceSprite("body_real");
        if (_baseFaceSprite == null)
            _baseFaceSprite = LoadInterfaceSprite("head");

        // A안: 프리팹에서 지정한 베이스 이미지 스프라이트/색은 덮어쓰지 않는다.
        // (스프라이트가 비어 있을 때만 기본값을 채운다.)
        if (_baseBodyImg != null && _baseBodyImg.sprite == null)
        {
            if (_baseBodySprite != null)
                _baseBodyImg.sprite = _baseBodySprite;
            _baseBodyImg.color = _baseBodyImg.sprite == null ? new Color(0.17f, 0.15f, 0.13f, 0.20f) : Color.white;
        }

        if (_baseFaceImg != null && _baseFaceImg.sprite == null)
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
            case BodySlot.Body: return "몸통";
            default: return "알 수 없는 부위";
        }
    }

    static string Dots(BodyPart p)
    {
        int f = Mathf.Clamp(Mathf.RoundToInt((float)p.currentHp / p.maxHp * 5f), 0, 5);
        return new string('●', f) + new string('○', 5 - f);
    }
}
