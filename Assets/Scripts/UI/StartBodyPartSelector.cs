using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StartBodyPartSelector : MonoBehaviour
{
    [SerializeField] StartSceneTransition transition;
    [SerializeField] GameObject optionPanel;
    [SerializeField] string targetSceneName = "RoomScene";
    [SerializeField] float moveDuration = 0.30f;
    [SerializeField] Color hoverColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField, Range(0f, 1f)] float bodyPartAlphaHitThreshold = 0.1f;
    [SerializeField] Sprite optionPanelSprite;
    [SerializeField] Sprite optionLabelSprite;
    [SerializeField] Vector2 optionPanelSize = new Vector2(900f, 562.5f);
    [SerializeField] Vector2 optionClosePosition = new Vector2(354f, 142f);
    [SerializeField] Vector2 optionCloseSize = new Vector2(64f, 64f);
    [SerializeField] float panelSlideDuration = 0.36f;
    [SerializeField] float panelOvershootDistance = 46f;
    [SerializeField] Image eyeAnimationImage;
    [SerializeField] Sprite[] eyeOpenFrames;
    [SerializeField] float[] eyeFrameDurations = { 0.12f, 0.10f, 0.08f, 0.25f };
    [SerializeField] float eyeFinalHoldDuration = 0.5f;
    [SerializeField] Image startBackgroundImage;
    [SerializeField] Image finalTitleImage;
    [SerializeField, Min(0f)] float startBackdropFadeDuration = 0.42f;
    [SerializeField, Min(0f)] float startSequenceInitialDelay = 0.3f;
    [SerializeField] float autoAttachPause = 0.05f;
    [SerializeField] float panelShowDelayAfterAttach = 0.12f;
    [SerializeField] GameObject exitPanel;
    [SerializeField, Range(0f, 1f)] float exitAnswerAlphaHitThreshold = 0.1f;
    [SerializeField] Sprite roadPanelSprite;
    [SerializeField] Sprite roadBackgroundSprite;
    [SerializeField] Sprite roadPanelOverlaySprite;
    [SerializeField] Sprite roadButtonSprite;
    [SerializeField] Vector2 roadPanelSize = new Vector2(900f, 562.5f);
    [SerializeField] Vector2 roadClosePosition = new Vector2(354f, 142f);
    [SerializeField] Vector2 roadCloseSize = new Vector2(180f, 180f);
    [SerializeField] Vector2 roadButtonBasePosition = Vector2.zero;
    [SerializeField] float roadButtonFloatAmplitude = 6f;
    [SerializeField] float roadButtonFloatSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] float roadButtonAlphaHitThreshold = 0.1f;
    StartBodyPartChoice leftHandChoice;
    StartBodyPartChoice leftLegChoice;
    StartBodyPartChoice rightHandChoice;
    StartBodyPartChoice rightLegChoice;
    Button optionCloseButton;
    Button roadCloseButton;
    Image roadButtonImage;
    RectTransform roadButtonRect;
    GameObject roadPanel;
    GameObject quitPanel;
    SpriteRenderer exitQuestionRenderer;
    SpriteRenderer exitAnswer1Renderer;
    SpriteRenderer exitAnswer2Renderer;
    CanvasGroup exitPanelCanvasGroup;
    Image exitQuestionImage;
    Image exitAnswer1Image;
    Image exitAnswer2Image;
    TextMeshPro exitQuestionText;
    TextMeshPro exitAnswer1Text;
    TextMeshPro exitAnswer2Text;
    TextMeshProUGUI exitQuestionUIText;
    TextMeshProUGUI exitAnswer1UIText;
    TextMeshProUGUI exitAnswer2UIText;
    Color exitQuestionColor = Color.white;
    Color exitAnswer1Color = Color.white;
    Color exitAnswer2Color = Color.white;
    RectTransform optionPanelRect;
    Vector2 optionPanelCenterPosition;
    Vector2 optionPanelHiddenPosition;
    Coroutine optionPanelRoutine;
    RectTransform roadPanelRect;
    Vector2 roadPanelCenterPosition;
    Vector2 roadPanelHiddenPosition;
    Coroutine roadPanelRoutine;
    Coroutine roadButtonFloatRoutine;
    Coroutine startSequenceRoutine;
    Coroutine delayedChoicePanelRoutine;
    bool isEyeAnimationPlaying;
    bool optionPanelClosing;
    bool roadPanelClosing;
    float lastPanelOpenSoundTime = -1f;
    const float PanelOpenSoundRepeatGuard = 0.2f;
    const float HiddenButtonAlpha = 0.001f;

    void Awake()
    {
        if (transition == null)
            transition = GetComponentInChildren<StartSceneTransition>(true);

        if (optionPanel == null)
        {
            Transform option = transform.Find("OptionPanel");
            if (option != null)
                optionPanel = option.gameObject;
        }

        if (exitPanel == null)
        {
            Transform exit = transform.Find("ExitPanel");
            if (exit != null)
                exitPanel = exit.gameObject;
        }

        leftHandChoice = ConfigureChoice("lefthand", new Vector2(-163f, -88f), StartBodyPartChoice.ClickAction.LoadScene);
        leftLegChoice = ConfigureChoice("leftleg", new Vector2(-92f, -311f), StartBodyPartChoice.ClickAction.ShowOptionPanel);
        rightLegChoice = ConfigureChoice("rightleg", new Vector2(64f, -317f), StartBodyPartChoice.ClickAction.ShowQuitPanel);
        rightHandChoice = ConfigureChoice("righthand", new Vector2(131f, -90f), StartBodyPartChoice.ClickAction.ShowRoadPanel);

        Transform hotspot = transform.Find("StartHotspotButton");
        if (hotspot != null)
            hotspot.gameObject.SetActive(false);

        EnsureOptionPanelVisuals();
        EnsureOptionCloseButton();
        EnsureRoadPanel();
        DisablePanelChildAudioSources();
        EnsureQuitPanel();
        CacheOptionPanelPositions();
        CacheRoadPanelPositions();

        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (roadPanel != null)
            roadPanel.SetActive(false);

        if (quitPanel != null)
            quitPanel.SetActive(false);

        AutoWireStartBackdropImages();
        PrepareEyeAnimationImage();
    }

    void Update()
    {
        HandlePanelCloseHotkeys();

        if (exitAnswer1Image != null)
            return;

        HandleExitPanelPointer();
    }

    StartBodyPartChoice ConfigureChoice(string childName, Vector2 selectedPosition, StartBodyPartChoice.ClickAction action)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            return null;

        Image image = child.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            image.alphaHitTestMinimumThreshold = bodyPartAlphaHitThreshold;
        }

        StartBodyPartChoice choice = child.GetComponent<StartBodyPartChoice>();
        if (choice == null)
            choice = child.gameObject.AddComponent<StartBodyPartChoice>();

        choice.Configure(this, selectedPosition, action, hoverColor, moveDuration);
        return choice;
    }

    void EnsureOptionCloseButton()
    {
        if (optionPanel == null)
            return;

        Transform existing = optionPanel.transform.Find("OptionCloseHotspot");
        bool created = existing == null;
        GameObject hotspot = existing != null ? existing.gameObject : new GameObject("OptionCloseHotspot");
        hotspot.transform.SetParent(optionPanel.transform, false);
        hotspot.SetActive(true);
        hotspot.transform.SetAsLastSibling();

        RectTransform rect = hotspot.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = hotspot.AddComponent<RectTransform>();
            created = true;
        }

        if (created)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = optionClosePosition;
            rect.sizeDelta = optionCloseSize;
        }

        Image image = hotspot.GetComponent<Image>();
        if (image == null)
        {
            image = hotspot.AddComponent<Image>();
        }

        image.color = new Color(1f, 1f, 1f, HiddenButtonAlpha);
        image.raycastTarget = true;

        optionCloseButton = hotspot.GetComponent<Button>();
        if (optionCloseButton == null)
            optionCloseButton = hotspot.AddComponent<Button>();

        optionCloseButton.transition = Selectable.Transition.None;
        optionCloseButton.targetGraphic = image;
        ApplyHiddenButtonHoverTint(optionCloseButton);
        optionCloseButton.onClick.RemoveListener(CloseOptionPanel);
        optionCloseButton.onClick.AddListener(CloseOptionPanel);
    }

    void EnsureOptionPanelVisuals()
    {
        if (optionPanel == null)
            return;

        Image panelImage = optionPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            if (panelImage.sprite == null && optionPanelSprite != null)
                panelImage.sprite = optionPanelSprite;

            panelImage.raycastTarget = true;
        }

        Image labelImage = EnsureOptionLabelImage();
        ConfigureOptionText(labelImage != null ? labelImage.transform : optionPanel.transform);
    }

    Image EnsureOptionLabelImage()
    {
        Transform existing = optionPanel.transform.Find("OptionLabel");
        bool created = existing == null;
        GameObject labelObject = existing != null ? existing.gameObject : new GameObject("OptionLabel");
        labelObject.transform.SetParent(optionPanel.transform, false);
        if (created)
            labelObject.SetActive(true);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = labelObject.AddComponent<RectTransform>();
            StretchToParent(rect);
        }
        else if (created)
        {
            StretchToParent(rect);
        }

        Image image = labelObject.GetComponent<Image>();
        if (image == null)
        {
            image = labelObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = false;
        }

        if (image.sprite == null && optionLabelSprite != null)
            image.sprite = optionLabelSprite;

        image.raycastTarget = false;
        return image;
    }

    void ConfigureOptionText(Transform parent)
    {
        ConfigureOptionLabelText(parent, "OptionTitleText", "설정 <<", new Vector2(-285f, 133f), new Vector2(180f, 58f), 34f);
        ConfigureOptionLabelText(parent, "BgmVolumeText", "BGM 음량", new Vector2(-282f, 62f), new Vector2(230f, 44f), 30f);
        ConfigureOptionLabelText(parent, "SfxVolumeText", "SFX 음량", new Vector2(-282f, -50f), new Vector2(230f, 44f), 30f);
    }

    void ConfigureOptionLabelText(Transform parent, string textName, string textValue, Vector2 position, Vector2 size, float fontSize)
    {
        bool created = parent.Find(textName) == null;
        TextMeshProUGUI text = EnsureTMPText(parent, textName);
        text.raycastTarget = false;

        if (!created)
            return;

        text.font = UIThinDungFont.Get();
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public void HandleChoiceComplete(StartBodyPartChoice choice, StartBodyPartChoice.ClickAction action)
    {
        if (action == StartBodyPartChoice.ClickAction.LoadScene)
        {
            if (startSequenceRoutine == null)
                startSequenceRoutine = StartCoroutine(StartSequenceRoutine());

            return;
        }

        if (action == StartBodyPartChoice.ClickAction.ShowOptionPanel ||
            action == StartBodyPartChoice.ClickAction.ShowRoadPanel ||
            action == StartBodyPartChoice.ClickAction.ShowQuitPanel)
        {
            if (delayedChoicePanelRoutine != null)
                StopCoroutine(delayedChoicePanelRoutine);

            delayedChoicePanelRoutine = StartCoroutine(DelayedChoicePanelRoutine(action));
        }
    }

    System.Collections.IEnumerator DelayedChoicePanelRoutine(StartBodyPartChoice.ClickAction action)
    {
        if (panelShowDelayAfterAttach > 0f)
            yield return new WaitForSecondsRealtime(panelShowDelayAfterAttach);

        delayedChoicePanelRoutine = null;
        ShowChoicePanel(action);
    }

    void ShowChoicePanel(StartBodyPartChoice.ClickAction action)
    {
        if (action == StartBodyPartChoice.ClickAction.ShowOptionPanel)
            ShowOptionPanel();
        else if (action == StartBodyPartChoice.ClickAction.ShowRoadPanel)
            ShowRoadPanel();
        else if (action == StartBodyPartChoice.ClickAction.ShowQuitPanel)
            ShowQuitPanel();
    }

    System.Collections.IEnumerator StartSequenceRoutine()
    {
        SetChoicesInputEnabled(false);
        HidePanels();

        if (startSequenceInitialDelay > 0f)
            yield return new WaitForSecondsRealtime(startSequenceInitialDelay);

        yield return AttachChoiceRoutine(leftHandChoice);
        yield return AttachChoiceRoutine(rightHandChoice);
        yield return AttachChoiceRoutine(leftLegChoice);
        yield return AttachChoiceRoutine(rightLegChoice);
        yield return FadeStartBackdropOutRoutine();
        yield return PlayEyeOpeningRoutine();

        if (transition != null)
            transition.BeginTransition(targetSceneName);
    }

    System.Collections.IEnumerator AttachChoiceRoutine(StartBodyPartChoice choice)
    {
        if (choice == null)
            yield break;

        bool done = false;
        choice.AttachToBody(() => done = true);
        while (!done)
            yield return null;

        if (autoAttachPause > 0f)
            yield return new WaitForSecondsRealtime(autoAttachPause);
    }

    System.Collections.IEnumerator PlayEyeOpeningRoutine()
    {
        if (isEyeAnimationPlaying)
            yield break;

        if (eyeAnimationImage == null || eyeOpenFrames == null || eyeOpenFrames.Length == 0)
            yield break;

        isEyeAnimationPlaying = true;
        eyeAnimationImage.gameObject.SetActive(true);
        eyeAnimationImage.transform.SetAsLastSibling();
        SetImageAlpha(eyeAnimationImage, 1f);

        Sprite lastFrame = null;
        for (int i = 0; i < eyeOpenFrames.Length; i++)
        {
            if (eyeOpenFrames[i] == null)
                continue;

            eyeAnimationImage.sprite = eyeOpenFrames[i];
            lastFrame = eyeOpenFrames[i];

            float duration = GetEyeFrameDuration(i);
            SetImageAlpha(eyeAnimationImage, 1f);
            if (duration > 0f)
                yield return new WaitForSecondsRealtime(duration);
        }

        if (lastFrame != null)
            eyeAnimationImage.sprite = lastFrame;

        if (eyeFinalHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(eyeFinalHoldDuration);

        isEyeAnimationPlaying = false;
    }

    System.Collections.IEnumerator FadeStartBackdropOutRoutine()
    {
        AutoWireStartBackdropImages();

        if (startBackgroundImage == null && finalTitleImage == null)
        {
            List<Image> fallbackImages = CollectStartBackdropImages();
            if (fallbackImages.Count == 0)
                yield break;
        }

        float duration = Mathf.Max(0f, startBackdropFadeDuration);
        List<Image> fadeImages = CollectStartBackdropImages();
        List<float> startAlphas = new List<float>(fadeImages.Count);
        for (int i = 0; i < fadeImages.Count; i++)
            startAlphas.Add(GetImageAlpha(fadeImages[i]));

        if (duration <= 0f)
        {
            for (int i = 0; i < fadeImages.Count; i++)
            {
                SetImageAlpha(fadeImages[i], 0f);
                DeactivateFadedImage(fadeImages[i]);
            }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < fadeImages.Count; i++)
                SetImageAlpha(fadeImages[i], Mathf.Lerp(startAlphas[i], 0f, t));
            yield return null;
        }

        for (int i = 0; i < fadeImages.Count; i++)
        {
            SetImageAlpha(fadeImages[i], 0f);
            DeactivateFadedImage(fadeImages[i]);
        }
    }

    void AutoWireStartBackdropImages()
    {
        if (startBackgroundImage == null)
            startBackgroundImage = FindChildImage("StartBackground");

        if (finalTitleImage == null)
            finalTitleImage = FindChildImage("titleimage (1)");
    }

    Image FindChildImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    List<Image> CollectStartBackdropImages()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        List<Image> results = new List<Image>(images.Length);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || ShouldKeepDuringBackdropFade(image))
                continue;

            if (!results.Contains(image))
                results.Add(image);
        }

        return results;
    }

    bool ShouldKeepDuringBackdropFade(Image image)
    {
        Transform current = image.transform;
        while (current != null && current != transform)
        {
            string objectName = current.name;
            if (objectName == "body" ||
                objectName == "lefthand" ||
                objectName == "righthand" ||
                objectName == "leftleg" ||
                objectName == "rightleg" ||
                objectName == "EyeOpeningAnimation" ||
                objectName == "FadePanel")
                return true;

            current = current.parent;
        }

        return image.GetComponentInParent<StartSceneTransition>(true) != null;
    }

    float GetImageAlpha(Image image)
    {
        return image != null ? image.color.a : 0f;
    }

    void DeactivateFadedImage(Image image)
    {
        if (image != null)
            image.gameObject.SetActive(false);
    }

    void PrepareEyeAnimationImage()
    {
        if (eyeAnimationImage == null)
        {
            Transform existing = transform.Find("EyeOpeningAnimation");
            if (existing != null)
                eyeAnimationImage = existing.GetComponent<Image>();
        }

        if (eyeAnimationImage == null)
            return;

        eyeAnimationImage.raycastTarget = false;
        SetImageAlpha(eyeAnimationImage, 0f);
        eyeAnimationImage.gameObject.SetActive(false);
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    float GetEyeFrameDuration(int frameIndex)
    {
        if (eyeFrameDurations == null || eyeFrameDurations.Length == 0)
            return 0.05f;

        int durationIndex = Mathf.Clamp(frameIndex, 0, eyeFrameDurations.Length - 1);
        return Mathf.Max(0f, eyeFrameDurations[durationIndex]);
    }

    void SetChoicesInputEnabled(bool enabled)
    {
        if (leftHandChoice != null)
            leftHandChoice.SetInputEnabled(enabled);

        if (rightHandChoice != null)
            rightHandChoice.SetInputEnabled(enabled);

        if (leftLegChoice != null)
            leftLegChoice.SetInputEnabled(enabled);

        if (rightLegChoice != null)
            rightLegChoice.SetInputEnabled(enabled);
    }

    void HidePanels()
    {
        if (optionPanelRoutine != null)
        {
            StopCoroutine(optionPanelRoutine);
            optionPanelRoutine = null;
        }

        if (roadPanelRoutine != null)
        {
            StopCoroutine(roadPanelRoutine);
            roadPanelRoutine = null;
        }

        if (roadButtonFloatRoutine != null)
        {
            StopCoroutine(roadButtonFloatRoutine);
            roadButtonFloatRoutine = null;
        }

        if (delayedChoicePanelRoutine != null)
        {
            StopCoroutine(delayedChoicePanelRoutine);
            delayedChoicePanelRoutine = null;
        }

        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (roadPanel != null)
            roadPanel.SetActive(false);

        if (quitPanel != null)
            quitPanel.SetActive(false);
    }

    void ShowOptionPanel()
    {
        if (optionPanel == null || optionPanelRect == null)
            return;

        optionPanelClosing = false;
        if (optionPanelRoutine != null)
            StopCoroutine(optionPanelRoutine);

        optionPanel.SetActive(true);
        optionPanel.transform.SetAsLastSibling();
        optionPanelRect.anchoredPosition = optionPanelHiddenPosition;

        if (optionCloseButton != null)
            optionCloseButton.gameObject.SetActive(true);

        PlayInventoryPanelOpenSound();
        optionPanelRoutine = StartCoroutine(SlideOptionPanel(
            optionPanelHiddenPosition,
            optionPanelCenterPosition,
            optionPanelCenterPosition + Vector2.up * panelOvershootDistance,
            null));
    }

    public void CloseOptionPanel()
    {
        if (optionPanelClosing)
            return;

        if (optionPanel == null || optionPanelRect == null)
        {
            if (leftLegChoice != null)
                leftLegChoice.ReturnHome();
            return;
        }

        optionPanelClosing = true;
        if (optionPanelRoutine != null)
            StopCoroutine(optionPanelRoutine);

        PlayInventoryPanelCloseSound();
        optionPanelRoutine = StartCoroutine(SlideOptionPanel(
            optionPanelRect.anchoredPosition,
            optionPanelHiddenPosition,
            optionPanelRect.anchoredPosition + Vector2.up * panelOvershootDistance,
            () =>
        {
            optionPanel.SetActive(false);

            if (leftLegChoice != null)
                leftLegChoice.ReturnHome();

            optionPanelClosing = false;
        }));
    }

    void CacheOptionPanelPositions()
    {
        if (optionPanel == null)
            return;

        optionPanelRect = optionPanel.GetComponent<RectTransform>();
        if (optionPanelRect == null)
            return;

        optionPanelCenterPosition = optionPanelRect.anchoredPosition;
        RectTransform canvasRect = GetComponent<RectTransform>();
        float canvasHeight = canvasRect != null ? canvasRect.rect.height : 1080f;
        float panelHeight = optionPanelRect.rect.height > 0f ? optionPanelRect.rect.height : canvasHeight;
        optionPanelHiddenPosition = optionPanelCenterPosition + new Vector2(0f, -(canvasHeight + panelHeight) * 0.55f);
        optionPanelRect.anchoredPosition = optionPanelCenterPosition;
    }

    System.Collections.IEnumerator SlideOptionPanel(Vector2 start, Vector2 destination, Vector2 reactionPosition, System.Action onComplete)
    {
        float duration = Mathf.Max(0.01f, panelSlideDuration);
        yield return SlidePanelSegment(optionPanelRect, start, reactionPosition, duration * 0.38f);
        yield return SlidePanelSegment(optionPanelRect, reactionPosition, destination, duration * 0.62f);

        optionPanelRect.anchoredPosition = destination;
        optionPanelRoutine = null;
        onComplete?.Invoke();
    }

    System.Collections.IEnumerator SlideRoadPanel(Vector2 start, Vector2 destination, Vector2 reactionPosition, System.Action onComplete)
    {
        float duration = Mathf.Max(0.01f, panelSlideDuration);
        yield return SlidePanelSegment(roadPanelRect, start, reactionPosition, duration * 0.38f);
        yield return SlidePanelSegment(roadPanelRect, reactionPosition, destination, duration * 0.62f);

        roadPanelRect.anchoredPosition = destination;
        roadPanelRoutine = null;
        onComplete?.Invoke();
    }

    System.Collections.IEnumerator SlidePanelSegment(RectTransform panelRect, Vector2 start, Vector2 destination, float duration)
    {
        if (panelRect == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            panelRect.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
            if (t >= 1f)
                break;

            yield return null;
        }

        panelRect.anchoredPosition = destination;
    }

    public void PlayInventoryPanelOpenSound()
    {
        if (Time.unscaledTime - lastPanelOpenSoundTime < PanelOpenSoundRepeatGuard)
            return;

        lastPanelOpenSoundTime = Time.unscaledTime;
        SoundManager.PlayPanel();
    }

    void PlayInventoryPanelCloseSound()
    {
        SoundManager.PlayPanel();
    }

    public void PlayBodyPartSlimeSound()
    {
        SoundManager.PlaySlime();
    }

    void PlayExitClickSound()
    {
        SoundManager.PlayClick();
    }

    void DisablePanelChildAudioSources()
    {
        SoundManager.DisableAudioSourcesInChildren(optionPanel);

        Transform road = transform.Find("RoadPanel");
        if (road != null)
            SoundManager.DisableAudioSourcesInChildren(road.gameObject);
    }

    void EnsureRoadPanel()
    {
        Transform existing = transform.Find("RoadPanel");
        bool created = existing == null;
        roadPanel = existing != null ? existing.gameObject : CreatePanel("RoadPanel", roadPanelSize);
        roadPanel.transform.SetParent(transform, false);

        Image panelImage = roadPanel.GetComponent<Image>();
        if (panelImage == null)
            panelImage = roadPanel.AddComponent<Image>();

        if (panelImage.sprite == null)
        {
            if (roadBackgroundSprite != null)
                panelImage.sprite = roadBackgroundSprite;
            else if (roadPanelSprite != null)
                panelImage.sprite = roadPanelSprite;
        }

        panelImage.raycastTarget = true;

        Image overlayImage = EnsureRoadLayerImage("RoadPanelOverlay", roadPanelOverlaySprite, false);
        if (overlayImage != null && created)
            overlayImage.transform.SetAsLastSibling();

        roadButtonImage = EnsureRoadLayerImage("RoadButton", roadButtonSprite, true);
        if (roadButtonImage != null)
        {
            if (created)
                roadButtonImage.transform.SetAsLastSibling();

            roadButtonRect = roadButtonImage.rectTransform;
            roadButtonBasePosition = roadButtonRect.anchoredPosition;
            ConfigureRoadButton(roadButtonImage);
        }

        ConfigureRoadText(overlayImage != null ? overlayImage.transform : roadPanel.transform);
        EnsureRoadSaveSlotHotspots();

        roadCloseButton = EnsureButton(roadPanel.transform, "RoadCloseHotspot", roadClosePosition, roadCloseSize, new Color(1f, 1f, 1f, HiddenButtonAlpha));
        roadCloseButton.gameObject.SetActive(true);
        RectTransform roadCloseRect = roadCloseButton.GetComponent<RectTransform>();
        if (roadCloseRect != null)
        {
            roadCloseRect.anchorMin = new Vector2(0.5f, 0.5f);
            roadCloseRect.anchorMax = new Vector2(0.5f, 0.5f);
            roadCloseRect.pivot = new Vector2(0.5f, 0.5f);
            roadCloseRect.anchoredPosition = roadClosePosition;
            roadCloseRect.sizeDelta = roadCloseSize;
        }

        Image roadCloseImage = roadCloseButton.GetComponent<Image>();
        if (roadCloseImage != null)
        {
            roadCloseImage.color = new Color(1f, 1f, 1f, HiddenButtonAlpha);
            roadCloseImage.raycastTarget = true;
        }

        ApplyHiddenButtonHoverTint(roadCloseButton);
        roadCloseButton.onClick.RemoveListener(CloseRoadPanel);
        roadCloseButton.onClick.AddListener(CloseRoadPanel);
        roadCloseButton.transform.SetAsLastSibling();

    }

    Image EnsureRoadLayerImage(string childName, Sprite sprite, bool raycastTarget)
    {
        Transform existing = roadPanel.transform.Find(childName);
        bool created = existing == null;
        GameObject layerObject = existing != null ? existing.gameObject : new GameObject(childName);
        layerObject.transform.SetParent(roadPanel.transform, false);
        if (created)
            layerObject.SetActive(true);

        RectTransform rect = layerObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = layerObject.AddComponent<RectTransform>();
            StretchToParent(rect);
        }
        else if (created)
        {
            StretchToParent(rect);
        }

        Image image = layerObject.GetComponent<Image>();
        if (image == null)
        {
            image = layerObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = false;
        }

        if (image.sprite == null && sprite != null)
            image.sprite = sprite;

        image.raycastTarget = raycastTarget;
        return image;
    }

    void ConfigureRoadButton(Image image)
    {
        if (image == null)
            return;

        image.raycastTarget = false;

        try
        {
            image.alphaHitTestMinimumThreshold = roadButtonAlphaHitThreshold;
        }
        catch (System.InvalidOperationException)
        {
            // Read/write import settings are applied from the editor setup path.
        }

        Button button = image.GetComponent<Button>();
        if (button == null)
        {
            button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            ApplyButtonTint(button, Color.white, new Color(1.18f, 1.18f, 1.18f, 1f));
        }

        button.interactable = false;
        button.enabled = false;
    }

    void ConfigureRoadText(Transform parent)
    {
        ConfigureRoadLabelText(parent, "RoadTitleText", "저장 <<", new Vector2(-285f, 133f), new Vector2(180f, 58f), 34f);
    }

    void ConfigureRoadLabelText(Transform parent, string textName, string textValue, Vector2 position, Vector2 size, float fontSize)
    {
        bool created = parent.Find(textName) == null;
        TextMeshProUGUI text = EnsureTMPText(parent, textName);
        text.raycastTarget = false;

        if (!created)
            return;

        text.font = UIThinDungFont.Get();
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    void EnsureRoadSaveSlotHotspots()
    {
        float[] rowY = { 37f, -17f, -71f, -125f };
        for (int i = 0; i < rowY.Length; i++)
        {
            EnsureRoadSlotButton("RoadSaveSlot" + (i + 1) + "Left", new Vector2(-285f, rowY[i]), new Vector2(169f, 52f));
            EnsureRoadSlotButton("RoadSaveSlot" + (i + 1) + "Right", new Vector2(87f, rowY[i]), new Vector2(574f, 52f));
        }
    }

    void EnsureRoadSlotButton(string buttonName, Vector2 position, Vector2 size)
    {
        Button slot = EnsureButton(roadPanel.transform, buttonName, position, size, new Color(1f, 1f, 1f, HiddenButtonAlpha));
        slot.transition = Selectable.Transition.None;
        slot.onClick.RemoveAllListeners();
        slot.transform.SetAsLastSibling();
    }

    void CacheRoadPanelPositions()
    {
        if (roadPanel == null)
            return;

        roadPanelRect = roadPanel.GetComponent<RectTransform>();
        if (roadPanelRect == null)
            return;

        roadPanelCenterPosition = roadPanelRect.anchoredPosition;
        RectTransform canvasRect = GetComponent<RectTransform>();
        float canvasHeight = canvasRect != null ? canvasRect.rect.height : 1080f;
        float panelHeight = roadPanelRect.rect.height > 0f ? roadPanelRect.rect.height : canvasHeight;
        roadPanelHiddenPosition = roadPanelCenterPosition + new Vector2(0f, -(canvasHeight + panelHeight) * 0.55f);
        roadPanelRect.anchoredPosition = roadPanelCenterPosition;
    }

    void EnsureQuitPanel()
    {
        Transform oldPanel = transform.Find("QuitConfirmPanel");
        if (oldPanel != null)
            Destroy(oldPanel.gameObject);

        if (exitPanel == null)
        {
            Transform existing = transform.Find("ExitPanel");
            if (existing != null)
                exitPanel = existing.gameObject;
        }

        quitPanel = exitPanel;
        if (quitPanel == null)
            return;

        Transform question = quitPanel.transform.Find("question");
        Transform answer1 = quitPanel.transform.Find("answer1");
        Transform answer2 = quitPanel.transform.Find("answer2");

        exitQuestionRenderer = question != null ? question.GetComponent<SpriteRenderer>() : null;
        exitAnswer1Renderer = answer1 != null ? answer1.GetComponent<SpriteRenderer>() : null;
        exitAnswer2Renderer = answer2 != null ? answer2.GetComponent<SpriteRenderer>() : null;

        if (exitQuestionRenderer != null)
        {
            exitQuestionColor = exitQuestionRenderer.color;
            exitQuestionColor.a = 1f;
        }

        if (exitAnswer1Renderer != null)
        {
            exitAnswer1Color = exitAnswer1Renderer.color;
            exitAnswer1Color.a = 1f;
        }

        if (exitAnswer2Renderer != null)
        {
            exitAnswer2Color = exitAnswer2Renderer.color;
            exitAnswer2Color.a = 1f;
        }

        exitQuestionText = EnsureWorldTMPText(question, "QuestionText", "정말로 게임을 종료하시겠습니까?", Color.black, 0.24f);
        exitAnswer1Text = EnsureWorldTMPText(answer1, "AnswerText", "예", Color.black, 0.42f);
        exitAnswer2Text = EnsureWorldTMPText(answer2, "AnswerText", "아니오", Color.black, 0.34f);

        exitPanelCanvasGroup = quitPanel.GetComponent<CanvasGroup>();
        if (exitPanelCanvasGroup == null)
            exitPanelCanvasGroup = quitPanel.AddComponent<CanvasGroup>();

        exitQuestionImage = GetExitUIImage("question_ui");
        exitAnswer1Image = GetExitUIImage("answer1_ui");
        exitAnswer2Image = GetExitUIImage("answer2_ui");
        exitQuestionUIText = exitQuestionImage != null ? EnsureTMPText(exitQuestionImage.transform, "QuestionLabel") : null;
        exitAnswer1UIText = exitAnswer1Image != null ? EnsureTMPText(exitAnswer1Image.transform, "AnswerLabel") : null;
        exitAnswer2UIText = exitAnswer2Image != null ? EnsureTMPText(exitAnswer2Image.transform, "AnswerLabel") : null;

        ConfigureExitUIText(exitQuestionUIText, "정말로 게임을 종료하시겠습니까?", 46f);
        ConfigureExitUIText(exitAnswer1UIText, "예", 52f);
        ConfigureExitUIText(exitAnswer2UIText, "아니오", 48f);
        ConfigureExitAnswerButton(exitAnswer1Image, ConfirmQuit);
        ConfigureExitAnswerButton(exitAnswer2Image, CloseQuitPanel);

        if (exitQuestionRenderer != null)
            exitQuestionRenderer.enabled = exitQuestionImage == null;

        if (exitAnswer1Renderer != null)
            exitAnswer1Renderer.enabled = exitAnswer1Image == null;

        if (exitAnswer2Renderer != null)
            exitAnswer2Renderer.enabled = exitAnswer2Image == null;

        SetExitPanelAlpha(0f);
        if (exitPanelCanvasGroup != null)
        {
            exitPanelCanvasGroup.interactable = false;
            exitPanelCanvasGroup.blocksRaycasts = false;
        }
    }

    GameObject CreatePanel(string panelName, Vector2 size)
    {
        GameObject panel = new GameObject(panelName);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        panel.AddComponent<CanvasRenderer>();
        panel.AddComponent<Image>();
        return panel;
    }

    Button EnsureButton(Transform parent, string buttonName, Vector2 position, Vector2 size, Color color)
    {
        Transform existing = parent.Find(buttonName);
        bool created = existing == null;
        GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(buttonName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = buttonObject.AddComponent<RectTransform>();
            created = true;
        }

        if (created)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
        {
            image = buttonObject.AddComponent<Image>();
            image.color = color;
        }

        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
        }

        return button;
    }

    void ApplyButtonTint(Button button, Color normalColor, Color highlightedColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.selectedColor = highlightedColor;
        colors.pressedColor = new Color(0.52f, 0.52f, 0.52f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    void ApplyHiddenButtonHoverTint(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0f);
        colors.highlightedColor = new Color(0f, 0f, 0f, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0f, 0f, 0f, 0.28f);
        colors.disabledColor = new Color(0f, 0f, 0f, 0f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;
    }

    Image GetExitUIImage(string childName)
    {
        if (quitPanel == null)
            return null;

        Transform child = quitPanel.transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    void ConfigureExitUIText(TextMeshProUGUI text, string value, float fontSize)
    {
        if (text == null)
            return;

        text.font = UIThinDungFont.Get();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        text.raycastTarget = false;
        StretchToParent(text.rectTransform);
    }

    void ConfigureExitAnswerButton(Image image, UnityEngine.Events.UnityAction onClick)
    {
        if (image == null)
            return;

        image.raycastTarget = true;
        try
        {
            image.alphaHitTestMinimumThreshold = exitAnswerAlphaHitThreshold;
        }
        catch (System.InvalidOperationException)
        {
            // Non-readable textures reject alpha hit testing; import settings below keep the intended behavior.
        }

        Button button = image.GetComponent<Button>();
        if (button == null)
            button = image.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.ColorTint;
        ApplyButtonTint(button, Color.white, new Color(0.82f, 0.82f, 0.82f, 1f));
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            PlayExitClickSound();
            onClick?.Invoke();
        });
    }

    TextMeshPro EnsureWorldTMPText(Transform parent, string textName, string textValue, Color color, float fontSize)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(textName);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(textName);
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        if (text == null)
            text = textObject.AddComponent<TextMeshPro>();

        text.font = UIThinDungFont.Get();
        text.text = textValue;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        SpriteRenderer parentRenderer = parent.GetComponent<SpriteRenderer>();
        if (parentRenderer != null && parentRenderer.sprite != null)
        {
            Vector2 size = parentRenderer.sprite.bounds.size;
            rect.sizeDelta = new Vector2(size.x * 0.92f, size.y * 0.78f);

            MeshRenderer meshRenderer = text.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingLayerID = parentRenderer.sortingLayerID;
                meshRenderer.sortingOrder = parentRenderer.sortingOrder + 1;
            }
        }

        return text;
    }

    void HandleExitPanelPointer()
    {
        if (quitPanel == null || !quitPanel.activeInHierarchy)
            return;

        bool overAnswer1 = IsMouseOverOpaqueSprite(exitAnswer1Renderer);
        bool overAnswer2 = IsMouseOverOpaqueSprite(exitAnswer2Renderer);

        SetAnswerHover(exitAnswer1Renderer, exitAnswer1Color, overAnswer1);
        SetAnswerHover(exitAnswer2Renderer, exitAnswer2Color, overAnswer2);

        if (!WasPrimaryPointerPressedThisFrame())
            return;

        if (overAnswer1)
        {
            PlayExitClickSound();
            ConfirmQuit();
        }
        else if (overAnswer2)
        {
            PlayExitClickSound();
            CloseQuitPanel();
        }
    }

    bool IsMouseOverOpaqueSprite(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null || Camera.main == null)
            return false;

        Vector3 screenPoint = Input.mousePosition;
        float distance = Mathf.Abs(renderer.transform.position.z - Camera.main.transform.position.z);
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, distance));
        world.z = renderer.transform.position.z;

        Vector3 local = renderer.transform.InverseTransformPoint(world);
        Bounds bounds = renderer.sprite.bounds;
        if (!bounds.Contains(local))
            return false;

        Rect textureRect = renderer.sprite.textureRect;
        float u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, local.x);
        float v = Mathf.InverseLerp(bounds.min.y, bounds.max.y, local.y);

        if (renderer.flipX)
            u = 1f - u;

        if (renderer.flipY)
            v = 1f - v;

        int x = Mathf.Clamp(Mathf.FloorToInt(textureRect.x + textureRect.width * u), Mathf.FloorToInt(textureRect.x), Mathf.FloorToInt(textureRect.xMax) - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(textureRect.y + textureRect.height * v), Mathf.FloorToInt(textureRect.y), Mathf.FloorToInt(textureRect.yMax) - 1);

        try
        {
            Color pixel = renderer.sprite.texture.GetPixel(x, y);
            return pixel.a > exitAnswerAlphaHitThreshold;
        }
        catch (UnityException)
        {
            return true;
        }
    }

    void SetAnswerHover(SpriteRenderer renderer, Color baseColor, bool hovering)
    {
        if (renderer == null)
            return;

        Color color = hovering ? new Color(baseColor.r * 0.82f, baseColor.g * 0.82f, baseColor.b * 0.82f, baseColor.a) : baseColor;
        color.a = renderer.color.a;
        renderer.color = color;
    }

    void SetExitPanelAlpha(float alpha)
    {
        if (exitPanelCanvasGroup != null)
            exitPanelCanvasGroup.alpha = alpha;

        SetRendererAlpha(exitQuestionRenderer, exitQuestionColor, alpha);
        SetRendererAlpha(exitAnswer1Renderer, exitAnswer1Color, alpha);
        SetRendererAlpha(exitAnswer2Renderer, exitAnswer2Color, alpha);
        SetTMPAlpha(exitQuestionText, alpha);
        SetTMPAlpha(exitAnswer1Text, alpha);
        SetTMPAlpha(exitAnswer2Text, alpha);
    }

    void SetRendererAlpha(SpriteRenderer renderer, Color baseColor, float alpha)
    {
        if (renderer == null)
            return;

        Color color = baseColor;
        color.a *= alpha;
        renderer.color = color;
    }

    void SetTMPAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    TextMeshProUGUI EnsureTMPText(Transform parent, string textName)
    {
        Transform existing = parent.Find(textName);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(textName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = textObject.AddComponent<TextMeshProUGUI>();

        text.font = UIThinDungFont.Get();
        text.raycastTarget = false;
        return text;
    }

    void EnsureButtonText(Transform buttonTransform, string textValue)
    {
        TextMeshProUGUI text = EnsureTMPText(buttonTransform, "Label");
        text.text = textValue;
        text.fontSize = 44f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        StretchToParent(text.rectTransform);
    }

    void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    void ShowRoadPanel()
    {
        if (roadPanel == null || roadPanelRect == null)
            return;

        roadPanelClosing = false;
        if (roadPanelRoutine != null)
            StopCoroutine(roadPanelRoutine);

        SetChoicesInputEnabled(false);
        roadPanel.SetActive(true);
        roadPanel.transform.SetAsLastSibling();
        roadPanelRect.anchoredPosition = roadPanelHiddenPosition;

        if (roadCloseButton != null)
            roadCloseButton.gameObject.SetActive(true);

        StartRoadButtonFloat();
        PlayInventoryPanelOpenSound();
        roadPanelRoutine = StartCoroutine(SlideRoadPanel(
            roadPanelHiddenPosition,
            roadPanelCenterPosition,
            roadPanelCenterPosition + Vector2.up * panelOvershootDistance,
            null));
    }

    public void CloseRoadPanel()
    {
        if (roadPanelClosing)
            return;

        if (roadPanel == null || roadPanelRect == null)
        {
            if (rightHandChoice != null)
                rightHandChoice.ReturnHome(() => SetChoicesInputEnabled(true));
            else
                SetChoicesInputEnabled(true);

            return;
        }

        roadPanelClosing = true;
        if (roadPanelRoutine != null)
            StopCoroutine(roadPanelRoutine);

        StopRoadButtonFloat();
        PlayInventoryPanelCloseSound();
        roadPanelRoutine = StartCoroutine(SlideRoadPanel(
            roadPanelRect.anchoredPosition,
            roadPanelHiddenPosition,
            roadPanelRect.anchoredPosition + Vector2.up * panelOvershootDistance,
            () =>
        {
            roadPanel.SetActive(false);

            if (rightHandChoice != null)
                rightHandChoice.ReturnHome(() => SetChoicesInputEnabled(true));
            else
                SetChoicesInputEnabled(true);

            roadPanelClosing = false;
        }));
    }

    void HandlePanelCloseHotkeys()
    {
        if (!WasPrimaryPointerPressedThisFrame())
            return;

        if (IsPointerInsideRoadCloseArea())
        {
            CloseRoadPanel();
            return;
        }

        if (IsPointerInsideRect(optionCloseButton != null ? optionCloseButton.GetComponent<RectTransform>() : null))
            CloseOptionPanel();
    }

    bool IsPointerInsideRect(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
            return false;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
    }

    bool WasPrimaryPointerPressedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    bool IsPointerInsideRoadCloseArea()
    {
        if (roadPanel == null || roadPanelRect == null || !roadPanel.activeInHierarchy)
            return false;

        Canvas canvas = roadPanelRect.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(roadPanelRect, Input.mousePosition, eventCamera, out localPoint))
            return false;

        Rect rect = roadPanelRect.rect;
        float closeMinX = rect.xMax - Mathf.Max(260f, roadCloseSize.x);
        float closeMinY = rect.yMax - Mathf.Max(220f, roadCloseSize.y);
        return localPoint.x >= closeMinX && localPoint.y >= closeMinY;
    }

    void StartRoadButtonFloat()
    {
        StopRoadButtonFloat();

        if (roadButtonRect != null)
        {
            roadButtonRect.anchoredPosition = roadButtonBasePosition;
            roadButtonFloatRoutine = StartCoroutine(RoadButtonFloatRoutine());
        }
    }

    void StopRoadButtonFloat()
    {
        if (roadButtonFloatRoutine != null)
        {
            StopCoroutine(roadButtonFloatRoutine);
            roadButtonFloatRoutine = null;
        }

        if (roadButtonRect != null)
            roadButtonRect.anchoredPosition = roadButtonBasePosition;
    }

    System.Collections.IEnumerator RoadButtonFloatRoutine()
    {
        while (roadPanel != null && roadPanel.activeInHierarchy)
        {
            float offset = Mathf.Sin(Time.unscaledTime * roadButtonFloatSpeed) * roadButtonFloatAmplitude;
            roadButtonRect.anchoredPosition = roadButtonBasePosition + Vector2.up * offset;
            yield return null;
        }

        roadButtonFloatRoutine = null;
    }

    void ShowQuitPanel()
    {
        if (quitPanel == null)
            return;

        quitPanel.SetActive(true);
        quitPanel.transform.SetAsLastSibling();
        SetExitPanelAlpha(1f);

        if (exitPanelCanvasGroup != null)
        {
            exitPanelCanvasGroup.interactable = true;
            exitPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    public void CloseQuitPanel()
    {
        if (quitPanel != null)
        {
            SetExitPanelAlpha(0f);

            if (exitPanelCanvasGroup != null)
            {
                exitPanelCanvasGroup.interactable = false;
                exitPanelCanvasGroup.blocksRaycasts = false;
            }

            quitPanel.SetActive(false);
        }

        if (rightLegChoice != null)
            rightLegChoice.ReturnHome();
    }

    void ConfirmQuit()
    {
        if (transition != null)
            transition.BeginQuit();
    }
}
