using UnityEngine;

// 아이템/동전을 던질 때(Toss) 목표 지점이 벽이나 책상/문 같은 다른 솔리드 콜라이더 너머라면,
// 거기 부딪혀 튕겨 나온 지점으로 목표를 옮긴다. CoinWorldPickup/ItemWorldPickup/
// BodyPartWorldDrop이 공통으로 사용한다.
public static class DropWallBounce
{
    public static Vector3 ResolveTarget(Vector3 from, Vector3 to, Transform self)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return to;

        Vector2 dir = delta / distance;
        RaycastHit2D[] hits = Physics2D.RaycastAll(from, dir, distance);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger || hitCollider.transform == self)
                continue;
            if (!IsBlockingObstacle(hitCollider))
                continue;

            Vector2 reflected = Vector2.Reflect(dir, hits[i].normal);
            float remaining = Mathf.Max(0.15f, (distance - hits[i].distance) * 0.5f);
            Vector2 bounced = hits[i].point + reflected * remaining;
            return new Vector3(bounced.x, bounced.y, to.z);
        }

        return to;
    }

    // 벽뿐 아니라 책상/문 등 방 안의 모든 솔리드(트리거 아닌) 콜라이더를 장애물로 취급한다.
    // 던지는 사람(플레이어) 본인의 콜라이더만 예외로 둔다 — 그렇지 않으면 자기 발밑에서
    // 던지자마자 자기 몸에 튕겨서 거리가 거의 0이 돼 버린다.
    static bool IsBlockingObstacle(Collider2D collider)
    {
        return !collider.CompareTag("Player");
    }
}
