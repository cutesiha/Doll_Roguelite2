using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFogEffect : MonoBehaviour
{
    static ScreenFogEffect _active;

    // fogDuration: 뿌연 상태로 유지되는 시간. 이후 fadeDuration 동안 서서히 사라진다.
    public static void Show(float fogDuration = 1.5f, float fadeDuration = 0.5f, Color? color = null)
    {
        if (_active != null)
        {
            _active.StopAllCoroutines();
            Destroy(_active.gameObject);
        }

        var go = new GameObject("ScreenFogEffect");
        DontDestroyOnLoad(go);
        var effect = go.AddComponent<ScreenFogEffect>();
        _active = effect;
        effect.StartCoroutine(effect.Run(fogDuration, fadeDuration, color ?? new Color(1f, 1f, 0.85f, 1f)));
    }

    IEnumerator Run(float fogDuration, float fadeDuration, Color fogColor)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;

        var imgGo = new GameObject("FogImage");
        imgGo.transform.SetParent(transform, false);
        var img = imgGo.AddComponent<RawImage>();
        img.texture = BuildFogTexture();
        img.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0f);

        var rect = imgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        const float fadeInDuration = 0.3f;
        const float maxAlpha = 0.55f;

        // 페이드 인
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            img.color = new Color(fogColor.r, fogColor.g, fogColor.b,
                Mathf.Lerp(0f, maxAlpha, elapsed / fadeInDuration));
            yield return null;
        }

        img.color = new Color(fogColor.r, fogColor.g, fogColor.b, maxAlpha);

        // 유지
        yield return new WaitForSeconds(fogDuration);

        // 페이드 아웃
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            img.color = new Color(fogColor.r, fogColor.g, fogColor.b,
                Mathf.Lerp(maxAlpha, 0f, elapsed / fadeDuration));
            yield return null;
        }

        if (_active == this) _active = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_active == this) _active = null;
    }

    static Texture2D BuildFogTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / center;
                float ny = (y - center) / center;
                // 중심은 진하고 가장자리로 갈수록 연해지는 안개
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01(1f - dist * 0.6f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
