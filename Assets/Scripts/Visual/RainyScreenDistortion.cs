using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class RainyScreenDistortion : MonoBehaviour
{
    [SerializeField] bool effectActive;
    [SerializeField, Range(0f, 0.08f)] float distortionStrength = 0.018f;
    [SerializeField, Min(0f)] float distortionSpeed = 0.22f;
    [SerializeField, Min(0.1f)] float distortionScale = 5.5f;

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
    static readonly int StrengthId = Shader.PropertyToID("_Strength");
    static readonly int SpeedId = Shader.PropertyToID("_Speed");
    static readonly int ScaleId = Shader.PropertyToID("_Scale");
    static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
    static readonly int TempTexId = Shader.PropertyToID("_RainyScreenDistortionTemp");

    Camera targetCamera;
    Material material;
    Texture2D noiseTexture;

    public float DistortionStrength
    {
        get => distortionStrength;
        set => distortionStrength = Mathf.Max(0f, value);
    }

    public float DistortionSpeed
    {
        get => distortionSpeed;
        set => distortionSpeed = Mathf.Max(0f, value);
    }

    public float DistortionScale
    {
        get => distortionScale;
        set => distortionScale = Mathf.Max(0.1f, value);
    }

    public bool EffectActive
    {
        get => effectActive;
        set => effectActive = value;
    }

    public void Configure(float strength, float speed, float scale)
    {
        DistortionStrength = strength;
        DistortionSpeed = speed;
        DistortionScale = scale;
    }

    public void SetEffectActive(bool active)
    {
        effectActive = active;
    }

    void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        EnsureResources();
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    void OnDestroy()
    {
        if (material != null)
            Destroy(material);
        if (noiseTexture != null)
            Destroy(noiseTexture);
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!effectActive || camera != targetCamera || camera == null || camera.cameraType != CameraType.Game)
            return;
        if (camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            return;

        EnsureResources();
        if (material == null)
            return;

        material.SetFloat(StrengthId, distortionStrength);
        material.SetFloat(SpeedId, distortionSpeed);
        material.SetFloat(ScaleId, distortionScale);
        material.SetFloat(TimeOffsetId, Time.time);
        material.SetTexture(NoiseTexId, noiseTexture);

        CommandBuffer cmd = CommandBufferPool.Get("Rainy Screen Distortion");
        cmd.GetTemporaryRT(TempTexId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
        cmd.Blit(BuiltinRenderTextureType.CameraTarget, TempTexId);
        cmd.SetGlobalTexture(MainTexId, TempTexId);
        cmd.Blit(TempTexId, BuiltinRenderTextureType.CameraTarget, material, 0);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    void EnsureResources()
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Hidden/RainyScreenDistortion");
            if (shader != null)
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        if (noiseTexture == null)
            noiseTexture = CreateNoiseTexture(64, 64);
    }

    static Texture2D CreateNoiseTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "RainyScreenDistortionNoise",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        System.Random random = new System.Random(1729);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float n0 = (float)random.NextDouble();
                float n1 = (float)random.NextDouble();
                pixels[y * width + x] = new Color(n0, n1, 0f, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
