using System;
using UnityEngine;

[DefaultExecutionOrder(0)]
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectOfType<InventoryManager>() != null) return;
        var go = new GameObject("InventoryManager");
        go.AddComponent<InventoryManager>();
    }

    // indexed by (int)BodySlot — null means not equipped
    public BodyPart[] equipped  = new BodyPart[6];
    // 4 storage slots — null means empty
    public BodyPart[] storage   = new BodyPart[4];

    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitEquipped();
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

        s.eyeLeft  = equipped[(int)BodySlot.EyeLeft]  != null;
        s.eyeRight = equipped[(int)BodySlot.EyeRight] != null;
        s.armLeft  = equipped[(int)BodySlot.ArmLeft]  != null;
        s.armRight = equipped[(int)BodySlot.ArmRight] != null;
        s.legLeft  = equipped[(int)BodySlot.LegLeft]  != null;
        s.legRight = equipped[(int)BodySlot.LegRight] != null;
    }

    int FreeStorageIndex()
    {
        for (int i = 0; i < storage.Length; i++)
            if (storage[i] == null) return i;
        return -1;
    }
}
