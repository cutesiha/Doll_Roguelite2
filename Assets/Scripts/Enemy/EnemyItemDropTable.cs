using UnityEngine;

// 특정 잡몹 처치 시 낮은 확률로 그 몬스터 전용 아이템을 드랍한다.
public static class EnemyItemDropTable
{
    const float DropChance = 1f; // TODO: 테스트 후 0.05f로 되돌리기

    public static void TryDropSpecialItem(EnemyKind kind, Vector3 position)
    {
        string itemId = kind switch
        {
            EnemyKind.Chaser => "button_eye",   // 단추 몬스터 -> 단추 눈
            EnemyKind.Needle => "round_pin",    // 바늘 몬스터 -> 원형 시침핀
            EnemyKind.Spool => "knitted_body",  // 제봉틀 몬스터 -> 뜨개질 몸
            EnemyKind.Ribbon => "ribbon",        // 리본 몬스터 -> 리본
            _ => null
        };

        if (itemId == null || Random.value >= DropChance)
            return;

        ItemData item = ItemCatalog.Find(itemId);
        if (item == null)
            return;

        ItemWorldPickup pickup = ItemDropSpawner.Spawn(item, position, false, 0);
        pickup?.Toss(position);
        ItemDropParticleEffect.Spawn(position);
    }
}
