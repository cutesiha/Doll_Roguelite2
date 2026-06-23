using UnityEngine;

public static class MapGenerator
{
    // Fixed route:
    // START -> 2 condition rooms -> 4 branch rooms -> MINOTAUR
    //       -> 2 branch rooms -> 4 branch rooms -> BOOK BOSS

    static int _nextId;
    static NodeConditionType[] _conditionPool;
    static int _nextCondition;

    public static MapNode GenerateTree()
    {
        _nextId = 0;
        PrepareConditionPool();

        var start = Node(0, 0, RoomType.Start);

        var a1 = ConditionNode(1, 0);
        var a2 = ConditionNode(1, 1);
        Link(start, a1);
        Link(start, a2);

        var b1 = Node(2, 0, RoomType.Treasure);
        var b2 = Node(2, 1, RoomType.Shop);
        var b3 = Node(2, 2, RoomType.Challenge);
        var b4 = ConditionNode(2, 3);
        Link(a1, b1);
        Link(a1, b2);
        Link(a2, b3);
        Link(a2, b4);

        var middleBoss = Node(3, 0, RoomType.MiddleBoss);
        Link(b1, middleBoss);
        Link(b2, middleBoss);
        Link(b3, middleBoss);
        Link(b4, middleBoss);

        var d1 = Node(4, 0, RoomType.Treasure);
        var d2 = Node(4, 1, RoomType.Shop);
        Link(middleBoss, d1);
        Link(middleBoss, d2);

        var e1 = ConditionNode(5, 0);
        var e2 = Node(5, 1, RoomType.Challenge);
        var e3 = ConditionNode(5, 2);
        var e4 = Node(5, 3, RoomType.Treasure);
        Link(d1, e1);
        Link(d1, e2);
        Link(d2, e3);
        Link(d2, e4);

        var finalBoss = Node(6, 0, RoomType.FinalBoss);
        Link(e1, finalBoss);
        Link(e2, finalBoss);
        Link(e3, finalBoss);
        Link(e4, finalBoss);

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
        if (_conditionPool == null || _conditionPool.Length == 0)
            PrepareConditionPool();

        NodeConditionType condition = _conditionPool[_nextCondition % _conditionPool.Length];
        _nextCondition++;
        return condition;
    }

    static void PrepareConditionPool()
    {
        _conditionPool = new[]
        {
            NodeConditionType.NoLeftArm,
            NodeConditionType.NoRightArm,
            NodeConditionType.NoLeftEye,
            NodeConditionType.NoRightEye,
            NodeConditionType.NoLeftLeg,
            NodeConditionType.NoRightLeg
        };

        for (int i = _conditionPool.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            NodeConditionType temp = _conditionPool[i];
            _conditionPool[i] = _conditionPool[swapIndex];
            _conditionPool[swapIndex] = temp;
        }

        _nextCondition = 0;
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
