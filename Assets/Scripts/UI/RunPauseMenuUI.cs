using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RunPauseMenuUI : MonoBehaviour
{
    [SerializeField] Button menuButton;
    [SerializeField] GameObject startOptionPanelPrefab;
    [SerializeField] GameObject startRoadPanelPrefab;
    [SerializeField] GameObject startExitPanelPrefab;
    [SerializeField] Sprite optionBackgroundSprite;
    [SerializeField] Sprite questionSprite;
    [SerializeField] TMP_FontAsset uiFont;
    [SerializeField] string startSceneName = "StartScene";
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField] Color panelLineColor = new Color(0.30f, 0.18f, 0.10f, 1f);
    [Tooltip("메뉴 패널 화면 중심 기준 오프셋 (Inspector에서 조정한 값이 유지됩니다)")]
    [SerializeField] Vector2 menuPanelOffset = Vector2.zero;

    GameObject menuPanel;
    GameObject settingsPanel;
    GameObject savePanel;
    GameObject confirmPanel;
    Image fadeImage;
    TextMeshProUGUI confirmQuestionText;
    Button confirmYesButton;
    Button confirmNoButton;
    TextMeshProUGUI[] saveNameLabels;
    TextMeshProUGUI[] saveDateLabels;
    TextMeshProUGUI saveStatusLabel;
    Image[] bgmVolumeFills;
    Image[] sfxVolumeFills;
    System.Action confirmYesAction;
    Coroutine menuPanelAnimationRoutine;
    Vector2 menuPanelShownPosition;
    bool hasMenuPanelShownPosition;
    // RunMenuPanel이 씬/프리팹에 이미 손으로 배치돼 있었는지 여부. true면 크기를 하드코딩된
    // 기본값으로 되돌리지 않고 에디터에서 잡아둔 크기를 그대로 유지한다.
    bool menuPanelPreExisting;

    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    static readonly Color TextColor = new Color(0.17f, 0.11f, 0.06f, 1f);
    static readonly Color ButtonHover = new Color(0.64f, 0.45f, 0.28f, 1f);
    static readonly Color ButtonPressed = new Color(0.48f, 0.31f, 0.18f, 1f);
    static readonly Vector2 MenuPanelSize = new Vector2(720f, 360f);
    static readonly Vector2 PanelSpriteSize = new Vector2(400f, 250f);
    const float MenuPanelHiddenOffsetY = 980f;
    const float MenuPanelOvershootY = 42f;

    public bool IsAnyOpen
    {
        get
        {
            return (menuPanel != null && menuPanel.activeSelf)
                || (settingsPanel != null && settingsPanel.activeSelf)
                || (savePanel != null && savePanel.activeSelf)
                || (confirmPanel != null && confirmPanel.activeSelf);
        }
    }

    void Awake()
    {
        EnsureAssets();
        Build();
        SetMenuButton(menuButton);
    }

    public void SetMenuButton(Button button)
    {
        if (menuButton != null)
            menuButton.onClick.RemoveListener(OnMenuButtonClicked);

        menuButton = button;

        if (menuButton != null)
        {
            menuButton.onClick.RemoveListener(OnMenuButtonClicked);
            menuButton.onClick.AddListener(OnMenuButtonClicked);
        }
    }

    void OnMenuButtonClicked()
    {
        SoundManager.PlayClick();
        RunUiPauseManager.ClearUiSelection();
        ToggleMenu();
    }

    // RunHudUI 가 설정한다. 다른 패널(지도/인벤토리)이 열려 있으면 false 를 반환해
    // 메뉴가 그 위에 겹쳐 열리는 것을 막는다.
    public System.Func<bool> CanOpenMenu;

    public void ToggleMenu()
    {
        EnsureMenuPanelReady();

        if (IsAnyOpen)
        {
            CloseAll();
            return;
        }

        // 다른 패널이 열려 있으면 메뉴를 열지 않는다 (패널 겹침 방지)
        if (CanOpenMenu != null && !CanOpenMenu())
            return;

        ShowMenuPanel();
    }

    public void CloseAll()
    {
        bool onlyMenuOpen = menuPanel != null && menuPanel.activeSelf
            && (settingsPanel == null || !settingsPanel.activeSelf)
            && (savePanel == null || !savePanel.activeSelf)
            && (confirmPanel == null || !confirmPanel.activeSelf);

        if (onlyMenuOpen)
        {
            PlayMenuPanelAnimation(false);
            return;
        }

        HidePanels();
    }

    void OnDestroy()
    {
        RunUiPauseManager.SetPaused("Menu", false);
    }

void Build()
    {
        EnsureAssets();
        FindExistingPanels();

        if (menuPanel == null)
            BuildMenuPanel();
        else
            ConfigureMenuPanel();

        HidePanels();
    }

    void EnsureMenuPanelReady()
    {
        EnsureAssets();
        FindExistingPanels();

        if (menuPanel == null)
            BuildMenuPanel();
        else
            ConfigureMenuPanel();
    }

    void FindExistingPanels()
    {
        if (menuPanel == null)
        {
            menuPanel = FindDirectChild("RunMenuPanel");
            if (menuPanel != null)
                menuPanelPreExisting = true;
        }
        if (settingsPanel == null)
            settingsPanel = FindDirectChild("RunSettingsPanel");
        if (savePanel == null)
            savePanel = FindDirectChild("RunSavePanel");
        if (confirmPanel == null)
            confirmPanel = FindDirectChild("RunConfirmPanel");
        if (fadeImage == null)
        {
            GameObject fade = FindDirectChild("RunMenuFadePanel");
            if (fade != null)
                fadeImage = fade.GetComponent<Image>();
        }
    }

    GameObject FindDirectChild(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    void BuildMenuPanel()
    {
        menuPanel = CreateImagePanel("RunMenuPanel", MenuPanelSize, optionBackgroundSprite);
        ConfigureMenuPanel();
        menuPanel.SetActive(false);
    }

    void ShowMenuPanel()
    {
        HidePanels();
        if (menuPanel == null)
            BuildMenuPanel();

        if (menuPanel == null)
            return;

        menuPanel.SetActive(true);
        menuPanel.transform.SetAsLastSibling();
        RefreshPauseLock();
        PlayMenuPanelAnimation(true);
    }

    void ConfigureMenuPanel()
    {
        if (menuPanel == null)
            return;

        NormalizeMenuPanel();
        RememberMenuPanelShownPosition();

        string[] labels =
        {
            "\uC124\uC815",
            "\uC800\uC7A5",
            "\uBA54\uC778\uC73C\uB85C",
            "\uB098\uAC00\uAE30"
        };
        UnityAction[] actions =
        {
            ShowSettingsPanel,
            ShowSavePanel,
            ShowMainConfirm,
            ShowExitConfirm
        };

        for (int i = 0; i < labels.Length; i++)
        {
            Button button = GetOrCreateSpriteButton(menuPanel.transform, "RunMenuButton_" + i, new Vector2(0f, 114f - i * 76f), new Vector2(560f, 68f), labels[i], 42f);
            button.onClick.AddListener(actions[i]);
        }

        ConfigureMenuCloseButton();
    }

    void NormalizeMenuPanel()
    {
        RectTransform rect = menuPanel.transform as RectTransform;
        if (rect == null)
            rect = menuPanel.AddComponent<RectTransform>();

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = menuPanelOffset;
        // 이미 배치돼 있던(에디터에서 손으로 만든) 패널이면 크기를 하드코딩된 기본값으로
        // 되돌리지 않는다 (디자인해둔 크기가 매번 720x360으로 뭉개지는 문제).
        if (!menuPanelPreExisting)
            rect.sizeDelta = MenuPanelSize;
        rect.localScale = Vector3.one;

        Image image = menuPanel.GetComponent<Image>();
        if (image == null)
            image = menuPanel.AddComponent<Image>();
        // 이미 씬/프리팹에서 직접 지정해둔 배경 스프라이트가 있으면 덮어쓰지 않는다
        // (에디터에서 꾸며둔 테두리 디자인이 런타임에 기본 스프라이트로 뭉개지는 문제).
        if (image.sprite == null)
        {
            image.sprite = optionBackgroundSprite;
            image.type = optionBackgroundSprite != null && optionBackgroundSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        image.color = Color.white;
        image.raycastTarget = true;
    }

    void RememberMenuPanelShownPosition()
    {
        if (menuPanel == null || hasMenuPanelShownPosition)
            return;

        RectTransform rect = menuPanel.transform as RectTransform;
        if (rect == null)
            rect = menuPanel.GetComponent<RectTransform>();

        if (rect == null)
            return;

        menuPanelShownPosition = rect.anchoredPosition;
        hasMenuPanelShownPosition = true;
    }

    void ConfigureMenuCloseButton()
    {
        Transform closeTransform = menuPanel.transform.Find("RunMenuCloseButton");
        bool createdCloseButton = false;
        if (closeTransform == null)
        {
            GameObject closeObject = new GameObject("RunMenuCloseButton");
            closeObject.transform.SetParent(menuPanel.transform, false);
            closeTransform = closeObject.AddComponent<RectTransform>();
            createdCloseButton = true;
        }

        RectTransform closeRect = closeTransform as RectTransform;
        if (closeRect == null)
            closeRect = closeTransform.gameObject.AddComponent<RectTransform>();
        if (createdCloseButton)
        {
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -18f);
            closeRect.sizeDelta = new Vector2(60f, 60f);
        }

        Image background = closeTransform.GetComponent<Image>();
        if (background == null)
            background = closeTransform.gameObject.AddComponent<Image>();
        background.sprite = null;
        background.color = Clear;
        background.raycastTarget = true;

        Button closeButton = closeTransform.GetComponent<Button>();
        if (closeButton == null)
            closeButton = closeTransform.gameObject.AddComponent<Button>();
        closeButton.transition = Selectable.Transition.None;
        closeButton.targetGraphic = background;
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(PlayClickSound);
        closeButton.onClick.AddListener(CloseAll);

        StartPanelHoverTint hover = closeTransform.GetComponent<StartPanelHoverTint>();
        if (hover == null)
            hover = closeTransform.gameObject.AddComponent<StartPanelHoverTint>();
        hover.Configure(background, Clear, WithAlpha(panelLineColor, 0.20f), WithAlpha(panelLineColor, 0.34f));

        for (int i = 0; i < 4; i++)
        {
            float coordinate = -18f + i * 12f;
            ConfigureCloseDash(closeTransform, "BorderDash_Top_" + i, new Vector2(coordinate, 27f), new Vector2(8f, 3f));
            ConfigureCloseDash(closeTransform, "BorderDash_Bottom_" + i, new Vector2(coordinate, -27f), new Vector2(8f, 3f));
            ConfigureCloseDash(closeTransform, "BorderDash_Left_" + i, new Vector2(-27f, coordinate), new Vector2(3f, 8f));
            ConfigureCloseDash(closeTransform, "BorderDash_Right_" + i, new Vector2(27f, coordinate), new Vector2(3f, 8f));
        }

        Transform xTransform = closeTransform.Find("CloseX");
        if (xTransform == null)
        {
            GameObject xObject = new GameObject("CloseX");
            xObject.transform.SetParent(closeTransform, false);
            xTransform = xObject.AddComponent<RectTransform>();
        }
        RectTransform xRect = xTransform as RectTransform;
        if (xRect == null)
            xRect = xTransform.gameObject.AddComponent<RectTransform>();
        StretchToParent(xRect);
        TextMeshProUGUI xLabel = xTransform.GetComponent<TextMeshProUGUI>();
        if (xLabel == null)
            xLabel = xTransform.gameObject.AddComponent<TextMeshProUGUI>();
        xLabel.font = UIThinDungFont.Get(uiFont);
        xLabel.text = "X";
        xLabel.fontSize = 34f;
        xLabel.alignment = TextAlignmentOptions.Center;
        xLabel.color = panelLineColor;
        xLabel.raycastTarget = false;
        xLabel.textWrappingMode = TextWrappingModes.NoWrap;

        closeTransform.SetAsLastSibling();
    }

    void ConfigureCloseDash(Transform parent, string name, Vector2 position, Vector2 size)
    {
        Transform dashTransform = parent.Find(name);
        if (dashTransform == null)
        {
            GameObject dashObject = new GameObject(name);
            dashObject.transform.SetParent(parent, false);
            dashTransform = dashObject.AddComponent<RectTransform>();
        }

        RectTransform dashRect = dashTransform as RectTransform;
        dashRect.anchorMin = dashRect.anchorMax = new Vector2(0.5f, 0.5f);
        dashRect.pivot = new Vector2(0.5f, 0.5f);
        dashRect.anchoredPosition = position;
        dashRect.sizeDelta = size;

        Image dash = dashTransform.GetComponent<Image>();
        if (dash == null)
            dash = dashTransform.gameObject.AddComponent<Image>();
        dash.color = panelLineColor;
        dash.raycastTarget = false;
    }

    void DestroyDirectChildrenWithPrefix(Transform parent, string prefix)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (!child.name.StartsWith(prefix))
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }


    void BuildSettingsPanel()
    {
        settingsPanel = InstantiateStartPanel(startOptionPanelPrefab, "RunSettingsPanel", "StartOptionPanel");
        if (settingsPanel == null)
            settingsPanel = CreateImagePanel("RunSettingsPanel", new Vector2(900f, 562.5f), optionBackgroundSprite);

        ConfigureSettingsPanel();
    }

    void ConfigureSettingsPanel()
    {
        if (settingsPanel == null)
            return;

        ConfigureTMP(FindDeep(settingsPanel.transform, "OptionTitleText"), "설정 <<", 34f);
        ConfigureTMP(FindDeep(settingsPanel.transform, "BgmVolumeText"), "BGM 음량", 30f);
        ConfigureTMP(FindDeep(settingsPanel.transform, "SfxVolumeText"), "SFX 음량", 30f);

        Transform close = FindDeep(settingsPanel.transform, "OptionCloseHotspot");
        if (close != null)
            ConfigureHiddenButton(close, HidePanels);

        bgmVolumeFills = ConfigureVolumeCells("BgmVolumeCells", SoundManager.SetBgmVolumeLevel);
        sfxVolumeFills = ConfigureVolumeCells("SfxVolumeCells", SoundManager.SetSfxVolumeLevel);
        UpdateVolumeCells();
    }

    Image[] ConfigureVolumeCells(string groupName, System.Action<int> setter)
    {
        Image[] fills = new Image[10];
        Transform group = FindDeep(settingsPanel.transform, groupName);
        if (group == null)
            return fills;

        for (int i = 0; i < fills.Length; i++)
        {
            int level = i + 1;
            Transform cell = group.Find("Cell" + level.ToString("00"));
            if (cell == null)
                continue;

            ConfigureHiddenButton(cell, () =>
            {
                setter(level);
                SoundManager.PlayClick();
                UpdateVolumeCells();
            }, false);

            Transform fill = FindDeep(cell, "Fill");
            fills[i] = fill != null ? fill.GetComponent<Image>() : null;
        }

        return fills;
    }

    void UpdateVolumeCells()
    {
        UpdateVolumeCells(bgmVolumeFills, SoundManager.GetBgmVolumeLevel());
        UpdateVolumeCells(sfxVolumeFills, SoundManager.GetSfxVolumeLevel());
    }

    void UpdateVolumeCells(Image[] fills, int level)
    {
        if (fills == null)
            return;

        for (int i = 0; i < fills.Length; i++)
        {
            if (fills[i] == null)
                continue;

            fills[i].color = WithAlpha(panelLineColor, i < level ? 1f : 0f);
        }
    }

    void BuildSavePanel()
    {
        savePanel = InstantiateStartPanel(startRoadPanelPrefab, "RunSavePanel", "StartRoadPanel");
        if (savePanel == null)
            savePanel = CreateImagePanel("RunSavePanel", new Vector2(900f, 562.5f), optionBackgroundSprite);

        ConfigureSavePanel();
    }

    void ConfigureSavePanel()
    {
        if (savePanel == null)
            return;

        ConfigureTMP(FindDeep(savePanel.transform, "RoadTitleText"), "저장 <<", 34f);

        Transform close = FindDeep(savePanel.transform, "RoadCloseHotspot");
        if (close != null)
            ConfigureHiddenButton(close, HidePanels);

        Transform roadButton = FindDeep(savePanel.transform, "RoadButton");
        if (roadButton != null)
        {
            Button button = roadButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
        }

        saveNameLabels = new TextMeshProUGUI[GameSaveSystem.MaxSlots];
        saveDateLabels = new TextMeshProUGUI[GameSaveSystem.MaxSlots];

        float[] rowTop = { 97f, 120f, 144f, 167f };
        float[] rowBottom = { 120f, 144f, 167f, 190f };
        for (int i = 0; i < GameSaveSystem.MaxSlots; i++)
        {
            int slot = i;
            string rowName = "RoadSaveSlot" + (i + 1);
            DestroyChildIfExists(savePanel.transform, rowName + "Left");
            DestroyChildIfExists(savePanel.transform, rowName + "Right");

            Transform row = FindDeep(savePanel.transform, rowName);
            if (row == null)
                row = CreateTransparentRect(savePanel.transform, rowName, new Rect(36f, rowTop[i], 330f, rowBottom[i] - rowTop[i])).transform;

            ConfigureHiddenButton(row, () => AskSave(slot));

            TextMeshProUGUI nameLabel = EnsurePanelTMP(savePanel.transform, "RoadSaveSlotNameText" + (i + 1), new Rect(36f, rowTop[i], 75f, rowBottom[i] - rowTop[i]), 18f);
            TextMeshProUGUI dateLabel = EnsurePanelTMP(savePanel.transform, "RoadSaveSlotDateText" + (i + 1), new Rect(111f, rowTop[i], 255f, rowBottom[i] - rowTop[i]), 17f);
            saveNameLabels[i] = nameLabel;
            saveDateLabels[i] = dateLabel;
        }

        saveStatusLabel = EnsurePanelTMP(savePanel.transform, "RoadEmptySlotMessage", new Rect(111f, 195f, 255f, 35f), 25f);
        RefreshSavePanel();
    }

    TextMeshProUGUI EnsurePanelTMP(Transform parent, string textName, Rect pixelRect, float fontSize)
    {
        Transform existing = FindDeep(parent, textName);
        TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            GameObject go = new GameObject(textName);
            go.transform.SetParent(parent, false);
            text = go.AddComponent<TextMeshProUGUI>();
        }

        text.font = UIThinDungFont.Get(uiFont);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyPanelPixelRect(text.rectTransform, parent, pixelRect);
        return text;
    }

    void BuildConfirmPanel()
    {
        confirmPanel = InstantiateStartPanel(startExitPanelPrefab, "RunConfirmPanel", "StartExitPanel");
        if (confirmPanel == null)
            confirmPanel = CreateFallbackConfirmPanel();

        ConfigureConfirmPanel();
    }

    void ConfigureConfirmPanel()
    {
        if (confirmPanel == null)
            return;

        CanvasGroup group = confirmPanel.GetComponent<CanvasGroup>();
        if (group == null)
            group = confirmPanel.AddComponent<CanvasGroup>();

        Transform question = FindDeep(confirmPanel.transform, "question_ui");
        Transform answer1 = FindDeep(confirmPanel.transform, "answer1_ui");
        Transform answer2 = FindDeep(confirmPanel.transform, "answer2_ui");

        if (question == null || answer1 == null || answer2 == null)
        {
            Destroy(confirmPanel);
            confirmPanel = CreateFallbackConfirmPanel();
            question = FindDeep(confirmPanel.transform, "question_ui");
            answer1 = FindDeep(confirmPanel.transform, "answer1_ui");
            answer2 = FindDeep(confirmPanel.transform, "answer2_ui");
            group = confirmPanel.GetComponent<CanvasGroup>();
        }

        confirmQuestionText = EnsureChildLabel(question, "QuestionLabel", "", 46f);
        ConfigureTMP(confirmQuestionText != null ? confirmQuestionText.transform : null, "", 46f);
        ConfigureTMP(EnsureChildLabel(answer1, "AnswerLabel", "예", 52f).transform, "예", 52f);
        ConfigureTMP(EnsureChildLabel(answer2, "AnswerLabel", "아니오", 48f).transform, "아니오", 48f);

        confirmYesButton = ConfigureVisibleButton(answer1, () =>
        {
            SoundManager.PlayClick();
            RunUiPauseManager.ClearUiSelection();
            confirmYesAction?.Invoke();
        });

        confirmNoButton = ConfigureVisibleButton(answer2, () =>
        {
            SoundManager.PlayClick();
            RunUiPauseManager.ClearUiSelection();
            CloseConfirm();
        });

        SetConfirmVisible(false);
    }

    TextMeshProUGUI EnsureChildLabel(Transform parent, string labelName, string textValue, float fontSize)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(labelName);
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (label == null)
        {
            GameObject go = new GameObject(labelName);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            StretchToParent(rect);
            label = go.AddComponent<TextMeshProUGUI>();
        }

        label.font = UIThinDungFont.Get(uiFont);
        label.text = textValue;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    void BuildFadeImage()
    {
        GameObject go = new GameObject("RunMenuFadePanel");
        go.transform.SetParent(transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fadeImage = go.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        go.SetActive(false);
    }

    void ShowSettingsPanel()
    {
        HidePanels();
        if (settingsPanel == null)
            BuildSettingsPanel();
        UpdateVolumeCells();
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
        RefreshPauseLock();
    }

    void ShowSavePanel()
    {
        HidePanels();
        if (savePanel == null)
            BuildSavePanel();
        RefreshSavePanel();
        savePanel.SetActive(true);
        savePanel.transform.SetAsLastSibling();
        RefreshPauseLock();
    }

    void AskSave(int slot)
    {
        ShowConfirm("저장하시겠습니까?", () =>
        {
            GameSaveSystem.SaveSlot(slot);
            CloseConfirm();
            RefreshSavePanel();
            if (saveStatusLabel != null)
            {
                saveStatusLabel.text = "저장됨";
                saveStatusLabel.color = panelLineColor;
            }
            savePanel.SetActive(true);
            savePanel.transform.SetAsLastSibling();
        });
    }

    void ShowMainConfirm()
    {
        ShowConfirm("시작화면으로 돌아가시겠습니까?", () => StartCoroutine(ReturnToStartScene()));
    }

    void ShowExitConfirm()
    {
        ShowConfirm("정말로 게임을 종료하시겠습니까?", QuitGame);
    }

    void ShowConfirm(string question, System.Action yesAction)
    {
        HidePanels();
        if (confirmPanel == null)
            BuildConfirmPanel();
        if (confirmQuestionText != null)
            confirmQuestionText.text = question;
        confirmYesAction = yesAction;
        SetConfirmVisible(true);
        confirmPanel.transform.SetAsLastSibling();
        RefreshPauseLock();
    }

    void CloseConfirm()
    {
        confirmYesAction = null;
        SetConfirmVisible(false);
        RefreshPauseLock();
    }

    void SetConfirmVisible(bool visible)
    {
        if (confirmPanel == null)
            return;

        CanvasGroup group = confirmPanel.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        confirmPanel.SetActive(visible);
    }

    void RefreshSavePanel()
    {
        if (saveNameLabels == null || saveDateLabels == null)
            return;

        for (int i = 0; i < saveNameLabels.Length; i++)
        {
            GameSaveSystem.SlotInfo info = GameSaveSystem.GetSlotInfo(i);
            if (saveNameLabels[i] != null)
                saveNameLabels[i].text = info.exists ? info.saveName : "";
            if (saveDateLabels[i] != null)
                saveDateLabels[i].text = info.exists ? info.savedAt + " 저장됨" : "빈칸";
        }

        if (saveStatusLabel != null)
        {
            saveStatusLabel.text = "";
            saveStatusLabel.color = WithAlpha(panelLineColor, 0f);
        }
    }

    IEnumerator ReturnToStartScene()
    {
        if (fadeImage == null)
            BuildFadeImage();

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, fadeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color color = fadeImage.color;
                color.a = Mathf.Clamp01(elapsed / duration);
                fadeImage.color = color;
                yield return null;
            }
        }

        StartIntroSequence.SkipNextIntro = true;
        RunUiPauseManager.ClearAll();
        SceneManager.LoadScene(startSceneName);
        Destroy(gameObject);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void HidePanels()
    {
        StopMenuPanelAnimation();
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        SetConfirmVisible(false);
        RefreshPauseLock();
    }

    void RefreshPauseLock()
    {
        RunUiPauseManager.SetPaused("Menu", IsAnyOpen);
    }

    void PlayMenuPanelAnimation(bool show)
    {
        if (menuPanel == null)
            return;

        RectTransform rect = menuPanel.transform as RectTransform;
        if (rect == null)
            rect = menuPanel.AddComponent<RectTransform>();

        if (menuPanelAnimationRoutine != null)
            StopCoroutine(menuPanelAnimationRoutine);

        SoundManager.PlayPanel();
        menuPanelAnimationRoutine = StartCoroutine(MenuPanelAnimationRoutine(rect, show));
    }

    IEnumerator MenuPanelAnimationRoutine(RectTransform rect, bool show)
    {
        Vector2 shown = hasMenuPanelShownPosition ? menuPanelShownPosition : rect.anchoredPosition;
        Vector2 hidden = shown + Vector2.down * MenuPanelHiddenOffsetY;
        Vector2 overshoot = shown + Vector2.up * MenuPanelOvershootY;

        if (show)
        {
            rect.anchoredPosition = hidden;
            yield return StartCoroutine(AnimateMenuPanelSegment(rect, hidden, overshoot, 0.24f));
            yield return StartCoroutine(AnimateMenuPanelSegment(rect, overshoot, shown, 0.11f));
            rect.anchoredPosition = shown;
        }
        else
        {
            Vector2 from = rect.anchoredPosition;
            yield return StartCoroutine(AnimateMenuPanelSegment(rect, from, overshoot, 0.09f));
            yield return StartCoroutine(AnimateMenuPanelSegment(rect, overshoot, hidden, 0.24f));
            rect.anchoredPosition = shown;
            if (menuPanel != null)
                menuPanel.SetActive(false);
            RefreshPauseLock();
        }

        menuPanelAnimationRoutine = null;
    }

    IEnumerator AnimateMenuPanelSegment(RectTransform rect, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        rect.anchoredPosition = to;
    }

    void StopMenuPanelAnimation()
    {
        if (menuPanelAnimationRoutine == null)
            return;

        StopCoroutine(menuPanelAnimationRoutine);
        menuPanelAnimationRoutine = null;
        RectTransform rect = menuPanel != null ? menuPanel.transform as RectTransform : null;
        if (rect != null)
            rect.anchoredPosition = hasMenuPanelShownPosition ? menuPanelShownPosition : rect.anchoredPosition;
    }

    GameObject InstantiateStartPanel(GameObject prefab, string instanceName, string resourcesName)
    {
        GameObject source = prefab;
        if (source == null)
            source = Resources.Load<GameObject>("StartUIPanelPrefabs/" + resourcesName);
        if (source == null)
            return null;

        GameObject panel = Instantiate(source, transform, false);
        panel.name = instanceName;
        panel.SetActive(false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        return panel;
    }

    GameObject CreateImagePanel(string name, Vector2 size, Sprite sprite)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = panel.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        return panel;
    }

    GameObject CreateFallbackConfirmPanel()
    {
        GameObject panel = CreateImagePanel("RunConfirmPanel", new Vector2(900f, 562.5f), optionBackgroundSprite);
        panel.AddComponent<CanvasGroup>();
        CreateImage(panel.transform, "question_ui", questionSprite, new Vector2(0f, 96f), new Vector2(720f, 112f));
        CreateImage(panel.transform, "answer1_ui", questionSprite, new Vector2(-170f, -78f), new Vector2(250f, 76f));
        CreateImage(panel.transform, "answer2_ui", questionSprite, new Vector2(170f, -78f), new Vector2(250f, 76f));
        return panel;
    }

    Button CreateSpriteButton(Transform parent, string name, Vector2 position, Vector2 size, string label, float fontSize)
    {
        Image image = CreateImage(parent, name, questionSprite, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyVisibleButtonTint(button);
        button.onClick.AddListener(PlayClickSound);
        CreateLabel(image.transform, "Label", label, Vector2.zero, size, fontSize);
        return button;
    }

    Button GetOrCreateSpriteButton(Transform parent, string name, Vector2 position, Vector2 size, string label, float fontSize)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
            return CreateSpriteButton(parent, name, position, size, label, fontSize);

        RectTransform rect = existing as RectTransform;
        if (rect == null)
            rect = existing.gameObject.AddComponent<RectTransform>();

        Image image = existing.GetComponent<Image>();
        if (image == null)
            image = existing.gameObject.AddComponent<Image>();

        // 씬/프리팹에 이미 지정된 버튼 배경 스프라이트가 있으면 유지한다.
        if (image.sprite == null)
        {
            image.sprite = questionSprite;
            image.type = questionSprite != null && questionSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = existing.GetComponent<Button>();
        if (button == null)
            button = existing.gameObject.AddComponent<Button>();

        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        ApplyVisibleButtonTint(button);
        button.onClick.AddListener(PlayClickSound);
        EnsureButtonLabel(existing, label, fontSize);
        return button;
    }

    TextMeshProUGUI EnsureButtonLabel(Transform parent, string text, float fontSize)
    {
        Transform existing = parent.Find("Label");
        TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (label == null)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            StretchToParent(rect);
            label = go.AddComponent<TextMeshProUGUI>();
        }

        label.font = UIThinDungFont.Get(uiFont);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = TextColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    void PlayClickSound()
    {
        SoundManager.PlayClick();
        RunUiPauseManager.ClearUiSelection();
    }

    Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        return image;
    }

    TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get(uiFont);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = TextColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    Button ConfigureHiddenButton(Transform target, UnityAction action, bool playClick = true)
    {
        if (target == null)
            return null;

        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.gameObject.AddComponent<Image>();

        Button button = target.GetComponent<Button>();
        if (button == null)
            button = target.gameObject.AddComponent<Button>();

        image.color = WithAlpha(panelLineColor, 0f);
        image.raycastTarget = true;
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        if (playClick)
            button.onClick.AddListener(PlayClickSound);
        button.onClick.AddListener(action);

        StartPanelHoverTint hover = target.GetComponent<StartPanelHoverTint>();
        if (hover == null)
            hover = target.gameObject.AddComponent<StartPanelHoverTint>();
        hover.Configure(image, WithAlpha(panelLineColor, 0f), WithAlpha(panelLineColor, 0.34f), WithAlpha(panelLineColor, 0.50f));
        return button;
    }

    Button ConfigureVisibleButton(Transform target, UnityAction action)
    {
        if (target == null)
            return null;

        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.gameObject.AddComponent<Image>();

        Button button = target.GetComponent<Button>();
        if (button == null)
            button = target.gameObject.AddComponent<Button>();

        image.raycastTarget = true;
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        ApplyVisibleButtonTint(button);
        return button;
    }

    void ApplyVisibleButtonTint(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonHover;
        colors.pressedColor = ButtonPressed;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    GameObject CreateTransparentRect(Transform parent, string name, Rect panelPixelRect)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        ApplyPanelPixelRect(rect, parent, panelPixelRect);
        Image image = go.AddComponent<Image>();
        image.color = WithAlpha(panelLineColor, 0f);
        image.raycastTarget = true;
        return go;
    }

    void ApplyPanelPixelRect(RectTransform rect, Transform referenceParent, Rect panelPixelRect)
    {
        if (rect == null)
            return;

        Vector2 referenceSize = GetPanelReferenceSize(referenceParent);
        float scaleX = referenceSize.x / PanelSpriteSize.x;
        float scaleY = referenceSize.y / PanelSpriteSize.y;
        float centerX = panelPixelRect.x + panelPixelRect.width * 0.5f;
        float centerY = panelPixelRect.y + panelPixelRect.height * 0.5f;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2((centerX - PanelSpriteSize.x * 0.5f) * scaleX, (PanelSpriteSize.y * 0.5f - centerY) * scaleY);
        rect.sizeDelta = new Vector2(panelPixelRect.width * scaleX, panelPixelRect.height * scaleY);
    }

    Vector2 GetPanelReferenceSize(Transform referenceParent)
    {
        RectTransform rect = referenceParent as RectTransform;
        if (rect == null && referenceParent != null)
            rect = referenceParent.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 size = rect.rect.size;
            if (size.x > 0.01f && size.y > 0.01f)
                return size;
        }

        return PanelSpriteSize;
    }

    void ConfigureTMP(Transform target, string value, float fontSize)
    {
        if (target == null)
            return;

        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        if (text == null)
            return;

        text.font = UIThinDungFont.Get(uiFont);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    Transform FindDeep(Transform parent, string childName)
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

    void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform child = FindDeep(parent, childName);
        if (child == null)
            return;

        if (Application.isPlaying)
            Destroy(child.gameObject);
        else
            DestroyImmediate(child.gameObject);
    }

    Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    void EnsureAssets()
    {
        if (startOptionPanelPrefab == null)
            startOptionPanelPrefab = Resources.Load<GameObject>("StartUIPanelPrefabs/StartOptionPanel");
        if (startRoadPanelPrefab == null)
            startRoadPanelPrefab = Resources.Load<GameObject>("StartUIPanelPrefabs/StartRoadPanel");
        if (startExitPanelPrefab == null)
            startExitPanelPrefab = Resources.Load<GameObject>("StartUIPanelPrefabs/StartExitPanel");

#if UNITY_EDITOR
        if (optionBackgroundSprite == null)
            optionBackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/startscene/option2.png");
        if (questionSprite == null)
            questionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/startscene/question.png");
        if (uiFont == null)
            uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/ThinDungGeunMo SDF.asset");
#endif
    }
}
