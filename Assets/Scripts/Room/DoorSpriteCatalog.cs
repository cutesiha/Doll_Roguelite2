using UnityEngine;

public sealed class DoorSpriteCatalog : ScriptableObject
{
    public Sprite enemyRoomDoor;
    public Sprite treasureDoor;
    public Sprite shopDoor;
    public Sprite challengeDoor;
    public Sprite bossDoor;

    public Sprite treasureIcon;
    public Sprite shopIcon;
    public Sprite challengeIcon;
    public Sprite middleBossIcon;
    public Sprite mainBossIcon;
    public Sprite startRoomIcon;
    public Sprite noLeftArmIcon;
    public Sprite noRightArmIcon;
    public Sprite noLeftEyeIcon;
    public Sprite noRightEyeIcon;
    public Sprite noLeftLegIcon;
    public Sprite noRightLegIcon;
    public AudioClip doorOpenSfx;

    [Header("Door Layout (authored once, applied to every door)")]
    // Door image size = sprite.bounds * doorVisualScale; placed at doorVisualOffset from the door root.
    public float doorVisualScale = 2.4928f;
    public Vector2 doorVisualOffset = new Vector2(-0.3441f, 0.5196f);
    // Icon is normalized to iconLocalHeight (in the door-visual local space) and placed at iconLocalOffset.
    public float iconLocalHeight = 0.62f;
    public Vector3 iconLocalOffset = new Vector3(0.016f, 0.223f, -0.02f);
    // Interaction trigger collider. If doorColliderPath has >= 3 points, a PolygonCollider2D
    // with those points (world-offset from the door root) is used; otherwise a BoxCollider2D
    // of doorColliderSize/doorColliderOffset is used.
    public Vector2 doorColliderSize = new Vector2(2.34f, 2.70f);
    public Vector2 doorColliderOffset = Vector2.zero;
    public Vector2[] doorColliderPath = new Vector2[0];

    static DoorSpriteCatalog instance;

    public static DoorSpriteCatalog Load()
    {
        if (instance == null)
            instance = Resources.Load<DoorSpriteCatalog>("Config/DoorSpriteCatalog");
        return instance;
    }

    public Sprite DoorFor(MapNode node)
    {
        if (node == null)
            return null;

        switch (node.roomType)
        {
            case RoomType.Treasure:
            case RoomType.Supply:
                return treasureDoor;
            case RoomType.Shop:
                return shopDoor;
            case RoomType.Challenge:
            case RoomType.Event:
                return challengeDoor;
            case RoomType.Boss:
            case RoomType.MiddleBoss:
            case RoomType.FinalBoss:
                return bossDoor;
            default:
                return enemyRoomDoor;
        }
    }

    public Sprite IconFor(MapNode node)
    {
        if (node == null)
            return null;

        switch (node.roomType)
        {
            case RoomType.Treasure:
            case RoomType.Supply:
                return treasureIcon;
            case RoomType.Shop:
                return shopIcon;
            case RoomType.Challenge:
            case RoomType.Event:
                return challengeIcon;
            case RoomType.MiddleBoss:
                return middleBossIcon;
            case RoomType.Boss:
            case RoomType.FinalBoss:
                return mainBossIcon;
            case RoomType.Start:
                return startRoomIcon;
            case RoomType.ConditionCombat:
                return ConditionIcon(node.conditionType);
            default:
                return null;
        }
    }

    Sprite ConditionIcon(NodeConditionType condition)
    {
        switch (condition)
        {
            case NodeConditionType.NoLeftArm: return noLeftArmIcon;
            case NodeConditionType.NoRightArm: return noRightArmIcon;
            case NodeConditionType.NoLeftEye: return noLeftEyeIcon;
            case NodeConditionType.NoRightEye: return noRightEyeIcon;
            case NodeConditionType.NoLeftLeg: return noLeftLegIcon;
            case NodeConditionType.NoRightLeg: return noRightLegIcon;
            default: return null;
        }
    }
}
