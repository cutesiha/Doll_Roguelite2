using System.Collections.Generic;
using UnityEngine;

public enum NodeConditionType { None, NoLeftArm, NoRightEye, NoLeftLeg, NoRightLeg }
public enum RoomType { NormalCombat, ConditionCombat, Supply, Event, Boss }
public enum NodeState { Hidden, RouteOnly, Visible, Current, Cleared }

public class MapNode
{
    public int id;
    public int layer;
    public int indexInLayer;
    public MapNode parent;
    public List<MapNode> children = new List<MapNode>();
    public RoomType roomType;
    public NodeConditionType conditionType; // ConditionCombat 일 때만 유효
    public NodeState state = NodeState.Hidden;
    public bool isCleared;
    public Vector2 position;
}
