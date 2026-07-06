using UnityEngine;

// ItemData는 모든 개체가 공유하는 에셋(정의)이라 개별 내구도를 담을 수 없다.
// 인벤토리 안에 실제로 존재하는 아이템 한 개(보관 중이든 장착 중이든)를
// 이 래퍼로 감싸서, 아이템마다 독립적인 HP가 교체/재장착 후에도 유지되게 한다.
public class ItemInstance
{
    public readonly ItemData data;
    public int maxHp;
    public int currentHp;
    bool hasLastBodySlot;
    BodySlot lastBodySlot;

    public ItemInstance(ItemData data, int maxHp)
    {
        this.data = data;
        this.maxHp = Mathf.Max(1, maxHp);
        currentHp = this.maxHp;
    }

    public void SetLastBodySlot(BodySlot slot)
    {
        lastBodySlot = slot;
        hasLastBodySlot = true;
    }

    public bool TryGetLastBodySlot(out BodySlot slot)
    {
        slot = lastBodySlot;
        return hasLastBodySlot;
    }

    // 기본(레거시) 부위 파츠와 동일한 최대 HP 규칙: 눈은 2, 그 외(팔/다리/몸)는 3.
    public static int DefaultMaxHp(ItemEquipLocation location)
    {
        return location == ItemEquipLocation.Eye ? 2 : 3;
    }
}
