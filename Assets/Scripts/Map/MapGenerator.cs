using UnityEngine;

public static class MapGenerator
{
    // Fixed route:
    // START -> 2 condition rooms -> 4 branch rooms -> 2 branch rooms x2 -> MINOTAUR
    //       -> 2 branch rooms x2 -> 2 branch rooms -> 4 branch rooms -> BOOK BOSS
    // (중간보스 바로 앞/뒤에 "2개 중 선택" 층을 2개씩 추가. 어느 쪽을 고르든 다음 층에서는
    //  항상 같은 2개 옵션이 나오도록 완전 연결(모든 이전 노드 -> 모든 다음 노드)한다.)

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

        // ── 중간보스 바로 앞 "2개 중 선택" 구간 (2개 층) ──────────────────
        // b1~b4 중 무엇을 골랐든 다음엔 항상 c1/c2 둘 다 선택지로 나온다 (완전 연결).
        var c1 = Node(3, 0, RoomType.Treasure);
        var c2 = Node(3, 1, RoomType.Shop);
        Link(b1, c1); Link(b1, c2);
        Link(b2, c1); Link(b2, c2);
        Link(b3, c1); Link(b3, c2);
        Link(b4, c1); Link(b4, c2);

        var f1 = ConditionNode(4, 0);
        var f2 = ConditionNode(4, 1);
        Link(c1, f1); Link(c1, f2);
        Link(c2, f1); Link(c2, f2);
        // ─────────────────────────────────────────────────────────────

        var middleBoss = Node(5, 0, RoomType.MiddleBoss);
        Link(f1, middleBoss);
        Link(f2, middleBoss);

        // ── 중간보스 바로 뒤 "2개 중 선택" 구간 (2개 층) ──────────────────
        var h1 = Node(6, 0, RoomType.Shop);
        var h2 = Node(6, 1, RoomType.Treasure);
        Link(middleBoss, h1);
        Link(middleBoss, h2);

        var k1 = Node(7, 0, RoomType.Treasure);
        var k2 = Node(7, 1, RoomType.Challenge);
        Link(h1, k1); Link(h1, k2);
        Link(h2, k1); Link(h2, k2);
        // ─────────────────────────────────────────────────────────────

        var d1 = ConditionNode(8, 0);
        var d2 = ConditionNode(8, 1);
        Link(k1, d1); Link(k1, d2);
        Link(k2, d1); Link(k2, d2);

        var e1 = ConditionNode(9, 0);
        var e2 = Node(9, 1, RoomType.Challenge);
        var e3 = ConditionNode(9, 2);
        var e4 = Node(9, 3, RoomType.Shop);
        Link(d1, e1);
        Link(d1, e2);
        Link(d2, e3);
        Link(d2, e4);

        var finalBoss = Node(10, 0, RoomType.FinalBoss);
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
