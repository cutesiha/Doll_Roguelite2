using System;

public enum BodySlot { EyeLeft, EyeRight, ArmLeft, ArmRight, LegLeft, LegRight }

[Serializable]
public class BodyPart
{
    public BodySlot slot;
    public int maxHp     = 100;
    public int currentHp = 100;

    public BodyPart(BodySlot slot) { this.slot = slot; }

    public string SlotName()
    {
        switch (slot)
        {
            case BodySlot.EyeLeft:  return "왼쪽 눈";
            case BodySlot.EyeRight: return "오른쪽 눈";
            case BodySlot.ArmLeft:  return "왼쪽 팔";
            case BodySlot.ArmRight: return "오른쪽 팔";
            case BodySlot.LegLeft:  return "왼쪽 다리";
            case BodySlot.LegRight: return "오른쪽 다리";
            default:                return "?";
        }
    }
}
