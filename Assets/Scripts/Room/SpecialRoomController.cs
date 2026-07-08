using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum SpecialRoomKind
{
    Treasure,
    Shop
}

public class SpecialRoomController : MonoBehaviour
{
    [SerializeField] SpecialRoomKind roomKind = SpecialRoomKind.Treasure;
    [SerializeField] Vector2 mapSize = new Vector2(28.8f, 16.2f);
    [SerializeField] float cameraOrthographicSize = 5.4f;
    [SerializeField] float interactRadius = 1.8f;
    [SerializeField] Color floorColor = new Color(0.19f, 0.15f, 0.13f, 1f);
    [SerializeField] Color wallColor = new Color(0.42f, 0.30f, 0.22f, 1f);
    [SerializeField] Color treasureColor = new Color(1.00f, 0.72f, 0.16f, 1f);
    [SerializeField] Color shopColor = new Color(0.36f, 0.62f, 0.74f, 1f);
    [SerializeField] Vector2 nextDoorLine = new Vector2(8f, -2.45f);
    [SerializeField] Vector2 nextDoorSize = new Vector2(2.8f, 0.75f);
    [SerializeField] Sprite rectangleSprite;
    [Header("Authoring Templates")]
    [SerializeField] ItemWorldPickup itemPickupTemplate;
    [SerializeField] DoorTrigger nextDoorTemplate;

    Transform player;
    TextMeshPro promptText;
    TextMeshPro messageText;
    GameObject chestObject;
    readonly GameObject[] shopObjects = new GameObject[3];
    readonly List<DoorTrigger> nextDoors = new List<DoorTrigger>();
    bool treasureClaimed;
    bool shopChoiceUsed;
    Color backgroundColor;
    bool preservedAuthoredLayout;

    static Sprite squareSprite;

    public ItemWorldPickup ItemPickupTemplate => itemPickupTemplate != null ? itemPickupTemplate : FindTemplate<ItemWorldPickup>("ItemPickupTemplate");
    public DoorTrigger NextDoorTemplate => nextDoorTemplate != null ? nextDoorTemplate : FindTemplate<DoorTrigger>("NextDoorTemplate");

    void Start()
    {
        MapRunState.EnsureRun();
        CompletePendingRoomIfNeeded();
        ResolveAuthoringTemplates();
        BuildRoomVisuals();
        SetupPlayerAndCamera();
        UpdatePrompt();
    }

    void ResolveAuthoringTemplates()
    {
        if (itemPickupTemplate == null)
            itemPickupTemplate = FindTemplate<ItemWorldPickup>("ItemPickupTemplate");
        if (nextDoorTemplate == null)
            nextDoorTemplate = FindTemplate<DoorTrigger>("NextDoorTemplate");
    }

    T FindTemplate<T>(string preferredName) where T : Component
    {
        T[] candidates = GetComponentsInChildren<T>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate != null && candidate.name == preferredName)
                return candidate;
        }

        return null;
    }

    void Update()
    {
        ResolvePlayer();
        UpdatePrompt();

        if (!WasInteractPressed())
            return;

        if (player == null)
            return;

        if (roomKind == SpecialRoomKind.Treasure)
        {
            if (!treasureClaimed && IsNear(chestObject))
                ClaimTreasure();
        }
        else
        {
            int option = ClosestShopOption();
            if (!shopChoiceUsed && option >= 0)
                ChooseShopOption(option);
        }
    }

    void BuildRoomVisuals()
    {
        Transform oldArt = transform.Find("SpecialRoomArt");
        backgroundColor = roomKind == SpecialRoomKind.Treasure
            ? new Color(0.22f, 0.16f, 0.10f, 1f)
            : new Color(0.13f, 0.18f, 0.20f, 1f);

        if (ShouldPreserveAuthoredLayout(oldArt))
        {
            preservedAuthoredLayout = true;
            EnsureSpecialRoomWallColliders(oldArt);
            ResolveAuthoredRoomReferences(oldArt);
            BuildNextDoors(oldArt);

            if (promptText == null)
                promptText = CreateWorldText(oldArt, "InteractionPrompt", "", new Vector2(0f, -mapSize.y * 0.5f + 1.2f), 0.62f, new Color(1f, 0.90f, 0.68f, 1f), 40);
            if (messageText == null)
                messageText = CreateWorldText(oldArt, "RoomMessage", "", new Vector2(0f, -mapSize.y * 0.5f + 2.0f), 0.58f, new Color(1f, 0.86f, 0.48f, 1f), 40);
            return;
        }

        SpriteRenderer authoredBackground = FindAuthoredBackground(oldArt);
        if (authoredBackground != null)
            authoredBackground.transform.SetParent(transform, true);

        if (oldArt != null)
            Destroy(oldArt.gameObject);

        GameObject art = new GameObject("SpecialRoomArt");
        art.transform.SetParent(transform, false);

        if (authoredBackground != null)
            authoredBackground.transform.SetParent(art.transform, true);

        CreateRect(art.transform, "Floor_28_8x16_2", Vector2.zero, mapSize, backgroundColor, -40);
        CreateRect(art.transform, "Wall_Top", new Vector2(0f, mapSize.y * 0.5f), new Vector2(mapSize.x, 0.5f), wallColor, -35);
        CreateRect(art.transform, "Wall_Bottom", new Vector2(0f, -mapSize.y * 0.5f), new Vector2(mapSize.x, 0.5f), wallColor, -35);
        CreateRect(art.transform, "Wall_Left", new Vector2(-mapSize.x * 0.5f, 0f), new Vector2(0.5f, mapSize.y), wallColor, -35);
        CreateRect(art.transform, "Wall_Right", new Vector2(mapSize.x * 0.5f, 0f), new Vector2(0.5f, mapSize.y), wallColor, -35);

        string title = roomKind == SpecialRoomKind.Treasure ? "보물방" : "상점";
        CreateWorldText(art.transform, "RoomTitle", title, new Vector2(0f, mapSize.y * 0.5f - 1.25f), 1.1f, Color.white, 25);

        if (roomKind == SpecialRoomKind.Treasure)
            BuildTreasureProps(art.transform);
        else
            BuildShopProps(art.transform);

        BuildNextDoors(art.transform);

        promptText = CreateWorldText(art.transform, "InteractionPrompt", "", new Vector2(0f, -mapSize.y * 0.5f + 1.2f), 0.62f, new Color(1f, 0.90f, 0.68f, 1f), 40);
        messageText = CreateWorldText(art.transform, "RoomMessage", "", new Vector2(0f, -mapSize.y * 0.5f + 2.0f), 0.58f, new Color(1f, 0.86f, 0.48f, 1f), 40);
    }

    SpriteRenderer FindAuthoredBackground(Transform artRoot)
    {
        if (artRoot == null)
            return null;

        Sprite generatedSprite = RoomSprite();
        SpriteRenderer[] renderers = artRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null && renderer.sprite != null && renderer.sprite != generatedSprite)
                return renderer;
        }

        return null;
    }

    // ShopScene/TreasureRoomScene/ChallengeRewardScene 모두 에디터에서 손으로 배치한
    // "SpecialRoomArt" 레이아웃을 갖고 있다. 씬 이름으로 챌린지보상방만 골라내던 이전 방식은
    // 나머지 두 씬의 손으로 배치한 벽/카메라 배치를 매번 파괴하고 코드로 재생성했었다
    // (카메라가 고정되지 않고 플레이어를 따라가며, 플레이어 위치도 강제로 리셋되는 원인).
    // 손으로 배치한 아트가 있으면 어떤 씬이든 그대로 보존한다.
    bool ShouldPreserveAuthoredLayout(Transform artRoot)
    {
        return artRoot != null;
    }

    void ResolveAuthoredRoomReferences(Transform artRoot)
    {
        if (roomKind == SpecialRoomKind.Treasure)
            chestObject = FindChildGameObject(artRoot, "TreasureChest");

        promptText = FindChildText(artRoot, "InteractionPrompt");
        messageText = FindChildText(artRoot, "RoomMessage");
    }

    static GameObject FindChildGameObject(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i] != null && children[i].name == objectName)
                return children[i].gameObject;

        return null;
    }

    static TextMeshPro FindChildText(Transform root, string objectName)
    {
        GameObject go = FindChildGameObject(root, objectName);
        return go != null ? go.GetComponent<TextMeshPro>() : null;
    }

    void BuildTreasureProps(Transform parent)
    {
        chestObject = CreateRect(parent, "TreasureChest", new Vector2(0f, 0.55f), new Vector2(2.4f, 1.25f), treasureColor, 8);
        CreateRect(chestObject.transform, "ChestLid", new Vector2(0f, 0.46f), new Vector2(2.6f, 0.35f), new Color(0.72f, 0.38f, 0.10f, 1f), 9);
        CreateRect(chestObject.transform, "ChestLock", new Vector2(0f, 0.0f), new Vector2(0.32f, 0.42f), new Color(0.12f, 0.08f, 0.06f, 1f), 10);
        CreateWorldText(chestObject.transform, "ChestLabel", "OPEN", new Vector2(0f, -1.0f), 0.42f, Color.white, 20);
    }

    void BuildShopProps(Transform parent)
    {
        BuildShopOption(parent, 0, new Vector2(-5.0f, 0.45f), "치료", new Color(0.55f, 0.84f, 0.58f, 1f));
        BuildShopOption(parent, 1, new Vector2(0.0f, 0.45f), "수리", new Color(0.88f, 0.66f, 0.30f, 1f));
        BuildShopOption(parent, 2, new Vector2(5.0f, 0.45f), "부위", new Color(0.65f, 0.50f, 0.88f, 1f));
    }

    void BuildShopOption(Transform parent, int index, Vector2 position, string label, Color color)
    {
        GameObject option = CreateRect(parent, "ShopCounter_" + label, position, new Vector2(3.1f, 1.2f), shopColor, 8);
        CreateRect(option.transform, "ShopItem_" + label, new Vector2(0f, 0.55f), new Vector2(1.15f, 0.75f), color, 12);
        CreateWorldText(option.transform, "Label", label, new Vector2(0f, -1.05f), 0.48f, Color.white, 20);
        shopObjects[index] = option;
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
            GameObject door = CreateNextDoorObject(parent, child, i, current.children.Count);

            BoxCollider2D collider = door.GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = door.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            DoorTrigger trigger = door.GetComponent<DoorTrigger>();
            if (trigger == null)
                trigger = door.AddComponent<DoorTrigger>();
            trigger.CopyPresentationFrom(NextDoorTemplate);
            trigger.Configure(child, true);
            nextDoors.Add(trigger);
        }
    }

    GameObject CreateNextDoorObject(Transform parent, MapNode child, int index, int count)
    {
        Vector2 position = NextDoorPosition(index, count);
        DoorTrigger template = NextDoorTemplate;
        if (template == null)
        {
            return CreateRect(
                parent,
                "NextDoor_ToNode_" + child.id,
                position,
                nextDoorSize,
                new Color(0.85f, 0.62f, 0.25f, 1f),
                14);
        }

        GameObject door = Instantiate(template.gameObject, parent);
        door.name = "NextDoor_ToNode_" + child.id;
        door.transform.localPosition = new Vector3(position.x, position.y, 0f);
        door.transform.localRotation = Quaternion.identity;
        door.transform.localScale = Vector3.one;
        door.SetActive(true);
        return door;
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
        renderer.sprite = RoomSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (IsWallObjectName(objectName))
            EnsureWallCollider2D(go);

        return go;
    }

    void EnsureSpecialRoomWallColliders(Transform artRoot)
    {
        if (artRoot == null)
            return;

        Transform[] children = artRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i] != null && IsWallObjectName(children[i].name))
                EnsureWallCollider2D(children[i].gameObject);
    }

    static bool IsWallObjectName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && (objectName.StartsWith("Wall_") || objectName.StartsWith("wall"));
    }

    static void EnsureWallCollider2D(GameObject wall)
    {
        if (wall == null)
            return;

        BoxCollider2D collider2D = wall.GetComponent<BoxCollider2D>();
        if (collider2D == null)
            collider2D = wall.AddComponent<BoxCollider2D>();

        BoxCollider collider3D = wall.GetComponent<BoxCollider>();
        if (collider3D != null)
        {
            collider2D.offset = new Vector2(collider3D.center.x, collider3D.center.y);
            collider2D.size = new Vector2(Mathf.Max(0.01f, collider3D.size.x), Mathf.Max(0.01f, collider3D.size.y));
            collider3D.enabled = false;
        }

        collider2D.isTrigger = false;
        collider2D.enabled = true;
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

    void SetupPlayerAndCamera()
    {
        ResolvePlayer();
        if (preservedAuthoredLayout)
        {
            // 손으로 배치한 방은 카메라도 에디터에서 방 전체가 보이도록 고정해 둔 것이므로
            // 플레이어를 따라다니게 하지 않고, 플레이어 위치도 강제로 리셋하지 않는다
            // (에디터에 배치해 둔 시작 위치를 그대로 사용).
            Camera authoredCamera = Camera.main;
            if (authoredCamera != null)
            {
                PlayerCameraFollow authoredFollow = authoredCamera.GetComponent<PlayerCameraFollow>();
                if (authoredFollow != null)
                    authoredFollow.enabled = false;
            }

            return;
        }

        if (player != null)
            player.position = new Vector3(0f, -mapSize.y * 0.5f + 2.35f, 0f);

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = cameraOrthographicSize;

        PlayerCameraFollow follow = mainCamera.GetComponent<PlayerCameraFollow>();
        if (follow != null)
            follow.ConfigureBounds(mapSize, Vector2.zero, cameraOrthographicSize, true);
        else
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = backgroundColor;
    }

    void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    bool WasInteractPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.eKey.wasPressedThisFrame;
    }

    bool IsNear(GameObject target)
    {
        return player != null
            && target != null
            && Vector2.Distance(player.position, target.transform.position) <= interactRadius;
    }

    int ClosestShopOption()
    {
        if (player == null || shopChoiceUsed)
            return -1;

        int best = -1;
        float bestDistance = interactRadius;
        for (int i = 0; i < shopObjects.Length; i++)
        {
            if (shopObjects[i] == null)
                continue;

            float distance = Vector2.Distance(player.position, shopObjects[i].transform.position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    void UpdatePrompt()
    {
        if (promptText == null)
            return;

        string prompt = "";
        if (roomKind == SpecialRoomKind.Treasure && !treasureClaimed && IsNear(chestObject))
            prompt = "[E] 상자 열기";
        else if (roomKind == SpecialRoomKind.Shop && !shopChoiceUsed)
        {
            int option = ClosestShopOption();
            if (option == 0) prompt = "[E] 체력을 회복하세요";
            else if (option == 1) prompt = "[E] 모든 부위를 수리하세요";
            else if (option == 2) prompt = "[E] 랜덤 부위를 받으세요";
        }

        promptText.text = prompt;
    }

    void ClaimTreasure()
    {
        treasureClaimed = true;
        string result = GrantRandomBodyPartOrRepair();
        if (messageText != null)
            messageText.text = "상자에서 " + result + "!";

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText("보물방: " + result);

        if (chestObject != null)
        {
            SpriteRenderer renderer = chestObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.color = new Color(0.44f, 0.28f, 0.14f, 1f);
        }
    }

    void ChooseShopOption(int option)
    {
        shopChoiceUsed = true;
        string result;
        if (option == 0)
        {
            InventoryManager.Instance?.RepairAllParts();
            result = "체력을 회복했습니다";
        }
        else if (option == 1)
        {
            int repaired = InventoryManager.Instance != null ? InventoryManager.Instance.RepairAllParts() : 0;
            result = repaired > 0 ? "모든 부위를 수리했습니다" : "수리할 부위가 없습니다";
        }
        else
        {
            result = GrantRandomBodyPartOrRepair();
        }

        if (messageText != null)
            messageText.text = result;

        RunHudUI hud = FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText("상점: " + result);
    }

    string GrantRandomBodyPartOrRepair()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
            return "아무 일도 일어나지 않았습니다";

        BodySlot slot = (BodySlot)Random.Range(0, System.Enum.GetValues(typeof(BodySlot)).Length);
        BodyPart part = new BodyPart(slot)
        {
            maxHp = 120,
            currentHp = 120
        };

        if (inventory.TryAddPart(part, true))
            return part.SlotName() + "을(를) 얻었습니다";

        int repaired = inventory.RepairAllParts();
        return repaired > 0 ? "보유 부위를 수리했습니다" : "보관함이 가득합니다";
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

    Sprite RoomSprite()
    {
        return rectangleSprite != null ? rectangleSprite : SquareSprite();
    }
}
