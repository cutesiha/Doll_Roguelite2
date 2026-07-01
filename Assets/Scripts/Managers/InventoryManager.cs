using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(0)]
public class InventoryManager : MonoBehaviour
{
    public const int StorageSlotCount = 9;
    // 동전은 한 보관함 칸에 이 개수까지 쌓인다.
    public const int MaxCoinStackCount = 9;
    public static InventoryManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<InventoryManager>() != null) return;
        var go = new GameObject("InventoryManager");
        go.AddComponent<InventoryManager>();
    }

    // indexed by (int)BodySlot — null means not equipped
    // [NonSerialized]: 이 배열들은 전적으로 런타임에서 관리된다. Unity 직렬화에 노출되면
    // 씬 전환 시 null 요소가 기본 BodyPart(EyeLeft, hp 0) 인스턴스로 치환되어
    // 보관함이 가짜 "눈"으로 가득 차는 버그가 발생한다.
    [System.NonSerialized] public BodyPart[] equipped  = new BodyPart[6];
    // storage slots — null means empty
    [System.NonSerialized] public BodyPart[] storage   = new BodyPart[StorageSlotCount];
    bool[] lockedSlots = new bool[6];

    // 동전: 슬롯 인덱스 → 동전 개수 (슬롯에 직접 귀속)
    [System.NonSerialized] public readonly Dictionary<int, int> coinSlots = new Dictionary<int, int>();
    [System.NonSerialized] public Sprite coinIcon;
    const int MaxCoinsPerStack = 9;

    public IReadOnlyDictionary<int, int> CoinSlots => coinSlots;

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

    // 보관함 안에서 아이템을 다른 칸으로 옮긴다. 대상 칸이 차 있으면 서로 교환한다.
    public bool MoveStorage(int fromIdx, int toIdx)
    {
        if (fromIdx < 0 || fromIdx >= storage.Length) return false;
        if (toIdx < 0 || toIdx >= storage.Length) return false;
        if (fromIdx == toIdx) return false;
        if (storage[fromIdx] == null) return false;

        var moved = storage[fromIdx];
        storage[fromIdx] = storage[toIdx];   // 대상이 비어있으면 null (= 단순 이동)
        storage[toIdx] = moved;

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 보관함 칸을 비우고 그 아이템을 돌려준다. "버리는 칸"에서 월드로 떨어뜨릴 때 사용.
    public BodyPart RemoveStorageAt(int index)
    {
        if (index < 0 || index >= storage.Length) return null;

        BodyPart part = storage[index];
        if (part == null) return null;

        storage[index] = null;
        OnInventoryChanged?.Invoke();
        return part;
    }

    // 장착된 부위를 떼어내 돌려준다. 잠긴 슬롯은 버릴 수 없다.
    public BodyPart RemoveEquipped(BodySlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= equipped.Length) return null;
        if (IsSlotLocked(slot)) return null;

        BodyPart part = equipped[index];
        if (part == null) return null;

        equipped[index] = null;
        SyncBodyState();
        OnInventoryChanged?.Invoke();
        return part;
    }

    public bool TryAddPartToSlot(BodyPart part, int slotIndex)
    {
        if (part == null || slotIndex < 0 || slotIndex >= storage.Length)
            return false;
        if (storage[slotIndex] != null)
            return false;

        storage[slotIndex] = part;
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 동전을 슬롯에 귀속하여 추가 (기존 슬롯 채우기 → 새 슬롯 할당 순)
    public bool AddCoin(int amount = 1, Sprite icon = null)
    {
        if (icon != null) coinIcon = icon;
        int remaining = amount;

        // 기존 슬롯 채우기
        for (int i = 0; i < StorageSlotCount && remaining > 0; i++)
        {
            if (storage[i] != null) continue;
            if (!coinSlots.TryGetValue(i, out int existing)) continue;
            int space = MaxCoinsPerStack - existing;
            if (space <= 0) continue;
            int add = Mathf.Min(space, remaining);
            coinSlots[i] = existing + add;
            remaining -= add;
        }

        // 빈 슬롯에 새 스택 생성
        for (int i = 0; i < StorageSlotCount && remaining > 0; i++)
        {
            if (storage[i] != null) continue;
            if (coinSlots.ContainsKey(i)) continue;
            int add = Mathf.Min(MaxCoinsPerStack, remaining);
            coinSlots[i] = add;
            remaining -= add;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    // 동전 슬롯을 다른 슬롯으로 이동 또는 교환
    public bool MoveCoinToSlot(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot) return false;
        if (!coinSlots.TryGetValue(fromSlot, out int fromCount)) return false;
        if (toSlot < 0 || toSlot >= StorageSlotCount) return false;
        if (storage[toSlot] != null) return false; // BodyPart 있는 슬롯엔 불가

        coinSlots.TryGetValue(toSlot, out int toCount);
        if (toCount > 0)
            coinSlots[fromSlot] = toCount;
        else
            coinSlots.Remove(fromSlot);
        coinSlots[toSlot] = fromCount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 슬롯에서 동전 스택 제거 후 개수 반환
    public int RemoveCoinFromSlot(int slotIndex)
    {
        if (!coinSlots.TryGetValue(slotIndex, out int count)) return 0;
        coinSlots.Remove(slotIndex);
        OnInventoryChanged?.Invoke();
        return count;
    }

    public bool TryAddPart(BodyPart part, bool equipIfEmpty = true)
    {
        if (part == null)
            return false;

        // 동전은 스택으로 별도 관리 (슬롯 하나에 최대 9개)
        if (part.kind == ItemKind.Coin)
            return AddCoin(1, part.icon);

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
        lockedSlots = new bool[6];
        SyncBodyState();
        OnInventoryChanged?.Invoke();
    }

    public void ReplaceState(BodyPart[] newEquipped, BodyPart[] newStorage, bool[] newLockedSlots)
    {
        equipped = NormalizeParts(newEquipped, 6);
        storage = NormalizeParts(newStorage, StorageSlotCount);
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

    // 주운 동전(들)을 여유 있는 기존 동전 더미에 나눠 합치고, 남으면 빈 칸에 새 더미로 놓는다.
    // coin.count 가 1보다 커도(예: 버렸던 동전 더미를 다시 주울 때) 개수를 잃지 않고 전부 반영한다.
    bool TryAddCoin(BodyPart coin)
    {
        int remaining = Mathf.Max(1, coin.count);

        for (int i = 0; i < storage.Length && remaining > 0; i++)
        {
            BodyPart existing = storage[i];
            if (existing == null || existing.kind != ItemKind.Coin || existing.count >= MaxCoinStackCount)
                continue;

            int space = MaxCoinStackCount - existing.count;
            int add = Mathf.Min(space, remaining);
            existing.count += add;
            remaining -= add;
        }

        while (remaining > 0)
        {
            int free = FreeStorageIndex();
            if (free < 0)
                return false; // 일부는 이미 기존 더미에 합쳐졌을 수 있으나 나머지를 넣을 칸이 없음

            int add = Mathf.Min(MaxCoinStackCount, remaining);
            storage[free] = new BodyPart(ItemKind.Coin) { icon = coin.icon, itemId = coin.itemId, count = add };
            remaining -= add;
        }

        OnInventoryChanged?.Invoke();
        return true;
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
