using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(0)]
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<InventoryManager>() != null) return;
        var go = new GameObject("InventoryManager");
        go.AddComponent<InventoryManager>();
    }

    // indexed by (int)BodySlot — null means not equipped
    public BodyPart[] equipped  = new BodyPart[6];
    // 2 storage slots — null means empty
    public BodyPart[] storage   = new BodyPart[2];

    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitEquipped();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        SyncBodyState();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncBodyState();
        OnInventoryChanged?.Invoke();
    }

    void InitEquipped()
    {
        foreach (BodySlot slot in Enum.GetValues(typeof(BodySlot)))
            equipped[(int)slot] = new BodyPart(slot);
        SyncBodyState();
    }

    // moves equipped part to a free storage slot; returns false if storage full
    public bool TryUnequip(BodySlot slot)
    {
        var part = equipped[(int)slot];
        if (part == null) return false;

        int free = FreeStorageIndex();
        if (free < 0) return false;

        storage[free] = part;
        equipped[(int)slot] = null;
        SyncBodyState();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool TryUnequipToStorage(BodySlot slot, int storageIdx)
    {
        if (storageIdx < 0 || storageIdx >= storage.Length) return false;
        if (storage[storageIdx] != null) return false;

        var part = equipped[(int)slot];
        if (part == null) return false;

        storage[storageIdx] = part;
        equipped[(int)slot] = null;
        SyncBodyState();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // equips the part in storage[storageIdx] into its matching slot
    // if that slot is already occupied, the existing part goes to the same storage index
    public void EquipFromStorage(int storageIdx)
    {
        if (storageIdx < 0 || storageIdx >= storage.Length) return;

        var part = storage[storageIdx];
        if (part == null) return;

        int idx = (int)part.slot;
        var displaced = equipped[idx];

        equipped[idx]       = part;
        storage[storageIdx] = displaced;   // may be null — that's fine

        SyncBodyState();
        OnInventoryChanged?.Invoke();
    }

    public void SyncBodyState()
    {
        var s = BodyManager.Instance?.State;
        if (s == null) return;

        BodyState snapshot = GetBodyStateSnapshot();
        s.eyeLeft  = snapshot.eyeLeft;
        s.eyeRight = snapshot.eyeRight;
        s.armLeft  = snapshot.armLeft;
        s.armRight = snapshot.armRight;
        s.legLeft  = snapshot.legLeft;
        s.legRight = snapshot.legRight;
    }

    public bool IsEquipped(BodySlot slot)
    {
        int index = (int)slot;
        return index >= 0 && index < equipped.Length && equipped[index] != null;
    }

    public BodyState GetBodyStateSnapshot()
    {
        return new BodyState
        {
            eyeLeft = IsEquipped(BodySlot.EyeLeft),
            eyeRight = IsEquipped(BodySlot.EyeRight),
            armLeft = IsEquipped(BodySlot.ArmLeft),
            armRight = IsEquipped(BodySlot.ArmRight),
            legLeft = IsEquipped(BodySlot.LegLeft),
            legRight = IsEquipped(BodySlot.LegRight)
        };
    }

    int FreeStorageIndex()
    {
        for (int i = 0; i < storage.Length; i++)
            if (storage[i] == null) return i;
        return -1;
    }
}
