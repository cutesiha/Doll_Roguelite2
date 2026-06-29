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

    readonly List<ItemData> storage = new();
    readonly Dictionary<ItemEquipLocation, ItemData> equipped = new();
    // 신체부위 아이템을 BodySlot별로 장착 (task3)
    [System.NonSerialized] readonly Dictionary<BodySlot, ItemData> equippedByBodySlot = new();
    // 동전 스택: 각 원소 = 해당 스택의 동전 개수 (최대 9) (task7)
    [System.NonSerialized] readonly List<int> coinStacks = new();
    // 동전 ItemData 참조 (3x3 표시용)
    ItemData coinItemRef;

    ItemSystemSettings settings;
    ItemData consumable;
    ItemData shield;
    int coins; // legacy abstract coins (AddCoins 등 비-아이템 경로)
    bool shieldWaitingForNextRoom;
    bool shieldArmed;
    int shieldEquippedSceneHandle = -1;
    int roomBuffSceneHandle = -1;
    float roomMoveSpeedBonus;
    float roomArmDamageBonus;
    float coinOnKillUntil;

    public event Action Changed;

    public IReadOnlyList<ItemData> Storage => storage;
    public ItemData Consumable => consumable;
    public ItemData Shield => shield;
    // 총 동전 = storage 스택 합계 + abstract coins
    public int Coins => ComputeTotalCoins();
    public bool ShieldArmed => shieldArmed;
    // task7: 동전 스택 리스트 (UI 3x3 표시용)
    public IReadOnlyList<int> CoinStacks => coinStacks;
    // task7: 동전 아이템 데이터 참조
    public ItemData CoinItemRef => coinItemRef;
    // task3: 신체부위 장착 아이템
    public ItemData GetEquippedByBodySlot(BodySlot slot)
    {
        equippedByBodySlot.TryGetValue(slot, out ItemData item);
        return item;
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
            return consumable;
        if (location == ItemEquipLocation.Shield)
            return shield;

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
                coinStacks.Add(Mathf.Min(9, remaining));
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
            storage.Add(item);
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
            storage.Add(item);
            message = item.ItemName + "을(를) 인벤토리에 넣었습니다.";
            NotifyChanged();
            return true;
        }

        if (storage.Count >= Capacity)
        {
            message = "인벤토리가 가득 참";
            return false;
        }

        storage.Add(item);
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
                coinStacks.Add(Mathf.Min(9, remaining));
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

        storage.Add(item);
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

        ItemData used = consumable;
        if (!ApplyConsumable(used))
            return false;

        consumable = null;
        NotifyChanged();
        return true;
    }

    public bool TryBlockHit()
    {
        if (!shieldArmed || shield == null)
            return false;

        shieldArmed = false;
        ItemData usedShield = shield;
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
        consumable = item;
        if (item != null)
            Announce(item.ItemName + " Q 슬롯 장착");
        NotifyChanged();
    }

    public bool TryEquipConsumableFromStorage(ItemData item)
    {
        if (item == null || item.Type != ItemType.GemConsumable)
            return false;

        int index = storage.IndexOf(item);
        if (index < 0)
            return false;

        storage.RemoveAt(index);

        if (consumable != null)
            storage.Add(consumable);

        consumable = item;
        Announce(item.ItemName + " Q 슬롯 장착");
        NotifyChanged();
        return true;
    }

    public bool RemoveItemFromStorage(ItemData item)
    {
        if (item == null)
            return false;
        bool removed = storage.Remove(item);
        if (removed)
            NotifyChanged();
        return removed;
    }

    public ItemData RemoveConsumable()
    {
        ItemData removed = consumable;
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

    // task3: 보관함의 신체부위 아이템을 특정 BodySlot에 장착
    public bool TryEquipBodyPartFromStorage(ItemData item, BodySlot targetSlot)
    {
        if (item == null || item.Type != ItemType.BodyPart)
            return false;
        if (!IsBodyPartCompatibleWithSlot(item.EquipLocation, targetSlot))
            return false;

        int idx = storage.IndexOf(item);
        if (idx < 0)
            return false;

        storage.RemoveAt(idx);

        // 기존 장착 아이템이 있으면 보관함으로
        if (equippedByBodySlot.TryGetValue(targetSlot, out ItemData old) && old != null)
        {
            if (storage.Count < Capacity)
                storage.Add(old);
        }

        equippedByBodySlot[targetSlot] = item;
        Announce(item.ItemName + " 장착");
        NotifyChanged();
        return true;
    }

    // task3: 장착된 신체부위 아이템을 보관함으로 되돌리기
    public bool TryUnequipBodyPartToStorage(BodySlot slot)
    {
        if (!equippedByBodySlot.TryGetValue(slot, out ItemData item) || item == null)
            return false;
        if (storage.Count >= Capacity)
            return false;

        storage.Add(item);
        equippedByBodySlot.Remove(slot);
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
                coinStacks.RemoveAt(i);
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
            case ItemEquipLocation.Body: return false; // Body는 별도 처리
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

    bool TryEquipBodyPart(ItemData item, out string message)
    {
        message = "";
        ItemEquipLocation location = item.EquipLocation;
        ItemData old = GetEquipped(location);
        int futureCapacity = CapacityAfterEquipping(item);
        int futureStorageCount = storage.Count + (old != null ? 1 : 0);

        if (futureStorageCount > futureCapacity)
        {
            message = "인벤토리가 가득 참";
            return false;
        }

        if (old != null)
            storage.Add(old);
        equipped[location] = item;
        message = item.ItemName + " 자동 장착";
        if (old != null)
            message += " / " + old.ItemName + "은(는) 인벤토리로 이동";

        NotifyChanged();
        return true;
    }

    bool TryEquipSpecial(ItemData item, ref ItemData slot, ItemEquipLocation location, out string message)
    {
        message = "";
        if (slot != null)
        {
            if (storage.Count >= Capacity)
            {
                message = "인벤토리가 가득 참";
                return false;
            }
            storage.Add(slot);
        }

        slot = item;
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
            if (enemies[i] != null && damaged.Add(enemies[i]))
                enemies[i].TakeDamage(99999);
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
            ItemData item = storage[i];
            if (item == null || item.Type != ItemType.GemConsumable)
                continue;
            storage.RemoveAt(i);
            consumable = item;
            return;
        }
    }

    void AutoEquipNextShield()
    {
        for (int i = 0; i < storage.Count; i++)
        {
            ItemData item = storage[i];
            if (item == null || item.Type != ItemType.Shield)
                continue;
            storage.RemoveAt(i);
            shield = item;
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
