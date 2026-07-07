using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class StartSceneTransition : MonoBehaviour
{
    [SerializeField] Button startButton;
    [SerializeField] Image fadeImage;
    [SerializeField] string targetSceneName = "TutorialScene";
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField, Min(0f)] float bgmFadeOutDuration = 0.9f;
    [SerializeField, Min(0f)] float nextSceneBgmFadeInDuration = 1.0f;

    bool isTransitioning;

    void Awake()
    {
        if (fadeImage != null)
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
    }

    void OnEnable()
    {
        if (startButton != null)
            startButton.onClick.AddListener(BeginTransition);
    }

    void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(BeginTransition);
    }

    public void BeginTransition()
    {
        BeginTransition(targetSceneName);
    }

    public void BeginTransition(string sceneName)
    {
        if (isTransitioning)
            return;

        if (!string.IsNullOrWhiteSpace(sceneName))
            targetSceneName = sceneName;

        StartCoroutine(FadeAndLoad());
    }

    public void BeginQuit()
    {
        if (isTransitioning)
            return;

        StartCoroutine(FadeAndQuit());
    }

    IEnumerator FadeAndLoad()
    {
        isTransitioning = true;
        if (startButton != null)
            startButton.interactable = false;

        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();

        // 화면 페이드와 BGM 페이드 아웃을 병행하고 둘 다 끝날 때까지 대기한다.
        SoundManager.FadeOutCurrentBgm(bgmFadeOutDuration);

        float duration = Mathf.Max(0.01f, fadeDuration);
        float totalWait = Mathf.Max(duration, bgmFadeOutDuration);
        float elapsed = 0f;
        while (elapsed < totalWait)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(alpha);
            yield return null;
        }

        SetFadeAlpha(1f);
        SoundManager.RequestBgmFadeInOnNextSceneLoad(nextSceneBgmFadeInDuration);
        GameSaveSystem.StartNewRun();
        // 시작 버튼은 항상 처음부터: 튜토리얼 완료 기록과 무관하게 지정한 씬(튜토리얼)으로 간다.
        SceneManager.LoadScene(targetSceneName);
    }

    IEnumerator FadeAndQuit()
    {
        isTransitioning = true;
        if (startButton != null)
            startButton.interactable = false;

        if (fadeImage != null)
            fadeImage.transform.SetAsLastSibling();

        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(alpha);
            if (alpha >= 1f)
                break;

            yield return null;
        }

        SetFadeAlpha(1f);

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
            return;

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}
