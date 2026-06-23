using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(0)]
public class InventoryManager : MonoBehaviour
{
    public const int StorageSlotCount = 9;
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
    // storage slots — null means empty
    public BodyPart[] storage   = new BodyPart[StorageSlotCount];
    // single jewel (Gem) slot — null means empty. Activated with Q, consumed on use.
    public BodyPart jewel;
    bool[] lockedSlots = new bool[6];

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
        lockedSlots = new bool[6];
        SyncBodyState();
    }

    public void ResetToDefault()
    {
        equipped = new BodyPart[6];
        storage = new BodyPart[StorageSlotCount];
        jewel = null;
        InitEquipped();
        if (BodyManager.Instance != null && BodyManager.Instance.State != null)
            BodyManager.Instance.State.body = true;
        OnInventoryChanged?.Invoke();
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
    public bool EquipFromStorage(int storageIdx)
    {
        if (storageIdx < 0 || storageIdx >= storage.Length) return false;

        var part = storage[storageIdx];
        if (part == null) return false;

        // 보석/동전 등 비-부위 아이템은 신체 슬롯에 장착할 수 없다.
        if (!part.IsEquippable) return false;

        int idx = (int)part.slot;
        if (IsSlotLocked(part.slot))
            return false;

        var displaced = equipped[idx];

        equipped[idx]       = part;
        storage[storageIdx] = displaced;   // may be null — that's fine

        SyncBodyState();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ── 보석(Gem) 전용 슬롯 ────────────────────────────────────────────

    // 보관함의 보석을 전용 슬롯에 장착. 슬롯에 이미 보석이 있으면 그 자리로 교체된다.
    public bool EquipJewelFromStorage(int storageIdx)
    {
        if (storageIdx < 0 || storageIdx >= storage.Length) return false;

        var part = storage[storageIdx];
        if (part == null || !part.IsJewel) return false;

        storage[storageIdx] = jewel;   // 기존 보석을 보관함으로 (없으면 null)
        jewel = part;

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 장착된 보석을 지정한 빈 보관함 칸으로 되돌린다.
    public bool TryUnequipJewelToStorage(int storageIdx)
    {
        if (jewel == null) return false;
        if (storageIdx < 0 || storageIdx >= storage.Length) return false;
        if (storage[storageIdx] != null) return false;

        storage[storageIdx] = jewel;
        jewel = null;

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 장착된 보석을 꺼내서 비운다(소모). 없으면 null.
    public BodyPart ConsumeJewel()
    {
        if (jewel == null) return null;

        BodyPart used = jewel;
        jewel = null;
        OnInventoryChanged?.Invoke();
        return used;
    }

    public bool TryAddPart(BodyPart part, bool equipIfEmpty = true)
    {
        if (part == null)
            return false;

        int equippedIndex = (int)part.slot;
        bool canEquip = equippedIndex >= 0
            && equippedIndex < equipped.Length
            && !IsSlotLocked(part.slot);
        if (equipIfEmpty && canEquip && equipped[equippedIndex] == null)
        {
            equipped[equippedIndex] = part;
            SyncBodyState();
            OnInventoryChanged?.Invoke();
            return true;
        }

        int free = FreeStorageIndex();
        if (free < 0)
            return false;

        storage[free] = part;
        SyncBodyState();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 월드 드랍 아이템 획득용. 동전/누더기/보석 등은 보관함에만 들어간다.
    // 보관함이 가득 차 있으면 false 를 반환해 호출 측이 나중에 다시 시도할 수 있다.
    public bool AddItem(ItemKind kind)
    {
        return TryAddPart(new BodyPart(kind), false);
    }

    public int RepairAllParts()
    {
        int repaired = 0;
        repaired += RepairParts(equipped);
        repaired += RepairParts(storage);

        if (repaired > 0)
        {
            SyncBodyState();
            OnInventoryChanged?.Invoke();
        }

        return repaired;
    }

    public void SyncBodyState()
    {
        var s = BodyManager.Instance?.State;
        if (s == null) return;

        BodyState snapshot = GetBodyStateSnapshot();
        s.body     = snapshot.body;
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

    public BodyPart GetEquippedPart(BodySlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= equipped.Length)
            return null;

        return equipped[index];
    }

    public bool TryDamageEquippedPart(BodySlot slot, int damage, out BodyPart brokenPart)
    {
        brokenPart = null;
        if (damage <= 0)
            return false;

        BodyPart part = GetEquippedPart(slot);
        if (part == null)
            return false;

        part.maxHp = Mathf.Max(1, part.maxHp);
        part.currentHp = Mathf.Clamp(part.currentHp - damage, 0, part.maxHp);

        if (part.currentHp <= 0)
        {
            brokenPart = part;
            equipped[(int)slot] = null;
        }

        SyncBodyState();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public BodyState GetBodyStateSnapshot()
    {
        bool bodyAlive = BodyManager.Instance == null
            || BodyManager.Instance.State == null
            || BodyManager.Instance.State.body;

        return new BodyState
        {
            body = bodyAlive,
            eyeLeft = IsEquipped(BodySlot.EyeLeft),
            eyeRight = IsEquipped(BodySlot.EyeRight),
            armLeft = IsEquipped(BodySlot.ArmLeft),
            armRight = IsEquipped(BodySlot.ArmRight),
            legLeft = IsEquipped(BodySlot.LegLeft),
            legRight = IsEquipped(BodySlot.LegRight)
        };
    }

    public void ReplaceState(BodyPart[] newEquipped, BodyPart[] newStorage)
    {
        equipped = NormalizeParts(newEquipped, 6);
        storage = NormalizeParts(newStorage, StorageSlotCount);
        jewel = null;
        lockedSlots = new bool[6];
        SyncBodyState();
        OnInventoryChanged?.Invoke();
    }

    public void ReplaceState(BodyPart[] newEquipped, BodyPart[] newStorage, bool[] newLockedSlots)
    {
        equipped = NormalizeParts(newEquipped, 6);
        storage = NormalizeParts(newStorage, StorageSlotCount);
        jewel = null;
        lockedSlots = NormalizeLocks(newLockedSlots);

        for (int i = 0; i < equipped.Length && i < lockedSlots.Length; i++)
            if (lockedSlots[i] && equipped[i] != null)
                MoveLockedEquippedPartToStorage(i);

        SyncBodyState();
        OnInventoryChanged?.Invoke();
    }

    public bool IsSlotLocked(BodySlot slot)
    {
        int index = (int)slot;
        return index >= 0 && index < lockedSlots.Length && lockedSlots[index];
    }

    public bool[] GetLockedSlotsSnapshot()
    {
        return NormalizeLocks(lockedSlots);
    }

    public void LockConditionSlot(NodeConditionType conditionType)
    {
        BodySlot slot;
        if (!BodyConditionUtility.TryGetRequiredMissingSlot(conditionType, out slot))
            return;

        int index = (int)slot;
        if (index < 0 || index >= lockedSlots.Length)
            return;

        lockedSlots[index] = true;
        SyncBodyState();
        OnInventoryChanged?.Invoke();
    }

    public void UnlockConditionSlot(NodeConditionType conditionType)
    {
        BodySlot slot;
        if (!BodyConditionUtility.TryGetRequiredMissingSlot(conditionType, out slot))
            return;

        UnlockSlot(slot);
    }

    public void UnlockSlot(BodySlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= lockedSlots.Length || !lockedSlots[index])
            return;

        lockedSlots[index] = false;
        OnInventoryChanged?.Invoke();
    }

    BodyPart[] NormalizeParts(BodyPart[] source, int length)
    {
        BodyPart[] result = new BodyPart[length];
        if (source == null)
            return result;

        int count = Mathf.Min(source.Length, length);
        for (int i = 0; i < count; i++)
            result[i] = source[i];

        return result;
    }

    bool[] NormalizeLocks(bool[] source)
    {
        bool[] result = new bool[6];
        if (source == null)
            return result;

        int count = Mathf.Min(source.Length, result.Length);
        for (int i = 0; i < count; i++)
            result[i] = source[i];

        return result;
    }

    void MoveLockedEquippedPartToStorage(int equippedIndex)
    {
        int free = FreeStorageIndex();
        if (free >= 0)
        {
            storage[free] = equipped[equippedIndex];
            equipped[equippedIndex] = null;
            return;
        }

        equipped[equippedIndex] = null;
    }

    int FreeStorageIndex()
    {
        for (int i = 0; i < storage.Length; i++)
            if (storage[i] == null) return i;
        return -1;
    }

    int RepairParts(BodyPart[] parts)
    {
        if (parts == null)
            return 0;

        int repaired = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            BodyPart part = parts[i];
            if (part == null)
                continue;

            part.maxHp = Mathf.Max(1, part.maxHp);
            if (part.currentHp < part.maxHp)
                repaired++;

            part.currentHp = part.maxHp;
        }

        return repaired;
    }
}
