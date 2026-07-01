#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 아이템 테스트룸(itemtestroom)에 하이어라키로 배치된 각 아이템의 크기(localScale)를
// 해당 ItemData 에셋의 worldScale에 기록한다.
// 이렇게 하면 상점/보물/중간보스/도전방 등에서 런타임 스폰될 때도
// 테스트룸에서 맞춰둔 개별 크기로 동일하게 나타난다.
public static class ItemWorldScaleSync
{
    const string ItemTestRoomPath = "Assets/Scenes/itemtestroom.unity";

    [MenuItem("Tools/아이템 테스트룸/아이템 크기 동기화 (테스트룸 → ItemData)")]
    static void SyncFromItemTestRoom()
    {
        bool openedAdditively = false;
        Scene scene = FindLoadedScene(ItemTestRoomPath);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ItemTestRoomPath, OpenSceneMode.Additive);
            openedAdditively = true;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("오류", "itemtestroom 씬을 열 수 없습니다.", "확인");
            return;
        }

        int updated = 0;
        var seen = new HashSet<ItemData>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ItemWorldPickup[] pickups = root.GetComponentsInChildren<ItemWorldPickup>(true);
            foreach (ItemWorldPickup pickup in pickups)
            {
                if (pickup == null)
                    continue;

                SerializedObject so = new SerializedObject(pickup);
                SerializedProperty itemProp = so.FindProperty("itemAsset");
                ItemData item = itemProp != null ? itemProp.objectReferenceValue as ItemData : null;
                if (item == null || !seen.Add(item))
                    continue;

                float scale = pickup.transform.localScale.x;
                if (scale <= 0.0001f)
                    continue;

                item.EditorSetWorldScale(scale);
                EditorUtility.SetDirty(item);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();

        if (openedAdditively)
            EditorSceneManager.CloseScene(scene, true);

        Debug.Log($"[ItemWorldScaleSync] {updated}개 아이템의 worldScale을 테스트룸 기준으로 동기화했습니다.");
        EditorUtility.DisplayDialog("완료", $"{updated}개 아이템의 크기를 동기화했습니다.", "확인");
    }

    static Scene FindLoadedScene(string path)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == path)
                return scene;
        }

        return default;
    }
}
#endif
