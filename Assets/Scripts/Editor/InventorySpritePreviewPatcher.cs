using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InventorySpritePreviewPatcher
{
    const string ResourcesPrefabPath = "Assets/Resources/InventoryCanvas.prefab";
    const string UiPrefabPath = "Assets/Prefabs/UI/InventoryCanvas.prefab";

    [MenuItem("Game/Inventory/Assign Character Preview Sprites")]
    public static void AssignCharacterPreviewSprites()
    {
        AssignCharacterPreviewSprites(ResourcesPrefabPath);
        AssignCharacterPreviewSprites(UiPrefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[InventoryUI] Character preview sprites assigned.");
    }

    static void AssignCharacterPreviewSprites(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SetImage(root.transform, "BodyBaseImage", "Assets/TextMesh Pro/Sprites/Player/body.png", "제목 없는 디자인 (9)_1");
            SetImage(root.transform, "FaceBaseImage", "Assets/TextMesh Pro/Sprites/Player/head.png", "pixil-frame-0 (4)_0");
            SetImage(root.transform, "EquipPart_EyeLeft", "Assets/TextMesh Pro/Sprites/Player/eye_left.png", null);
            SetImage(root.transform, "EquipPart_EyeRight", "Assets/TextMesh Pro/Sprites/Player/eye_right.png", null);
            SetImage(root.transform, "EquipPart_ArmLeft", "Assets/TextMesh Pro/Sprites/Player/arm_left.png", null);
            SetImage(root.transform, "EquipPart_ArmRight", "Assets/TextMesh Pro/Sprites/Player/arm_right.png", null);
            SetImage(root.transform, "EquipPart_LegLeft", "Assets/TextMesh Pro/Sprites/Player/leg_left.png", null);
            SetImage(root.transform, "EquipPart_LegRight", "Assets/TextMesh Pro/Sprites/Player/leg_right.png", null);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetImage(Transform root, string objectName, string spritePath, string spriteName)
    {
        Transform child = FindChildRecursive(root, objectName);
        if (child == null)
            return;

        Image image = child.GetComponent<Image>();
        Sprite sprite = LoadSprite(spritePath, spriteName);
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        EditorUtility.SetDirty(image);
    }

    static Sprite LoadSprite(string path, string spriteName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null && string.IsNullOrEmpty(spriteName))
            return sprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Sprite largest = null;
        float largestArea = -1f;

        foreach (Object asset in assets)
        {
            if (asset is Sprite subSprite)
            {
                if (!string.IsNullOrEmpty(spriteName) && subSprite.name == spriteName)
                    return subSprite;

                float area = subSprite.rect.width * subSprite.rect.height;
                if (area > largestArea)
                {
                    largest = subSprite;
                    largestArea = area;
                }
            }
        }

        return largest;
    }

    static Transform FindChildRecursive(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
                return child;

            Transform found = FindChildRecursive(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
