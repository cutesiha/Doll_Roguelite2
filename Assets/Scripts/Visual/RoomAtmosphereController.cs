using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RoomAtmosphereController : MonoBehaviour
{
    const string RootName = "_RoomAtmosphere";
    const int CanvasSortingOrder = 32000;
    const int LightCount = 3;

    enum AtmosphereKind
    {
        None,
        Workshop,
        Challenge,
        Treasure,
        Shop,
        Boss,
        Minotaur,
        Book
    }

    [System.Serializable]
    sealed class AtmospherePreset
    {
        [Header("Tone")]
        public Color colorFilter = Color.white;
        [Range(0f, 0.2f)] public float overlayAlpha = 0.025f;
        [Range(0f, 0.3f)] public float vignetteAlpha = 0.10f;
        [Range(0f, 0.18f)] public float bloomWashAlpha = 0.035f;

        [Header("URP Post Processing")]
        [Range(-30f, 30f)] public float contrast = 4f;
        [Range(-30f, 30f)] public float saturation = -2f;
        [Range(-1f, 1f)] public float postExposure = 0f;
        [Range(0f, 0.35f)] public float bloomIntensity = 0.08f;
        [Range(0f, 0.3f)] public float filmGrainIntensity = 0.07f;

        [Header("2D Light")]
        public Color primaryLightColor = new Color(1f, 0.72f, 0.42f, 1f);
        public Color secondaryLightColor = new Color(1f, 0.48f, 0.26f, 1f);
        public Color accentLightColor = new Color(0.95f, 0.78f, 0.55f, 1f);
        [Range(0f, 3f)] public float primaryLightIntensity = 0.85f;
        [Range(0f, 3f)] public float secondaryLightIntensity = 0.35f;
        [Range(0f, 3f)] public float accentLightIntensity = 0.20f;
        [Min(0.1f)] public float primaryLightRadius = 7.0f;
        [Min(0.1f)] public float secondaryLightRadius = 4.8f;
        [Min(0.1f)] public float accentLightRadius = 3.8f;
        public Vector2 primaryLightAnchor = new Vector2(0f, 0.08f);
        public Vector2 secondaryLightAnchor = new Vector2(-0.58f, 0.28f);
        public Vector2 accentLightAnchor = new Vector2(0.58f, -0.10f);
        [Range(0f, 0.35f)] public float screenGlowAlpha = 0.10f;

        [Header("Particles")]
        [Range(0, 120)] public int particleCount = 48;
        [Range(0f, 1f)] public float particleAlpha = 0.38f;
        [Range(0.2f, 4f)] public float particleSize = 1.55f;
        [Range(0.1f, 4f)] public float particleSpeed = 0.65f;
        public Color particleColorA = new Color(1f, 0.82f, 0.50f, 1f);
        public Color particleColorB = new Color(0.88f, 0.66f, 0.44f, 1f);
        public bool strandParticles;
    }

    sealed class AtmosphereParticle
    {
        public RectTransform rect;
        public RawImage image;
        public Vector2 position;
        public Vector2 velocity;
        public float phase;
        public float baseAlpha;
        public float twinkle;
    }

    static RoomAtmosphereController instance;

    [Header("Global Filter Strength")]
    [SerializeField, Range(0f, 1f)] float colorOverlayMultiplier = 0.35f;
    [SerializeField, Range(0f, 1f)] float vignetteMultiplier = 1.0f;
    [SerializeField, Range(0f, 1f)] float postProcessVignetteMultiplier = 0.22f;
    [SerializeField, Range(0f, 1f)] float colorFilterStrength = 0.08f;
    [SerializeField] Color warmEdgeColor = new Color(0.34f, 0.18f, 0.07f, 1f);
    [SerializeField, Range(0f, 0.35f)] float minimumVisibleEdgeAlpha = 0.16f;

    [Header("Global Light Strength")]
    [SerializeField, Range(0f, 3f)] float lightIntensityMultiplier = 1.15f;
    [SerializeField, Range(0f, 2f)] float lightRadiusMultiplier = 1.0f;
    [SerializeField, Range(0f, 2f)] float screenGlowMultiplier = 1.15f;

    [Header("Global Particle Strength")]
    [SerializeField, Range(0.25f, 3f)] float particleCountMultiplier = 1.25f;
    [SerializeField, Range(0f, 3f)] float particleAlphaMultiplier = 1.85f;
    [SerializeField, Range(0.25f, 3f)] float particleSizeMultiplier = 1.45f;
    [SerializeField, Range(0.1f, 2f)] float particleSpeedMultiplier = 0.42f;
    [SerializeField, Range(0f, 24f)] float particleSoftnessDrift = 8f;

    [Header("Room Presets")]
    [SerializeField] AtmospherePreset workshop = new AtmospherePreset
    {
        colorFilter = new Color(1.00f, 0.92f, 0.82f, 1f),
        overlayAlpha = 0.022f,
        vignetteAlpha = 0.08f,
        bloomWashAlpha = 0.04f,
        contrast = 3f,
        saturation = -1f,
        postExposure = 0.03f,
        bloomIntensity = 0.08f,
        filmGrainIntensity = 0.06f,
        primaryLightColor = new Color(1.00f, 0.72f, 0.38f, 1f),
        secondaryLightColor = new Color(1.00f, 0.54f, 0.30f, 1f),
        accentLightColor = new Color(1.00f, 0.86f, 0.62f, 1f),
        primaryLightIntensity = 0.75f,
        secondaryLightIntensity = 0.28f,
        accentLightIntensity = 0.18f,
        primaryLightRadius = 8.0f,
        secondaryLightRadius = 4.9f,
        accentLightRadius = 3.6f,
        primaryLightAnchor = new Vector2(0f, 0.05f),
        secondaryLightAnchor = new Vector2(-0.55f, 0.30f),
        accentLightAnchor = new Vector2(0.45f, -0.15f),
        screenGlowAlpha = 0.10f,
        particleCount = 54,
        particleAlpha = 0.35f,
        particleSize = 1.45f,
        particleSpeed = 0.55f,
        particleColorA = new Color(1.00f, 0.78f, 0.45f, 1f),
        particleColorB = new Color(0.76f, 0.58f, 0.42f, 1f)
    };

    [SerializeField] AtmospherePreset challenge = new AtmospherePreset
    {
        colorFilter = new Color(1.00f, 0.88f, 0.84f, 1f),
        overlayAlpha = 0.020f,
        vignetteAlpha = 0.09f,
        bloomWashAlpha = 0.028f,
        contrast = 5f,
        saturation = -2f,
        postExposure = -0.02f,
        bloomIntensity = 0.09f,
        filmGrainIntensity = 0.08f,
        primaryLightColor = new Color(1.00f, 0.30f, 0.24f, 1f),
        secondaryLightColor = new Color(0.78f, 0.05f, 0.07f, 1f),
        accentLightColor = new Color(1.00f, 0.52f, 0.34f, 1f),
        primaryLightIntensity = 0.55f,
        secondaryLightIntensity = 0.34f,
        accentLightIntensity = 0.18f,
        primaryLightRadius = 7.2f,
        secondaryLightRadius = 4.0f,
        accentLightRadius = 3.2f,
        primaryLightAnchor = new Vector2(0f, 0.02f),
        secondaryLightAnchor = new Vector2(-0.50f, 0.18f),
        accentLightAnchor = new Vector2(0.55f, -0.22f),
        screenGlowAlpha = 0.08f,
        particleCount = 52,
        particleAlpha = 0.42f,
        particleSize = 1.35f,
        particleSpeed = 0.70f,
        particleColorA = new Color(1.00f, 0.16f, 0.12f, 1f),
        particleColorB = new Color(0.68f, 0.04f, 0.06f, 1f),
        strandParticles = true
    };

    [SerializeField] AtmospherePreset treasure = new AtmospherePreset
    {
        colorFilter = new Color(1.00f, 0.94f, 0.78f, 1f),
        overlayAlpha = 0.018f,
        vignetteAlpha = 0.07f,
        bloomWashAlpha = 0.05f,
        contrast = 2f,
        saturation = 2f,
        postExposure = 0.05f,
        bloomIntensity = 0.13f,
        filmGrainIntensity = 0.05f,
        primaryLightColor = new Color(1.00f, 0.82f, 0.28f, 1f),
        secondaryLightColor = new Color(1.00f, 0.60f, 0.22f, 1f),
        accentLightColor = new Color(1.00f, 0.93f, 0.60f, 1f),
        primaryLightIntensity = 1.05f,
        secondaryLightIntensity = 0.46f,
        accentLightIntensity = 0.32f,
        primaryLightRadius = 6.8f,
        secondaryLightRadius = 4.5f,
        accentLightRadius = 3.3f,
        primaryLightAnchor = new Vector2(-0.25f, 0.10f),
        secondaryLightAnchor = new Vector2(0.42f, 0.22f),
        accentLightAnchor = new Vector2(0.15f, -0.18f),
        screenGlowAlpha = 0.13f,
        particleCount = 60,
        particleAlpha = 0.44f,
        particleSize = 1.45f,
        particleSpeed = 0.48f,
        particleColorA = new Color(1.00f, 0.88f, 0.38f, 1f),
        particleColorB = new Color(1.00f, 0.68f, 0.24f, 1f)
    };

    [SerializeField] AtmospherePreset shop = new AtmospherePreset
    {
        colorFilter = new Color(1.00f, 0.91f, 0.78f, 1f),
        overlayAlpha = 0.018f,
        vignetteAlpha = 0.065f,
        bloomWashAlpha = 0.045f,
        contrast = 2f,
        saturation = 0f,
        postExposure = 0.04f,
        bloomIntensity = 0.10f,
        filmGrainIntensity = 0.05f,
        primaryLightColor = new Color(1.00f, 0.75f, 0.36f, 1f),
        secondaryLightColor = new Color(1.00f, 0.55f, 0.28f, 1f),
        accentLightColor = new Color(1.00f, 0.88f, 0.62f, 1f),
        primaryLightIntensity = 0.92f,
        secondaryLightIntensity = 0.42f,
        accentLightIntensity = 0.24f,
        primaryLightRadius = 6.6f,
        secondaryLightRadius = 4.2f,
        accentLightRadius = 3.2f,
        primaryLightAnchor = new Vector2(-0.42f, 0.22f),
        secondaryLightAnchor = new Vector2(0.38f, 0.18f),
        accentLightAnchor = new Vector2(0.00f, -0.22f),
        screenGlowAlpha = 0.11f,
        particleCount = 58,
        particleAlpha = 0.40f,
        particleSize = 1.42f,
        particleSpeed = 0.50f,
        particleColorA = new Color(1.00f, 0.78f, 0.44f, 1f),
        particleColorB = new Color(1.00f, 0.91f, 0.66f, 1f)
    };

    [SerializeField] AtmospherePreset boss = new AtmospherePreset
    {
        colorFilter = new Color(0.96f, 0.88f, 1.00f, 1f),
        overlayAlpha = 0.024f,
        vignetteAlpha = 0.10f,
        bloomWashAlpha = 0.035f,
        contrast = 6f,
        saturation = -4f,
        postExposure = -0.04f,
        bloomIntensity = 0.10f,
        filmGrainIntensity = 0.09f,
        primaryLightColor = new Color(0.95f, 0.18f, 0.20f, 1f),
        secondaryLightColor = new Color(0.42f, 0.12f, 0.62f, 1f),
        accentLightColor = new Color(1.00f, 0.45f, 0.30f, 1f),
        primaryLightIntensity = 0.65f,
        secondaryLightIntensity = 0.40f,
        accentLightIntensity = 0.22f,
        primaryLightRadius = 7.0f,
        secondaryLightRadius = 5.1f,
        accentLightRadius = 3.8f,
        primaryLightAnchor = new Vector2(0.00f, 0.02f),
        secondaryLightAnchor = new Vector2(-0.55f, 0.22f),
        accentLightAnchor = new Vector2(0.50f, -0.15f),
        screenGlowAlpha = 0.09f,
        particleCount = 50,
        particleAlpha = 0.36f,
        particleSize = 1.35f,
        particleSpeed = 0.55f,
        particleColorA = new Color(0.90f, 0.18f, 0.20f, 1f),
        particleColorB = new Color(0.42f, 0.16f, 0.56f, 1f),
        strandParticles = true
    };

    [SerializeField] AtmospherePreset minotaur = new AtmospherePreset
    {
        colorFilter = new Color(0.95f, 0.88f, 1.00f, 1f),
        overlayAlpha = 0.025f,
        vignetteAlpha = 0.105f,
        bloomWashAlpha = 0.036f,
        contrast = 7f,
        saturation = -5f,
        postExposure = -0.05f,
        bloomIntensity = 0.10f,
        filmGrainIntensity = 0.10f,
        primaryLightColor = new Color(0.90f, 0.14f, 0.14f, 1f),
        secondaryLightColor = new Color(0.34f, 0.11f, 0.48f, 1f),
        accentLightColor = new Color(0.74f, 0.32f, 0.24f, 1f),
        primaryLightIntensity = 0.68f,
        secondaryLightIntensity = 0.38f,
        accentLightIntensity = 0.18f,
        primaryLightRadius = 7.2f,
        secondaryLightRadius = 5.0f,
        accentLightRadius = 3.5f,
        primaryLightAnchor = new Vector2(0.00f, -0.02f),
        secondaryLightAnchor = new Vector2(-0.52f, 0.18f),
        accentLightAnchor = new Vector2(0.50f, -0.24f),
        screenGlowAlpha = 0.09f,
        particleCount = 50,
        particleAlpha = 0.38f,
        particleSize = 1.32f,
        particleSpeed = 0.50f,
        particleColorA = new Color(0.86f, 0.10f, 0.12f, 1f),
        particleColorB = new Color(0.44f, 0.20f, 0.34f, 1f),
        strandParticles = true
    };

    [SerializeField] AtmospherePreset book = new AtmospherePreset
    {
        colorFilter = new Color(1.00f, 0.94f, 0.82f, 1f),
        overlayAlpha = 0.026f,
        vignetteAlpha = 0.11f,
        bloomWashAlpha = 0.035f,
        contrast = 5f,
        saturation = -4f,
        postExposure = -0.02f,
        bloomIntensity = 0.07f,
        filmGrainIntensity = 0.10f,
        primaryLightColor = new Color(1.00f, 0.78f, 0.45f, 1f),
        secondaryLightColor = new Color(0.36f, 0.25f, 0.58f, 1f),
        accentLightColor = new Color(0.15f, 0.12f, 0.10f, 1f),
        primaryLightIntensity = 0.82f,
        secondaryLightIntensity = 0.30f,
        accentLightIntensity = 0.18f,
        primaryLightRadius = 7.8f,
        secondaryLightRadius = 4.8f,
        accentLightRadius = 3.8f,
        primaryLightAnchor = new Vector2(0.00f, 0.14f),
        secondaryLightAnchor = new Vector2(-0.42f, 0.28f),
        accentLightAnchor = new Vector2(0.45f, -0.08f),
        screenGlowAlpha = 0.10f,
        particleCount = 62,
        particleAlpha = 0.40f,
        particleSize = 1.38f,
        particleSpeed = 0.45f,
        particleColorA = new Color(0.12f, 0.10f, 0.08f, 1f),
        particleColorB = new Color(0.88f, 0.66f, 0.38f, 1f)
    };

    Canvas overlayCanvas;
    RawImage tintImage;
    RawImage bloomImage;
    RawImage vignetteImage;
    readonly RawImage[] glowImages = new RawImage[LightCount];
    RectTransform particleRoot;
    Transform lightRoot;
    readonly List<Light2D> sceneLights = new List<Light2D>();
    readonly List<AtmosphereParticle> particles = new List<AtmosphereParticle>();

    Volume volume;
    VolumeProfile volumeProfile;
    ColorAdjustments colorAdjustments;
    Vignette vignette;
    Bloom bloom;
    FilmGrain filmGrain;

    AtmosphereKind currentKind = AtmosphereKind.None;
    AtmospherePreset currentPreset;
    Texture2D whiteTexture;
    Texture2D vignetteTexture;
    Texture2D bloomTexture;
    Texture2D circleTexture;
    Texture2D strandTexture;

    FieldInfo challengeRemainingField;
    FieldInfo challengeLimitField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance();
        instance.ApplyScene(scene);
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(RootName);
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RoomAtmosphereController>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildTextures();
        BuildOverlayCanvas();
        BuildLights();
        BuildVolume();
        CacheChallengeFields();
    }

    void Start()
    {
        if (currentKind == AtmosphereKind.None)
            ApplyScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (volumeProfile != null)
            Destroy(volumeProfile);
    }

    void Update()
    {
        if (currentKind == AtmosphereKind.None)
            return;

        float challengePressure = currentKind == AtmosphereKind.Challenge ? ChallengePressure() : 0f;
        ApplyDynamicTone(challengePressure);
        UpdateLightPositions(challengePressure);
        AnimateParticles(challengePressure);
    }

    void ApplyScene(Scene scene)
    {
        AtmosphereKind next = KindForScene(scene);
        if (next == AtmosphereKind.None)
        {
            SetActive(false);
            currentKind = AtmosphereKind.None;
            return;
        }

        SetActive(true);
        currentKind = next;
        currentPreset = PresetFor(next);
        ApplyStaticVisuals();
        ConfigureParticles(next, currentPreset);
        ConfigureCameraPostProcessing();
        ConfigureVolume(currentPreset);
        ConfigureSceneLights(currentPreset);
        UpdateLightPositions(0f);
    }

    AtmosphereKind KindForScene(Scene scene)
    {
        string sceneName = scene.name;
        if (sceneName == "BookBossScene")
            return AtmosphereKind.Book;
        if (sceneName == "MiddleBossScene")
            return AtmosphereKind.Minotaur;
        if (sceneName == "BossScene")
            return AtmosphereKind.Boss;
        if (sceneName == "ChallengeScene")
            return AtmosphereKind.Challenge;
        if (sceneName == "TreasureRoomScene" || sceneName == "PresentScene")
            return AtmosphereKind.Treasure;
        if (sceneName == "ShopScene")
            return AtmosphereKind.Shop;
        if (sceneName == "RoomScene")
        {
            MapNode pending = MapRunState.PendingNode;
            if (pending != null && pending.roomType == RoomType.Challenge)
                return AtmosphereKind.Challenge;
            return AtmosphereKind.Workshop;
        }

        return AtmosphereKind.Workshop;
    }

    AtmospherePreset PresetFor(AtmosphereKind kind)
    {
        switch (kind)
        {
            case AtmosphereKind.Challenge: return challenge;
            case AtmosphereKind.Treasure: return treasure;
            case AtmosphereKind.Shop: return shop;
            case AtmosphereKind.Boss: return boss;
            case AtmosphereKind.Minotaur: return minotaur;
            case AtmosphereKind.Book: return book;
            default: return workshop;
        }
    }

    void BuildOverlayCanvas()
    {
        overlayCanvas = gameObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = CanvasSortingOrder;
        gameObject.AddComponent<CanvasScaler>();

        tintImage = CreateFullScreenImage("SubtleTone", whiteTexture);
        bloomImage = CreateFullScreenImage("SoftLightWash", bloomTexture);
        for (int i = 0; i < glowImages.Length; i++)
            glowImages[i] = CreateFloatingImage("LightGlow_" + i, bloomTexture);

        particleRoot = new GameObject("VisibleAmbientParticles").AddComponent<RectTransform>();
        particleRoot.SetParent(transform, false);
        particleRoot.anchorMin = Vector2.zero;
        particleRoot.anchorMax = Vector2.one;
        particleRoot.offsetMin = Vector2.zero;
        particleRoot.offsetMax = Vector2.zero;

        vignetteImage = CreateFullScreenImage("SoftEdgeShade", vignetteTexture);
    }

    RawImage CreateFullScreenImage(string objectName, Texture texture)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        image.canvasRenderer.cullTransparentMesh = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    RawImage CreateFloatingImage(string objectName, Texture texture)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    void BuildLights()
    {
        lightRoot = new GameObject("RoomAtmosphere2DLights").transform;
        lightRoot.SetParent(transform, false);

        for (int i = 0; i < LightCount; i++)
        {
            GameObject go = new GameObject("AtmosphereLight_" + i);
            go.transform.SetParent(lightRoot, false);
            Light2D light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.intensity = 0f;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = 5f;
            sceneLights.Add(light);
        }
    }

    void BuildVolume()
    {
        GameObject volumeGo = new GameObject("RoomAtmosphereVolume");
        volumeGo.transform.SetParent(transform, false);
        volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 80f;
        volume.weight = 1f;

        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = volumeProfile;
        colorAdjustments = volumeProfile.Add<ColorAdjustments>(true);
        vignette = volumeProfile.Add<Vignette>(true);
        bloom = volumeProfile.Add<Bloom>(true);
        filmGrain = volumeProfile.Add<FilmGrain>(true);
    }

    void ConfigureCameraPostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
            data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
    }

    void ConfigureVolume(AtmospherePreset preset)
    {
        if (volume == null || colorAdjustments == null)
            return;

        volume.enabled = true;

        colorAdjustments.active = true;
        colorAdjustments.colorFilter.Override(Color.Lerp(Color.white, preset.colorFilter, colorFilterStrength));
        colorAdjustments.contrast.Override(preset.contrast);
        colorAdjustments.saturation.Override(preset.saturation);
        colorAdjustments.postExposure.Override(preset.postExposure);

        vignette.active = true;
        vignette.color.Override(warmEdgeColor);
        vignette.intensity.Override(Mathf.Clamp01(preset.vignetteAlpha * postProcessVignetteMultiplier));
        vignette.smoothness.Override(0.68f);

        bloom.active = true;
        bloom.intensity.Override(preset.bloomIntensity);
        bloom.threshold.Override(1.20f);

        filmGrain.active = true;
        filmGrain.intensity.Override(preset.filmGrainIntensity);
        filmGrain.response.Override(0.55f);
    }

    void ConfigureSceneLights(AtmospherePreset preset)
    {
        SetLight(sceneLights[0], preset.primaryLightColor, preset.primaryLightIntensity, preset.primaryLightRadius);
        SetLight(sceneLights[1], preset.secondaryLightColor, preset.secondaryLightIntensity, preset.secondaryLightRadius);
        SetLight(sceneLights[2], preset.accentLightColor, preset.accentLightIntensity, preset.accentLightRadius);
    }

    void SetLight(Light2D light, Color color, float intensity, float radius)
    {
        if (light == null)
            return;

        light.color = color;
        light.intensity = intensity * lightIntensityMultiplier;
        light.pointLightInnerRadius = Mathf.Max(0f, radius * 0.12f);
        light.pointLightOuterRadius = Mathf.Max(0.1f, radius * lightRadiusMultiplier);
    }

    void ApplyStaticVisuals()
    {
        tintImage.color = WithAlpha(currentPreset.colorFilter, currentPreset.overlayAlpha * colorOverlayMultiplier);
        bloomImage.color = WithAlpha(currentPreset.primaryLightColor, currentPreset.bloomWashAlpha * screenGlowMultiplier);
        vignetteImage.color = WithAlpha(warmEdgeColor, EdgeAlpha(currentPreset.vignetteAlpha));
        ApplyGlowVisuals(0f);
    }

    void ApplyDynamicTone(float challengePressure)
    {
        if (currentKind != AtmosphereKind.Challenge)
        {
            ApplyStaticVisuals();
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(1.5f, 3.8f, challengePressure));
        float pressureOverlay = Mathf.Lerp(0f, 0.045f, challengePressure);
        float pressureVignette = Mathf.Lerp(0f, 0.045f, challengePressure);
        tintImage.color = WithAlpha(currentPreset.colorFilter, currentPreset.overlayAlpha * colorOverlayMultiplier + pressureOverlay + pulse * challengePressure * 0.008f);
        vignetteImage.color = WithAlpha(warmEdgeColor, EdgeAlpha(currentPreset.vignetteAlpha) + pressureVignette);
        ApplyGlowVisuals(challengePressure);

        if (colorAdjustments != null)
        {
            colorAdjustments.colorFilter.Override(Color.Lerp(Color.white, currentPreset.primaryLightColor, colorFilterStrength + challengePressure * 0.06f));
            colorAdjustments.postExposure.Override(currentPreset.postExposure - challengePressure * 0.04f);
        }

        if (sceneLights.Count > 0 && sceneLights[0] != null)
            sceneLights[0].intensity = currentPreset.primaryLightIntensity * lightIntensityMultiplier * Mathf.Lerp(1f, 1.28f, challengePressure);
    }

    void ApplyGlowVisuals(float pressure)
    {
        Color[] colors =
        {
            currentPreset.primaryLightColor,
            currentPreset.secondaryLightColor,
            currentPreset.accentLightColor
        };
        Vector2[] anchors =
        {
            currentPreset.primaryLightAnchor,
            currentPreset.secondaryLightAnchor,
            currentPreset.accentLightAnchor
        };
        float[] radii =
        {
            currentPreset.primaryLightRadius,
            currentPreset.secondaryLightRadius,
            currentPreset.accentLightRadius
        };

        RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
        Vector2 size = canvasRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(Screen.width, Screen.height);

        for (int i = 0; i < glowImages.Length; i++)
        {
            RawImage glow = glowImages[i];
            if (glow == null)
                continue;

            RectTransform rect = glow.rectTransform;
            Vector2 anchor = anchors[i];
            float radiusPixels = Mathf.Lerp(size.y * 0.24f, size.y * 0.62f, Mathf.Clamp01(radii[i] / 9f));
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(anchor.x * size.x * 0.5f, anchor.y * size.y * 0.5f);
            rect.sizeDelta = Vector2.one * radiusPixels * lightRadiusMultiplier;

            float alpha = currentPreset.screenGlowAlpha * screenGlowMultiplier * (i == 0 ? 1f : 0.58f);
            if (currentKind == AtmosphereKind.Challenge)
                alpha += pressure * 0.045f;
            glow.color = WithAlpha(colors[i], alpha);
        }
    }

    void UpdateLightPositions(float pressure)
    {
        if (sceneLights.Count < LightCount)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector2[] anchors =
        {
            currentPreset.primaryLightAnchor,
            currentPreset.secondaryLightAnchor,
            currentPreset.accentLightAnchor
        };

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 basePos = cam.transform.position;
        for (int i = 0; i < LightCount; i++)
        {
            Light2D light = sceneLights[i];
            if (light == null)
                continue;

            Vector2 anchor = anchors[i];
            float breathing = Mathf.Sin(Time.unscaledTime * (0.55f + i * 0.17f) + i) * 0.12f;
            light.transform.position = new Vector3(
                basePos.x + anchor.x * halfWidth,
                basePos.y + anchor.y * halfHeight + breathing,
                0f);
        }
    }

    void ConfigureParticles(AtmosphereKind kind, AtmospherePreset preset)
    {
        int count = Mathf.RoundToInt(preset.particleCount * particleCountMultiplier);
        EnsureParticleCount(count);

        RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
        Vector2 size = canvasRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(Screen.width, Screen.height);

        for (int i = 0; i < particles.Count; i++)
        {
            AtmosphereParticle particle = particles[i];
            bool active = i < count;
            particle.rect.gameObject.SetActive(active);
            if (!active)
                continue;

            bool fiber = preset.strandParticles || kind == AtmosphereKind.Challenge || kind == AtmosphereKind.Minotaur;
            particle.image.texture = fiber ? strandTexture : circleTexture;
            particle.position = new Vector2(Random.Range(0f, size.x), Random.Range(0f, size.y));
            particle.velocity = ParticleVelocity(kind, preset);
            particle.phase = Random.Range(0f, 20f);
            particle.baseAlpha = Random.Range(0.65f, 1f) * preset.particleAlpha * particleAlphaMultiplier;
            particle.twinkle = Random.Range(0.25f, 1f);

            float pixelSize = Random.Range(3.6f, 9.2f) * preset.particleSize * particleSizeMultiplier;
            particle.rect.sizeDelta = fiber
                ? new Vector2(pixelSize * Random.Range(3.2f, 5.8f), Mathf.Max(2.2f, pixelSize * Random.Range(0.55f, 0.85f)))
                : Vector2.one * pixelSize;
            particle.rect.anchoredPosition = particle.position;
            particle.rect.localRotation = Quaternion.Euler(0f, 0f, fiber ? Random.Range(-28f, 28f) : Random.Range(0f, 360f));
            particle.image.color = ParticleColor(i, preset, particle.baseAlpha);
        }
    }

    Vector2 ParticleVelocity(AtmosphereKind kind, AtmospherePreset preset)
    {
        float speed = preset.particleSpeed * particleSpeedMultiplier * 13f;
        switch (kind)
        {
            case AtmosphereKind.Challenge:
                return new Vector2(Random.Range(-speed * 0.55f, speed * 0.55f), Random.Range(-speed * 0.12f, speed * 0.14f));
            case AtmosphereKind.Minotaur:
            case AtmosphereKind.Boss:
                return new Vector2(Random.Range(-speed * 0.34f, speed * 0.34f), Random.Range(-speed * 0.16f, speed * 0.12f));
            case AtmosphereKind.Book:
                return new Vector2(Random.Range(-speed * 0.22f, speed * 0.22f), Random.Range(-speed * 0.24f, -speed * 0.06f));
            default:
                return new Vector2(Random.Range(-speed * 0.16f, speed * 0.20f), Random.Range(speed * 0.08f, speed * 0.24f));
        }
    }

    void EnsureParticleCount(int count)
    {
        while (particles.Count < count)
            particles.Add(CreateParticle());

        for (int i = 0; i < particles.Count; i++)
            particles[i].rect.gameObject.SetActive(i < count);
    }

    AtmosphereParticle CreateParticle()
    {
        GameObject go = new GameObject("Particle");
        go.transform.SetParent(particleRoot, false);
        RawImage image = go.AddComponent<RawImage>();
        image.texture = circleTexture;
        image.raycastTarget = false;

        return new AtmosphereParticle
        {
            rect = go.GetComponent<RectTransform>(),
            image = image
        };
    }

    void AnimateParticles(float challengePressure)
    {
        RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
        Vector2 size = canvasRect.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(Screen.width, Screen.height);

        for (int i = 0; i < particles.Count; i++)
        {
            AtmosphereParticle particle = particles[i];
            if (!particle.rect.gameObject.activeSelf)
                continue;

            Vector2 drift = particle.velocity * Time.unscaledDeltaTime;
            drift.x += Mathf.Sin(Time.unscaledTime * 0.42f + particle.phase) * Time.unscaledDeltaTime * particleSoftnessDrift;
            drift.y += Mathf.Cos(Time.unscaledTime * 0.36f + particle.phase * 1.37f) * Time.unscaledDeltaTime * particleSoftnessDrift * 0.42f;
            if (currentKind == AtmosphereKind.Challenge)
                drift.x += challengePressure * Mathf.Sin(Time.unscaledTime * 3.2f + particle.phase) * Time.unscaledDeltaTime * 5f;

            particle.position += drift;
            if (particle.position.x < -50f) particle.position.x = size.x + 50f;
            if (particle.position.x > size.x + 50f) particle.position.x = -50f;
            if (particle.position.y < -50f) particle.position.y = size.y + 50f;
            if (particle.position.y > size.y + 50f) particle.position.y = -50f;

            float alphaPulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * particle.twinkle + particle.phase);
            particle.rect.anchoredPosition = particle.position;
            particle.rect.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 0.28f + particle.phase) * Time.unscaledDeltaTime * 6f);
            particle.image.color = ParticleColor(i, currentPreset, particle.baseAlpha * alphaPulse);
        }
    }

    Color ParticleColor(int index, AtmospherePreset preset, float alpha)
    {
        Color color = index % 2 == 0 ? preset.particleColorA : preset.particleColorB;
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    void SetActive(bool active)
    {
        if (overlayCanvas != null)
            overlayCanvas.enabled = active;
        if (volume != null)
            volume.enabled = active;
        if (lightRoot != null)
            lightRoot.gameObject.SetActive(active);
    }

    float ChallengePressure()
    {
        ThreadMazeChallengeManager manager = FindFirstObjectByType<ThreadMazeChallengeManager>(FindObjectsInactive.Include);
        if (manager == null || challengeRemainingField == null || challengeLimitField == null)
            return 0.20f + 0.08f * Mathf.Sin(Time.unscaledTime * 1.4f);

        object remainingValue = challengeRemainingField.GetValue(manager);
        object limitValue = challengeLimitField.GetValue(manager);
        if (!(remainingValue is float remaining) || !(limitValue is float limit) || limit <= 0.01f)
            return 0f;

        return Mathf.Clamp01(1f - Mathf.Clamp01(remaining / limit));
    }

    void CacheChallengeFields()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        challengeRemainingField = typeof(ThreadMazeChallengeManager).GetField("remainingTime", flags);
        challengeLimitField = typeof(ThreadMazeChallengeManager).GetField("timeLimit", flags);
    }

    void BuildTextures()
    {
        whiteTexture = Texture(1, 1, (x, y) => Color.white);
        circleTexture = Texture(48, 48, CirclePixel);
        strandTexture = Texture(64, 18, FiberPixel);
        vignetteTexture = Texture(256, 256, VignettePixel);
        bloomTexture = Texture(256, 256, BloomPixel);
    }

    delegate Color PixelBuilder(int x, int y);

    Texture2D Texture(int width, int height, PixelBuilder builder)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, builder(x, y));

        texture.Apply();
        return texture;
    }

    Color CirclePixel(int x, int y)
    {
        Vector2 center = new Vector2(23.5f, 23.5f);
        Vector2 point = new Vector2(x, y);
        float main = Mathf.Clamp01(1f - Mathf.InverseLerp(5f, 24f, Vector2.Distance(point, center)));
        float puffA = Mathf.Clamp01(1f - Mathf.InverseLerp(2f, 15f, Vector2.Distance(point, center + new Vector2(-5f, 4f))));
        float puffB = Mathf.Clamp01(1f - Mathf.InverseLerp(2f, 13f, Vector2.Distance(point, center + new Vector2(6f, -3f))));
        float grain = 0.86f + 0.14f * Mathf.Sin(x * 0.73f + y * 1.19f);
        float alpha = Mathf.Clamp01((main * 0.58f + puffA * 0.25f + puffB * 0.22f) * grain);
        alpha *= alpha;
        return new Color(1f, 1f, 1f, alpha);
    }

    Color FiberPixel(int x, int y)
    {
        float nx = (x + 0.5f) / 64f;
        float ny = (y + 0.5f) / 18f;
        float centerLine = 0.5f + Mathf.Sin(nx * Mathf.PI * 2.2f) * 0.10f;
        float distance = Mathf.Abs(ny - centerLine);
        float tapered = Mathf.Sin(nx * Mathf.PI);
        float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.03f, 0.32f, distance));
        alpha *= Mathf.Clamp01(tapered * 1.35f);
        float fray = 0.78f + 0.22f * Mathf.Sin(x * 1.7f + y * 0.9f);
        alpha *= fray;
        return new Color(1f, 1f, 1f, alpha * alpha);
    }

    Color VignettePixel(int x, int y)
    {
        Vector2 center = new Vector2(127.5f, 127.5f);
        Vector2 p = new Vector2(x, y);
        float dx = Mathf.Abs(p.x - center.x) / center.x;
        float dy = Mathf.Abs(p.y - center.y) / center.y;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1.08f, distance));
        return new Color(1f, 1f, 1f, alpha);
    }

    float EdgeAlpha(float presetAlpha)
    {
        return Mathf.Max(minimumVisibleEdgeAlpha, presetAlpha * vignetteMultiplier);
    }

    Color BloomPixel(int x, int y)
    {
        Vector2 center = new Vector2(127.5f, 110f);
        float distance = Vector2.Distance(new Vector2(x, y), center) / 142f;
        float alpha = Mathf.Clamp01(1f - distance);
        alpha *= alpha * 0.65f;
        return new Color(1f, 1f, 1f, alpha);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
