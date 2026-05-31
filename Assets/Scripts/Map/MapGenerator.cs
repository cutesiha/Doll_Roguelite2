using UnityEngine;

public static class MapGenerator
{
    // 고정 트리 — 중앙 직선 루트 제거
    //
    //               START
    //              /  |  \
    //            A1   A2   A3
    //           /|\  / \  /|\
    //          B1 B2 B3 B4 B5
    //          |  /X\ /X\ /|
    //     C1 C2  C3  C4  C5
    //      |  \  |  /  |
    //      D1  D2   D3
    //         \|/
    //         BOSS
    //
    // L1→L2 : A1→{B1,B2} / A2→{B2,B3,B4} / A3→{B4,B5}
    //          (B3은 A2만 연결 — A1,A3는 B3로 안 감)
    // L2→L3 : B1→{C1,C2} / B2→{C3} / B3→{C2,C4} / B4→{C3} / B5→{C4,C5}
    //          (선 8개 — B2→C2, B4→C4 제거로 정리)
    // L3→L4 : C1→{D1} / C2→{D1,D2} / C3→{D2} / C4→{D2,D3} / C5→{D3}
    // L4→L5 : D1,D2,D3→{BOSS}

    static int _nextId;

    public static MapNode GenerateTree()
    {
        _nextId = 0;

        // Layer 0: Start
        var start = new MapNode { id = _nextId++, layer = 0, indexInLayer = 0, roomType = RoomType.NormalCombat };

        // Layer 1: Normal 1 + Cond 2
        var l1 = BuildLayer(1, new[] { RoomType.NormalCombat, RoomType.ConditionCombat, RoomType.ConditionCombat });
        var a1 = l1[0]; var a2 = l1[1]; var a3 = l1[2];
        a1.parent = a2.parent = a3.parent = start;
        start.children.Add(a1); start.children.Add(a2); start.children.Add(a3);

        // Layer 2: Normal 1 + Cond 3 + Supply 1
        var l2 = BuildLayer(2, new[] {
            RoomType.NormalCombat,
            RoomType.ConditionCombat, RoomType.ConditionCombat, RoomType.ConditionCombat,
            RoomType.Supply });
        var b1 = l2[0]; var b2 = l2[1]; var b3 = l2[2]; var b4 = l2[3]; var b5 = l2[4];

        // A1→{B1,B2}  A2→{B2,B3,B4}  A3→{B4,B5}
        b1.parent = a1; b2.parent = a1; b3.parent = a2; b4.parent = a2; b5.parent = a3;
        a1.children.Add(b1); a1.children.Add(b2);
        a2.children.Add(b2); a2.children.Add(b3); a2.children.Add(b4);
        a3.children.Add(b4); a3.children.Add(b5);

        // Layer 3: Normal 1 + Cond 2 + Supply 1 + Event 1
        var l3 = BuildLayer(3, new[] {
            RoomType.NormalCombat,
            RoomType.ConditionCombat, RoomType.ConditionCombat,
            RoomType.Supply, RoomType.Event });
        var c1 = l3[0]; var c2 = l3[1]; var c3 = l3[2]; var c4 = l3[3]; var c5 = l3[4];

        // B1→{C1,C2}  B2→{C3}  B3→{C2,C4}  B4→{C3}  B5→{C4,C5}
        // (B2→C2, B4→C4 제거로 2개 선 삭제 — C2/C4는 B3 경유로만 연결)
        c1.parent = b1; c2.parent = b1; c3.parent = b2; c4.parent = b3; c5.parent = b5;
        b1.children.Add(c1); b1.children.Add(c2);
        b2.children.Add(c3);
        b3.children.Add(c2); b3.children.Add(c4);
        b4.children.Add(c3);
        b5.children.Add(c4); b5.children.Add(c5);

        // Layer 4: Normal 1 + Cond 1 + Event 1
        var l4 = BuildLayer(4, new[] { RoomType.NormalCombat, RoomType.ConditionCombat, RoomType.Event });
        var d1 = l4[0]; var d2 = l4[1]; var d3 = l4[2];

        // C1→{D1}  C2→{D1,D2}  C3→{D2}  C4→{D2,D3}  C5→{D3}
        d1.parent = c1; d2.parent = c2; d3.parent = c5;
        c1.children.Add(d1);
        c2.children.Add(d1); c2.children.Add(d2);
        c3.children.Add(d2);
        c4.children.Add(d2); c4.children.Add(d3);
        c5.children.Add(d3);

        // Layer 5: Boss
        var boss = new MapNode { id = _nextId, layer = 5, indexInLayer = 0, roomType = RoomType.Boss };
        d1.children.Add(boss); d2.children.Add(boss); d3.children.Add(boss);

        return start;
    }

    static MapNode[] BuildLayer(int layer, RoomType[] types)
    {
        ShuffleInPlace(types);

        int condCount = 0;
        foreach (var t in types) if (t == RoomType.ConditionCombat) condCount++;
        var conditions = UniqueConditions(condCount);

        int ci = 0;
        var nodes = new MapNode[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            var cond = types[i] == RoomType.ConditionCombat ? conditions[ci++] : NodeConditionType.None;
            nodes[i] = new MapNode
            {
                id = _nextId++, layer = layer, indexInLayer = i,
                roomType = types[i], conditionType = cond
            };
        }
        return nodes;
    }

    static NodeConditionType[] UniqueConditions(int count)
    {
        var pool = new[] {
            NodeConditionType.NoLeftArm,
            NodeConditionType.NoRightEye,
            NodeConditionType.NoLeftLeg,
            NodeConditionType.NoRightLeg
        };
        ShuffleInPlace(pool);
        var result = new NodeConditionType[count];
        for (int i = 0; i < count; i++) result[i] = pool[i];
        return result;
    }

    static void ShuffleInPlace<T>(T[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
        }
    }
}
