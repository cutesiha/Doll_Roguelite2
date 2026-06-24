#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildSceneSetup
{
    static readonly string[] RequiredScenes =
    {
        "Assets/Scenes/StartScene.unity",
        "Assets/Scenes/MapScene.unity",
        "Assets/Scenes/RoomScene.unity",
        "Assets/Scenes/BossScene.unity",
        "Assets/Scenes/MiddleBossScene.unity",
        "Assets/Scenes/BookBossScene.unity",
        "Assets/Scenes/ShopScene.unity",
        "Assets/Scenes/TreasureRoomScene.unity",
        "Assets/Scenes/TutorialScene.unity",
    };

    [MenuItem("Tools/Build/Add All Scenes to Build Settings")]
    public static void AddAllScenes()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int added = 0;

        foreach (string path in RequiredScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning($"[BuildSceneSetup] Scene not found, skipping: {path}");
                continue;
            }

            bool alreadyAdded = false;
            foreach (var existing in scenes)
                if (existing.path == path) { alreadyAdded = true; break; }

            if (!alreadyAdded)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                added++;
                Debug.Log($"[BuildSceneSetup] Added to Build Settings: {path}");
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[BuildSceneSetup] Done. {added} scene(s) added.");
    }
}
#endif
