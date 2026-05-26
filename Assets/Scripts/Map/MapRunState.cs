using System.Collections.Generic;

public static class MapRunState
{
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
}
