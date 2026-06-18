using UnityEngine;

public static class MapGenerator
{
    // Fixed route draft:
    //
    //              START
    //             /     \
    //          C1         C2
    //        /  |       / | \
    //      C3  SHOP  BODY C4
    //       \   / \   /  /
    //        MID   C5  CHALLENGE
    //          \   |   /
    //            C6 C7
    //             \ /
    //             BOSS

    static int _nextId;
    static int _nextCondition;

    public static MapNode GenerateTree()
    {
        _nextId = 0;
        _nextCondition = 0;

        var start = Node(0, 0, RoomType.Event);

        var a1 = ConditionNode(1, 0);
        var a2 = ConditionNode(1, 1);
        Link(start, a1);
        Link(start, a2);

        var b1 = ConditionNode(2, 0);
        var b2 = Node(2, 1, RoomType.Shop);
        var b3 = Node(2, 2, RoomType.Supply);
        var b4 = ConditionNode(2, 3);
        Link(a1, b1);
        Link(a1, b2);
        Link(a2, b2);
        Link(a2, b3);
        Link(a2, b4);

        var c1 = Node(3, 0, RoomType.Event);
        var c2 = ConditionNode(3, 1);
        var c3 = Node(3, 2, RoomType.Event);
        Link(b1, c1);
        Link(b2, c1);
        Link(b2, c2);
        Link(b3, c2);
        Link(b3, c3);
        Link(b4, c3);

        var d1 = ConditionNode(4, 0);
        var d2 = ConditionNode(4, 1);
        Link(c1, d1);
        Link(c2, d1);
        Link(c2, d2);
        Link(c3, d2);

        var boss = Node(5, 0, RoomType.Boss);
        Link(d1, boss);
        Link(d2, boss);

        return start;
    }

    static MapNode Node(int layer, int indexInLayer, RoomType roomType)
    {
        return new MapNode
        {
            id = _nextId++,
            layer = layer,
            indexInLayer = indexInLayer,
            roomType = roomType,
            conditionType = NodeConditionType.None
        };
    }

    static MapNode ConditionNode(int layer, int indexInLayer)
    {
        MapNode node = Node(layer, indexInLayer, RoomType.ConditionCombat);
        node.conditionType = NextCondition();
        return node;
    }

    static NodeConditionType NextCondition()
    {
        NodeConditionType[] pool =
        {
            NodeConditionType.NoLeftArm,
            NodeConditionType.NoRightEye,
            NodeConditionType.NoLeftLeg,
            NodeConditionType.NoRightLeg
        };

        NodeConditionType condition = pool[_nextCondition % pool.Length];
        _nextCondition++;
        return condition;
    }

    static void Link(MapNode parent, MapNode child)
    {
        if (parent == null || child == null)
            return;

        if (!parent.children.Contains(child))
            parent.children.Add(child);

        if (child.parent == null)
            child.parent = parent;
    }
}
