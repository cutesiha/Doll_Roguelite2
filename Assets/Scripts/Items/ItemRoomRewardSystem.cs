using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ItemRoomRewardSystem
{
    static readonly List<ItemData> selected = new();
    static readonly List<ItemData> matches = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

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
            ItemDropSpawner.Spawn(item, roomCenter, false, 0);
            Announce("도전방 보상이 생성되었습니다.");
            return;
        }

        if (pendingNode.roomType == RoomType.ConditionCombat)
        {
            ItemSystemSettings settings = ItemSystemSettings.Load();
            if (Random.value > settings.conditionRewardChance)
                return;

            ItemData item = ItemCatalog.RandomByAcquisition(ItemAcquisitionLocation.ConditionRoom);
            ItemDropSpawner.Spawn(item, roomCenter, false, 0);
            Announce("조건방 보상이 생성되었습니다.");
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
            ItemDropSpawner.Spawn(item, position + new Vector3(x, 0f, 0f), false, 0);
        }
        Announce("중간보스 아이템 2개가 생성되었습니다.");
    }

    public static void SpawnBodyRoomReward(Vector3 position)
    {
        ItemData item = ItemCatalog.RandomByCategory(ItemCategory.BodyRoom, ItemType.BodyPart);
        ItemDropSpawner.Spawn(item, position, false, 0);
        Announce("신체방 아이템이 생성되었습니다.");
    }

    public static void SpawnShop(Vector3 center)
    {
        ItemSystemSettings settings = ItemSystemSettings.Load();
        ItemData gem = ItemCatalog.RandomByType(ItemType.GemConsumable);
        ItemData rag = ItemCatalog.Find("rag");

        selected.Clear();
        ItemData bodyA = RandomUniqueBodyPart(selected);
        if (bodyA != null)
            selected.Add(bodyA);
        ItemData bodyB = RandomUniqueBodyPart(selected);

        ItemDropSpawner.Spawn(gem, center + new Vector3(-5.4f, 0.5f, 0f), true, settings.gemPrice);
        ItemDropSpawner.Spawn(rag, center + new Vector3(-1.8f, 0.5f, 0f), true, settings.ragPrice);
        ItemDropSpawner.Spawn(bodyA, center + new Vector3(1.8f, 0.5f, 0f), true, settings.bodyPartPrice);
        ItemDropSpawner.Spawn(bodyB, center + new Vector3(5.4f, 0.5f, 0f), true, settings.bodyPartPrice);
        Announce("상점 품목: 보석 1, 누더기 1, 신체 부위 2");
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
            if (item != null && item.Type == ItemType.BodyPart && (excluded == null || !excluded.Contains(item)))
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
            DisableLegacySpecialRoomInteraction();
            HideLegacyProps("TreasureChest");
            ItemRoomRewardSystem.SpawnBodyRoomReward(new Vector3(0f, 0.55f, 0f));
        }
        else if (sceneName == "ShopScene")
        {
            DisableLegacySpecialRoomInteraction();
            HideLegacyShopProps();
            ItemRoomRewardSystem.SpawnShop(Vector3.zero);
        }

        Destroy(gameObject);
    }

    void DisableLegacySpecialRoomInteraction()
    {
        SpecialRoomController controller = FindFirstObjectByType<SpecialRoomController>();
        if (controller != null)
            controller.enabled = false;
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
