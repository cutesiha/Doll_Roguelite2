using System;
using System.Collections.Generic;
using UnityEngine;

public static class MapRunState
{
    [Serializable]
    public class SaveData
    {
        public int rootId;
        public int currentId;
        public int pendingId = -1;
        public List<NodeSaveData> nodes = new List<NodeSaveData>();
    }

    [Serializable]
    public class NodeSaveData
    {
        public int id;
        public int layer;
        public int indexInLayer;
        public int[] children;
        public RoomType roomType;
        public NodeConditionType conditionType;
        public NodeState state;
        public bool isCleared;
        public float positionX;
        public float positionY;
    }

    public static MapNode Root { get; private set; }
    public static MapNode CurrentNode { get; private set; }
    public static MapNode PendingNode { get; private set; }

    public static void EnsureRun()
    {
        if (Root == null)
            ResetRun();
    }

    public static void ResetRun()
    {
        Root = MapGenerator.GenerateTree();
        CurrentNode = Root;
        PendingNode = null;
        UpdateVisibility();
    }

    public static SaveData Capture()
    {
        EnsureRun();

        SaveData save = new SaveData
        {
            rootId = Root != null ? Root.id : -1,
            currentId = CurrentNode != null ? CurrentNode.id : -1,
            pendingId = PendingNode != null ? PendingNode.id : -1
        };

        if (Root == null)
            return save;

        foreach (MapNode node in CollectNodes(Root))
        {
            NodeSaveData nodeSave = new NodeSaveData
            {
                id = node.id,
                layer = node.layer,
                indexInLayer = node.indexInLayer,
                children = ChildIds(node),
                roomType = node.roomType,
                conditionType = node.conditionType,
                state = node.state,
                isCleared = node.isCleared,
                positionX = node.position.x,
                positionY = node.position.y
            };
            save.nodes.Add(nodeSave);
        }

        return save;
    }

    public static void Restore(SaveData save)
    {
        if (save == null || save.nodes == null || save.nodes.Count == 0)
        {
            ResetRun();
            return;
        }

        Dictionary<int, MapNode> map = new Dictionary<int, MapNode>();
        for (int i = 0; i < save.nodes.Count; i++)
        {
            NodeSaveData source = save.nodes[i];
            map[source.id] = new MapNode
            {
                id = source.id,
                layer = source.layer,
                indexInLayer = source.indexInLayer,
                roomType = source.roomType,
                conditionType = source.conditionType,
                state = source.state,
                isCleared = source.isCleared,
                position = new Vector2(source.positionX, source.positionY)
            };
        }

        for (int i = 0; i < save.nodes.Count; i++)
        {
            NodeSaveData source = save.nodes[i];
            MapNode node;
            if (!map.TryGetValue(source.id, out node) || source.children == null)
                continue;

            for (int childIndex = 0; childIndex < source.children.Length; childIndex++)
            {
                MapNode child;
                if (!map.TryGetValue(source.children[childIndex], out child))
                    continue;

                node.children.Add(child);
                if (child.parent == null)
                    child.parent = node;
            }
        }

        map.TryGetValue(save.rootId, out MapNode root);
        map.TryGetValue(save.currentId, out MapNode current);
        MapNode pending = null;
        if (save.pendingId >= 0)
            map.TryGetValue(save.pendingId, out pending);

        Root = root;
        CurrentNode = current;
        PendingNode = pending;

        if (Root == null || CurrentNode == null)
            ResetRun();
    }

    public static bool BeginRoom(MapNode node)
    {
        if (CurrentNode == null || node == null) return false;
        if (!CurrentNode.children.Contains(node)) return false;
        if (node.state != NodeState.Visible) return false;

        PendingNode = node;
        return true;
    }

    public static bool CompletePendingRoom()
    {
        if (CurrentNode == null || PendingNode == null) return false;
        if (!CurrentNode.children.Contains(PendingNode))
        {
            PendingNode = null;
            return false;
        }

        CurrentNode.isCleared = true;
        CurrentNode.state = NodeState.Cleared;
        CurrentNode = PendingNode;
        PendingNode = null;
        UpdateVisibility();
        return true;
    }

    static void UpdateVisibility()
    {
        if (Root == null || CurrentNode == null) return;

        foreach (var node in CollectNodes(Root))
            if (node.state != NodeState.Cleared)
                node.state = NodeState.Hidden;

        CurrentNode.state = NodeState.Current;

        foreach (var child in CurrentNode.children)
        {
            if (child.state != NodeState.Cleared)
                child.state = NodeState.Visible;

            foreach (var grand in child.children)
                if (grand.state != NodeState.Cleared)
                    grand.state = NodeState.RouteOnly;
        }
    }

    static IEnumerable<MapNode> CollectNodes(MapNode root)
    {
        var visited = new HashSet<MapNode>();
        var queue = new Queue<MapNode>();
        queue.Enqueue(root);
        visited.Add(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            yield return node;

            foreach (var child in node.children)
                if (visited.Add(child))
                    queue.Enqueue(child);
        }
    }

    static int[] ChildIds(MapNode node)
    {
        if (node == null || node.children == null)
            return new int[0];

        int[] ids = new int[node.children.Count];
        for (int i = 0; i < node.children.Count; i++)
            ids[i] = node.children[i] != null ? node.children[i].id : -1;
        return ids;
    }
}
