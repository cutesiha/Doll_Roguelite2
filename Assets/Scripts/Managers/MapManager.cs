using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-1)]
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    public MapNode Root { get; private set; }
    public MapNode CurrentNode { get; private set; }

    public System.Action OnMapChanged;

    void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        Instance = this;
        Root = MapGenerator.GenerateTree();
        CurrentNode = Root;
        UpdateVisibility();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public bool TryMoveToNode(MapNode node)
    {
        if (!CurrentNode.children.Contains(node)) return false;
        CurrentNode.isCleared = true;
        CurrentNode.state = NodeState.Cleared;
        CurrentNode = node;
        UpdateVisibility();
        OnMapChanged?.Invoke();
        return true;
    }

    void UpdateVisibility()
    {
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
}
