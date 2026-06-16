using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class RoomSceneProloguePanel : MonoBehaviour
{
    const string RoomSceneName = "RoomScene";
    const string ObjectName = "RoomSceneProloguePanel";
    const string LegacyObjectName = "_RoomSceneProloguePanel";
    static readonly Color OverlayColor = new Color(1f, 1f, 1f, 0.28f);
    static readonly Color Brown = new Color(0.30f, 0.18f, 0.10f, 1f);

    float previousTimeScale = 1f;
    bool paused;
    bool closing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate(scene);
    }

    static void TryCreate(Scene scene)
    {
        if (!scene.IsValid() || scene.name != RoomSceneName)
            return;

        GameObject existing = FindSceneObject(scene, ObjectName);
        if (existing == null)
            existing = FindSceneObject(scene, LegacyObjectName);

        if (existing != null)
        {
            if (existing.GetComponent<RoomSceneProloguePanel>() == null)
                existing.AddComponent<RoomSceneProloguePanel>();

            existing.SetActive(true);
            return;
        }

        GameObject root = new GameObject(ObjectName, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<RoomSceneProloguePanel>();
    }

    static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go != null && go.name == objectName && go.scene == scene)
                return go;
        }

        return null;
    }

    void Awake()
    {
        BuildPanel();
        PauseRoom();
    }

    void Update()
    {
        if (WasPointerPressed())
            Close();
    }

    void OnDestroy()
    {
        RestoreTimeScale();
    }

    void BuildPanel()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = GetComponent<Image>();
        if (overlay == null)
            overlay = gameObject.AddComponent<Image>();
        overlay.color = OverlayColor;
        overlay.raycastTarget = true;

        Button clickCatcher = GetComponent<Button>();
        if (clickCatcher == null)
            clickCatcher = gameObject.AddComponent<Button>();
        clickCatcher.transition = Selectable.Transition.None;
        clickCatcher.targetGraphic = overlay;
        clickCatcher.onClick.RemoveListener(Close);
        clickCatcher.onClick.AddListener(Close);

        if (transform.Find("InventoryHint") == null)
        {
            CreateHintBox(
                "InventoryHint",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-36f, 126f),
                new Vector2(540f, 76f),
                "[Tap] 키를 눌러 부위를 교체하세요");
        }

        if (transform.Find("MapHint") == null)
        {
            CreateHintBox(
                "MapHint",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-36f, -170f),
                new Vector2(540f, 76f),
                "[M] 키를 눌러 맵을 확인하세요");
        }
    }

    void CreateHintBox(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, string text)
    {
        GameObject box = new GameObject(name, typeof(RectTransform));
        box.transform.SetParent(transform, false);

        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        CreateDashedBorder(rect, size);

        GameObject labelObject = new GameObject("Text", typeof(RectTransform));
        labelObject.transform.SetParent(box.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 6f);
        labelRect.offsetMax = new Vector2(-18f, -6f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = UIThinDungFont.Get();
        label.text = text;
        label.color = Brown;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    void CreateDashedBorder(RectTransform parent, Vector2 size)
    {
        const float dash = 18f;
        const float gap = 10f;
        const float thickness = 3f;

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        for (float x = -halfWidth; x < halfWidth; x += dash + gap)
        {
            float length = Mathf.Min(dash, halfWidth - x);
            float center = x + length * 0.5f;
            CreateDash(parent, new Vector2(center, halfHeight), new Vector2(length, thickness));
            CreateDash(parent, new Vector2(center, -halfHeight), new Vector2(length, thickness));
        }

        for (float y = -halfHeight; y < halfHeight; y += dash + gap)
        {
            float length = Mathf.Min(dash, halfHeight - y);
            float center = y + length * 0.5f;
            CreateDash(parent, new Vector2(-halfWidth, center), new Vector2(thickness, length));
            CreateDash(parent, new Vector2(halfWidth, center), new Vector2(thickness, length));
        }
    }

    void CreateDash(RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject dash = new GameObject("Dash", typeof(RectTransform));
        dash.transform.SetParent(parent, false);

        RectTransform rect = dash.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = dash.AddComponent<Image>();
        image.color = Brown;
        image.raycastTarget = false;
    }

    bool WasPointerPressed()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    void Close()
    {
        if (closing)
            return;

        closing = true;
        RestoreTimeScale();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    void PauseRoom()
    {
        if (paused)
            return;

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        paused = true;
    }

    void RestoreTimeScale()
    {
        if (!paused)
            return;

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        paused = false;
    }
}
