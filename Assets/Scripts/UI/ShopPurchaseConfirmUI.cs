using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// task6/7: 상점에서 아이템 근처 + E → 시작화면 종료 패널(StartExitPanel)을 복제해
// "이 아이템을 구매하시겠습니까?" 예/아니오 확인창을 띄운다.
// 예 → onConfirm(동전 차감), 아니오 → 그냥 닫힘.
public class ShopPurchaseConfirmUI : MonoBehaviour
{
    const string PauseKey = "ShopPurchase";
    static ShopPurchaseConfirmUI instance;

    public static bool IsOpen { get; private set; }

    GameObject panel;
    CanvasGroup group;
    Action onConfirm;

    public static void Present(string question, Action confirmAction)
    {
        EnsureInstance();
        if (instance != null)
            instance.Show(question, confirmAction);
    }

    public static void DismissIfOpen()
    {
        if (instance != null && IsOpen)
            instance.Hide();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("ShopPurchaseConfirmUI");
        instance = go.AddComponent<ShopPurchaseConfirmUI>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildCanvas();
        BuildPanel();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            if (IsOpen)
                RunUiPauseManager.SetPaused(PauseKey, false);
            IsOpen = false;
        }
    }

    void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        // 프로젝트가 새 InputSystem을 사용하므로 대응하는 UI 모듈을 붙인다.
        es.AddComponent<InputSystemUIInputModule>();
    }

    void BuildPanel()
    {
        GameObject prefab = Resources.Load<GameObject>("StartUIPanelPrefabs/StartExitPanel");
        if (prefab != null)
        {
            panel = Instantiate(prefab, transform, false);
            panel.name = "ShopConfirmPanel";
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        if (panel == null)
            panel = CreateFallbackPanel();

        group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.AddComponent<CanvasGroup>();

        Transform question = FindDeep(panel.transform, "question_ui");
        Transform answer1 = FindDeep(panel.transform, "answer1_ui");
        Transform answer2 = FindDeep(panel.transform, "answer2_ui");

        if (question == null || answer1 == null || answer2 == null)
        {
            Destroy(panel);
            panel = CreateFallbackPanel();
            group = panel.GetComponent<CanvasGroup>();
            question = FindDeep(panel.transform, "question_ui");
            answer1 = FindDeep(panel.transform, "answer1_ui");
            answer2 = FindDeep(panel.transform, "answer2_ui");
        }

        SetAllLabels(answer1, "예", 52f);
        SetAllLabels(answer2, "아니오", 48f);

        ConfigureButton(answer1, () =>
        {
            SoundManager.PlayClick();
            Action action = onConfirm;
            Hide();
            action?.Invoke();
        });

        ConfigureButton(answer2, () =>
        {
            SoundManager.PlayClick();
            Hide();
        });

        // question 노드의 라벨은 Show()에서 매번 채운다.
        questionRoot = question;
    }

    Transform questionRoot;

    void Show(string question, Action confirmAction)
    {
        onConfirm = confirmAction;
        SetAllLabels(questionRoot, question, 40f);
        SetVisible(true);
        panel.transform.SetAsLastSibling();

        if (!IsOpen)
        {
            IsOpen = true;
            RunUiPauseManager.SetPaused(PauseKey, true);
        }
    }

    void Hide()
    {
        onConfirm = null;
        SetVisible(false);

        if (IsOpen)
        {
            IsOpen = false;
            RunUiPauseManager.SetPaused(PauseKey, false);
        }
    }

    void SetVisible(bool visible)
    {
        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
        if (panel != null)
            panel.SetActive(visible);
    }

    // 노드 아래의 모든 TextMeshProUGUI 텍스트를 동일하게 세팅(내부 구조에 의존하지 않도록).
    static void SetAllLabels(Transform node, string text, float fontSize)
    {
        if (node == null)
            return;

        TextMeshProUGUI[] labels = node.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels.Length == 0)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(node, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI created = go.AddComponent<TextMeshProUGUI>();
            created.font = UIThinDungFont.Get();
            created.alignment = TextAlignmentOptions.Center;
            created.color = Color.black;
            created.raycastTarget = false;
            created.textWrappingMode = TextWrappingModes.NoWrap;
            created.fontSize = fontSize;
            created.text = text;
            return;
        }

        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].text = text;
            labels[i].raycastTarget = false;
        }
    }

    static void ConfigureButton(Transform node, UnityEngine.Events.UnityAction action)
    {
        if (node == null)
            return;

        Image image = node.GetComponent<Image>();
        if (image == null)
            image = node.gameObject.AddComponent<Image>();
        image.raycastTarget = true;

        Button button = node.GetComponent<Button>();
        if (button == null)
            button = node.gameObject.AddComponent<Button>();

        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
        colors.pressedColor = new Color(0.82f, 0.72f, 0.55f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    GameObject CreateFallbackPanel()
    {
        GameObject root = new GameObject("ShopConfirmPanel");
        root.transform.SetParent(transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(720f, 420f);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.08f, 0.06f, 0.96f);
        root.AddComponent<CanvasGroup>();

        CreateNode(root.transform, "question_ui", new Vector2(0f, 96f), new Vector2(620f, 120f), new Color(0f, 0f, 0f, 0f));
        CreateNode(root.transform, "answer1_ui", new Vector2(-150f, -110f), new Vector2(230f, 90f), new Color(0.55f, 0.75f, 0.55f, 1f));
        CreateNode(root.transform, "answer2_ui", new Vector2(150f, -110f), new Vector2(230f, 90f), new Color(0.75f, 0.5f, 0.5f, 1f));
        return root;
    }

    static void CreateNode(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
    }

    static Transform FindDeep(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
