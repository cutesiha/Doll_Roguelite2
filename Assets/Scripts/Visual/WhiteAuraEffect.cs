using System.Collections;
using UnityEngine;

public class WhiteAuraEffect : MonoBehaviour
{
    static WhiteAuraEffect _active;

    public static void SpawnOn(Transform player, float duration = 20f)
    {
        if (_active != null)
        {
            _active.StopAllCoroutines();
            Destroy(_active.gameObject);
        }

        var go = new GameObject("WhiteAuraEffect");
        var effect = go.AddComponent<WhiteAuraEffect>();
        _active = effect;
        effect.StartCoroutine(effect.Run(player, duration));
    }

    IEnumerator Run(Transform target, float duration)
    {
        const int countOuter = 12;
        const int countInner = 8;
        const float outerRadius = 1.1f;
        const float innerRadius = 0.6f;
        const float outerSpeed = 120f;
        const float innerSpeed = -200f;
        const float fadeOutTime = 3f;

        var outerPts = new Transform[countOuter];
        var outerSrs = new SpriteRenderer[countOuter];
        var innerPts = new Transform[countInner];
        var innerSrs = new SpriteRenderer[countInner];
        Sprite dot = GetCircleSprite();

        for (int i = 0; i < countOuter; i++)
        {
            var go = new GameObject("wp_o");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dot;
            sr.sortingOrder = 16;
            outerPts[i] = go.transform;
            outerSrs[i] = sr;
        }

        for (int i = 0; i < countInner; i++)
        {
            var go = new GameObject("wp_i");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dot;
            sr.sortingOrder = 16;
            innerPts[i] = go.transform;
            innerSrs[i] = sr;
        }

        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            float fadeAlpha = elapsed < duration - fadeOutTime
                ? 1f
                : Mathf.Lerp(1f, 0f, (elapsed - (duration - fadeOutTime)) / fadeOutTime);

            float outerAngle = elapsed * outerSpeed;
            for (int i = 0; i < countOuter; i++)
            {
                if (outerPts[i] == null) continue;

                float angle = outerAngle + (360f / countOuter) * i;
                float r = outerRadius + Mathf.Sin(elapsed * 4f + i * 0.8f) * 0.12f;

                outerPts[i].position = (Vector2)target.position + new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * r,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * r * 0.55f);

                float sparkle = 0.7f + Mathf.Abs(Mathf.Sin(elapsed * 8f + i * 1.3f)) * 0.3f;
                float alpha = sparkle * fadeAlpha;
                outerSrs[i].color = new Color(1f, 1f, 1f, alpha);

                float scale = 0.18f + Mathf.Sin(elapsed * 6f + i) * 0.04f;
                outerPts[i].localScale = Vector3.one * scale;
            }

            float innerAngle = elapsed * innerSpeed;
            for (int i = 0; i < countInner; i++)
            {
                if (innerPts[i] == null) continue;

                float angle = innerAngle + (360f / countInner) * i;
                float r = innerRadius + Mathf.Sin(elapsed * 5f + i * 1.1f) * 0.08f;

                innerPts[i].position = (Vector2)target.position + new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * r,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * r * 0.55f);

                float sparkle = 0.6f + Mathf.Abs(Mathf.Sin(elapsed * 10f + i * 0.9f)) * 0.4f;
                float alpha = sparkle * fadeAlpha;
                innerSrs[i].color = new Color(0.85f, 0.95f, 1f, alpha);

                float scale = 0.14f + Mathf.Sin(elapsed * 7f + i * 1.5f) * 0.03f;
                innerPts[i].localScale = Vector3.one * scale;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < countOuter; i++)
            if (outerPts[i] != null) Destroy(outerPts[i].gameObject);
        for (int i = 0; i < countInner; i++)
            if (innerPts[i] != null) Destroy(innerPts[i].gameObject);

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
