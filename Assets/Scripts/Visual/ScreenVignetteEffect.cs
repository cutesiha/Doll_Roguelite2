using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenVignetteEffect : MonoBehaviour
{
    static ScreenVignetteEffect _active;

    public static void Show(float duration = 3f, Color? color = null)
    {
        if (_active != null)
        {
            _active.StopAllCoroutines();
            Destroy(_active.gameObject);
        }

        var go = new GameObject("ScreenVignetteEffect");
        DontDestroyOnLoad(go);
        var effect = go.AddComponent<ScreenVignetteEffect>();
        _active = effect;
        effect.StartCoroutine(effect.Run(duration, color ?? Color.black));
    }

    IEnumerator Run(float duration, Color baseColor)
    {
        // Canvas 세팅
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        // 비네트 이미지 생성
        var imgGo = new GameObject("VignetteImage");
        imgGo.transform.SetParent(transform, false);
        var image = imgGo.AddComponent<RawImage>();
        image.raycastTarget = false;

        var rect = imgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.texture = BuildVignetteTexture(baseColor);

        const float fadeIn  = 0.3f;
        const float fadeOut = 0.6f;
        float hold = Mathf.Max(0f, duration - fadeIn - fadeOut);

        Color c = baseColor;

        // fade in
        for (float t = 0f; t < fadeIn; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(0f, 1f, t / fadeIn);
            image.color = c;
            yield return null;
        }

        // hold
        c.a = 1f;
        image.color = c;
        yield return new WaitForSeconds(hold);

        // fade out
        for (float t = 0f; t < fadeOut; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(1f, 0f, t / fadeOut);
            image.color = c;
            yield return null;
        }

        if (_active == this) _active = null;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_active == this) _active = null;
    }

    // 모서리가 불투명하고 가운데가 투명한 비네트 텍스처 생성
    static Texture2D BuildVignetteTexture(Color baseColor)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half; // -1 ~ 1
                float ny = (y - half) / half;

                // 원 기반 비네트: 중심에서 거리가 멀수록 진해짐
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01((dist - 0.4f) / 0.7f);
                alpha = alpha * alpha; // 부드러운 곡선

                byte a = (byte)(alpha * 255);
                byte r = (byte)(baseColor.r * 255);
                byte g = (byte)(baseColor.g * 255);
                byte b = (byte)(baseColor.b * 255);
                pixels[y * size + x] = new Color32(r, g, b, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
