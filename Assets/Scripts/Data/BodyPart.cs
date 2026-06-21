using System;

public enum BodySlot { EyeLeft, EyeRight, ArmLeft, ArmRight, LegLeft, LegRight }

// 인벤토리에 담기는 모든 오브젝트 종류. BodyPart 는 장착 가능한 부위,
// 나머지(Coin/Rag/Gem)는 보관함에만 들어가는 소모성/자원 아이템.
public enum ItemKind { BodyPart, Coin, Rag, Gem }

[Serializable]
public class BodyPart
{
    public BodySlot slot;
    public int maxHp;
    public int currentHp;
    public ItemKind kind = ItemKind.BodyPart;

    public BodyPart(BodySlot slot)
    {
        this.slot = slot;
        kind = ItemKind.BodyPart;
        int hp = (slot == BodySlot.EyeLeft || slot == BodySlot.EyeRight) ? 2 : 3;
        maxHp = hp;
        currentHp = hp;
    }

    // 동전/누더기/보석 같은 비-부위 아이템 생성용
    public BodyPart(ItemKind itemKind)
    {
        kind = itemKind;
        slot = BodySlot.EyeLeft; // 미사용 (장착 불가)
        maxHp = 1;
        currentHp = 1;
    }

    public bool IsEquippable => kind == ItemKind.BodyPart;

    public string DisplayName()
    {
        switch (kind)
        {
            case ItemKind.Coin: return "동전";
            case ItemKind.Rag:  return "누더기";
            case ItemKind.Gem:  return "보석";
            default:            return SlotName();
        }
    }

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
