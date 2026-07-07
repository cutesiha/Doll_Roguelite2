using System.Collections;
using UnityEngine;

// 몬스터 처치 시 아이템이 드랍될 때 터지는 동그란 노란 입자 이펙트.
public static class ItemDropParticleEffect
{
    public static readonly Color ParticleColor = new Color(1f, 0.86f, 0.25f, 1f);

    static Sprite _circleSprite;

    public static void Spawn(Vector3 position)
    {
        var host = new GameObject("ItemDropParticleEffect");
        host.transform.position = position;
        host.AddComponent<ItemDropParticleEffectRunner>().Play(position);
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
            return _circleSprite;

        const int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - dist) / 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _circleSprite;
    }
}

public class ItemDropParticleEffectRunner : MonoBehaviour
{
    public void Play(Vector3 position)
    {
        StartCoroutine(RunEffect(position));
    }

    IEnumerator RunEffect(Vector3 origin)
    {
        const int count = 10;
        const float duration = 0.5f;
        const float speed = 2.4f;
        const float startSize = 0.22f;
        const float gravity = -1.6f;

        var transforms = new Transform[count];
        var renderers = new SpriteRenderer[count];
        var velocities = new Vector2[count];

        Sprite dot = ItemDropParticleEffect.GetCircleSprite();
        Color baseColor = ItemDropParticleEffect.ParticleColor;

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-18f, 18f);
            float mag = Random.Range(0.6f, 1f) * speed;
            velocities[i] = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)) * mag;

            var go = new GameObject("dp");
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dot;
            sr.color = baseColor;
            sr.sortingOrder = 36;

            transforms[i] = go.transform;
            renderers[i] = sr;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float dt = Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                if (transforms[i] == null) continue;

                velocities[i].y += gravity * dt;
                transforms[i].position += (Vector3)(velocities[i] * dt);

                float scale = Mathf.Lerp(startSize, 0f, t * t);
                transforms[i].localScale = Vector3.one * scale;

                Color c = renderers[i].color;
                c.a = Mathf.Lerp(1f, 0f, t);
                renderers[i].color = c;
            }

            elapsed += dt;
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (transforms[i] != null)
                Destroy(transforms[i].gameObject);

        Destroy(gameObject);
    }
}
