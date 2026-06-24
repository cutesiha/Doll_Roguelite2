using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomSceneFadeIn : MonoBehaviour
{
    const string RoomSceneName = "RoomScene";
    const string OverlayObjectName = "_RoomSceneFadeIn";
    const float DefaultFadeDuration = 0.45f;

    [SerializeField] float fadeDuration = DefaultFadeDuration;
    Image fadeImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        CreateForScene(SceneManager.GetActiveScene());
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateForScene(scene);
    }

    static void CreateForScene(Scene scene)
    {
        if (!scene.IsValid()
            || scene.name != RoomSceneName
            || RoomPageTransition.IsTransitioning
            || GameObject.Find(OverlayObjectName) != null)
            return;

        GameObject fadeObject = new GameObject(OverlayObjectName);
        SceneManager.MoveGameObjectToScene(fadeObject, scene);
        fadeObject.AddComponent<RoomSceneFadeIn>();
    }

    void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        gameObject.AddComponent<CanvasRenderer>();

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(transform, false);
        imageObject.AddComponent<CanvasRenderer>();
        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    IEnumerator Start()
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetAlpha(0f);
        Destroy(gameObject);
    }

    void SetAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}
