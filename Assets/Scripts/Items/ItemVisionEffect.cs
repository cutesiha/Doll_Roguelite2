using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ItemVisionEffect : MonoBehaviour
{
    Camera sourceCamera;
    Camera captureCamera;
    RenderTexture renderTexture;
    RawImage overlay;
    Material visionMaterial;
    bool flipVertical;
    bool pixelated;
    bool inverted;
    bool buttonView;
    int lastWidth;
    int lastHeight;

    void Awake()
    {
        sourceCamera = GetComponent<Camera>();
    }

    void OnDestroy()
    {
        ReleaseRenderTexture();
        if (captureCamera != null)
            Destroy(captureCamera.gameObject);
        if (overlay != null)
            Destroy(overlay.transform.root.gameObject);
        if (visionMaterial != null)
            Destroy(visionMaterial);
    }

    public void Configure(bool verticalFlip, bool lowResolution, bool invertColors, bool buttonMask)
    {
        flipVertical = verticalFlip;
        pixelated = lowResolution;
        inverted = invertColors;
        buttonView = buttonMask;

        bool active = flipVertical || pixelated || inverted || buttonView;
        EnsureObjects();
        if (overlay != null)
            overlay.gameObject.SetActive(active);
        if (captureCamera != null)
            captureCamera.gameObject.SetActive(active);
        if (active)
            EnsureRenderTexture(true);
    }

    void LateUpdate()
    {
        if (overlay == null || !overlay.gameObject.activeSelf || sourceCamera == null)
            return;

        EnsureRenderTexture(false);
        SyncCaptureCamera();
        captureCamera.Render();

        overlay.uvRect = flipVertical ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        if (visionMaterial != null)
            visionMaterial.SetFloat("_Mode", buttonView ? 2f : inverted ? 1f : 0f);
    }

    void EnsureObjects()
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();
        if (sourceCamera == null)
            return;

        if (captureCamera == null)
        {
            GameObject cameraObject = new GameObject("_ItemVisionCaptureCamera");
            cameraObject.transform.SetParent(sourceCamera.transform, false);
            captureCamera = cameraObject.AddComponent<Camera>();
            UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderShadows = false;
            captureCamera.enabled = false;
        }

        if (overlay == null)
        {
            GameObject canvasObject = new GameObject("_ItemVisionCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -100;
            canvasObject.AddComponent<CanvasScaler>();

            GameObject imageObject = new GameObject("VisionOverlay");
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay = imageObject.AddComponent<RawImage>();
            overlay.raycastTarget = false;

            Shader shader = Shader.Find("Hidden/DollRoguelite/ItemVision");
            if (shader != null)
            {
                visionMaterial = new Material(shader);
                overlay.material = visionMaterial;
            }
        }
    }

    void EnsureRenderTexture(bool force)
    {
        int divisor = pixelated ? 7 : 1;
        int width = Mathf.Max(32, Screen.width / divisor);
        int height = Mathf.Max(18, Screen.height / divisor);
        if (!force && renderTexture != null && width == lastWidth && height == lastHeight)
            return;

        ReleaseRenderTexture();
        lastWidth = width;
        lastHeight = height;
        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "_ItemVisionRT",
            filterMode = pixelated ? FilterMode.Point : FilterMode.Bilinear
        };
        renderTexture.Create();
        if (captureCamera != null)
            captureCamera.targetTexture = renderTexture;
        if (overlay != null)
            overlay.texture = renderTexture;
    }

    void SyncCaptureCamera()
    {
        if (captureCamera == null || sourceCamera == null)
            return;

        RenderTexture target = captureCamera.targetTexture;
        captureCamera.CopyFrom(sourceCamera);
        captureCamera.targetTexture = target;
        captureCamera.enabled = false;
        captureCamera.transform.localPosition = Vector3.zero;
        captureCamera.transform.localRotation = Quaternion.identity;
        captureCamera.transform.localScale = Vector3.one;
    }

    void ReleaseRenderTexture()
    {
        if (renderTexture == null)
            return;
        renderTexture.Release();
        Destroy(renderTexture);
        renderTexture = null;
    }
}
