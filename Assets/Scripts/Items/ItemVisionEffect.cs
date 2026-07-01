using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 왼쪽/오른쪽 눈에 장착된 아이템의 시야 효과(반전/픽셀화/색반전/단추뷰)를
// 각각 화면의 왼쪽 절반, 오른쪽 절반에만 독립적으로 적용한다.
[DisallowMultipleComponent]
public class ItemVisionEffect : MonoBehaviour
{
    Camera sourceCamera;
    Canvas canvas;
    readonly Channel left = new Channel(true);
    readonly Channel right = new Channel(false);

    void Awake()
    {
        sourceCamera = GetComponent<Camera>();
    }

    void OnDestroy()
    {
        left.Dispose();
        right.Dispose();
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    public void Configure(
        bool leftFlip, bool leftPixelated, bool leftInverted, bool leftButton,
        bool rightFlip, bool rightPixelated, bool rightInverted, bool rightButton)
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();
        if (sourceCamera == null)
            return;

        EnsureCanvas();
        left.Configure(leftFlip, leftPixelated, leftInverted, leftButton, sourceCamera, canvas.transform);
        right.Configure(rightFlip, rightPixelated, rightInverted, rightButton, sourceCamera, canvas.transform);
    }

    void LateUpdate()
    {
        if (sourceCamera == null)
            return;

        left.Render(sourceCamera);
        right.Render(sourceCamera);
    }

    void EnsureCanvas()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("_ItemVisionCanvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -100;
        canvasObject.AddComponent<CanvasScaler>();
    }

    // 화면 절반(좌/우) 하나를 담당하는 캡처 카메라 + 오버레이 묶음.
    class Channel
    {
        readonly bool isLeft;
        bool flipVertical;
        bool pixelated;
        bool inverted;
        bool buttonView;
        Camera captureCamera;
        RenderTexture renderTexture;
        RawImage overlay;
        Material visionMaterial;
        int lastWidth;
        int lastHeight;

        public Channel(bool isLeft)
        {
            this.isLeft = isLeft;
        }

        public void Configure(bool flip, bool pix, bool inv, bool btn, Camera sourceCamera, Transform canvasParent)
        {
            flipVertical = flip;
            pixelated = pix;
            inverted = inv;
            buttonView = btn;

            bool active = flipVertical || pixelated || inverted || buttonView;
            EnsureObjects(sourceCamera, canvasParent);
            if (overlay != null)
                overlay.gameObject.SetActive(active);
            if (captureCamera != null)
                captureCamera.gameObject.SetActive(active);
            if (active)
                EnsureRenderTexture(true);
        }

        public void Render(Camera sourceCamera)
        {
            if (overlay == null || !overlay.gameObject.activeSelf || sourceCamera == null)
                return;

            EnsureRenderTexture(false);
            SyncCaptureCamera(sourceCamera);
            captureCamera.Render();

            // 캡처된 전체 화면 프레임 중 이 채널이 담당하는 절반(좌/우)만 표시한다.
            float u0 = isLeft ? 0f : 0.5f;
            float v0 = flipVertical ? 1f : 0f;
            float vHeight = flipVertical ? -1f : 1f;
            overlay.uvRect = new Rect(u0, v0, 0.5f, vHeight);

            if (visionMaterial != null)
                visionMaterial.SetFloat("_Mode", buttonView ? 2f : inverted ? 1f : 0f);
        }

        void EnsureObjects(Camera sourceCamera, Transform canvasParent)
        {
            if (captureCamera == null)
            {
                GameObject cameraObject = new GameObject(isLeft ? "_ItemVisionCaptureCamera_L" : "_ItemVisionCaptureCamera_R");
                cameraObject.transform.SetParent(sourceCamera.transform, false);
                captureCamera = cameraObject.AddComponent<Camera>();
                UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Base;
                data.renderShadows = false;
                captureCamera.enabled = false;
            }

            if (overlay == null)
            {
                GameObject imageObject = new GameObject(isLeft ? "VisionOverlay_L" : "VisionOverlay_R");
                imageObject.transform.SetParent(canvasParent, false);
                RectTransform rect = imageObject.AddComponent<RectTransform>();
                rect.anchorMin = isLeft ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
                rect.anchorMax = isLeft ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
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
                name = isLeft ? "_ItemVisionRT_L" : "_ItemVisionRT_R",
                filterMode = pixelated ? FilterMode.Point : FilterMode.Bilinear
            };
            renderTexture.Create();
            if (captureCamera != null)
                captureCamera.targetTexture = renderTexture;
            if (overlay != null)
                overlay.texture = renderTexture;
        }

        void SyncCaptureCamera(Camera sourceCamera)
        {
            if (captureCamera == null)
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
            Object.Destroy(renderTexture);
            renderTexture = null;
        }

        public void Dispose()
        {
            ReleaseRenderTexture();
            if (captureCamera != null)
                Object.Destroy(captureCamera.gameObject);
            if (visionMaterial != null)
                Object.Destroy(visionMaterial);
        }
    }
}
