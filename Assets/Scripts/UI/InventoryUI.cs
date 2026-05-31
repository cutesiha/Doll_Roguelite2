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
        if (FindObjectOfType<InventoryUI>() != null) return;
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

    [Header("보관 슬롯 ×2")]
    [SerializeField] Image[]           _storageImg  = new Image[2];
    [SerializeField] Button[]          _storageBtn  = new Button[2];
    [SerializeField] TextMeshProUGUI[] _storageName = new TextMeshProUGUI[2];
    [SerializeField] TextMeshProUGUI[] _storageHp   = new TextMeshProUGUI[2];

    [Header("캐릭터 부위 (EyeLeft=0 ~ LegRight=5)")]
    [SerializeField] Image[]  _charImg = new Image[6];
    [SerializeField] Button[] _charBtn = new Button[6];

    [Header("부위 상태 텍스트 (0~5=슬롯, 6=몸)")]
    [SerializeField] TextMeshProUGUI[] _statName = new TextMeshProUGUI[7];
    [SerializeField] TextMeshProUGUI[] _statHp   = new TextMeshProUGUI[7];

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
        DontDestroyOnLoad(gameObject);
        WireClicks();
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
            _panel.SetActive(!_panel.activeSelf);
    }

    void WireClicks()
    {
        for (int i = 0; i < 2; i++)
        {
            int ci = i;
            _storageBtn[i]?.onClick.AddListener(() => InventoryManager.Instance?.EquipFromStorage(ci));
        }
        for (int i = 0; i < 6; i++)
        {
            BodySlot slot = (BodySlot)i;
            _charBtn[i]?.onClick.AddListener(() => InventoryManager.Instance?.TryUnequip(slot));
        }
    }

    // ── RefreshUI ─────────────────────────────────────────────────────
    public void RefreshUI()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // 보관 슬롯
        for (int i = 0; i < 2; i++)
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
    }

    static string Dots(BodyPart p)
    {
        int f = Mathf.Clamp(Mathf.RoundToInt((float)p.currentHp / p.maxHp * 5f), 0, 5);
        return new string('●', f) + new string('○', 5 - f);
    }
}
