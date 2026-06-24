using System.Collections.Generic;
using UnityEngine;

public static class ItemCatalog
{
    static ItemData[] cachedItems;
    static readonly List<ItemData> matches = new();

    public static IReadOnlyList<ItemData> All
    {
        get
        {
            EnsureLoaded();
            return cachedItems;
        }
    }

    public static void Reload()
    {
        cachedItems = null;
        EnsureLoaded();
    }

    public static ItemData Find(string itemId)
    {
        EnsureLoaded();
        for (int i = 0; i < cachedItems.Length; i++)
            if (cachedItems[i] != null && cachedItems[i].ItemId == itemId)
                return cachedItems[i];

        return null;
    }

    public static ItemData RandomByCategory(ItemCategory category, ItemType? type = null)
    {
        EnsureLoaded();
        matches.Clear();
        for (int i = 0; i < cachedItems.Length; i++)
        {
            ItemData item = cachedItems[i];
            if (item == null || item.Category != category)
                continue;
            if (type.HasValue && item.Type != type.Value)
                continue;
            matches.Add(item);
        }

        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    public static ItemData RandomByType(ItemType type, ICollection<ItemData> excluded = null)
    {
        EnsureLoaded();
        matches.Clear();
        for (int i = 0; i < cachedItems.Length; i++)
        {
            ItemData item = cachedItems[i];
            if (item == null || item.Type != type || (excluded != null && excluded.Contains(item)))
                continue;
            matches.Add(item);
        }

        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    public static ItemData RandomByAcquisition(ItemAcquisitionLocation location, ItemType? type = null)
    {
        EnsureLoaded();
        matches.Clear();
        for (int i = 0; i < cachedItems.Length; i++)
        {
            ItemData item = cachedItems[i];
            if (item == null || (item.AcquisitionLocations & location) == 0)
                continue;
            if (type.HasValue && item.Type != type.Value)
                continue;
            matches.Add(item);
        }

        return matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : null;
    }

    static void EnsureLoaded()
    {
        if (cachedItems != null)
            return;

        cachedItems = Resources.LoadAll<ItemData>("Items");
        System.Array.Sort(cachedItems, (left, right) =>
            string.Compare(left != null ? left.ItemId : "", right != null ? right.ItemId : "", System.StringComparison.Ordinal));
    }
}
