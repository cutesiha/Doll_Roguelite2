using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
    public static MapNode GenerateTree()
    {
        int nextId = 0;

        // Layer 0: start
        var root = new MapNode { id = nextId++, layer = 0, conditionType = NodeConditionType.Free };

        // Layer 1: 항상 3개
        var layer1 = new List<MapNode>();
        int l1Count = 3;
        for (int i = 0; i < l1Count; i++)
        {
            var n = new MapNode { id = nextId++, layer = 1, indexInLayer = i, parent = root, conditionType = RollCondition() };
            root.children.Add(n);
            layer1.Add(n);
        }

        // Layer 2: 1–2 branches per layer1 node
        var layer2 = new List<MapNode>();
        foreach (var p in layer1)
        {
            int cnt = Random.Range(1, 3);
            for (int i = 0; i < cnt; i++)
            {
                var n = new MapNode { id = nextId++, layer = 2, indexInLayer = layer2.Count, parent = p, conditionType = RollCondition() };
                p.children.Add(n);
                layer2.Add(n);
            }
        }

        // Layer 3: boss (single node — all layer2 nodes connect here)
        var boss = new MapNode { id = nextId, layer = 3, indexInLayer = 0, conditionType = NodeConditionType.Boss };
        foreach (var n in layer2)
            n.children.Add(boss);

        return root;
    }

    static NodeConditionType RollCondition()
    {
        float r = Random.value;
        return r < 0.40f ? NodeConditionType.Free
             : r < 0.70f ? NodeConditionType.NoLeftArm
             : NodeConditionType.NoRightEye;
    }
}
