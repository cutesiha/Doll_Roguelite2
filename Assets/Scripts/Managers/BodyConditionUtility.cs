using UnityEngine;

public static class BodyConditionUtility
{
    public static BodyState CurrentState()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
            return inventory.GetBodyStateSnapshot();

        BodyManager bodyManager = BodyManager.Instance;
        if (bodyManager != null && bodyManager.State != null)
            return bodyManager.State;

        return new BodyState();
    }

    public static bool HasPart(BodySlot slot)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
            return inventory.IsEquipped(slot);

        BodyState state = CurrentState();
        switch (slot)
        {
            case BodySlot.EyeLeft: return state.eyeLeft;
            case BodySlot.EyeRight: return state.eyeRight;
            case BodySlot.ArmLeft: return state.armLeft;
            case BodySlot.ArmRight: return state.armRight;
            case BodySlot.LegLeft: return state.legLeft;
            case BodySlot.LegRight: return state.legRight;
            case BodySlot.Body: return state.body;
            default: return true;
        }
    }

    public static bool CanPass(MapNode node)
    {
        return CanPass(node, CurrentState());
    }

    public static bool CanPass(MapNode node, BodyState state)
    {
        if (node == null || node.roomType != RoomType.ConditionCombat)
            return true;

        if (state == null)
            state = CurrentState();

        switch (node.conditionType)
        {
            case NodeConditionType.NoLeftArm: return !state.armLeft;
            case NodeConditionType.NoRightArm: return !state.armRight;
            case NodeConditionType.NoLeftEye: return !state.eyeLeft;
            case NodeConditionType.NoRightEye: return !state.eyeRight;
            case NodeConditionType.NoLeftLeg: return !state.legLeft;
            case NodeConditionType.NoRightLeg: return !state.legRight;
            default: return true;
        }
    }

    public static bool TryGetRequiredMissingSlot(NodeConditionType conditionType, out BodySlot slot)
    {
        switch (conditionType)
        {
            case NodeConditionType.NoLeftArm:
                slot = BodySlot.ArmLeft;
                return true;
            case NodeConditionType.NoRightArm:
                slot = BodySlot.ArmRight;
                return true;
            case NodeConditionType.NoLeftEye:
                slot = BodySlot.EyeLeft;
                return true;
            case NodeConditionType.NoRightEye:
                slot = BodySlot.EyeRight;
                return true;
            case NodeConditionType.NoLeftLeg:
                slot = BodySlot.LegLeft;
                return true;
            case NodeConditionType.NoRightLeg:
                slot = BodySlot.LegRight;
                return true;
            default:
                slot = BodySlot.EyeLeft;
                return false;
        }
    }

    public static void LockRequiredMissingSlot(MapNode node)
    {
        if (node == null || node.roomType != RoomType.ConditionCombat)
            return;

        InventoryManager.Instance?.LockConditionSlot(node.conditionType);
    }

    public static void UnlockRequiredMissingSlot(MapNode node)
    {
        if (node == null || node.roomType != RoomType.ConditionCombat)
            return;

        InventoryManager.Instance?.UnlockConditionSlot(node.conditionType);
    }

    public static void ApplyToBodyManager()
    {
        InventoryManager inventory = InventoryManager.Instance;
        BodyManager bodyManager = BodyManager.Instance;
        if (inventory == null || bodyManager == null || bodyManager.State == null)
            return;

        BodyState source = inventory.GetBodyStateSnapshot();
        BodyState target = bodyManager.State;
        target.body = source.body;
        target.eyeLeft = source.eyeLeft;
        target.eyeRight = source.eyeRight;
        target.armLeft = source.armLeft;
        target.armRight = source.armRight;
        target.legLeft = source.legLeft;
        target.legRight = source.legRight;
    }
}
