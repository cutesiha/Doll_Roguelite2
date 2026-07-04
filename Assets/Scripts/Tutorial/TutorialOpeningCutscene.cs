using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialOpeningCutscene : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] Sprite[] pages = new Sprite[5];

    [Header("Timing")]
    [SerializeField, Min(0.1f)] float fadeInDuration = 1.8f;
    [SerializeField, Min(0.1f)] float pageTurnDuration = 0.38f;
    [SerializeField, Min(0.1f)] float eyelidDuration = 0.62f;

    Canvas cutsceneCanvas;
    GameObject blackBase;
    CanvasGroup pageFader;
    RectTransform currentPageRect;
    Image currentPageImage;
    Image nextPageImage;
    Image foldShadow;
    Button clickCatcher;
    RectTransform continueIndicator;
    RectTransform skipRect;
    GameObject skipRoot;
    GameObject confirmRoot;
    RectTransform topEyelid;
    RectTransform bottomEyelid;

    Vector2 continueBasePosition;
    Vector2 skipBasePosition;
    int pageIndex;
    bool isPlaying;
    bool isBusy;
    bool completed;
    bool loadingRoom;
    string roomSceneName = "RoomScene";

    static Sprite circleSprite;

    public bool HasAllPages
    {
        get
        {
            if (pages == null || pages.Length < 5)
                return false;

            for (int i = 0; i < 5; i++)
            {
                if (pages[i] == null)
                    return false;
            }

            return true;
        }
    }

    public IEnumerator Play(string targetRoomScene)
    {
        roomSceneName = string.IsNullOrWhiteSpace(targetRoomScene) ? "RoomScene" : targetRoomScene;
        if (!HasAllPages)
        {
            Debug.LogWarning("Tutorial opening cutscene was skipped because one or more page sprites are missing.");
            yield break;
        }

        BuildUi();
        isPlaying = true;
        isBusy = true;
        completed = false;
        pageIndex = 0;
        currentPageImage.sprite = pages[0];
        pageFader.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            pageFader.alpha = EaseInOut(Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }

        pageFader.alpha = 1f;
        isBusy = false;

        while (!completed && !loadingRoom)
            yield return null;

        isPlaying = false;
    }

    void Update()
    {
        if (!isPlaying)
            return;

        if (WasAdvancePressed())
            OnPageClicked();

        float bob = Mathf.Sin(Time.unscaledTime * 2.5f);
        if (continueIndicator != null)
            continueIndicator.anchoredPosition = continueBasePosition + Vector2.up * (bob * 7f);
        if (skipRect != null)
            skipRect.anchoredPosition = skipBasePosition + Vector2.up * (Mathf.Sin(Time.unscaledTime * 2.15f + 0.8f) * 6f);
    }

    void BuildUi()
    {
        GameObject canvasObject = new GameObject("TutorialOpeningCutsceneCanvas");
        cutsceneCanvas = canvasObject.AddComponent<Canvas>();
        cutsceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cutsceneCanvas.sortingOrder = 2000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        blackBase = CreateStretchImage(canvasObject.transform, "CutsceneBlackBase", null, Color.black, true);
        blackBase.transform.SetAsFirstSibling();

        GameObject pageRoot = new GameObject("CutscenePages");
        pageRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform pageRootRect = pageRoot.AddComponent<RectTransform>();
        Stretch(pageRootRect);
        pageFader = pageRoot.AddComponent<CanvasGroup>();

        nextPageImage = CreateFullPage(pageRoot.transform, "NextPage");
        nextPageImage.gameObject.SetActive(false);
        currentPageImage = CreateFullPage(pageRoot.transform, "CurrentPage");
        currentPageRect = currentPageImage.rectTransform;

        GameObject shadowObject = CreateFixedImage(pageRoot.transform, "PageFoldShadow", null,
            new Vector2(160f, 1120f), new Vector2(1010f, 0f), new Color(0f, 0f, 0f, 0f));
        foldShadow = shadowObject.GetComponent<Image>();
        shadowObject.SetActive(false);

        GameObject clickObject = CreateStretchImage(canvasObject.transform, "CutsceneClickCatcher", null,
            new Color(1f, 1f, 1f, 0f), true);
        clickCatcher = clickObject.AddComponent<Button>();
        clickCatcher.transition = Selectable.Transition.None;
        clickCatcher.targetGraphic = clickObject.GetComponent<Image>();
        clickCatcher.onClick.AddListener(OnPageClicked);

        BuildContinueIndicator(canvasObject.transform);
        BuildSkipButton(canvasObject.transform);
        BuildEyelids(canvasObject.transform);
        BuildSkipConfirmation(canvasObject.transform);
    }

    Image CreateFullPage(Transform parent, string objectName)
    {
        GameObject go = CreateFixedImage(parent, objectName, null, new Vector2(1920f, 1080f), Vector2.zero, Color.white);
        Image image = go.GetComponent<Image>();
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    void BuildContinueIndicator(Transform parent)
    {
        GameObject root = new GameObject("ContinueIndicator");
        root.transform.SetParent(parent, false);
        continueIndicator = root.AddComponent<RectTransform>();
        continueIndicator.anchorMin = continueIndicator.anchorMax = new Vector2(1f, 0f);
        continueIndicator.pivot = new Vector2(0.5f, 0.5f);
        continueIndicator.sizeDelta = new Vector2(72f, 54f);
        continueBasePosition = new Vector2(-78f, 64f);
        continueIndicator.anchoredPosition = continueBasePosition;

        Color dotColor = new Color(0.30f, 0.11f, 0.05f, 0.84f);
        CreateOutlinedDot(root.transform, "DotTop", new Vector2(-13f, 14f), 24f, dotColor);
        CreateOutlinedDot(root.transform, "DotBottom", new Vector2(-13f, -14f), 24f, dotColor);
        CreateOutlinedDot(root.transform, "DotPoint", new Vector2(18f, 0f), 24f, dotColor);
    }

    void CreateOutlinedDot(Transform parent, string objectName, Vector2 position, float size, Color color)
    {
        CreateDot(parent, objectName + "_Outline", position, size + 10f, new Color(1f, 0.94f, 0.78f, 0.95f));
        CreateDot(parent, objectName, position, size, color);
    }

    void CreateDot(Transform parent, string objectName, Vector2 position, float size, Color color)
    {
        GameObject dot = CreateFixedImage(parent, objectName, CircleSprite(), new Vector2(size, size), position, color);
        dot.GetComponent<Image>().raycastTarget = false;
    }

    void BuildSkipButton(Transform parent)
    {
        skipRoot = new GameObject("TutorialSkipButton");
        skipRoot.transform.SetParent(parent, false);
        skipRect = skipRoot.AddComponent<RectTransform>();
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
        skipRect.pivot = new Vector2(0.5f, 0.5f);
        skipRect.sizeDelta = new Vector2(210f, 86f);
        skipBasePosition = new Vector2(-128f, -72f);
        skipRect.anchoredPosition = skipBasePosition;

        Image background = skipRoot.AddComponent<Image>();
        background.sprite = null;
        background.color = new Color(1f, 0.94f, 0.78f, 0.92f);
        background.raycastTarget = true;
        Outline outline = skipRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.11f, 0.04f, 0.02f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);

        GameObject labelObject = new GameObject("SkipLabel");
        labelObject.transform.SetParent(skipRoot.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        Stretch(labelRect);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = "SKIP";
        label.fontSize = 46f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.12f, 0.035f, 0.015f, 1f);
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(1f, 0.96f, 0.82f, 0.9f);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        Button button = skipRoot.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.52f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.95f, 0.67f, 0.28f, 1f);
        colors.fadeDuration = 0.10f;
        button.colors = colors;
        button.onClick.AddListener(ShowSkipConfirmation);
    }

    void BuildEyelids(Transform parent)
    {
        GameObject top = CreateFixedImage(parent, "TopEyelid", null, new Vector2(1980f, 560f), new Vector2(0f, 830f), Color.black);
        GameObject bottom = CreateFixedImage(parent, "BottomEyelid", null, new Vector2(1980f, 560f), new Vector2(0f, -830f), Color.black);
        topEyelid = top.GetComponent<RectTransform>();
        bottomEyelid = bottom.GetComponent<RectTransform>();
        top.GetComponent<Image>().raycastTarget = false;
        bottom.GetComponent<Image>().raycastTarget = false;
    }

    void BuildSkipConfirmation(Transform parent)
    {
        confirmRoot = new GameObject("TutorialSkipConfirmation");
        confirmRoot.transform.SetParent(parent, false);
        RectTransform rootRect = confirmRoot.AddComponent<RectTransform>();
        Stretch(rootRect);

        CreateStretchImage(confirmRoot.transform, "SkipConfirmBackdrop", null, new Color(0f, 0f, 0f, 0.38f), true);

        GameObject source = Resources.Load<GameObject>("StartUIPanelPrefabs/StartExitPanel");
        GameObject panel = source != null
            ? Instantiate(source, confirmRoot.transform, false)
            : CreateFallbackConfirmPanel(confirmRoot.transform);
        panel.name = "TutorialSkipConfirmPanel";

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null)
            panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        Transform question = FindDeep(panel.transform, "question_ui");
        Transform answer1 = FindDeep(panel.transform, "answer1_ui");
        Transform answer2 = FindDeep(panel.transform, "answer2_ui");
        if (question == null || answer1 == null || answer2 == null)
        {
            Destroy(panel);
            panel = CreateFallbackConfirmPanel(confirmRoot.transform);
            question = FindDeep(panel.transform, "question_ui");
            answer1 = FindDeep(panel.transform, "answer1_ui");
            answer2 = FindDeep(panel.transform, "answer2_ui");
        }

        DisableExistingLabels(question);
        DisableExistingLabels(answer1);
        DisableExistingLabels(answer2);
        EnsureLabel(question, "QuestionLabel", "스킵하시겠습니까?", 42f);
        EnsureLabel(answer1, "AnswerLabel", "예", 52f);
        EnsureLabel(answer2, "AnswerLabel", "아니오", 48f);
        ConfigureConfirmButton(answer1, ConfirmSkip);
        ConfigureConfirmButton(answer2, HideSkipConfirmation);
        CanvasGroup panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null)
            panelGroup = panel.AddComponent<CanvasGroup>();
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;
        panel.SetActive(true);
        confirmRoot.SetActive(false);
    }

    GameObject CreateFallbackConfirmPanel(Transform parent)
    {
        GameObject panel = CreateFixedImage(parent, "TutorialSkipConfirmPanel", null,
            new Vector2(900f, 562.5f), Vector2.zero, new Color(0.97f, 0.91f, 0.80f, 1f));
        CreateFixedImage(panel.transform, "question_ui", null, new Vector2(720f, 112f), new Vector2(0f, 96f), new Color(1f, 0.96f, 0.88f, 1f));
        CreateFixedImage(panel.transform, "answer1_ui", null, new Vector2(250f, 76f), new Vector2(-170f, -78f), new Color(1f, 0.96f, 0.88f, 1f));
        CreateFixedImage(panel.transform, "answer2_ui", null, new Vector2(250f, 76f), new Vector2(170f, -78f), new Color(1f, 0.96f, 0.88f, 1f));
        return panel;
    }

    void EnsureLabel(Transform parent, string objectName, string value, float fontSize)
    {
        if (parent == null)
            return;

        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        Stretch(rect);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = value;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static void DisableExistingLabels(Transform parent)
    {
        if (parent == null)
            return;

        TMP_Text[] labels = parent.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            labels[i].gameObject.SetActive(false);
    }

    void ConfigureConfirmButton(Transform target, UnityEngine.Events.UnityAction action)
    {
        if (target == null)
            return;

        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.gameObject.AddComponent<Image>();
        image.raycastTarget = true;

        Button button = target.GetComponent<Button>();
        if (button == null)
            button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SoundManager.PlayClick(0f));
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    void ShowSkipConfirmation()
    {
        if (confirmRoot == null || loadingRoom)
            return;

        SoundManager.PlayClick(0f);
        confirmRoot.SetActive(true);
        confirmRoot.transform.SetAsLastSibling();
        if (clickCatcher != null)
            clickCatcher.interactable = false;
    }

    void HideSkipConfirmation()
    {
        if (confirmRoot != null)
            confirmRoot.SetActive(false);
        if (clickCatcher != null)
            clickCatcher.interactable = true;
    }

    void ConfirmSkip()
    {
        if (loadingRoom || completed)
            return;

        Time.timeScale = 1f;
        completed = true;

        if (cutsceneCanvas != null)
            Destroy(cutsceneCanvas.gameObject);
    }

    void OnPageClicked()
    {
        if (isBusy || completed || loadingRoom || (confirmRoot != null && confirmRoot.activeSelf))
            return;

        if (pageIndex < 4)
            StartCoroutine(TurnPage());
        else
            StartCoroutine(CloseAndOpenEyes());
    }

    static bool WasAdvancePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.spaceKey.wasPressedThisFrame
            || keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    IEnumerator TurnPage()
    {
        isBusy = true;
        if (clickCatcher != null)
            clickCatcher.interactable = false;
        SoundManager.PlaySfxResource("Sounds/paper555", SoundManager.PanelSfxPath, 0f, 0.82f);

        nextPageImage.sprite = pages[pageIndex + 1];
        nextPageImage.gameObject.SetActive(true);
        nextPageImage.transform.SetAsFirstSibling();
        currentPageImage.transform.SetAsLastSibling();
        foldShadow.gameObject.SetActive(true);
        foldShadow.transform.SetAsLastSibling();

        currentPageRect.anchorMin = currentPageRect.anchorMax = new Vector2(0.5f, 0.5f);
        currentPageRect.pivot = new Vector2(0f, 0.5f);
        currentPageRect.sizeDelta = new Vector2(1920f, 1080f);
        currentPageRect.anchoredPosition = new Vector2(-960f, 0f);

        float elapsed = 0f;
        while (elapsed < pageTurnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pageTurnDuration);
            float eased = EaseInOut(t);
            float width = Mathf.Max(0.015f, Mathf.Cos(eased * Mathf.PI * 0.5f));
            currentPageRect.localScale = new Vector3(width, 1f, 1f);
            currentPageRect.anchoredPosition = new Vector2(-960f - Mathf.Sin(t * Mathf.PI) * 34f, 0f);

            RectTransform shadowRect = foldShadow.rectTransform;
            shadowRect.anchoredPosition = new Vector2(Mathf.Lerp(970f, -930f, eased), 0f);
            Color shadowColor = foldShadow.color;
            shadowColor.a = Mathf.Sin(t * Mathf.PI) * 0.34f;
            foldShadow.color = shadowColor;
            yield return null;
        }

        pageIndex++;
        currentPageImage.sprite = pages[pageIndex];
        currentPageRect.pivot = new Vector2(0.5f, 0.5f);
        currentPageRect.anchoredPosition = Vector2.zero;
        currentPageRect.localScale = Vector3.one;
        nextPageImage.gameObject.SetActive(false);
        foldShadow.gameObject.SetActive(false);
        if (clickCatcher != null)
            clickCatcher.interactable = true;
        isBusy = false;
    }

    IEnumerator CloseAndOpenEyes()
    {
        isBusy = true;
        if (clickCatcher != null)
            clickCatcher.interactable = false;
        SoundManager.PlayTutorialPaperClose(0f);

        yield return MoveEyelids(new Vector2(0f, 830f), new Vector2(0f, 280f),
            new Vector2(0f, -830f), new Vector2(0f, -280f), eyelidDuration);

        currentPageImage.transform.parent.gameObject.SetActive(false);
        if (blackBase != null)
            blackBase.SetActive(false);
        if (continueIndicator != null)
            continueIndicator.gameObject.SetActive(false);
        if (skipRoot != null)
            skipRoot.SetActive(false);
        if (confirmRoot != null)
            confirmRoot.SetActive(false);

        yield return new WaitForSecondsRealtime(0.16f);
        yield return MoveEyelids(new Vector2(0f, 280f), new Vector2(0f, 830f),
            new Vector2(0f, -280f), new Vector2(0f, -830f), eyelidDuration * 1.08f);

        completed = true;
        if (cutsceneCanvas != null)
            Destroy(cutsceneCanvas.gameObject);
    }

    IEnumerator MoveEyelids(Vector2 topFrom, Vector2 topTo, Vector2 bottomFrom, Vector2 bottomTo, float duration)
    {
        topEyelid.gameObject.SetActive(true);
        bottomEyelid.gameObject.SetActive(true);
        topEyelid.transform.SetAsLastSibling();
        bottomEyelid.transform.SetAsLastSibling();

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / safeDuration));
            topEyelid.anchoredPosition = Vector2.LerpUnclamped(topFrom, topTo, t);
            bottomEyelid.anchoredPosition = Vector2.LerpUnclamped(bottomFrom, bottomTo, t);
            yield return null;
        }

        topEyelid.anchoredPosition = topTo;
        bottomEyelid.anchoredPosition = bottomTo;
    }

    static GameObject CreateStretchImage(Transform parent, string objectName, Sprite sprite, Color color, bool raycastTarget)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        Stretch(rect);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return go;
    }

    static GameObject CreateFixedImage(Transform parent, string objectName, Sprite sprite, Vector2 size, Vector2 position, Color color)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    static Transform FindDeep(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
            Transform nested = FindDeep(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static Sprite CircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.44f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
