using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RoomPageTransition : MonoBehaviour
{
    const float TurnDuration = 0.38f;
    static RoomPageTransition activeTransition;

    string targetScene;
    RectTransform pageRect;
    Image foldShadow;

    public static bool IsTransitioning => activeTransition != null;

    public static void LoadScene(string sceneName)
    {
        if (activeTransition != null || string.IsNullOrWhiteSpace(sceneName))
            return;

        GameObject transitionObject = new GameObject("_RoomPageTransition");
        DontDestroyOnLoad(transitionObject);
        activeTransition = transitionObject.AddComponent<RoomPageTransition>();
        activeTransition.targetScene = sceneName;
    }

    void Awake()
    {
        BuildOverlay();
    }

    IEnumerator Start()
    {
        yield return TurnPageIn();
        SceneManager.LoadScene(targetScene);
        yield return null;
        yield return TurnPageOut();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (activeTransition == this)
            activeTransition = null;
    }

    void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject pageObject = new GameObject("TurningPage");
        pageObject.transform.SetParent(transform, false);
        Image page = pageObject.AddComponent<Image>();
        page.color = new Color(0.91f, 0.82f, 0.68f, 1f);
        page.raycastTarget = true;
        pageRect = page.rectTransform;
        pageRect.anchorMin = pageRect.anchorMax = new Vector2(0.5f, 0.5f);
        pageRect.pivot = new Vector2(1f, 0.5f);
        pageRect.anchoredPosition = new Vector2(960f, 0f);
        pageRect.sizeDelta = new Vector2(1920f, 1080f);
        pageRect.localScale = new Vector3(0.001f, 1f, 1f);

        GameObject shadowObject = new GameObject("PageFoldShadow");
        shadowObject.transform.SetParent(transform, false);
        foldShadow = shadowObject.AddComponent<Image>();
        foldShadow.color = new Color(0.22f, 0.12f, 0.07f, 0f);
        foldShadow.raycastTarget = false;
        RectTransform shadowRect = foldShadow.rectTransform;
        shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(42f, 1080f);
        shadowRect.anchoredPosition = new Vector2(960f, 0f);
    }

    IEnumerator TurnPageIn()
    {
        float elapsed = 0f;
        while (elapsed < TurnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Ease(Mathf.Clamp01(elapsed / TurnDuration));
            pageRect.localScale = new Vector3(Mathf.Max(0.001f, t), 1f, 1f);
            SetShadow(960f - 1920f * t, Mathf.Sin(t * Mathf.PI) * 0.42f);
            yield return null;
        }

        pageRect.localScale = Vector3.one;
        SetShadow(-960f, 0f);
    }

    IEnumerator TurnPageOut()
    {
        pageRect.pivot = new Vector2(0f, 0.5f);
        pageRect.anchoredPosition = new Vector2(-960f, 0f);
        pageRect.localScale = Vector3.one;

        float elapsed = 0f;
        while (elapsed < TurnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Ease(Mathf.Clamp01(elapsed / TurnDuration));
            float scale = Mathf.Max(0.001f, 1f - t);
            pageRect.localScale = new Vector3(scale, 1f, 1f);
            SetShadow(-960f + 1920f * scale, Mathf.Sin(t * Mathf.PI) * 0.42f);
            yield return null;
        }
    }

    void SetShadow(float x, float alpha)
    {
        RectTransform rect = foldShadow.rectTransform;
        rect.anchoredPosition = new Vector2(x, 0f);
        Color color = foldShadow.color;
        color.a = alpha;
        foldShadow.color = color;
    }

    static float Ease(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
