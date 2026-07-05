using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-15)]
public class ItemInventoryManager : MonoBehaviour
{
    public static ItemInventoryManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<ItemInventoryManager>() != null)
            return;

        GameObject go = new GameObject("ItemInventoryManager");
        go.AddComponent<ItemInventoryManager>();
    }

    readonly List<ItemInstance> storage = new();
    readonly Dictionary<ItemEquipLocation, ItemData> equipped = new();
    // 신체부위 아이템을 BodySlot별로 장착 (task3). ItemInstance라서 교체돼 보관함에
    // 갔다가 다시 장착돼도 그 아이템 고유의 HP가 그대로 유지된다.
    [System.NonSerialized] readonly Dictionary<BodySlot, ItemInstance> equippedByBodySlot = new();
    // 동전 스택: 각 원소 = 해당 스택의 동전 개수 (최대 9) (task7)
    [System.NonSerialized] readonly List<int> coinStacks = new();
    // 동전 ItemData 참조 (3x3 표시용)
    ItemData coinItemRef;

    // storage/coinStacks와 1:1 대응하는 "고정 물리 슬롯" 정보.
    // -1 = 고정 안 됨(자동으로 앞에서부터 채움), 그 외 = 사용자가 드래그해서 고정한 UI 슬롯 번호.
    readonly List<int> storageSlot = new();
    readonly List<int> coinStackSlot = new();

    public enum StorageEntryKind { Item, CoinStack }

    ItemSystemSettings settings;
    ItemInstance consumable;
    ItemInstance shield;
    int coins; // legacy abstract coins (AddCoins 등 비-아이템 경로)
    bool shieldWaitingForNextRoom;
    bool shieldArmed;
    int shieldEquippedSceneHandle = -1;
    int roomBuffSceneHandle = -1;
    float roomMoveSpeedBonus;
    float roomArmDamageBonus;
    float coinOnKillUntil;

    public event Action Changed;

    public IReadOnlyList<ItemInstance> Storage => storage;
    public ItemData Consumable => consumable?.data;
    public ItemData Shield => shield?.data;
    // 총 동전 = storage 스택 합계 + abstract coins
    public int Coins => ComputeTotalCoins();
    public bool ShieldArmed => shieldArmed;
    // task7: 동전 스택 리스트 (UI 3x3 표시용)
    public IReadOnlyList<int> CoinStacks => coinStacks;
    // task7: 동전 아이템 데이터 참조
    public ItemData CoinItemRef => coinItemRef;

    // storage/coinStacks 리스트에 안전하게 넣고 빼기 위한 래퍼.
    // storageSlot/coinStackSlot 배열이 항상 같은 길이를 유지하도록 여기서만 Add/Remove 한다.
    void AddToStorage(ItemInstance instance)
    {
        storage.Add(instance);
        storageSlot.Add(-1);
    }

    // 처음 획득하는 아이템(ItemData)을 새 ItemInstance로 감싸 보관함에 넣는다.
    // maxHp는 해당 부위 기본(레거시) 파츠와 같은 규칙(눈=2, 그 외=3)을 따른다.
    void AddNewItemToStorage(ItemData item)
    {
        AddToStorage(new ItemInstance(item, ItemInstance.DefaultMaxHp(item.EquipLocation)));
    }

    void RemoveStorageAt(int index)
    {
        storage.RemoveAt(index);
        storageSlot.RemoveAt(index);
    }

    bool RemoveStorageItem(ItemData item)
    {
        int idx = storage.FindIndex(inst => inst != null && inst.data == item);
        if (idx < 0)
            return false;
        RemoveStorageAt(idx);
        return true;
    }

    void AddCoinStack(int amount)
    {
        coinStacks.Add(amount);
        coinStackSlot.Add(-1);
    }

    void RemoveCoinStackAt(int index)
    {
        coinStacks.RemoveAt(index);
        coinStackSlot.RemoveAt(index);
    }

    public int GetItemSlotPin(int index) => (index >= 0 && index < storageSlot.Count) ? storageSlot[index] : -1;
    public int GetCoinStackSlotPin(int index) => (index >= 0 && index < coinStackSlot.Count) ? coinStackSlot[index] : -1;

    bool SetEntryPin(StorageEntryKind kind, int index, int physicalSlot)
    {
        if (kind == StorageEntryKind.Item)
        {
            if (index < 0 || index >= storageSlot.Count) return false;
            storageSlot[index] = physicalSlot;
            return true;
        }

        if (index < 0 || index >= coinStackSlot.Count) return false;
        coinStackSlot[index] = physicalSlot;
        return true;
    }

    // 보관함 안의 아이템/동전더미를 특정 UI 물리 슬롯에 고정시킨다.
    // 그 슬롯에 이미 다른 아이템/동전더미가 있으면(hasOccupant) 서로 자리를 맞바꾼다.
    public bool PinEntryToPhysicalSlot(
        StorageEntryKind kind, int index, int fromPhysicalSlot, int toPhysicalSlot,
        StorageEntryKind occupantKind, int occupantIndex, bool hasOccupant)
    {
        if (!SetEntryPin(kind, index, toPhysicalSlot))
            return false;

        if (hasOccupant)
            SetEntryPin(occupantKind, occupantIndex, fromPhysicalSlot);

        NotifyChanged();
        return true;
    }

    // task3: 신체부위 장착 아이템
    public ItemData GetEquippedByBodySlot(BodySlot slot)
    {
        equippedByBodySlot.TryGetValue(slot, out ItemInstance instance);
        return instance?.data;
    }

    // 장착된 신규 아이템의 현재/최대 HP. 그 슬롯에 신규 아이템이 없으면 false.
    public bool TryGetEquippedItemHp(BodySlot slot, out int currentHp, out int maxHp)
    {
        if (equippedByBodySlot.TryGetValue(slot, out ItemInstance instance) && instance != null)
        {
            currentHp = instance.currentHp;
            maxHp = instance.maxHp;
            return true;
        }

        currentHp = 0;
        maxHp = 0;
        return false;
    }

    // 그 슬롯에 신규 아이템이 장착돼 있으면 데미지를 적용한다 (레거시 BodyPart와 별개 경로).
    // brokenItem은 이 데미지로 파괴돼 장착 해제된 아이템(월드로 떨어뜨릴 때 필요). 안 부서졌으면 null.
    public bool TryDamageEquippedItem(BodySlot slot, int damage, out ItemData brokenItem)
    {
        brokenItem = null;
        if (damage <= 0)
            return false;

        if (!equippedByBodySlot.TryGetValue(slot, out ItemInstance instance) || instance == null)
            return false;

        instance.currentHp = Mathf.Clamp(instance.currentHp - damage, 0, instance.maxHp);
        if (instance.currentHp <= 0)
        {
            brokenItem = instance.data;
            equippedByBodySlot.Remove(slot);
            SyncEquippedLocationFromSlots(instance.data.EquipLocation);
        }

        NotifyChanged();
        return true;
    }
    public float RoomMoveSpeedBonus => roomBuffSceneHandle == SceneManager.GetActiveScene().handle ? roomMoveSpeedBonus : 0f;
    public float RoomArmDamageBonus => roomBuffSceneHandle == SceneManager.GetActiveScene().handle ? roomArmDamageBonus : 0f;

    public int Capacity
    {
        get
        {
            ItemData body = GetEquipped(ItemEquipLocation.Body);
            if (body != null)
            {
                ItemEffectData capacity = body.GetEffect(ItemEffectType.InventoryCapacity);
                if (capacity != null)
                    return Mathf.Max(1, Mathf.RoundToInt(capacity.value));
            }

            return Mathf.Max(1, settings != null ? settings.defaultInventoryCapacity : 9);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        settings = ItemSystemSettings.Load();
        coins = settings != null ? settings.startingCoins : 0;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureDebugHud();
    }

    void Start()
    {
        AttachEffectsToPlayer();
        Changed?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (roomBuffSceneHandle != scene.handle)
        {
            roomMoveSpeedBonus = 0f;
            roomArmDamageBonus = 0f;
            roomBuffSceneHandle = -1;
        }

        if (shieldWaitingForNextRoom && scene.handle != shieldEquippedSceneHandle)
        {
            shieldWaitingForNextRoom = false;
            shieldArmed = shield != null;
            Announce(shieldArmed ? "누더기 방어막이 활성화되었습니다. 다음 피격 1회를 막습니다." : "");
        }

        EnsureDebugHud();
        AttachEffectsToPlayer();
        Changed?.Invoke();
    }

    public ItemData GetEquipped(ItemEquipLocation location)
    {
        if (location == ItemEquipLocation.Consumable)
            return consumable?.data;
        if (location == ItemEquipLocation.Shield)
            return shield?.data;

        equipped.TryGetValue(location, out ItemData item);
        return item;
    }

    public bool TryAcquire(ItemData item, out string message)
    {
        message = "";
        if (item == null)
            return false;

        if (item.Type == ItemType.Currency)
        {
            // 동전을 스택으로 인벤토리 슬롯에 저장 (task7)
            coinItemRef = item;
            int amount = Mathf.Max(1, Mathf.RoundToInt(item.Value));
            int remaining = amount;
            for (int idx = 0; idx < coinStacks.Count && remaining > 0; idx++)
            {
                int space = 9 - coinStacks[idx];
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                coinStacks[idx] += add;
                remaining -= add;
            }
            while (remaining > 0)
            {
                AddCoinStack(Mathf.Min(9, remaining));
                remaining -= 9;
            }
            message = item.ItemName + " 획득";
            NotifyChanged();
            return true;
        }

        if (item.Type == ItemType.GemConsumable || item.EquipLocation == ItemEquipLocation.Consumable)
        {
            if (storage.Count >= Capacity)
            {
                message = "인벤토리가 가득 참";
                return false;
            }
            AddNewItemToStorage(item);
            message = item.ItemName + "을(를) 인벤토리에 넣었습니다. Q 보석 칸에 끌어다 놓아 장착하세요.";
            NotifyChanged();
            return true;
        }

        if (item.Type == ItemType.Shield || item.EquipLocation == ItemEquipLocation.Shield)
        {
            bool acquired = TryEquipSpecial(item, ref shield, ItemEquipLocation.Shield, out message);
            if (acquired)
            {
                shieldWaitingForNextRoom = true;
                shieldArmed = false;
                shieldEquippedSceneHandle = SceneManager.GetActiveScene().handle;
                message += " (다음 방에서 방어막 활성화)";
            }
            return acquired;
        }

        if (item.Type == ItemType.BodyPart && item.EquipLocation != ItemEquipLocation.None)
        {
            // 자동 장착 대신 인벤토리 보관함에 넣는다. 사용자가 직접 드래그해서 장착.
            if (storage.Count >= Capacity)
            {
                message = "인벤토리가 가득 참";
                return false;
            }
            AddNewItemToStorage(item);
            message = item.ItemName + "을(를) 인벤토리에 넣었습니다.";
            NotifyChanged();
            return true;
        }

        if (storage.Count >= Capacity)
        {
            message = "인벤토리가 가득 참";
            return false;
        }

        AddNewItemToStorage(item);
        message = item.ItemName + "을(를) 인벤토리에 넣었습니다.";
        NotifyChanged();
        return true;
    }

    public bool TryStoreWithoutEquip(ItemData item, out string message)
    {
        message = "";
        if (item == null)
            return false;

        if (item.Type == ItemType.Currency)
        {
            coinItemRef = item;
            int amount = Mathf.Max(1, Mathf.RoundToInt(item.Value));
            int remaining = amount;
            for (int idx = 0; idx < coinStacks.Count && remaining > 0; idx++)
            {
                int space = 9 - coinStacks[idx];
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                coinStacks[idx] += add;
                remaining -= add;
            }
            while (remaining > 0)
            {
                AddCoinStack(Mathf.Min(9, remaining));
                remaining -= 9;
            }
            message = item.ItemName + " 획득";
            NotifyChanged();
            return true;
        }

        if (storage.Count >= Capacity)
        {
            message = "인벤토리가 가득 찼습니다.";
            return false;
        }

        AddNewItemToStorage(item);
        message = item.ItemName + "을(를) 보관함에 넣었습니다.";
        NotifyChanged();
        return true;
    }

    public bool TryPurchase(ItemData item, int price, out string message)
    {
        price = Mathf.Max(0, price);
        if (Coins < price)
        {
            message = "동전이 부족합니다.";
            return false;
        }

        if (!CanAcquire(item, out message))
            return false;

        SpendCoins(price);
        bool acquired = TryAcquire(item, out message);
        if (!acquired)
        {
            AddCoins(price); // 환불
            return false;
        }

        message = item.ItemName + " 구매 완료 (-" + price + " 동전)";
        NotifyChanged();
        return true;
    }

    public bool TryUseEquippedConsumable()
    {
        if (consumable == null)
            return false;

        ItemData used = consumable.data;
        if (!ApplyConsumable(used))
            return false;

        consumable = null;
        NotifyChanged();
        SoundManager.PlayGemUse();
        return true;
    }

    public bool TryBlockHit()
    {
        if (!shieldArmed || shield == null)
            return false;

        shieldArmed = false;
        ItemData usedShield = shield.data;
        shield = null;
        AutoEquipNextShield();
        Announce(usedShield.ItemName + " 방어막이 피격을 막았습니다.");
        NotifyChanged();
        return true;
    }

    public void NotifyEnemyKilled(Vector3 deathPosition)
    {
        if (Time.time >= coinOnKillUntil)
            return;

        GameObject coinPrefab = Resources.Load<GameObject>("Drops/동전");
        if (coinPrefab != null)
        {
            GameObject go = Instantiate(coinPrefab, deathPosition, Quaternion.identity);
            go.GetComponent<CoinWorldPickup>()?.Toss(deathPosition);
        }
        else
            AddCoins(1);
    }

    public void ForceSetConsumable(ItemData item)
    {
        consumable = item != null ? new ItemInstance(item, ItemInstance.DefaultMaxHp(item.EquipLocation)) : null;
        if (item != null)
            Announce(item.ItemName + " Q 슬롯 장착");
        NotifyChanged();
    }

    public bool TryEquipConsumableFromStorage(ItemData item)
    {
        if (item == null || item.Type != ItemType.GemConsumable)
            return false;

        int index = storage.FindIndex(inst => inst != null && inst.data == item);
        if (index < 0)
            return false;

        ItemInstance instance = storage[index];
        RemoveStorageAt(index);

        if (consumable != null)
            AddToStorage(consumable);

        consumable = instance;
        Announce(item.ItemName + " Q 슬롯 장착");
        NotifyChanged();
        return true;
    }

    public bool RemoveItemFromStorage(ItemData item)
    {
        if (item == null)
            return false;
        bool removed = RemoveStorageItem(item);
        if (removed)
            NotifyChanged();
        return removed;
    }

    public ItemData RemoveConsumable()
    {
        ItemData removed = consumable?.data;
        consumable = null;
        if (removed != null)
        {
            NotifyChanged();
        }
        return removed;
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        coins += amount;
        NotifyChanged();
    }

    // task3: 보관함의 신체부위 아이템을 특정 BodySlot에 장착.
    // storageIndex로 정확히 어떤 인스턴스인지 지정한다 (동일 아이템이 여러 개 있어도
    // 서로 다른 HP를 가진 인스턴스를 헷갈리지 않도록).
    public bool TryEquipBodyPartFromStorage(int storageIndex, BodySlot targetSlot)
    {
        if (storageIndex < 0 || storageIndex >= storage.Count)
            return false;

        ItemInstance instance = storage[storageIndex];
        ItemData item = instance?.data;
        if (item == null || item.Type != ItemType.BodyPart)
            return false;
        if (!IsBodyPartCompatibleWithSlot(item.EquipLocation, targetSlot))
            return false;

        // 몸통 부위를 교체하면 인벤토리 용량이 줄어들 수 있다 (예: 16칸짜리 → 9칸짜리).
        // 실제로 스왑을 진행하기 전에, 교체 후 예상 보관함 개수가 새 용량을 넘는지 미리 확인해서
        // 넘으면 아예 교체를 막는다 (그렇지 않으면 보관함이 용량보다 많은 아이템을 담게 된다).
        bool willDisplaceOld = equippedByBodySlot.TryGetValue(targetSlot, out ItemInstance existingOld) && existingOld != null;
        int projectedStorageCount = (storage.Count - 1) + (willDisplaceOld ? 1 : 0);
        if (projectedStorageCount > CapacityAfterEquipping(item))
            return false; // 교체하면 보관함이 새 용량을 초과하게 됨 - 먼저 보관함을 비워야 함

        // 이 부위에 레거시 시스템(BodyPart)으로 이미 장착된 기본 파츠가 있으면 보관함으로 되돌린다.
        // 게임 시작 시 모든 부위에는 이 레거시 파츠가 기본으로 채워져 있어서, 여기서 치우지 않으면
        // 나중에 신규 아이템을 뺄 때 드롭 처리가 이 파츠를 대신 해제해버리는 문제가 생긴다.
        InventoryManager legacyInv = InventoryManager.Instance;
        int legacyIdx = (int)targetSlot;
        bool hasLegacyPart = legacyInv != null && legacyIdx >= 0 && legacyIdx < legacyInv.equipped.Length && legacyInv.equipped[legacyIdx] != null;
        if (hasLegacyPart && (legacyInv.IsSlotLocked(targetSlot) || !legacyInv.HasFreeStorageSlot()))
            return false; // 잠긴 슬롯이거나 보관함이 가득 차서 기존 파츠를 치울 수 없음

        RemoveStorageAt(storageIndex);

        if (hasLegacyPart)
        {
            BodyPart legacyPart = legacyInv.RemoveEquipped(targetSlot);
            if (legacyPart != null)
            {
                // icon이 비어 있으면(기본 파츠) 방금 장착한 아이템 그림을 잘못 물려받지 않도록
                // 이 부위 고유의 기본 그림을 미리 박아둔다.
                if (legacyPart.icon == null)
                    legacyPart.icon = InventoryUI.FindBaseSpriteForSlot(targetSlot);
                legacyInv.TryAddPart(legacyPart, false);
            }
        }

        // 기존 장착 아이템(신규 시스템)이 있으면 보관함으로 (HP 유지).
        // 위에서 이미 교체 후 용량을 넘지 않는지 확인했으므로 여기선 안전하게 추가한다.
        if (willDisplaceOld)
            AddToStorage(existingOld);

        equippedByBodySlot[targetSlot] = instance;
        // task-C: 부위 슬롯 장착 아이템의 효과가 PlayerItemEffects(GetEquipped 기반)에 반영되도록
        // location별 equipped 사전도 동기화한다. (보석 외 아이템 효과 미적용 버그)
        SyncEquippedLocationFromSlots(item.EquipLocation);
        Announce(item.ItemName + " 장착");
        NotifyChanged();
        return true;
    }

    // equippedByBodySlot(좌/우 슬롯)에 장착된 아이템 기준으로 equipped[location]을 다시 맞춘다.
    void SyncEquippedLocationFromSlots(ItemEquipLocation location)
    {
        if (location == ItemEquipLocation.None)
            return;

        ItemData found = null;
        foreach (var kv in equippedByBodySlot)
        {
            if (kv.Value != null && kv.Value.data != null && kv.Value.data.EquipLocation == location)
            {
                found = kv.Value.data;
                break;
            }
        }

        if (found != null)
            equipped[location] = found;
        else
            equipped.Remove(location);
    }

    // task3: 장착된 신체부위 아이템을 보관함으로 되돌리기 (HP 유지)
    public bool TryUnequipBodyPartToStorage(BodySlot slot)
    {
        if (!equippedByBodySlot.TryGetValue(slot, out ItemInstance instance) || instance == null)
            return false;
        if (storage.Count >= Capacity)
            return false;

        AddToStorage(instance);
        equippedByBodySlot.Remove(slot);
        SyncEquippedLocationFromSlots(instance.data.EquipLocation);
        NotifyChanged();
        return true;
    }

    // task7: 동전 총 개수 계산 (coinStacks + abstract coins)
    int ComputeTotalCoins()
    {
        int total = coins;
        for (int i = 0; i < coinStacks.Count; i++)
            total += coinStacks[i];
        return total;
    }

    // task7: 동전 소비 (coinStacks → abstract coins 순)
    void SpendCoins(int amount)
    {
        for (int i = coinStacks.Count - 1; i >= 0 && amount > 0; i--)
        {
            int take = Mathf.Min(amount, coinStacks[i]);
            coinStacks[i] -= take;
            amount -= take;
            if (coinStacks[i] == 0)
                RemoveCoinStackAt(i);
        }
        if (amount > 0)
            coins = Mathf.Max(0, coins - amount);
    }

    public static bool IsBodyPartCompatibleWithSlot(ItemEquipLocation location, BodySlot slot)
    {
        switch (location)
        {
            case ItemEquipLocation.Eye: return slot == BodySlot.EyeLeft || slot == BodySlot.EyeRight;
            case ItemEquipLocation.Arm: return slot == BodySlot.ArmLeft || slot == BodySlot.ArmRight;
            case ItemEquipLocation.Leg: return slot == BodySlot.LegLeft || slot == BodySlot.LegRight;
            case ItemEquipLocation.Body: return slot == BodySlot.Body;
            default: return false;
        }
    }

    bool CanAcquire(ItemData item, out string message)
    {
        message = "";
        if (item == null)
            return false;

        if (item.Type == ItemType.GemConsumable || item.EquipLocation == ItemEquipLocation.Consumable)
        {
            if (storage.Count >= Capacity)
            {
                message = "인벤토리가 가득 참";
                return false;
            }
            return true;
        }

        ItemEquipLocation location = item.EquipLocation;
        ItemData current = GetEquipped(location);
        if (current == null)
            return true;

        int futureCapacity = CapacityAfterEquipping(item);
        if (storage.Count >= futureCapacity)
        {
            message = "인벤토리가 가득 참";
            return false;
        }

        return true;
    }

    bool TryEquipSpecial(ItemData item, ref ItemInstance slot, ItemEquipLocation location, out string message)
    {
        message = "";
        if (slot != null)
        {
            if (storage.Count >= Capacity)
            {
                message = "인벤토리가 가득 참";
                return false;
            }
            AddToStorage(slot);
        }

        slot = new ItemInstance(item, ItemInstance.DefaultMaxHp(item.EquipLocation));
        message = item.ItemName + (location == ItemEquipLocation.Consumable ? " Q 슬롯 장착" : " 방어막 슬롯 장착");
        NotifyChanged();
        return true;
    }

    int CapacityAfterEquipping(ItemData item)
    {
        if (item == null || item.EquipLocation != ItemEquipLocation.Body)
            return Capacity;

        ItemEffectData capacity = item.GetEffect(ItemEffectType.InventoryCapacity);
        return capacity != null ? Mathf.Max(1, Mathf.RoundToInt(capacity.value)) : settings.defaultInventoryCapacity;
    }

    bool ApplyConsumable(ItemData item)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return false;

        bool applied = false;
        IReadOnlyList<ItemEffectData> effects = item.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            ItemEffectData effect = effects[i];
            if (effect == null)
                continue;

            switch (effect.effectType)
            {
                case ItemEffectType.Invincibility:
                    player.GetComponent<PlayerDamageReceiver>()?.SetInvincible(Mathf.Max(0f, effect.duration));
                    applied = true;
                    break;
                case ItemEffectType.DamageAllEnemies:
                    DamageAllEnemies(Mathf.Max(1, Mathf.RoundToInt(effect.value)));
                    CameraShake.Shake(0.18f, 0.14f);   // 약간의 화면 흔들림
                    applied = true;
                    break;
                case ItemEffectType.CoinOnKill:
                    coinOnKillUntil = Mathf.Max(coinOnKillUntil, Time.time + Mathf.Max(0f, effect.duration));
                    applied = true;
                    break;
                case ItemEffectType.RoomMoveSpeed:
                    roomBuffSceneHandle = SceneManager.GetActiveScene().handle;
                    roomMoveSpeedBonus = effect.value;
                    applied = true;
                    break;
                case ItemEffectType.DamageAllParts:
                    DamageAllPlayerParts(Mathf.Max(1, Mathf.RoundToInt(effect.value)));
                    applied = true;
                    break;
                case ItemEffectType.BothArmDamage:
                    roomBuffSceneHandle = SceneManager.GetActiveScene().handle;
                    roomArmDamageBonus = effect.value;
                    applied = true;
                    break;
            }
        }

        if (applied)
        {
            if (item.ItemId == "black_gem")
            {
                DarkAuraEffect.SpawnOn(player.transform, 3f);
                ScreenVignetteEffect.Show(3f, Color.black);
            }
            else if (item.ItemId == "pink_gem")
            {
                Color pink = new Color(1f, 0.35f, 0.68f);
                DarkAuraEffect.SpawnOn(player.transform, 3f, pink);
                ScreenVignetteEffect.Show(3f, pink);
            }
            else if (item.ItemId == "blue_gem")
            {
                Color blue = new Color(0.18f, 0.48f, 1f);
                DarkAuraEffect.SpawnOn(player.transform, 3f, blue);
                ScreenVignetteEffect.Show(3f, blue);
            }
            else if (item.ItemId == "yellow_gem")
            {
                YellowAuraEffect.SpawnOn(player.transform, 15f);
                ScreenFogEffect.Show(1.5f, 0.5f, new Color(1f, 0.9f, 0.3f));
            }
            else if (item.ItemId == "white_gem")
            {
                WhiteAuraEffect.SpawnOn(player.transform, 20f);
            }

            ShowItemPopup(item);
            Announce(item.ItemName + " 사용: " + item.Description);
            AttachEffectsToPlayer();
        }
        return applied;
    }

    void DamageAllEnemies(int damage)
    {
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        HashSet<EnemyBase> damaged = new();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || !damaged.Add(enemies[i]))
                continue;

            BookBossPart bossPart = enemies[i] as BookBossPart;
            if (bossPart != null)
                bossPart.TakeDamage(4);
            else
                enemies[i].TakeDamage(99999);
        }
    }

    void DamageAllPlayerParts(int damage)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return;

        foreach (BodySlot slot in Enum.GetValues(typeof(BodySlot)))
            inventory.TryDamageEquippedPart(slot, damage, out _);
    }

    void AutoEquipNextConsumable()
    {
        for (int i = 0; i < storage.Count; i++)
        {
            ItemInstance instance = storage[i];
            if (instance == null || instance.data == null || instance.data.Type != ItemType.GemConsumable)
                continue;
            RemoveStorageAt(i);
            consumable = instance;
            return;
        }
    }

    void AutoEquipNextShield()
    {
        for (int i = 0; i < storage.Count; i++)
        {
            ItemInstance instance = storage[i];
            if (instance == null || instance.data == null || instance.data.Type != ItemType.Shield)
                continue;
            RemoveStorageAt(i);
            shield = instance;
            shieldWaitingForNextRoom = true;
            shieldEquippedSceneHandle = SceneManager.GetActiveScene().handle;
            return;
        }
    }

    void AttachEffectsToPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        PlayerItemEffects effects = player.GetComponent<PlayerItemEffects>();
        if (effects == null)
            effects = player.AddComponent<PlayerItemEffects>();
        effects.Refresh();
    }

    void EnsureDebugHud()
    {
        bool isTestRoom = SceneManager.GetActiveScene().name == "itemtestroom";
        ItemDebugHud hud = GetComponent<ItemDebugHud>();
        if (isTestRoom && hud == null)
            gameObject.AddComponent<ItemDebugHud>();
        else if (!isTestRoom && hud != null)
            Destroy(hud);
    }

    void NotifyChanged()
    {
        AttachEffectsToPlayer();
        Changed?.Invoke();
    }

    static void Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText(message);
        Debug.Log("[Item] " + message);
    }

    // 소모성 아이템 사용 시 아이콘 + 이름을 1.4초 동안 화면에 띄운다.
    static void ShowItemPopup(ItemData item)
    {
        if (item == null)
            return;

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowJewelPopup(item.Sprite, item.ItemName, 1.4f);
    }
}
