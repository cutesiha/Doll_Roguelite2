using System.Collections.Generic;
using UnityEngine;

public enum NodeConditionType { None, Normal, Hard, Boss }
public enum NodeState { Hidden, RouteOnly, Visible, Current, Cleared }

public class MapNode
{
    public int id;
    public int layer;
    public int indexInLayer;
    public MapNode parent;
    public List<MapNode> children = new List<MapNode>();
    public NodeConditionType conditionType;
    public NodeState state = NodeState.Hidden;
    public bool isCleared;
    public Vector2 position;
}
