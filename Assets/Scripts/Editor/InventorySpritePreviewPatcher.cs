using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InventorySpritePreviewPatcher
{
    const string ResourcesPrefabPath = "Assets/Resources/InventoryCanvas.prefab";
    const string UiPrefabPath = "Assets/Prefabs/UI/InventoryCanvas.prefab";
    const string RunHudPrefabPath = "Assets/Prefabs/UI/RunHudCanvas.prefab";

    [MenuItem("Game/Inventory/Assign Character Preview Sprites")]
    public static void AssignCharacterPreviewSprites()
    {
        AssignCharacterPreviewSprites(ResourcesPrefabPath);
        AssignCharacterPreviewSprites(UiPrefabPath);
        AssignCharacterPreviewSprites(RunHudPrefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[InventoryUI] Character preview sprites assigned.");
    }

    static void AssignCharacterPreviewSprites(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SetImage(root.transform, "BodyBaseImage", "Assets/TextMesh Pro/Sprites/Player/body.png", null, false);
            SetImage(root.transform, "FaceBaseImage", "Assets/TextMesh Pro/Sprites/Player/head.png", null, false);
            SetImage(root.transform, "EquipPart_EyeLeft", "Assets/TextMesh Pro/Sprites/Player/eye_left.png", null, true);
            SetImage(root.transform, "EquipPart_EyeRight", "Assets/TextMesh Pro/Sprites/Player/eye_right.png", null, true);
            SetImage(root.transform, "EquipPart_ArmLeft", "Assets/TextMesh Pro/Sprites/Player/arm_left.png", null, true);
            SetImage(root.transform, "EquipPart_ArmRight", "Assets/TextMesh Pro/Sprites/Player/arm_right.png", null, true);
            SetImage(root.transform, "EquipPart_LegLeft", "Assets/TextMesh Pro/Sprites/Player/leg_left.png", null, true);
            SetImage(root.transform, "EquipPart_LegRight", "Assets/TextMesh Pro/Sprites/Player/leg_right.png", null, true);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetImage(Transform root, string objectName, string spritePath, string spriteName, bool raycastTarget)
    {
        Transform child = FindChildRecursive(root, objectName);
        if (child == null)
            return;

        Image image = child.GetComponent<Image>();
        Sprite sprite = LoadSprite(spritePath, spriteName);
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = raycastTarget;
        EditorUtility.SetDirty(image);

        Button button = child.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.63f, 0.52f, 1f);
            colors.pressedColor = new Color(0.56f, 0.46f, 0.36f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.50f, 0.45f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            EditorUtility.SetDirty(button);
        }
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
