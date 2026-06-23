// 동전·누더기·보석 같은 소모성 아이템을 인벤토리에 넣는 확장 메서드.
// InventoryManager.cs 본체를 건드리지 않으므로, 협업 중 그 파일이
// 다른 버전으로 덮여도 이 기능은 유지된다.
public static class InventoryManagerItemExtensions
{
    // 보관함 빈칸에 소모성 아이템 추가. 가득 차면 false.
    public static bool AddItem(this InventoryManager inv, ItemKind kind)
    {
        if (inv == null)
            return false;

        // equipIfEmpty=false → 부위 슬롯 자동 장착을 건너뛰고 보관함으로
        return inv.TryAddPart(new BodyPart(kind), false);
    }
}
