using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BossRoom : MonoBehaviour
{
    [SerializeField] Vector3 playerSpawnPos = new Vector3(0f, -3f, 0f);
    [SerializeField] Vector2 roomSize = new Vector2(28.8f, 16.2f);
    [SerializeField] Vector2 nextDoorLine = new Vector2(8f, -2.25f);
    [SerializeField] Vector2 nextDoorSize = new Vector2(2.8f, 0.75f);
    [SerializeField] Color floorColor = new Color(0.14f, 0.11f, 0.13f, 1f);
    [SerializeField] Color wallColor = new Color(0.32f, 0.22f, 0.25f, 1f);
    [SerializeField] Color doorColor = new Color(0.85f, 0.62f, 0.25f, 1f);
    [SerializeField] GameObject[] doors;

    readonly List<DoorTrigger> nextDoors = new List<DoorTrigger>();
    static Sprite squareSprite;

    void Start()
    {
        MapRunState.EnsureRun();
        CompletePendingRoomIfNeeded();
        HideLegacyDoors();
        SetupPlayer();
        BuildRoom();
    }

    void HideLegacyDoors()
    {
        if (doors == null)
            return;

        for (int i = 0; i < doors.Length; i++)
            if (doors[i] != null)
                doors[i].SetActive(false);
    }

    void SetupPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            playerObj.transform.position = playerSpawnPos;
    }

    void BuildRoom()
    {
        Transform oldArt = transform.Find("BossRoomArt");
        if (oldArt != null)
            Destroy(oldArt.gameObject);

        GameObject art = new GameObject("BossRoomArt");
        art.transform.SetParent(transform, false);

        CreateRect(art.transform, "Floor", Vector2.zero, roomSize, floorColor, -40);
        CreateRect(art.transform, "Wall_Top", new Vector2(0f, roomSize.y * 0.5f), new Vector2(roomSize.x, 0.5f), wallColor, -35);
        CreateRect(art.transform, "Wall_Bottom", new Vector2(0f, -roomSize.y * 0.5f), new Vector2(roomSize.x, 0.5f), wallColor, -35);
        CreateRect(art.transform, "Wall_Left", new Vector2(-roomSize.x * 0.5f, 0f), new Vector2(0.5f, roomSize.y), wallColor, -35);
        CreateRect(art.transform, "Wall_Right", new Vector2(roomSize.x * 0.5f, 0f), new Vector2(0.5f, roomSize.y), wallColor, -35);

        CreateWorldText(art.transform, "BossRoomTitle", "중간보스 방입니다", new Vector2(0f, 1.2f), 1.05f, Color.white, 30);
        BuildNextDoors(art.transform);
    }

    void BuildNextDoors(Transform parent)
    {
        nextDoors.Clear();

        MapNode current = MapRunState.CurrentNode;
        if (current == null || current.children == null || current.children.Count == 0)
        {
            CreateWorldText(parent, "NoNextNodeLabel", "다음 노드가 없습니다", new Vector2(0f, nextDoorLine.y), 0.52f, Color.white, 30);
            return;
        }

        for (int i = 0; i < current.children.Count; i++)
        {
            MapNode child = current.children[i];
            GameObject door = CreateRect(
                parent,
                "NextDoor_ToNode_" + child.id,
                NextDoorPosition(i, current.children.Count),
                nextDoorSize,
                doorColor,
                14);

            BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            DoorTrigger trigger = door.AddComponent<DoorTrigger>();
            trigger.Configure(child, true);
            nextDoors.Add(trigger);
        }
    }

    Vector2 NextDoorPosition(int index, int count)
    {
        float x = count <= 1
            ? 0f
            : Mathf.Lerp(-nextDoorLine.x * 0.5f, nextDoorLine.x * 0.5f, index / (float)(count - 1));
        return new Vector2(x, nextDoorLine.y);
    }

    GameObject CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return go;
    }

    TextMeshPro CreateWorldText(Transform parent, string objectName, string text, Vector2 position, float fontSize, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(position.x, position.y, -0.1f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.font = UIThinDungFont.Get();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.sortingOrder = sortingOrder;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 1.2f);
        return tmp;
    }

    void CompletePendingRoomIfNeeded()
    {
        if (MapRunState.PendingNode != null)
            MapRunState.CompletePendingRoom();
    }

    static Sprite SquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }
}
