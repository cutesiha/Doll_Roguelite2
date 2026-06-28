using System.Collections;
using UnityEngine;

public static class EnemyDeathEffect
{
    static Sprite _dotSprite;

    public static void Spawn(Vector3 position, Color color)
    {
        var host = new GameObject("EnemyDeathEffect");
        host.transform.position = position;
        host.AddComponent<EnemyDeathEffectRunner>().Play(position, color);
    }

    public static Sprite GetDotSprite()
    {
        if (_dotSprite != null)
            return _dotSprite;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        _dotSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _dotSprite;
    }
}

public class EnemyDeathEffectRunner : MonoBehaviour
{
    public void Play(Vector3 position, Color color)
    {
        StartCoroutine(RunEffect(position, color));
    }

    IEnumerator RunEffect(Vector3 origin, Color baseColor)
    {
        const int count = 9;
        const float duration = 0.55f;
        const float speed = 2.2f;
        const float startSize = 0.28f;
        const float gravity = -1.4f;

        var transforms = new Transform[count];
        var renderers = new SpriteRenderer[count];
        var velocities = new Vector2[count];

        Sprite dot = EnemyDeathEffect.GetDotSprite();

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-20f, 20f);
            float mag = Random.Range(0.55f, 1f) * speed;
            velocities[i] = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)) * mag;

            var go = new GameObject("dp");
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dot;
            sr.color = baseColor;
            sr.sortingOrder = 20;

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
