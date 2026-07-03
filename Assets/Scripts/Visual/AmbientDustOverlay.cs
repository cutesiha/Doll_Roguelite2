using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 화면에 둥둥 떠다니는 솜/먼지 파티클을 '최상단' Screen Space - Overlay 레이어에 그린다.
// URP/포스트프로세싱과 무관하게 무조건 보인다. [ExecuteAlways] 라서 에디터 Game 뷰에서도 보인다.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class AmbientDustOverlay : MonoBehaviour
{
    [Header("Amount / Look")]
    [SerializeField, Range(0, 240)] int particleCount = 32;
    [Tooltip("반딧불이 느낌의 노랑-주황.")]
    [SerializeField] Color dustColor = new Color(1f, 0.82f, 0.42f, 1f);
    [Tooltip("전체 불투명도.")]
    [SerializeField, Range(0f, 1f)] float alpha = 0.85f;
    [SerializeField, Range(2f, 40f)] float minSize = 7f;
    [SerializeField, Range(2f, 60f)] float maxSize = 16f;
    [Tooltip("반딧불이처럼 깜빡이는 정도(0=은은, 1=확실히 점멸).")]
    [SerializeField, Range(0f, 1f)] float firefly = 0.8f;

    [Header("Motion")]
    [Tooltip("떠다니는 기본 속도(px/초).")]
    [SerializeField, Range(0f, 60f)] float driftSpeed = 9f;
    [SerializeField, Range(0f, 30f)] float swayAmount = 8f;

    [Header("Canvas")]
    [Tooltip("최상단이 되도록 큰 값. 낮추면 다른 UI 뒤로 감.")]
    [SerializeField] int sortingOrder = 32600;

    const string CanvasName = "_AmbientDustCanvas";

    sealed class Dust
    {
        public RectTransform rect;
        public RawImage image;
        public Vector2 pos;
        public Vector2 vel;
        public float phase;
        public float baseAlpha;
        public float twinkle;
    }

    bool autoCreated;
    static AmbientDustOverlay activeInstance;

    Canvas canvas;
    RectTransform canvasRect;
    Texture2D dustTexture;
    readonly List<Dust> dust = new List<Dust>();
    float lastTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureRuntimeInstance();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRuntimeInstance();
    }

    static void EnsureRuntimeInstance()
    {
        if (!Application.isPlaying)
            return;
        if (FindFirstObjectByType<AmbientDustOverlay>() != null)
            return;

        GameObject go = new GameObject("_AmbientDustOverlay");
        DontDestroyOnLoad(go);
        AmbientDustOverlay v = go.AddComponent<AmbientDustOverlay>();
        v.autoCreated = true;
    }

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (activeInstance != null && activeInstance != this)
            {
                if (!autoCreated && activeInstance.autoCreated)
                {
                    Destroy(activeInstance.gameObject);
                    activeInstance = this;
                }
                else if (autoCreated)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                activeInstance = this;
            }
        }

        lastTime = Time.realtimeSinceStartup;
        Build();
    }

    void OnDisable()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled)
                return;
            Build();
        };
    }
#endif

    void Build()
    {
        EnsureTexture();
        EnsureCanvas();
        EnsureParticles();
#if UNITY_EDITOR
        // 에디터(비플레이)에서는 동적 생성 UI 지오메트리가 자동 갱신되지 않으므로 강제로 리빌드.
        if (!Application.isPlaying)
        {
            for (int i = 0; i < dust.Count; i++)
                if (dust[i].image != null) dust[i].image.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }
#endif
    }

    void EnsureCanvas()
    {
        if (canvas == null)
        {
            Transform existing = transform.Find(CanvasName);
            canvas = existing != null ? existing.GetComponent<Canvas>() : null;
        }

        if (canvas == null)
        {
            GameObject go = new GameObject(CanvasName);
            go.transform.SetParent(transform, false);
            go.hideFlags = HideFlags.DontSave;
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasRect = go.GetComponent<RectTransform>();
        }
        canvas.sortingOrder = sortingOrder;
        if (canvasRect == null)
            canvasRect = canvas.GetComponent<RectTransform>();
    }

    Vector2 CanvasSize()
    {
        Vector2 size = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (size.x <= 1f || size.y <= 1f)
            size = new Vector2(Screen.width, Screen.height);
        return size;
    }

    void EnsureParticles()
    {
        // 개수 맞추기
        while (dust.Count < particleCount)
            dust.Add(CreateDust());

        Vector2 size = CanvasSize();
        for (int i = 0; i < dust.Count; i++)
        {
            bool active = i < particleCount;
            dust[i].rect.gameObject.SetActive(active);
            if (!active)
                continue;

            Dust d = dust[i];
            if (d.pos == Vector2.zero)
            {
                d.pos = new Vector2(Random.Range(0f, size.x), Random.Range(0f, size.y));
                d.vel = new Vector2(Random.Range(-driftSpeed * 0.4f, driftSpeed * 0.4f), Random.Range(driftSpeed * 0.25f, driftSpeed));
                d.phase = Random.Range(0f, 20f);
                d.baseAlpha = Random.Range(0.75f, 1f);
                d.twinkle = Random.Range(0.4f, 1.4f);
                float s = Random.Range(minSize, Mathf.Max(minSize, maxSize));
                d.rect.sizeDelta = new Vector2(s, s);
                d.rect.anchoredPosition = d.pos - size * 0.5f;
                d.image.color = new Color(dustColor.r, dustColor.g, dustColor.b, Mathf.Clamp01(alpha * d.baseAlpha));
            }
            d.image.texture = dustTexture;
        }
    }

    Dust CreateDust()
    {
        GameObject go = new GameObject("Dust");
        go.transform.SetParent(canvas.transform, false);
        go.hideFlags = HideFlags.DontSave;
        RawImage img = go.AddComponent<RawImage>();
        img.texture = dustTexture;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        return new Dust { rect = rt, image = img };
    }

    void Update()
    {
        if (canvas == null)
            Build();

        // 시작 씬에서는 먼지 오버레이(카메라 효과)를 표시하지 않는다.
        if (canvas != null)
            canvas.enabled = SceneManager.GetActiveScene().name != "StartScene";
        if (canvas != null && !canvas.enabled)
            return;

        float now = Time.realtimeSinceStartup;
        float dt = Mathf.Clamp(now - lastTime, 0f, 0.1f);
        lastTime = now;

        Vector2 size = CanvasSize();
        // anchoredPosition 은 중앙(0,0) 기준 → 화면 좌하단(0,0)~우상단(size) 좌표를 중앙기준으로 변환
        Vector2 half = size * 0.5f;

        for (int i = 0; i < dust.Count; i++)
        {
            Dust d = dust[i];
            if (!d.rect.gameObject.activeSelf)
                continue;

            Vector2 drift = d.vel * dt;
            drift.x += Mathf.Sin(now * 0.5f + d.phase) * dt * swayAmount;
            d.pos += drift;

            // 화면 밖으로 나가면 반대편에서 재등장
            if (d.pos.x < -40f) d.pos.x = size.x + 40f;
            if (d.pos.x > size.x + 40f) d.pos.x = -40f;
            if (d.pos.y < -40f) d.pos.y = size.y + 40f;
            if (d.pos.y > size.y + 40f) d.pos.y = -40f;

            d.rect.anchoredPosition = d.pos - half;

            // 반딧불이 점멸: firefly 가 클수록 알파가 0 근처까지 뚝 떨어졌다 살아난다.
            float tw = Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(now * d.twinkle + d.phase));
            float blink = Mathf.Pow(tw, 2f);
            float pulse = Mathf.Lerp(0.85f, blink, firefly);
            Color c = dustColor;
            c.a = Mathf.Clamp01(alpha * d.baseAlpha * pulse);
            d.image.color = c;
        }

        if (canvas != null)
            canvas.sortingOrder = sortingOrder;
    }

    void EnsureTexture()
    {
        if (dustTexture != null)
            return;

        const int size = 48;
        dustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AmbientDustTex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 솜뭉치 느낌: 메인 원 + 작은 퍼프 2개 합성
                float main = Mathf.Clamp01(1f - Mathf.InverseLerp(4f, 22f, Vector2.Distance(new Vector2(x, y), c)));
                float puffA = Mathf.Clamp01(1f - Mathf.InverseLerp(2f, 12f, Vector2.Distance(new Vector2(x, y), c + new Vector2(-5f, 4f))));
                float puffB = Mathf.Clamp01(1f - Mathf.InverseLerp(2f, 11f, Vector2.Distance(new Vector2(x, y), c + new Vector2(6f, -3f))));
                float grain = 0.85f + 0.15f * Mathf.Sin(x * 0.7f + y * 1.2f);
                float a = Mathf.Clamp01((main * 0.6f + puffA * 0.26f + puffB * 0.24f) * grain);
                a *= a;
                dustTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        dustTexture.Apply();
    }
}
