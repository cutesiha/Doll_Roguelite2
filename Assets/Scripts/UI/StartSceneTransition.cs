using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartSceneTransition : MonoBehaviour
{
    [SerializeField] Button startButton;
    [SerializeField] Image fadeImage;
    [SerializeField] string targetSceneName = "TutorialScene";
    [SerializeField] float fadeDuration = 0.35f;

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
        if (isTransitioning)
            return;

        StartCoroutine(FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        isTransitioning = true;
        if (startButton != null)
            startButton.interactable = false;

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
        SceneManager.LoadScene(targetSceneName);
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
