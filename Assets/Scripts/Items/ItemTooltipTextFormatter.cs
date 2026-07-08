public static class ItemTooltipTextFormatter
{
    public static string Build(ItemData item, int? shopPrice = null)
    {
        if (item == null)
            return "아이템 데이터 없음";

        string description = string.IsNullOrWhiteSpace(item.Description)
            ? "설명 없음"
            : item.Description.Trim();

        string text = "<b>" + item.ItemName + "</b>";

        // 누더기(rag)는 종류 줄(2번째 줄)을 표시하지 않는다.
        if (item.ItemId != "rag")
            text += "\n<size=82%>" + KindLabel(item) + "</size>";

        text += "\n<size=72%>" + description + "</size>";

        // 상점 아이템은 맨 아랫줄에 구매 안내를 표시한다.
        if (shopPrice.HasValue)
            text += "\n<size=78%>[E] 구매   가격 " + shopPrice.Value + "코인</size>";

        return text;
    }

    public static string KindLabel(ItemData item)
    {
        if (item == null)
            return "알 수 없음";

        if (item.ItemId == "rag")
            return "누더기";

        switch (item.Type)
        {
            case ItemType.BodyPart:
                return "신체부위 - " + EquipLocationLabel(item.EquipLocation);
            case ItemType.GemConsumable:
                return "보석";
            case ItemType.Shield:
                return "방어막";
            case ItemType.Currency:
                return "동전";
            case ItemType.Passive:
                return "패시브";
            default:
                return item.Type.ToString();
        }
    }

    static string EquipLocationLabel(ItemEquipLocation location)
    {
        switch (location)
        {
            case ItemEquipLocation.Eye:
                return "눈";
            case ItemEquipLocation.Arm:
                return "팔";
            case ItemEquipLocation.Body:
                return "몸통";
            case ItemEquipLocation.Leg:
                return "다리";
            case ItemEquipLocation.Consumable:
                return "소모품";
            case ItemEquipLocation.Shield:
                return "방어막";
            default:
                return "공용";
        }
    }
}
