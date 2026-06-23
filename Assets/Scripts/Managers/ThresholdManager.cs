using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ThresholdManager : MonoBehaviour
{
    [Header("Vision Overlay")]
    [SerializeField] Color overlayColor = Color.black;
    [SerializeField, Range(0f, 1f)] float overlayAlpha = 0.72f;

    [Header("Scene")]
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField] string middleBossSceneName = "MiddleBossScene";
    [SerializeField] string finalBossSceneName = "BookBossScene";
    [SerializeField] int overlaySortingOrder = 60;
    [SerializeField] Canvas overlayCanvas;
    [SerializeField] Image leftOverlay;
    [SerializeField] Image rightOverlay;

    BodyManager bodyManager;

    void Awake()
    {
        bodyManager = BodyManager.Instance;
        if (overlayCanvas == null)
            BuildOverlay();
    }

    void Update()
    {
        if (bodyManager == null)
            bodyManager = BodyManager.Instance;

        ApplyOverlayColor();

        if (overlayCanvas != null)
            overlayCanvas.sortingOrder = overlaySortingOrder;

        if (!IsRoomOrBossScene())
        {
            if (leftOverlay != null)  leftOverlay.gameObject.SetActive(false);
            if (rightOverlay != null) rightOverlay.gameObject.SetActive(false);
            return;
        }

        UpdateOverlay();
    }

    void OnValidate()
    {
        overlayAlpha = Mathf.Clamp01(overlayAlpha);
        ApplyOverlayColor();
    }

    bool IsRoomOrBossScene()
    {
        string name = SceneManager.GetActiveScene().name;
        return name == roomSceneName
            || name == bossSceneName
            || name == middleBossSceneName
            || name == finalBossSceneName;
    }

    void UpdateOverlay()
    {
        if (leftOverlay == null || rightOverlay == null)
            return;

        BodyState state = BodyConditionUtility.CurrentState();

        bool showLeft  = !state.eyeLeft  && state.eyeRight;
        bool showRight = !state.eyeRight && state.eyeLeft;
        bool showBoth  = !state.eyeLeft && !state.eyeRight;

        leftOverlay.gameObject.SetActive(showLeft || showBoth);
        rightOverlay.gameObject.SetActive(showRight || showBoth);
    }

    void BuildOverlay()
    {
        var canvasGO = new GameObject("ThresholdOverlayCanvas");
        canvasGO.hideFlags = HideFlags.HideAndDontSave;
        canvasGO.transform.SetParent(transform, false);

        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = overlaySortingOrder;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        leftOverlay = CreateOverlayImage("LeftEyeBlock", new Vector2(0f, 0f), new Vector2(0.5f, 1f));
        rightOverlay = CreateOverlayImage("RightEyeBlock", new Vector2(0.5f, 0f), new Vector2(1f, 1f));

        leftOverlay.gameObject.SetActive(false);
        rightOverlay.gameObject.SetActive(false);
    }

    Image CreateOverlayImage(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var imageGO = new GameObject(name);
        imageGO.hideFlags = HideFlags.HideAndDontSave;
        imageGO.transform.SetParent(overlayCanvas.transform, false);

        var image = imageGO.AddComponent<Image>();
        image.color = CurrentOverlayColor();
        image.raycastTarget = false;

        var rt = image.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return image;
    }

    void ApplyOverlayColor()
    {
        Color color = CurrentOverlayColor();
        if (leftOverlay != null)
            leftOverlay.color = color;
        if (rightOverlay != null)
            rightOverlay.color = color;
    }

    Color CurrentOverlayColor()
    {
        Color color = overlayColor;
        color.a = overlayAlpha;
        return color;
    }
}
