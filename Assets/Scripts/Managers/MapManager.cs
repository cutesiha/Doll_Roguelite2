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
        MapRunState.EnsureRun();
        SyncFromRunState();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public bool TryMoveToNode(MapNode node)
    {
        if (!MapRunState.BeginRoom(node)) return false;
        if (!MapRunState.CompletePendingRoom()) return false;
        SyncFromRunState();
        OnMapChanged?.Invoke();
        return true;
    }

    public bool TryBeginRoom(MapNode node)
    {
        bool started = MapRunState.BeginRoom(node);
        SyncFromRunState();
        return started;
    }

    public bool CompletePendingRoom()
    {
        bool completed = MapRunState.CompletePendingRoom();
        SyncFromRunState();
        OnMapChanged?.Invoke();
        return completed;
    }

    void SyncFromRunState()
    {
        Root = MapRunState.Root;
        CurrentNode = MapRunState.CurrentNode;
    }
}
