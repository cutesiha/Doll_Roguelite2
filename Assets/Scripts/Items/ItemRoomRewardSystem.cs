using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ItemRoomRewardSystem
{
    static readonly List<ItemData> selected = new();
    static readonly List<ItemData> matches = new();
    static int lastStartedSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        lastStartedSceneHandle = -1;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapActiveScene()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;
        if (scene.handle == lastStartedSceneHandle)
            return;

        lastStartedSceneHandle = scene.handle;

        ItemRoomSceneBridge bridge = Object.FindFirstObjectByType<ItemRoomSceneBridge>();
        if (bridge == null)
        {
            GameObject go = new GameObject("ItemRoomSceneBridge");
            bridge = go.AddComponent<ItemRoomSceneBridge>();
        }
        bridge.Begin(scene.name);
    }

    public static void HandleCombatRoomCleared(MapNode pendingNode, Vector3 roomCenter)
    {
        if (pendingNode == null)
            return;

        if (pendingNode.roomType == RoomType.Challenge)
        {
            ItemData item = ItemCatalog.RandomByCategory(ItemCategory.ChallengeRoom);
            SpawnWithDropEffect(item, roomCenter);

            // 도전방 클리어 코인 보너스
            ItemSystemSettings settings = ItemSystemSettings.Load();
            int coinBonus = settings != null ? settings.challengeClearCoinBonus : 2;
            if (coinBonus > 0)
                ItemInventoryManager.Instance?.AddCoins(coinBonus);

            Announce("도전방 클리어! 아이템 + " + coinBonus + " 코인 보너스.");
            return;
        }

        if (pendingNode.roomType == RoomType.ConditionCombat)
        {
            ItemSystemSettings settings = ItemSystemSettings.Load();

            // 조건방 클리어 코인 보너스 (웨이브당 1코인은 Room.cs에서 별도 지급)
            int coinBonus = settings != null ? settings.challengeClearCoinBonus : 2;
            if (coinBonus > 0)
                ItemInventoryManager.Instance?.AddCoins(coinBonus);

            // 추가 아이템 보상 확률 (보석 10%, 누더기 5%)
            float roll = Random.value;
            float gemChance = settings != null ? settings.conditionGemChance : 0.10f;
            float ragChance = settings != null ? settings.conditionRagChance : 0.05f;

            if (roll < gemChance)
            {
                ItemData gem = ItemCatalog.RandomByType(ItemType.GemConsumable);
                SpawnWithDropEffect(gem, roomCenter);
                Announce("조건방 클리어! +" + coinBonus + " 코인 + 보석 보상!");
            }
            else if (roll < gemChance + ragChance)
            {
                ItemData rag = ItemCatalog.Find("rag");
                SpawnWithDropEffect(rag, roomCenter);
                Announce("조건방 클리어! +" + coinBonus + " 코인 + 누더기 보상!");
            }
            else
            {
                Announce("조건방 클리어! +" + coinBonus + " 코인 보너스.");
            }
        }
    }

    public static void SpawnMiddleBossRewards(Vector3 position)
    {
        selected.Clear();
        for (int i = 0; i < 2; i++)
        {
            ItemData item = RandomUniqueByCategory(ItemCategory.MiddleBossRoom, selected);
            if (item == null)
                continue;
            selected.Add(item);
            float x = i == 0 ? -1.1f : 1.1f;
            SpawnWithDropEffect(item, position + new Vector3(x, 0f, 0f));
        }
        Announce("중간보스 아이템 2개가 생성되었습니다.");
    }

    public static void SpawnBodyRoomReward(Vector3 position, ItemWorldPickup template = null)
    {
        ItemData item = ItemCatalog.RandomByCategory(ItemCategory.BodyRoom, ItemType.BodyPart);
        Vector3 spawnPosition = TreasureRewardPosition(position);
        ItemWorldPickup pickup = ItemDropSpawner.Spawn(item, spawnPosition, false, 0, template, true);
        pickup?.Toss(spawnPosition);
        ItemDropParticleEffect.Spawn(spawnPosition);
        Announce("신체방 아이템이 생성되었습니다.");
    }

    // 도전방 성공 보상: 도전 카테고리 아이템 1개를 트레저 테이블 앵커 위에 스폰 + 코인 보너스.
    public static void SpawnChallengeReward(Vector3 fallbackPosition, ItemWorldPickup template = null)
    {
        ItemData item = ItemCatalog.RandomByCategory(ItemCategory.ChallengeRoom);
        Vector3 spawnPosition = TreasureRewardPosition(fallbackPosition);
        ItemWorldPickup pickup = ItemDropSpawner.Spawn(item, spawnPosition, false, 0, template, true);
        pickup?.Toss(spawnPosition);
        ItemDropParticleEffect.Spawn(spawnPosition);

        ItemSystemSettings settings = ItemSystemSettings.Load();
        int coinBonus = settings != null ? settings.challengeClearCoinBonus : 2;
        if (coinBonus > 0)
            ItemInventoryManager.Instance?.AddCoins(coinBonus);

        Announce("도전 성공! 아이템 획득" + (coinBonus > 0 ? " + " + coinBonus + " 코인 보너스." : "."));
    }

    // 몬스터 킬 드랍과 동일하게, 보상 아이템이 튕겨 나가며 노란 입자 이펙트를 재생한다 (상점 진열 제외).
    static void SpawnWithDropEffect(ItemData item, Vector3 position)
    {
        ItemWorldPickup pickup = ItemDropSpawner.Spawn(item, position, false, 0);
        pickup?.Toss(position);
        ItemDropParticleEffect.Spawn(position);
    }

    static Vector3 TreasureRewardPosition(Vector3 fallback)
    {
        GameObject anchor = GameObject.Find("TreasureRewardAnchor");
        if (anchor != null)
            return anchor.transform.position;

        GameObject layout = GameObject.Find("TreasureLayout");
        if (layout != null)
        {
            Transform child = layout.transform.Find("TreasureRewardAnchor");
            if (child != null)
                return child.position;
        }

        return fallback;
    }

    public static void SpawnShop(Vector3 center, ItemWorldPickup template = null)
    {
        ItemSystemSettings settings = ItemSystemSettings.Load();
        ItemData gem = ItemCatalog.RandomByType(ItemType.GemConsumable);
        ItemData rag = ItemCatalog.Find("rag");

        selected.Clear();
        ItemData bodyA = RandomUniqueBodyPart(selected);
        if (bodyA != null)
            selected.Add(bodyA);
        ItemData bodyB = RandomUniqueBodyPart(selected);

        ItemDropSpawner.Spawn(gem, ShopItemPosition(center, 0), true, settings.gemPrice, template);
        ItemDropSpawner.Spawn(rag, ShopItemPosition(center, 1), true, settings.ragPrice, template);
        ItemDropSpawner.Spawn(bodyA, ShopItemPosition(center, 2), true, settings.bodyPartPrice, template);
        ItemDropSpawner.Spawn(bodyB, ShopItemPosition(center, 3), true, settings.bodyPartPrice, template);
        Announce("상점 품목: 보석 1, 누더기 1, 신체 부위 2");
    }

    static Vector3 ShopItemPosition(Vector3 center, int index)
    {
        Transform anchor = FindShopAnchor(index);
        if (anchor != null)
            return anchor.position;

        float x = -5.4f + index * 3.6f;
        return center + new Vector3(x, 0.5f, 0f);
    }

    static Transform FindShopAnchor(int index)
    {
        GameObject exact = GameObject.Find("ShopItemAnchor_" + index);
        if (exact != null)
            return exact.transform;

        GameObject layout = GameObject.Find("ShopLayout");
        if (layout == null)
            return null;

        Transform child = layout.transform.Find("ShopItemAnchor_" + index);
        return child;
    }

    static ItemData RandomUniqueByCategory(ItemCategory category, ICollection<ItemData> excluded)
    {
        IReadOnlyList<ItemData> all = ItemCatalog.All;
        matches.Clear();
        for (int i = 0; i < all.Count; i++)
        {
            ItemData item = all[i];
            if (item != null && item.Category == category && (excluded == null || !excluded.Contains(item)))
                matches.Add(item);
        }
        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    static ItemData RandomUniqueBodyPart(ICollection<ItemData> excluded)
    {
        IReadOnlyList<ItemData> all = ItemCatalog.All;
        List<ItemData> matches = new List<ItemData>();
        for (int i = 0; i < all.Count; i++)
        {
            ItemData item = all[i];
            // Default 카테고리(게임 시작 기본 파츠)는 어떤 보상 풀에도 나오면 안 된다.
            if (item != null && item.Type == ItemType.BodyPart && item.Category != ItemCategory.Default
                && (excluded == null || !excluded.Contains(item)))
                matches.Add(item);
        }
        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    static void Announce(string message)
    {
        RunHudUI hud = Object.FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText(message);
        Debug.Log("[Item] " + message);
    }
}

public class ItemRoomSceneBridge : MonoBehaviour
{
    string sceneName;

    public void Begin(string loadedSceneName)
    {
        sceneName = loadedSceneName;
        StartCoroutine(SetupRoutine());
    }

    IEnumerator SetupRoutine()
    {
        yield return null;

        if (sceneName == "TreasureRoomScene")
        {
            SpecialRoomController controller = DisableLegacySpecialRoomInteraction();
            HideLegacyProps("TreasureChest");
            ItemRoomRewardSystem.SpawnBodyRoomReward(new Vector3(0f, 0.55f, 0f), controller != null ? controller.ItemPickupTemplate : null);
        }
        else if (sceneName == "ShopScene")
        {
            SpecialRoomController controller = DisableLegacySpecialRoomInteraction();
            HideLegacyShopProps();
            ItemRoomRewardSystem.SpawnShop(Vector3.zero, controller != null ? controller.ItemPickupTemplate : null);
        }
        else if (sceneName == "ChallengeRewardScene")
        {
            SpecialRoomController controller = DisableLegacySpecialRoomInteraction();
            HideLegacyProps("TreasureChest");

            if (ThreadMazeChallengeManager.LastSucceeded)
            {
                // 성공: 트레저 테이블 유지 + 도전 보상 아이템 스폰 (+ 다음문은 SpecialRoomController가 생성)
                ItemRoomRewardSystem.SpawnChallengeReward(new Vector3(0f, 0.55f, 0f), controller != null ? controller.ItemPickupTemplate : null);
            }
            else
            {
                // 실패: 트레저 테이블/아이템 없이 다음문만. 테이블 시각물을 숨긴다.
                HideLegacyProps("TreasureTable");
            }
        }

        Destroy(gameObject);
    }

    SpecialRoomController DisableLegacySpecialRoomInteraction()
    {
        SpecialRoomController controller = FindFirstObjectByType<SpecialRoomController>();
        if (controller != null)
            controller.enabled = false;
        return controller;
    }

    static void HideLegacyProps(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        if (go != null)
            go.SetActive(false);
    }

    static void HideLegacyShopProps()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.name.StartsWith("ShopCounter_"))
                renderer.gameObject.SetActive(false);
        }
    }
}
