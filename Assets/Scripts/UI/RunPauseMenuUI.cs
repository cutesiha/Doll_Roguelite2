using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RunPauseMenuUI : MonoBehaviour
{
    [SerializeField] Button menuButton;
    [SerializeField] Sprite optionBackgroundSprite;
    [SerializeField] Sprite questionSprite;
    [SerializeField] TMP_FontAsset uiFont;
    [SerializeField] string startSceneName = "StartScene";
    [SerializeField] float fadeDuration = 0.35f;

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
    System.Action confirmYesAction;

    static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    static readonly Color TextColor = new Color(0.17f, 0.11f, 0.06f, 1f);
    static readonly Color ButtonHover = new Color(0.64f, 0.45f, 0.28f, 1f);
    static readonly Color ButtonPressed = new Color(0.48f, 0.31f, 0.18f, 1f);
    static readonly Vector2 PanelSize = new Vector2(560f, 250f);

    void Awake()
    {
        EnsureAssets();
        Build();
    }

    public void SetMenuButton(Button button)
    {
        if (menuButton == button)
            return;

        if (menuButton != null)
            menuButton.onClick.RemoveListener(ToggleMenu);

        menuButton = button;

        if (menuButton != null)
        {
            menuButton.onClick.RemoveListener(ToggleMenu);
            menuButton.onClick.RemoveListener(OnMenuButtonClicked);
            menuButton.onClick.AddListener(OnMenuButtonClicked);
        }
    }

    void OnMenuButtonClicked()
    {
        SoundManager.PlayClick();
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        bool wasVisible = menuPanel != null && menuPanel.activeSelf;
        Build();
        HidePanels();
        if (menuPanel != null && !wasVisible)
        {
            menuPanel.SetActive(true);
            menuPanel.transform.SetAsLastSibling();
        }
    }

    public void CloseAll()
    {
        HidePanels();
    }

    void Build()
    {
        EnsureAssets();
        if (menuPanel == null)
            BuildMenuPanel();
        if (settingsPanel == null)
            BuildSettingsPanel();
        if (savePanel == null)
            BuildSavePanel();
        if (confirmPanel == null)
            BuildConfirmPanel();
        if (fadeImage == null)
            BuildFadeImage();

        HidePanels();
    }

    void BuildMenuPanel()
    {
        menuPanel = CreateImagePanel("RunMenuPanel", PanelSize, optionBackgroundSprite);
        string[] labels = { "설정", "저장", "메인으로", "나가기" };
        UnityEngine.Events.UnityAction[] actions =
        {
            ShowSettingsPanel,
            ShowSavePanel,
            ShowMainConfirm,
            ShowExitConfirm
        };

        for (int i = 0; i < labels.Length; i++)
        {
            Button button = CreateSpriteButton(menuPanel.transform, "RunMenuButton_" + labels[i], new Vector2(0f, 75f - i * 50f), new Vector2(440f, 44f), labels[i], 34f);
            button.onClick.AddListener(actions[i]);
        }
    }

    void BuildSettingsPanel()
    {
        settingsPanel = CreateImagePanel("RunSettingsPanel", new Vector2(760f, 440f), optionBackgroundSprite);
        CreateLabel(settingsPanel.transform, "SettingsTitle", "설정", new Vector2(0f, 150f), new Vector2(500f, 58f), 42f);
        CreateLabel(settingsPanel.transform, "BgmLabel", "BGM", new Vector2(-270f, 70f), new Vector2(120f, 40f), 28f);
        CreateLabel(settingsPanel.transform, "SfxLabel", "SFX", new Vector2(-270f, -10f), new Vector2(120f, 40f), 28f);
        BuildVolumeRow("Bgm", 70f, SoundManager.GetBgmVolumeLevel(), SoundManager.SetBgmVolumeLevel);
        BuildVolumeRow("Sfx", -10f, SoundManager.GetSfxVolumeLevel(), SoundManager.SetSfxVolumeLevel);
        CreateSpriteButton(settingsPanel.transform, "SettingsCloseButton", new Vector2(0f, -145f), new Vector2(240f, 50f), "닫기", 30f)
            .onClick.AddListener(HidePanels);
    }

    void BuildVolumeRow(string prefix, float y, int currentLevel, System.Action<int> setter)
    {
        for (int i = 0; i <= 10; i++)
        {
            int level = i;
            Button button = CreateSpriteButton(settingsPanel.transform, prefix + "Volume_" + i, new Vector2(-170f + i * 34f, y), new Vector2(28f, 34f), i < currentLevel ? "■" : "□", 22f);
            button.onClick.AddListener(() =>
            {
                setter(level);
                RebuildSettingsPanel();
            });
        }
    }

    void RebuildSettingsPanel()
    {
        if (settingsPanel != null)
            Destroy(settingsPanel);
        settingsPanel = null;
        BuildSettingsPanel();
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    void BuildSavePanel()
    {
        savePanel = CreateImagePanel("RunSavePanel", new Vector2(900f, 562.5f), optionBackgroundSprite);
        CreateLabel(savePanel.transform, "SaveTitle", "저장", new Vector2(0f, 205f), new Vector2(480f, 58f), 42f);
        saveNameLabels = new TextMeshProUGUI[GameSaveSystem.MaxSlots];
        saveDateLabels = new TextMeshProUGUI[GameSaveSystem.MaxSlots];

        for (int i = 0; i < GameSaveSystem.MaxSlots; i++)
        {
            float y = 112f - i * 58f;
            int slot = i;
            Button left = CreateSpriteButton(savePanel.transform, "SaveSlotLeft_" + i, new Vector2(-260f, y), new Vector2(170f, 46f), "", 23f);
            Button right = CreateSpriteButton(savePanel.transform, "SaveSlotRight_" + i, new Vector2(70f, y), new Vector2(470f, 46f), "", 23f);
            left.onClick.AddListener(() => AskSave(slot));
            right.onClick.AddListener(() => AskSave(slot));
            saveNameLabels[i] = left.GetComponentInChildren<TextMeshProUGUI>(true);
            saveDateLabels[i] = right.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        saveStatusLabel = CreateLabel(savePanel.transform, "SaveStatus", "", new Vector2(0f, -145f), new Vector2(600f, 40f), 25f);
        CreateSpriteButton(savePanel.transform, "SaveCloseButton", new Vector2(0f, -205f), new Vector2(240f, 50f), "닫기", 30f)
            .onClick.AddListener(HidePanels);
        RefreshSavePanel();
    }

    void BuildConfirmPanel()
    {
        confirmPanel = new GameObject("RunConfirmPanel");
        confirmPanel.transform.SetParent(transform, false);
        RectTransform rect = confirmPanel.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(620f, 250f);

        Image blocker = confirmPanel.AddComponent<Image>();
        blocker.color = Clear;
        blocker.raycastTarget = true;

        Image question = CreateImage(confirmPanel.transform, "question_ui", questionSprite, new Vector2(0f, 52f), new Vector2(570f, 86f));
        confirmQuestionText = CreateLabel(question.transform, "QuestionLabel", "", Vector2.zero, new Vector2(520f, 68f), 31f);

        confirmYesButton = CreateSpriteButton(confirmPanel.transform, "answer1_ui", new Vector2(-135f, -64f), new Vector2(210f, 66f), "예", 34f);
        confirmNoButton = CreateSpriteButton(confirmPanel.transform, "answer2_ui", new Vector2(135f, -64f), new Vector2(210f, 66f), "아니오", 34f);
        confirmYesButton.onClick.AddListener(() => confirmYesAction?.Invoke());
        confirmNoButton.onClick.AddListener(CloseConfirm);
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
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    void ShowSavePanel()
    {
        HidePanels();
        RefreshSavePanel();
        savePanel.SetActive(true);
        savePanel.transform.SetAsLastSibling();
    }

    void AskSave(int slot)
    {
        ShowConfirm("저장하시겠습니까?", () =>
        {
            GameSaveSystem.SaveSlot(slot);
            CloseConfirm();
            RefreshSavePanel();
            saveStatusLabel.text = "저장됨";
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
        confirmQuestionText.text = question;
        confirmYesAction = yesAction;
        confirmPanel.SetActive(true);
        confirmPanel.transform.SetAsLastSibling();
    }

    void CloseConfirm()
    {
        confirmYesAction = null;
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
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
            saveStatusLabel.text = "";
    }

    IEnumerator ReturnToStartScene()
    {
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
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
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

    Button CreateSpriteButton(Transform parent, string name, Vector2 position, Vector2 size, string label, float fontSize)
    {
        Image image = CreateImage(parent, name, questionSprite, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonHover;
        colors.pressedColor = ButtonPressed;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(PlayClickSound);
        CreateLabel(image.transform, "Label", label, Vector2.zero, size, fontSize);
        return button;
    }

    void PlayClickSound()
    {
        SoundManager.PlayClick();
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

    void EnsureAssets()
    {
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
