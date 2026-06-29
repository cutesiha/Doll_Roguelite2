#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ItemTestRoomPopulator
{
    const string BackgroundSpritePath = "Assets/Sprites/room/room_fullhd.png";
    const string FallbackBgPath = "Assets/Sprites/room/room1back.png";
    const string AuthoredRootName = "ItemTestRoom_Authored";
    const string BgObjectName = "ItemTestRoom_Background";
    static readonly Vector2 StartPos = new Vector2(-12f, 5f);
    static readonly Vector2 Spacing = new Vector2(3.7f, 2.35f);
    const int Columns = 7;
    const float LabelFontSize = 0.34f;

    [MenuItem("Tools/아이템 테스트룸/하이어라키에 배치 _%#i")]
    static void Populate()
    {
        ItemTestRoomSpawner spawner = Object.FindFirstObjectByType<ItemTestRoomSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 ItemTestRoomSpawner가 없습니다.\n아이템 테스트룸 씬을 열어주세요.", "확인");
            return;
        }

        Undo.SetCurrentGroupName("아이템 테스트룸 하이어라키 배치");
        int undoGroup = Undo.GetCurrentGroup();

        // 기존 authored 루트 제거
        Transform oldAuthored = spawner.transform.Find(AuthoredRootName);
        if (oldAuthored != null)
            Undo.DestroyObjectImmediate(oldAuthored.gameObject);

        // 기존 런타임 루트 제거
        Transform oldRuntime = spawner.transform.Find("ItemTestRoom_AllItems");
        if (oldRuntime != null)
            Undo.DestroyObjectImmediate(oldRuntime.gameObject);

        // 기존 배경 제거
        foreach (var root in spawner.gameObject.scene.GetRootGameObjects())
        {
            if (root.name == BgObjectName)
            {
                Undo.DestroyObjectImmediate(root);
                break;
            }
        }

        // 배경 생성
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath)
                        ?? AssetDatabase.LoadAssetAtPath<Sprite>(FallbackBgPath);

        GameObject bg = new GameObject(BgObjectName);
        Undo.RegisterCreatedObjectUndo(bg, "배경 생성");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(bg, spawner.gameObject.scene);
        bg.transform.position = new Vector3(0f, 0f, 1f);
        SpriteRenderer bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = bgSprite;
        bgSr.sortingOrder = -10;

        // Authored 루트 생성
        GameObject rootGo = new GameObject(AuthoredRootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "아이템 루트 생성");
        rootGo.transform.SetParent(spawner.transform, false);

        // 아이템 배치
        ItemData[] items = Resources.LoadAll<ItemData>("Items");
        System.Array.Sort(items, (a, b) =>
            string.Compare(a != null ? a.ItemId : "", b != null ? b.ItemId : "", System.StringComparison.Ordinal));

        int index = 0;
        foreach (ItemData item in items)
        {
            if (item == null)
                continue;

            int row = index / Columns;
            int col = index % Columns;
            Vector3 pos = new Vector3(
                StartPos.x + col * Spacing.x,
                StartPos.y - row * Spacing.y,
                0f);

            PlaceItem(rootGo.transform, item, pos);
            PlaceLabel(rootGo.transform, item, pos + new Vector3(0f, -0.95f, 0f));
            index++;
        }

        // Spawner에 authored 플래그 설정
        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("authoredInHierarchy").boolValue = true;
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

        Debug.Log($"[ItemTestRoom] 아이템 {index}개 하이어라키 배치 완료");
        EditorUtility.DisplayDialog("완료", $"아이템 {index}개를 하이어라키에 배치했습니다.\n씬을 저장하세요 (Ctrl+S).", "확인");
    }

    [MenuItem("Tools/아이템 테스트룸/런타임 스폰으로 되돌리기")]
    static void RevertToRuntime()
    {
        ItemTestRoomSpawner spawner = Object.FindFirstObjectByType<ItemTestRoomSpawner>();
        if (spawner == null)
            return;

        Transform authored = spawner.transform.Find(AuthoredRootName);
        if (authored != null)
            Undo.DestroyObjectImmediate(authored.gameObject);

        foreach (var root in spawner.gameObject.scene.GetRootGameObjects())
        {
            if (root.name == BgObjectName)
            {
                Undo.DestroyObjectImmediate(root);
                break;
            }
        }

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("authoredInHierarchy").boolValue = false;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        Debug.Log("[ItemTestRoom] 런타임 스폰 모드로 복원");
    }

    static void PlaceItem(Transform parent, ItemData item, Vector3 pos)
    {
        GameObject go = new GameObject("Item_" + item.ItemId);
        Undo.RegisterCreatedObjectUndo(go, "아이템 배치");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        float size = item.Type == ItemType.BodyPart ? 0.95f : 0.72f;
        go.transform.localScale = Vector3.one * size;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = item.Sprite;
        sr.sortingOrder = 35;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.62f;

        ItemWorldPickup pickup = go.AddComponent<ItemWorldPickup>();
        SerializedObject pso = new SerializedObject(pickup);
        pso.FindProperty("itemAsset").objectReferenceValue = item;
        pso.ApplyModifiedPropertiesWithoutUndo();
    }

    static void PlaceLabel(Transform parent, ItemData item, Vector3 pos)
    {
        GameObject go = new GameObject("Label_" + item.ItemId);
        Undo.RegisterCreatedObjectUndo(go, "레이블 배치");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.font = UIThinDungFont.Get();
        tmp.fontSize = LabelFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.sortingOrder = 80;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.rectTransform.sizeDelta = new Vector2(3.2f, 0.85f);
        tmp.text = item.ItemId + "\n" + item.ItemName;
    }
}
#endif
