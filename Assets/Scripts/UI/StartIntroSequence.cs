using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StartIntroSequence : MonoBehaviour
{
    public static bool SkipNextIntro { get; set; }

    [Header("Timing")]
    [SerializeField] float fadeDuration = 1.25f;
    [SerializeField] float pauseAfterIntroTitleFade = 1.5f;
    [SerializeField] float titleDropDuration = 0.45f;
    [SerializeField] float titleDropOvershootDistance = 46f;

    [Header("Images")]
    [SerializeField] Image blackPanel;
    [SerializeField] Image introTitleImage;
    [SerializeField] Sprite introTitleSprite;
    [SerializeField] Image backgroundImage;
    [SerializeField] Image finalTitleImage;
    [SerializeField] Image[] bodyPartImages;

    [Header("Audio")]
    [SerializeField] AudioSource bgmSource;

    Vector2 finalTitlePosition;
    bool[] bodyPartRaycastTargets;

    void Awake()
    {
        AutoWireMissingReferences();
        SoundManager.ApplySavedVolumes();
        CacheOriginalState();

        if (SkipNextIntro)
        {
            SkipNextIntro = false;
            ApplySkippedIntroState();
            enabled = false;
            return;
        }

        PrepareIntroState();
    }

    IEnumerator Start()
    {
        if (!enabled)
            yield break;

        yield return RunIntroSequence();
    }

    void AutoWireMissingReferences()
    {
        if (blackPanel == null)
            blackPanel = FindImage("FadePanel");

        if (backgroundImage == null)
            backgroundImage = FindImage("StartBackground");

        if (finalTitleImage == null)
            finalTitleImage = FindImage("titleimage (1)");

        if (bgmSource == null)
        {
            GameObject bgmObject = GameObject.Find("bgm");
            if (bgmObject != null)
                bgmSource = bgmObject.GetComponent<AudioSource>();
        }

        if (bodyPartImages == null || bodyPartImages.Length == 0)
        {
            bodyPartImages = new[]
            {
                FindImage("lefthand"),
                FindImage("leftleg"),
                FindImage("righthand"),
                FindImage("rightleg"),
                FindImage("body")
            };
        }

        if (introTitleImage == null)
            introTitleImage = CreateIntroTitleImage();

        if (introTitleImage != null && introTitleSprite != null)
        {
            introTitleImage.sprite = introTitleSprite;
            introTitleImage.preserveAspect = false;
            StretchToParent(introTitleImage.rectTransform);
        }
    }

    Image FindImage(string objectName)
    {
        Transform child = transform.Find(objectName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    Image CreateIntroTitleImage()
    {
        GameObject titleObject = new GameObject("IntroTitle1");
        titleObject.transform.SetParent(transform, false);
        titleObject.AddComponent<CanvasRenderer>();
        Image image = titleObject.AddComponent<Image>();
        image.raycastTarget = false;

        image.preserveAspect = false;
        StretchToParent(image.rectTransform);

        return image;
    }

    void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    void CacheOriginalState()
    {
        if (finalTitleImage != null)
            finalTitlePosition = finalTitleImage.rectTransform.anchoredPosition;

        if (bodyPartImages != null)
        {
            bodyPartRaycastTargets = new bool[bodyPartImages.Length];
            for (int i = 0; i < bodyPartImages.Length; i++)
                bodyPartRaycastTargets[i] = bodyPartImages[i] != null && bodyPartImages[i].raycastTarget;
        }
    }

    void PrepareIntroState()
    {
        SetImageAlpha(blackPanel, 1f);
        SetImageAlpha(introTitleImage, 0f);
        SetImageAlpha(backgroundImage, 0f);
        SetImageAlpha(finalTitleImage, 0f);

        if (introTitleImage != null)
            introTitleImage.gameObject.SetActive(true);

        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(true);

        if (finalTitleImage != null)
        {
            finalTitleImage.gameObject.SetActive(false);
            finalTitleImage.rectTransform.anchoredPosition = finalTitlePosition;
        }

        SetBodyPartsAlpha(0f);
        SetBodyPartsRaycast(false);
        SetLayerOrder();

        if (bgmSource != null)
        {
            bgmSource.playOnAwake = false;
            bgmSource.Stop();
        }
    }

    void ApplySkippedIntroState()
    {
        SetImageAlpha(blackPanel, 0f);
        SetImageAlpha(introTitleImage, 0f);
        if (introTitleImage != null)
            introTitleImage.gameObject.SetActive(false);

        SetImageAlpha(backgroundImage, 1f);
        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(true);

        SetImageAlpha(finalTitleImage, 1f);
        if (finalTitleImage != null)
        {
            finalTitleImage.gameObject.SetActive(true);
            finalTitleImage.rectTransform.anchoredPosition = finalTitlePosition;
        }

        SetBodyPartsAlpha(1f);
        SetBodyPartsRaycast(true);
        SetLayerOrder();
        PlayBgmOnce();
    }

    void SetLayerOrder()
    {
        if (blackPanel != null)
            blackPanel.transform.SetAsFirstSibling();

        if (introTitleImage != null)
            introTitleImage.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));

        if (backgroundImage != null)
            backgroundImage.transform.SetSiblingIndex(Mathf.Min(2, transform.childCount - 1));

        if (finalTitleImage != null)
            finalTitleImage.transform.SetSiblingIndex(Mathf.Min(3, transform.childCount - 1));

        if (bodyPartImages != null)
            BringBodyPartsToFront();

        if (finalTitleImage != null)
            finalTitleImage.transform.SetAsLastSibling();
    }

    IEnumerator RunIntroSequence()
    {
        SetBodyPartsRaycast(false);

        yield return FadeImage(introTitleImage, 0f, 1f, fadeDuration);
        if (pauseAfterIntroTitleFade > 0f)
            yield return new WaitForSecondsRealtime(pauseAfterIntroTitleFade);

        yield return FadeBodyParts(0f, 1f, fadeDuration);
        yield return FadeImage(backgroundImage, 0f, 1f, fadeDuration);

        PlayBgmOnce();
        yield return DropFinalTitleRoutine();

        SetBodyPartsAlpha(1f);
        SetBodyPartsRaycast(true);
    }

    void PlayBgmOnce()
    {
        if (bgmSource == null || bgmSource.isPlaying)
            return;

        bgmSource.volume = SoundManager.GetBgmVolume01();
        bgmSource.Play();
    }

    IEnumerator DropFinalTitleRoutine()
    {
        if (finalTitleImage == null)
            yield break;

        RectTransform titleRect = finalTitleImage.rectTransform;
        RectTransform canvasRect = transform as RectTransform;
        float canvasHeight = canvasRect != null ? canvasRect.rect.height : 1080f;
        float titleHeight = titleRect.rect.height > 0f ? titleRect.rect.height : Mathf.Abs(titleRect.sizeDelta.y);
        Vector2 start = finalTitlePosition + Vector2.up * (canvasHeight + titleHeight);
        Vector2 overshoot = finalTitlePosition + Vector2.down * titleDropOvershootDistance;

        finalTitleImage.gameObject.SetActive(true);
        BringBodyPartsToFront();
        finalTitleImage.transform.SetAsLastSibling();
        SetImageAlpha(finalTitleImage, 1f);
        titleRect.anchoredPosition = start;

        float duration = Mathf.Max(0.01f, titleDropDuration);
        yield return MoveRect(titleRect, start, overshoot, duration * 0.38f);
        yield return MoveRect(titleRect, overshoot, finalTitlePosition, duration * 0.62f);
        titleRect.anchoredPosition = finalTitlePosition;
        BringBodyPartsToFront();
        finalTitleImage.transform.SetAsLastSibling();
    }

    void BringBodyPartsToFront()
    {
        if (bodyPartImages == null)
            return;

        for (int i = 0; i < bodyPartImages.Length; i++)
        {
            if (bodyPartImages[i] != null)
                bodyPartImages[i].transform.SetAsLastSibling();
        }
    }

    IEnumerator FadeBodyParts(float from, float to, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            SetBodyPartsAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetBodyPartsAlpha(to);
    }

    IEnumerator FadeImage(Image image, float from, float to, float duration)
    {
        if (image == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        SetImageAlpha(image, from);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            SetImageAlpha(image, Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetImageAlpha(image, to);
    }

    IEnumerator MoveRect(RectTransform rect, Vector2 start, Vector2 destination, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Smooth01(elapsed / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
            yield return null;
        }

        rect.anchoredPosition = destination;
    }

    float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    void SetBodyPartsAlpha(float alpha)
    {
        if (bodyPartImages == null)
            return;

        for (int i = 0; i < bodyPartImages.Length; i++)
            SetImageAlpha(bodyPartImages[i], alpha);
    }

    void SetBodyPartsRaycast(bool enabled)
    {
        if (bodyPartImages == null)
            return;

        for (int i = 0; i < bodyPartImages.Length; i++)
        {
            if (bodyPartImages[i] == null)
                continue;

            bool original = bodyPartRaycastTargets != null && i < bodyPartRaycastTargets.Length && bodyPartRaycastTargets[i];
            bodyPartImages[i].raycastTarget = enabled && original;
        }
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
