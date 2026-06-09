using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StartBodyPartSelector : MonoBehaviour
{
    [SerializeField] StartSceneTransition transition;
    [SerializeField] GameObject optionPanel;
    [SerializeField] string targetSceneName = "RoomScene";
    [SerializeField] float moveDuration = 0.33f;
    [SerializeField] Color hoverColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField, Range(0f, 1f)] float bodyPartAlphaHitThreshold = 0.1f;
    [SerializeField] Vector2 optionClosePosition = new Vector2(533f, -383f);
    [SerializeField] Vector2 optionCloseSize = new Vector2(140f, 130f);
    [SerializeField] float panelSlideDuration = 0.36f;
    [SerializeField] float panelOvershootDistance = 46f;
    [SerializeField] AudioClip inventoryPanelOpenSound;
    [SerializeField] AudioClip inventoryPanelCloseSound;
    [SerializeField] string inventoryPanelOpenSoundResourcePath = "Sounds/inventory_panel_open";
    [SerializeField] string inventoryPanelCloseSoundResourcePath = "Sounds/inventory_panel_close";

    StartBodyPartChoice leftLegChoice;
    StartBodyPartChoice rightHandChoice;
    StartBodyPartChoice rightLegChoice;
    Button optionCloseButton;
    AudioSource inventoryPanelAudioSource;
    GameObject roadPanel;
    GameObject quitPanel;
    RectTransform optionPanelRect;
    Vector2 optionPanelCenterPosition;
    Vector2 optionPanelHiddenPosition;
    Coroutine optionPanelRoutine;

    void Awake()
    {
        if (transition == null)
            transition = GetComponentInChildren<StartSceneTransition>(true);

        inventoryPanelAudioSource = GetComponent<AudioSource>();
        if (inventoryPanelAudioSource == null)
            inventoryPanelAudioSource = gameObject.AddComponent<AudioSource>();

        if (optionPanel == null)
        {
            Transform option = transform.Find("OptionPanel");
            if (option != null)
                optionPanel = option.gameObject;
        }

        ConfigureChoice("lefthand", new Vector2(-163f, -88f), StartBodyPartChoice.ClickAction.LoadScene);
        leftLegChoice = ConfigureChoice("leftleg", new Vector2(-92f, -311f), StartBodyPartChoice.ClickAction.ShowOptionPanel);
        rightLegChoice = ConfigureChoice("rightleg", new Vector2(64f, -317f), StartBodyPartChoice.ClickAction.ShowQuitPanel);
        rightHandChoice = ConfigureChoice("righthand", new Vector2(131f, -90f), StartBodyPartChoice.ClickAction.ShowRoadPanel);

        Transform hotspot = transform.Find("StartHotspotButton");
        if (hotspot != null)
            hotspot.gameObject.SetActive(false);

        EnsureOptionCloseButton();
        EnsureRoadPanel();
        EnsureQuitPanel();
        CacheOptionPanelPositions();

        if (optionPanel != null)
            optionPanel.SetActive(false);

        if (roadPanel != null)
            roadPanel.SetActive(false);

        if (quitPanel != null)
            quitPanel.SetActive(false);
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
        GameObject hotspot = existing != null ? existing.gameObject : new GameObject("OptionCloseHotspot");
        hotspot.transform.SetParent(optionPanel.transform, false);
        hotspot.SetActive(true);

        RectTransform rect = hotspot.GetComponent<RectTransform>();
        if (rect == null)
            rect = hotspot.AddComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = optionClosePosition;
        rect.sizeDelta = optionCloseSize;

        Image image = hotspot.GetComponent<Image>();
        if (image == null)
            image = hotspot.AddComponent<Image>();

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        optionCloseButton = hotspot.GetComponent<Button>();
        if (optionCloseButton == null)
            optionCloseButton = hotspot.AddComponent<Button>();

        optionCloseButton.transition = Selectable.Transition.None;
        optionCloseButton.onClick.RemoveListener(CloseOptionPanel);
        optionCloseButton.onClick.AddListener(CloseOptionPanel);
    }

    public void HandleChoiceComplete(StartBodyPartChoice choice, StartBodyPartChoice.ClickAction action)
    {
        if (action == StartBodyPartChoice.ClickAction.LoadScene)
        {
            if (transition != null)
                transition.BeginTransition(targetSceneName);

            return;
        }

        if (action == StartBodyPartChoice.ClickAction.ShowOptionPanel)
            ShowOptionPanel();

        if (action == StartBodyPartChoice.ClickAction.ShowRoadPanel)
            ShowRoadPanel();

        if (action == StartBodyPartChoice.ClickAction.ShowQuitPanel)
            ShowQuitPanel();
    }

    void ShowOptionPanel()
    {
        if (optionPanel == null || optionPanelRect == null)
            return;

        if (optionPanelRoutine != null)
            StopCoroutine(optionPanelRoutine);

        optionPanel.SetActive(true);
        optionPanel.transform.SetAsLastSibling();
        optionPanelRect.anchoredPosition = optionPanelHiddenPosition;

        if (optionCloseButton != null)
            optionCloseButton.gameObject.SetActive(true);

        PlayInventoryPanelSound(GetInventoryPanelOpenSound());
        optionPanelRoutine = StartCoroutine(SlideOptionPanel(
            optionPanelHiddenPosition,
            optionPanelCenterPosition,
            optionPanelCenterPosition + Vector2.up * panelOvershootDistance,
            null));
    }

    public void CloseOptionPanel()
    {
        if (optionPanel == null || optionPanelRect == null)
        {
            if (leftLegChoice != null)
                leftLegChoice.ReturnHome();
            return;
        }

        if (optionPanelRoutine != null)
            StopCoroutine(optionPanelRoutine);

        PlayInventoryPanelSound(GetInventoryPanelCloseSound());
        optionPanelRoutine = StartCoroutine(SlideOptionPanel(
            optionPanelRect.anchoredPosition,
            optionPanelHiddenPosition,
            optionPanelRect.anchoredPosition + Vector2.up * panelOvershootDistance,
            () =>
        {
            optionPanel.SetActive(false);

            if (leftLegChoice != null)
                leftLegChoice.ReturnHome();
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
        yield return SlideOptionPanelSegment(start, reactionPosition, duration * 0.38f);
        yield return SlideOptionPanelSegment(reactionPosition, destination, duration * 0.62f);

        optionPanelRect.anchoredPosition = destination;
        optionPanelRoutine = null;
        onComplete?.Invoke();
    }

    System.Collections.IEnumerator SlideOptionPanelSegment(Vector2 start, Vector2 destination, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            optionPanelRect.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
            if (t >= 1f)
                break;

            yield return null;
        }

        optionPanelRect.anchoredPosition = destination;
    }

    AudioClip GetInventoryPanelOpenSound()
    {
        if (inventoryPanelOpenSound == null && !string.IsNullOrWhiteSpace(inventoryPanelOpenSoundResourcePath))
            inventoryPanelOpenSound = Resources.Load<AudioClip>(inventoryPanelOpenSoundResourcePath);

        return inventoryPanelOpenSound;
    }

    AudioClip GetInventoryPanelCloseSound()
    {
        if (inventoryPanelCloseSound == null && !string.IsNullOrWhiteSpace(inventoryPanelCloseSoundResourcePath))
            inventoryPanelCloseSound = Resources.Load<AudioClip>(inventoryPanelCloseSoundResourcePath);

        return inventoryPanelCloseSound;
    }

    void PlayInventoryPanelSound(AudioClip clip)
    {
        if (clip == null || inventoryPanelAudioSource == null)
            return;

        inventoryPanelAudioSource.PlayOneShot(clip);
    }

    void EnsureRoadPanel()
    {
        Transform existing = transform.Find("RoadPanel");
        roadPanel = existing != null ? existing.gameObject : CreatePanel("RoadPanel", new Vector2(720f, 420f));
        roadPanel.transform.SetParent(transform, false);

        Image panelImage = roadPanel.GetComponent<Image>();
        panelImage.color = new Color(0.06f, 0.06f, 0.06f, 0.94f);
        panelImage.raycastTarget = true;

        TextMeshProUGUI title = EnsureTMPText(roadPanel.transform, "RoadText");
        title.text = "ROAD";
        title.fontSize = 110f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(0f, 0f);
        titleRect.offsetMax = new Vector2(0f, 0f);

        Button closeButton = EnsureButton(roadPanel.transform, "RoadCloseCheckbox", new Vector2(270f, 130f), new Vector2(84f, 84f), Color.white);
        closeButton.onClick.RemoveListener(CloseRoadPanel);
        closeButton.onClick.AddListener(CloseRoadPanel);

        TextMeshProUGUI xText = EnsureTMPText(closeButton.transform, "RoadCloseX");
        xText.text = "X";
        xText.fontSize = 54f;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = Color.black;
        StretchToParent(xText.rectTransform);
    }

    void EnsureQuitPanel()
    {
        Transform existing = transform.Find("QuitConfirmPanel");
        quitPanel = existing != null ? existing.gameObject : CreatePanel("QuitConfirmPanel", new Vector2(980f, 410f));
        quitPanel.transform.SetParent(transform, false);

        Image panelImage = quitPanel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.05f, 0.94f);
        panelImage.raycastTarget = true;

        TextMeshProUGUI question = EnsureTMPText(quitPanel.transform, "QuitQuestionText");
        question.text = "정말로 게임을 종료하시겠습니까?";
        question.fontSize = 56f;
        question.alignment = TextAlignmentOptions.Center;
        question.color = Color.white;
        RectTransform questionRect = question.rectTransform;
        questionRect.anchorMin = new Vector2(0.5f, 0.5f);
        questionRect.anchorMax = new Vector2(0.5f, 0.5f);
        questionRect.pivot = new Vector2(0.5f, 0.5f);
        questionRect.anchoredPosition = new Vector2(0f, 85f);
        questionRect.sizeDelta = new Vector2(900f, 130f);

        Button yesButton = EnsureButton(quitPanel.transform, "QuitYesButton", new Vector2(-135f, -95f), new Vector2(210f, 92f), new Color(0.88f, 0.88f, 0.88f, 1f));
        ApplyButtonTint(yesButton, new Color(0.88f, 0.88f, 0.88f, 1f), new Color(0.68f, 0.68f, 0.68f, 1f));
        yesButton.onClick.RemoveListener(ConfirmQuit);
        yesButton.onClick.AddListener(ConfirmQuit);
        EnsureButtonText(yesButton.transform, "예");

        Button noButton = EnsureButton(quitPanel.transform, "QuitNoButton", new Vector2(135f, -95f), new Vector2(210f, 92f), new Color(0.88f, 0.88f, 0.88f, 1f));
        ApplyButtonTint(noButton, new Color(0.88f, 0.88f, 0.88f, 1f), new Color(0.68f, 0.68f, 0.68f, 1f));
        noButton.onClick.RemoveListener(CloseQuitPanel);
        noButton.onClick.AddListener(CloseQuitPanel);
        EnsureButtonText(noButton.transform, "아니오");
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
        GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(buttonName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (rect == null)
            rect = buttonObject.AddComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();

        image.color = color;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();

        button.transition = Selectable.Transition.ColorTint;
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
        if (roadPanel == null)
            return;

        roadPanel.SetActive(true);
        roadPanel.transform.SetAsLastSibling();
    }

    public void CloseRoadPanel()
    {
        if (roadPanel != null)
            roadPanel.SetActive(false);

        if (rightHandChoice != null)
            rightHandChoice.ReturnHome();
    }

    void ShowQuitPanel()
    {
        if (quitPanel == null)
            return;

        quitPanel.SetActive(true);
        quitPanel.transform.SetAsLastSibling();
    }

    public void CloseQuitPanel()
    {
        if (quitPanel != null)
            quitPanel.SetActive(false);

        if (rightLegChoice != null)
            rightLegChoice.ReturnHome();
    }

    void ConfirmQuit()
    {
        if (transition != null)
            transition.BeginQuit();
    }
}
