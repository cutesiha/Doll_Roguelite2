using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 왼쪽 눈/오른쪽 눈에 장착된 아이템이 서로 다르면 화면도 좌/우 절반으로 나눠서
// 각자 자기 쪽 눈의 효과만 보여준다 (예: 왼쪽=상하반전, 오른쪽=저해상도).
[DisallowMultipleComponent]
public class ItemVisionEffect : MonoBehaviour
{
    public struct EyeVisionFlags
    {
        public bool flipVertical;
        public bool pixelated;
        public bool inverted;
        public bool buttonView;
        public bool Any => flipVertical || pixelated || inverted || buttonView;
    }

    Camera sourceCamera;
    EyeChannel leftChannel;
    EyeChannel rightChannel;

    void Awake()
    {
        sourceCamera = GetComponent<Camera>();
    }

    void OnDestroy()
    {
        leftChannel?.Dispose();
        rightChannel?.Dispose();
    }

    public void Configure(EyeVisionFlags left, EyeVisionFlags right)
    {
        if (sourceCamera == null)
            sourceCamera = GetComponent<Camera>();
        if (sourceCamera == null)
            return;

        if (leftChannel == null)
            leftChannel = new EyeChannel(sourceCamera, true);
        if (rightChannel == null)
            rightChannel = new EyeChannel(sourceCamera, false);

        leftChannel.Configure(left);
        rightChannel.Configure(right);
    }

    void LateUpdate()
    {
        leftChannel?.Update();
        rightChannel?.Update();
    }

    // 화면 좌/우 절반마다 독립된 캡처 카메라·렌더텍스처·머티리얼을 갖고 그 쪽 눈 효과만 적용한다.
    // 카메라는 전체 장면을 찍지만, 오버레이는 그 캡처에서 자기 쪽 절반의 UV만 잘라서 표시한다
    // (실제 화면의 그 절반과 내용이 정확히 겹치도록).
    class EyeChannel
    {
        readonly Camera sourceCamera;
        readonly bool isLeft;
        Camera captureCamera;
        RenderTexture renderTexture;
        RawImage overlay;
        Material material;
        EyeVisionFlags flags;
        int lastWidth;
        int lastHeight;

        public EyeChannel(Camera sourceCamera, bool isLeft)
        {
            this.sourceCamera = sourceCamera;
            this.isLeft = isLeft;
        }

        public void Configure(EyeVisionFlags newFlags)
        {
            flags = newFlags;
            EnsureObjects();

            bool active = flags.Any;
            if (overlay != null)
                overlay.gameObject.SetActive(active);
            if (captureCamera != null)
                captureCamera.gameObject.SetActive(active);
            if (active)
                EnsureRenderTexture(true);
        }

        public void Update()
        {
            if (overlay == null || !overlay.gameObject.activeSelf || sourceCamera == null)
                return;

            EnsureRenderTexture(false);
            SyncCaptureCamera();
            captureCamera.Render();

            float u = isLeft ? 0f : 0.5f;
            overlay.uvRect = flags.flipVertical
                ? new Rect(u, 1f, 0.5f, -1f)
                : new Rect(u, 0f, 0.5f, 1f);

            if (material != null)
                material.SetFloat("_Mode", flags.buttonView ? 2f : flags.inverted ? 1f : 0f);
        }

        void EnsureObjects()
        {
            if (captureCamera == null)
            {
                GameObject cameraObject = new GameObject(isLeft ? "_ItemVisionCaptureCamera_Left" : "_ItemVisionCaptureCamera_Right");
                cameraObject.transform.SetParent(sourceCamera.transform, false);
                captureCamera = cameraObject.AddComponent<Camera>();
                UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Base;
                data.renderShadows = false;
                captureCamera.enabled = false;
            }

            if (overlay == null)
            {
                GameObject canvasObject = new GameObject(isLeft ? "_ItemVisionCanvas_Left" : "_ItemVisionCanvas_Right");
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = -100;
                canvasObject.AddComponent<CanvasScaler>();

                GameObject imageObject = new GameObject("VisionOverlay");
                imageObject.transform.SetParent(canvasObject.transform, false);
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
                    material = new Material(shader);
                    material.SetFloat("_WidthFraction", 0.5f);
                    material.SetFloat("_UOffset", isLeft ? 0f : 0.5f);
                    overlay.material = material;
                }
            }
        }

        void EnsureRenderTexture(bool force)
        {
            int divisor = flags.pixelated ? 7 : 1;
            int width = Mathf.Max(32, Screen.width / divisor);
            int height = Mathf.Max(18, Screen.height / divisor);
            if (!force && renderTexture != null && width == lastWidth && height == lastHeight)
                return;

            ReleaseRenderTexture();
            lastWidth = width;
            lastHeight = height;
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = isLeft ? "_ItemVisionRT_Left" : "_ItemVisionRT_Right",
                filterMode = flags.pixelated ? FilterMode.Point : FilterMode.Bilinear
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
            Object.Destroy(renderTexture);
            renderTexture = null;
        }

        public void Dispose()
        {
            ReleaseRenderTexture();
            if (captureCamera != null)
                Object.Destroy(captureCamera.gameObject);
            if (overlay != null)
                Object.Destroy(overlay.transform.root.gameObject);
            if (material != null)
                Object.Destroy(material);
        }
    }
}
