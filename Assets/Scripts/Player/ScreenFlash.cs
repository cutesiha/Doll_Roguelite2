using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScreenFlash : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float maxAlpha = 0.42f;
    [SerializeField, Min(0.05f)] float fadeInDuration = 0.05f;
    [SerializeField, Min(0.05f)] float fadeOutDuration = 0.28f;
    // 순수 빨강 대신 주황/갈색이 살짝 섞인 연한 붉은색
    [SerializeField] Color flashColor = new Color(0.92f, 0.42f, 0.3f, 1f);

    static ScreenFlash instance;
    Image flashImage;
    Coroutine flashRoutine;

    public static void FlashRed()
    {
        if (instance == null)
            instance = CreateInstance();

        if (instance != null)
            instance.Play();
    }

    static ScreenFlash CreateInstance()
    {
        GameObject canvasGO = new GameObject("ScreenFlashCanvas");
        DontDestroyOnLoad(canvasGO);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject imageGO = new GameObject("FlashImage");
        imageGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rect = imageGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        Image img = imageGO.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 0f, 0f, 0f);

        ScreenFlash flash = canvasGO.AddComponent<ScreenFlash>();
        flash.flashImage = img;
        return flash;
    }

    void Play()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        if (flashImage == null)
            yield break;

        Color baseColor = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        Color peakColor = new Color(flashColor.r, flashColor.g, flashColor.b, maxAlpha);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            flashImage.color = Color.Lerp(baseColor, peakColor, elapsed / fadeInDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        flashImage.color = peakColor;
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            flashImage.color = Color.Lerp(peakColor, baseColor, elapsed / fadeOutDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        flashImage.color = baseColor;
        flashRoutine = null;
    }
}
