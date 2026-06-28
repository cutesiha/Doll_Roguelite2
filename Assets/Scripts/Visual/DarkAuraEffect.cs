using System.Collections;
using UnityEngine;

public class DarkAuraEffect : MonoBehaviour
{
    static DarkAuraEffect _active;

    public static void SpawnOn(Transform player, float duration = 3f)
    {
        if (_active != null)
        {
            _active.StopAllCoroutines();
            Destroy(_active.gameObject);
        }

        var go = new GameObject("DarkAuraEffect");
        var effect = go.AddComponent<DarkAuraEffect>();
        _active = effect;
        effect.StartCoroutine(effect.Run(player, duration));
    }

    IEnumerator Run(Transform target, float duration)
    {
        const int count = 14;
        const float orbitRadius = 0.9f;
        const float orbitSpeed = 150f;
        const float particleSize = 0.22f;

        var pts = new Transform[count];
        var srs = new SpriteRenderer[count];
        Sprite dot = GetCircleSprite();

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("ap");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dot;
            sr.color = new Color(0.04f, 0.04f, 0.06f, 0.85f);
            sr.sortingOrder = 15;
            pts[i] = go.transform;
            srs[i] = sr;
        }

        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            float t = elapsed / duration;
            float baseAngle = elapsed * orbitSpeed;

            for (int i = 0; i < count; i++)
            {
                if (pts[i] == null) continue;

                float angle = baseAngle + (360f / count) * i;
                float r = orbitRadius + Mathf.Sin(elapsed * 3f + i * 0.7f) * 0.1f;

                pts[i].position = (Vector2)target.position + new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * r,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * r * 0.6f);

                float alpha = t > 0.75f ? Mathf.Lerp(0.85f, 0f, (t - 0.75f) / 0.25f) : 0.85f;
                Color c = srs[i].color;
                c.a = alpha;
                srs[i].color = c;

                float scale = particleSize + Mathf.Sin(elapsed * 5f + i) * 0.05f;
                pts[i].localScale = Vector3.one * scale;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (pts[i] != null)
                Destroy(pts[i].gameObject);

        if (_active == this) _active = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_active == this) _active = null;
    }

    static Sprite _circleSprite;

    static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
            return _circleSprite;

        const int size = 16;
        float center = (size - 1) / 2f;
        float radius = center;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float alpha = Mathf.Clamp01(1f - (dist - (radius - 1.5f)) / 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 1f);
        return _circleSprite;
    }
}
