using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSaveSystem
{
    const int SlotCount = 4;
    const string KeyPrefix = "DollRoguelite.SaveSlot.";
    const string TutorialDoneKey = "DollRoguelite.TutorialDone";

    public static void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(TutorialDoneKey, 1);
        PlayerPrefs.Save();
    }

    public static bool HasCompletedTutorial()
    {
        return PlayerPrefs.GetInt(TutorialDoneKey, 0) != 0;
    }

    [Serializable]
    public class SlotInfo
    {
        public bool exists;
        public string saveName;
        public string savedAt;
        public string sceneName;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string saveName;
        public string savedAt;
        public string sceneName;
        public InventorySaveData inventory;
        public MapRunState.SaveData map;
    }

    [Serializable]
    public class InventorySaveData
    {
        public BodyPartSaveData[] equipped;
        public BodyPartSaveData[] storage;
        public bool[] lockedSlots;
    }

    [Serializable]
    public class BodyPartSaveData
    {
        public bool hasPart;
        public BodySlot slot;
        public int maxHp;
        public int currentHp;
    }

    public static int MaxSlots => SlotCount;

    public static string SlotName(int slotIndex)
    {
        return "Save" + Mathf.Clamp(slotIndex + 1, 1, SlotCount).ToString("00");
    }

    public static SlotInfo GetSlotInfo(int slotIndex)
    {
        SaveData data;
        if (!TryRead(slotIndex, out data))
            return new SlotInfo { exists = false, saveName = SlotName(slotIndex), savedAt = "", sceneName = "" };

        return new SlotInfo
        {
            exists = true,
            saveName = string.IsNullOrEmpty(data.saveName) ? SlotName(slotIndex) : data.saveName,
            savedAt = data.savedAt,
            sceneName = data.sceneName
        };
    }

    public static bool HasSave(int slotIndex)
    {
        return PlayerPrefs.HasKey(Key(slotIndex));
    }

    public static SlotInfo SaveSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);

        SaveData data = new SaveData
        {
            saveName = SlotName(slotIndex),
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            sceneName = SceneManager.GetActiveScene().name,
            inventory = CaptureInventory(),
            map = MapRunState.Capture()
        };

        PlayerPrefs.SetString(Key(slotIndex), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        return GetSlotInfo(slotIndex);
    }

    public static bool LoadSlotAndEnter(int slotIndex)
    {
        SaveData data;
        if (!TryRead(slotIndex, out data))
            return false;

        Time.timeScale = 1f;
        Apply(data);
        string sceneName = string.IsNullOrEmpty(data.sceneName) ? "RoomScene" : data.sceneName;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public static void StartNewRun()
    {
        Time.timeScale = 1f;
        MapRunState.ResetRun();

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
            inventory.ResetToDefault();

        ItemInventoryManager.Instance?.EnsureDefaultBodyPartsEquipped();
    }

    static void Apply(SaveData data)
    {
        if (data == null)
            return;

        MapRunState.Restore(data.map);

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            InventorySaveData inventoryData = data.inventory;
            inventory.ReplaceState(
                RestoreParts(inventoryData != null ? inventoryData.equipped : null, InventoryManager.BodySlotCount),
                RestoreParts(inventoryData != null ? inventoryData.storage : null, InventoryManager.StorageSlotCount),
                inventoryData != null ? inventoryData.lockedSlots : null);
        }
    }

    static InventorySaveData CaptureInventory()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return new InventorySaveData { equipped = new BodyPartSaveData[0], storage = new BodyPartSaveData[0] };

        return new InventorySaveData
        {
            equipped = CaptureParts(inventory.equipped),
            storage = CaptureParts(inventory.storage),
            lockedSlots = inventory.GetLockedSlotsSnapshot()
        };
    }

    static BodyPartSaveData[] CaptureParts(BodyPart[] parts)
    {
        if (parts == null)
            return new BodyPartSaveData[0];

        BodyPartSaveData[] result = new BodyPartSaveData[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            BodyPart part = parts[i];
            result[i] = new BodyPartSaveData
            {
                hasPart = part != null,
                slot = part != null ? part.slot : BodySlot.EyeLeft,
                maxHp = part != null ? part.maxHp : 100,
                currentHp = part != null ? part.currentHp : 100
            };
        }

        return result;
    }

    static BodyPart[] RestoreParts(BodyPartSaveData[] parts, int length)
    {
        BodyPart[] result = new BodyPart[length];
        if (parts == null)
            return result;

        int count = Mathf.Min(parts.Length, length);
        for (int i = 0; i < count; i++)
        {
            BodyPartSaveData saved = parts[i];
            if (saved == null || !saved.hasPart)
                continue;

            BodyPart part = new BodyPart(saved.slot)
            {
                maxHp = Mathf.Max(1, saved.maxHp),
                currentHp = Mathf.Clamp(saved.currentHp, 0, Mathf.Max(1, saved.maxHp))
            };
            result[i] = part;
        }

        return result;
    }

    static bool TryRead(int slotIndex, out SaveData data)
    {
        data = null;
        slotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
        string json = PlayerPrefs.GetString(Key(slotIndex), "");
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception)
        {
            data = null;
        }

        return data != null;
    }

    static string Key(int slotIndex)
    {
        return KeyPrefix + Mathf.Clamp(slotIndex, 0, SlotCount - 1);
    }
}
