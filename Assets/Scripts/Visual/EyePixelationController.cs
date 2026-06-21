using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class EyePixelationController : MonoBehaviour
{
    public static EyePixelationController Instance { get; private set; }

    Camera pixelCam;
    RenderTexture pixelRT;
    GameObject leftOverlay;
    GameObject rightOverlay;
    bool leftEyeLost;
    bool rightEyeLost;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        CreatePixelCamera();
        CreateOverlays();
        UpdateState();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pixelRT != null) { pixelRT.Release(); Destroy(pixelRT); }
    }

    void CreatePixelCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        int w = Mathf.Max(1, Screen.width / 2);
        int h = Mathf.Max(1, Screen.height / 2);
        pixelRT = new RenderTexture(w, h, 24);
        pixelRT.filterMode = FilterMode.Point;
        pixelRT.Create();

        GameObject go = new GameObject("_EyePixelCam");
        go.transform.SetParent(mainCam.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        pixelCam = go.AddComponent<Camera>();
        pixelCam.orthographic = mainCam.orthographic;
        pixelCam.orthographicSize = mainCam.orthographicSize;
        pixelCam.nearClipPlane = mainCam.nearClipPlane;
        pixelCam.farClipPlane = mainCam.farClipPlane;
        pixelCam.cullingMask = mainCam.cullingMask;
        pixelCam.clearFlags = mainCam.clearFlags;
        pixelCam.backgroundColor = mainCam.backgroundColor;
        pixelCam.depth = mainCam.depth - 1;
        pixelCam.targetTexture = pixelRT;
        pixelCam.enabled = false;

        var urpData = go.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderType = CameraRenderType.Base;
        urpData.renderShadows = false;
        urpData.requiresColorOption = CameraOverrideOption.Off;
        urpData.requiresDepthOption = CameraOverrideOption.Off;
    }

    void CreateOverlays()
    {
        if (pixelRT == null) return;

        GameObject canvasGO = new GameObject("_EyePixelCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();

        // 왼쪽 눈 오버레이: 화면 왼쪽 절반, RT의 왼쪽 절반 표시
        leftOverlay  = CreateHalfImage(canvasGO.transform, "_LeftEyeOverlay",
            new Vector2(0f, 0f), new Vector2(0.5f, 1f),
            new Rect(0f, 0f, 0.5f, 1f));

        // 오른쪽 눈 오버레이: 화면 오른쪽 절반, RT의 오른쪽 절반 표시
        rightOverlay = CreateHalfImage(canvasGO.transform, "_RightEyeOverlay",
            new Vector2(0.5f, 0f), new Vector2(1f, 1f),
            new Rect(0.5f, 0f, 0.5f, 1f));

        leftOverlay.SetActive(false);
        rightOverlay.SetActive(false);
    }

    GameObject CreateHalfImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Rect uvRect)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        RawImage img = go.AddComponent<RawImage>();
        img.texture = pixelRT;
        img.uvRect = uvRect;
        return go;
    }

    public void OnEyeFallen(BodySlot slot)
    {
        if (slot == BodySlot.EyeLeft)  leftEyeLost  = true;
        if (slot == BodySlot.EyeRight) rightEyeLost = true;
        UpdateState();
    }

    void UpdateState()
    {
        bool anyLost = leftEyeLost || rightEyeLost;
        if (pixelCam != null) pixelCam.enabled = anyLost;
        if (leftOverlay  != null) leftOverlay.SetActive(leftEyeLost);
        if (rightOverlay != null) rightOverlay.SetActive(rightEyeLost);
    }
}
