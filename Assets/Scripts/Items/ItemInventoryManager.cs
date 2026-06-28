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

    ItemSystemSettings settings;
    ItemData consumable;
    ItemData shield;
    int coins;
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
    public int Coins => coins;
    public bool ShieldArmed => shieldArmed;
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
        coins = settings.startingCoins;
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
            AddCoins(Mathf.Max(1, Mathf.RoundToInt(item.Value)));
            message = item.ItemName + " 획득";
            return true;
        }

        if (item.Type == ItemType.GemConsumable || item.EquipLocation == ItemEquipLocation.Consumable)
            return TryEquipSpecial(item, ref consumable, ItemEquipLocation.Consumable, out message);

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
            return TryEquipBodyPart(item, out message);

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
            AddCoins(Mathf.Max(1, Mathf.RoundToInt(item.Value)));
            message = item.ItemName + " 획득";
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
        if (coins < price)
        {
            message = "동전이 부족합니다.";
            return false;
        }

        if (!CanAcquire(item, out message))
            return false;

        coins -= price;
        bool acquired = TryAcquire(item, out message);
        if (!acquired)
        {
            coins += price;
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
        AutoEquipNextConsumable();
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

        ItemData coin = ItemCatalog.Find("coin");
        if (coin != null)
            ItemDropSpawner.Spawn(coin, deathPosition, false, 0);
        else
            AddCoins(1);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        coins += amount;
        NotifyChanged();
    }

    bool CanAcquire(ItemData item, out string message)
    {
        message = "";
        if (item == null)
            return false;

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
                enemies[i].TakeDamage(damage);
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
