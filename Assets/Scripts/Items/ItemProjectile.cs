using System.Collections.Generic;
using UnityEngine;

public class ItemProjectile : MonoBehaviour
{
    Vector2 direction;
    float speed;
    int damage;
    float lifetime;
    float radius;
    bool piercing;
    float spinDegreesPerSecond;
    readonly HashSet<EnemyBase> hitEnemies = new();

    public static ItemProjectile Spawn(
        Vector3 position,
        Vector2 direction,
        float speed,
        int damage,
        float lifetime,
        float radius,
        bool piercing,
        Color color,
        ItemPlaceholderShape shape = ItemPlaceholderShape.Circle,
        Sprite spriteOverride = null,
        float spriteForwardOffsetDegrees = 0f,
        float spinDegreesPerSecond = 0f)
    {
        GameObject go = new GameObject("ItemProjectile");
        go.transform.position = position;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        if (spriteOverride != null)
        {
            // 아이템 전용 아트가 있으면 도형 대체 없이 원본 색 그대로 사용.
            renderer.sprite = spriteOverride;
            renderer.color = Color.white;
            go.transform.localScale = Vector3.one * radius * 2f;
        }
        else
        {
            renderer.sprite = BossVisuals.CircleSprite();
            renderer.color = color;
            go.transform.localScale = shape == ItemPlaceholderShape.Diamond
                ? new Vector3(radius * 2.2f, radius * 0.7f, 1f)
                : Vector3.one * radius * 2f;
        }
        renderer.sortingOrder = 45;

        ItemProjectile projectile = go.AddComponent<ItemProjectile>();
        projectile.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        projectile.speed = Mathf.Max(0.1f, speed);
        projectile.damage = Mathf.Max(1, damage);
        projectile.lifetime = Mathf.Max(0.05f, lifetime);
        projectile.radius = Mathf.Max(0.03f, radius);
        projectile.piercing = piercing;
        projectile.spinDegreesPerSecond = spinDegreesPerSecond;
        float travelAngle = Mathf.Atan2(projectile.direction.y, projectile.direction.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, travelAngle + spriteForwardOffsetDegrees);
        return projectile;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += (Vector3)(direction * step);
        lifetime -= Time.deltaTime;

        if (spinDegreesPerSecond != 0f)
            transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyBase enemy = hits[i] != null ? hits[i].GetComponentInParent<EnemyBase>() : null;
            if (enemy == null || !hitEnemies.Add(enemy))
                continue;

            enemy.TakeDamage(damage);
            if (!piercing)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (lifetime <= 0f)
            Destroy(gameObject);
    }
}
