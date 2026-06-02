using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    // Bootstrap: Resources/InventoryCanvas 프리팹을 로드
    // 씬에 이미 있으면 아무것도 안 함
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) return;
        var prefab = Resources.Load<GameObject>("InventoryCanvas");
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

    [Header("보관 슬롯 ×4")]
    [SerializeField] Image[]           _storageImg  = new Image[4];
    [SerializeField] Button[]          _storageBtn  = new Button[4];
    [SerializeField] TextMeshProUGUI[] _storageName = new TextMeshProUGUI[4];
    [SerializeField] TextMeshProUGUI[] _storageHp   = new TextMeshProUGUI[4];

    [Header("캐릭터 부위 (EyeLeft=0 ~ LegRight=5)")]
    [SerializeField] Image[]  _charImg = new Image[6];
    [SerializeField] Button[] _charBtn = new Button[6];

    [Header("부위 상태 텍스트 (0~5=슬롯, 6=몸)")]
    [SerializeField] TextMeshProUGUI[] _statName = new TextMeshProUGUI[7];
    [SerializeField] TextMeshProUGUI[] _statHp   = new TextMeshProUGUI[7];

    [Header("재봉 상태")]
    [SerializeField] TextMeshProUGUI _sewingStatus;

    GameObject _toggleHotspot;
    Button _toggleHotspotButton;

    // ── 색상 ───────────────────────────────────────────────────────────
    static readonly Color CSlot  = new Color(0.20f, 0.20f, 0.27f, 1f);
    static readonly Color CEmpty = new Color(0.13f, 0.13f, 0.17f, 1f);

    static Color HpColor(BodyPart p)
    {
        float r = (float)p.currentHp / p.maxHp;
        if (r > 0.66f) return new Color(0.22f, 0.52f, 0.22f, 1f);
        if (r > 0.33f) return new Color(0.60f, 0.48f, 0.08f, 1f);
        return new Color(0.60f, 0.15f, 0.15f, 1f);
    }

    // ── Unity 수명 ─────────────────────────────────────────────────────
    void Awake()
    {
        NormalizeCanvasTransform();
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        WireClicks();
        EnsureToggleHotspot();
    }

    void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;
        if (_panel != null) _panel.SetActive(false);
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
        if (_panel != null) _panel.SetActive(false);
        SetToggleHotspotVisible(false);
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        NormalizeCanvasTransform();
        EnsureToggleHotspot();
        if (_panel == null) return;
        transform.SetAsLastSibling();
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);
        SetToggleHotspotVisible(true);
        RefreshUI();
    }

    public void TogglePanel()
    {
        if (_panel == null) return;
        if (_panel.activeSelf) ClosePanel();
        else OpenPanel();
    }

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public bool IsScreenPointInsidePanel(Vector2 screenPoint)
    {
        if (_panel == null || !_panel.activeSelf) return false;
        RectTransform rect = _panel.transform as RectTransform;
        if (rect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint);
    }

    void NormalizeCanvasTransform()
    {
        transform.localScale = Vector3.one;

        RectTransform rect = transform as RectTransform;
        if (rect != null && transform.parent != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
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
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
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
        for (int i = 0; i < storageCount; i++)
        {
            var p = inv.storage[i];
            if (_storageImg[i]  != null) _storageImg[i].color   = p != null ? CSlot : CEmpty;
            if (_storageName[i] != null) _storageName[i].text   = p != null ? p.SlotName() : "빈 슬롯";
            if (_storageHp[i]   != null) _storageHp[i].text     = p != null ? Dots(p) : "";
        }

        // 캐릭터 부위
        for (int i = 0; i < 6; i++)
        {
            var p = inv.equipped[i];
            if (_charImg[i] != null) _charImg[i].color = p != null ? HpColor(p) : CEmpty;
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
                    ? new Color(0.85f, 0.60f, 0.20f, 1f)
                    : new Color(0.30f, 0.30f, 0.34f, 1f);
            }
        }
        if (_statName[6] != null) _statName[6].text  = "장착됨 (고정)";
        if (_statHp[6]   != null)
        {
            _statHp[6].text  = new string('●', 5);
            _statHp[6].color = new Color(0.85f, 0.60f, 0.20f, 1f);
        }

        RefreshSewingStatus(inv);
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
            _sewingStatus.color = new Color(0.72f, 0.95f, 0.72f, 1f);
        }
        else if (emptyCount >= 3)
        {
            _sewingStatus.text = "[재봉 상태] 빈 슬롯 3개 이상 · 몸이 공격받을 수 있음!";
            _sewingStatus.color = new Color(1.00f, 0.42f, 0.34f, 1f);
        }
        else
        {
            _sewingStatus.text = "[재봉 상태] "
                + string.Join(" · ", missingParts)
                + $" · 빈 슬롯 {emptyCount}개 · 몸 안전";
            _sewingStatus.color = new Color(0.94f, 0.90f, 0.82f, 1f);
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
