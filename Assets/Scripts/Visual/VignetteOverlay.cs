using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 화면 가장자리를 어둡게 만드는 비네트 오버레이.
// URP Post Processing 과 무관하게, Screen Space - Overlay 캔버스 위 전체화면 이미지로 '무조건' 렌더된다.
// 중앙은 투명 → 가장자리는 짙은 갈색(edgeColor) → 완전 끝부분(테두리)은 더 짙은 갈색(rimColor)으로 진해진다.
// [ExecuteAlways] 라서 플레이하지 않아도 에디터 Game 뷰에서 바로 보인다.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VignetteOverlay : MonoBehaviour
{
    [Header("Strength (인스펙터에서 조절)")]
    [Tooltip("비네트 강도. 확인 후 0.25~0.35 권장.")]
    [SerializeField, Range(0f, 1f)] float alpha = 0.55f;
    [Tooltip("체크하면 확인용 강한 알파(debugAlpha)로 강제 표시.")]
    [SerializeField] bool debugBoost = false;
    [Tooltip("디버그용 알파(확인용).")]
    [SerializeField, Range(0f, 1f)] float debugAlpha = 0.7f;

    [Header("Look")]
    [Tooltip("가장자리 색(짙은 갈색).")]
    [SerializeField] Color edgeColor = new Color(0.20f, 0.10f, 0.045f, 1f);
    [Tooltip("완전 끝부분(테두리) 색 — 더 짙은 갈색으로 진하게.")]
    [SerializeField] Color rimColor = new Color(0.05f, 0.025f, 0.012f, 1f);
    [Tooltip("이 반경 안쪽은 완전 투명(중앙이 잘 보임).")]
    [SerializeField, Range(0f, 1.1f)] float innerRadius = 0.42f;
    [Tooltip("이 반경에서 완전히 어두워짐(모서리).")]
    [SerializeField, Range(0.3f, 1.7f)] float outerRadius = 1.12f;
    [Tooltip("이 반경부터 rimColor 로 더 진해짐(완전 끝부분).")]
    [SerializeField, Range(0.5f, 1.6f)] float rimStart = 0.95f;

    [Header("Canvas")]
    [Tooltip("높을수록 앞에 그려짐. HUD 뒤로 보내려면 낮추면 됨.")]
    [SerializeField] int sortingOrder = 31000;

    const string CanvasName = "_VignetteOverlayCanvas";
    const string ImageName = "VignetteImage";

    bool autoCreated;
    static VignetteOverlay activeInstance;

    Canvas canvas;
    RawImage image;
    Texture2D texture;
    int builtKey = int.MinValue;

    // ---- 모든 씬에서 런타임에 하나 보장 ----
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
        if (FindFirstObjectByType<VignetteOverlay>() != null)
            return;

        GameObject go = new GameObject("_VignetteOverlay");
        DontDestroyOnLoad(go);
        VignetteOverlay v = go.AddComponent<VignetteOverlay>();
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

        Build();
        Apply();
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
            Apply();
        };
    }
#endif

    void Update()
    {
        Build();   // rebuilds texture only when look params change (keyed)
        // 시작 씬에서는 카메라 효과(비네트)를 표시하지 않는다.
        if (canvas != null)
            canvas.enabled = !IsEffectHiddenScene();
        if (canvas != null && !canvas.enabled)
            return;
        Apply();
    }

    static bool IsEffectHiddenScene()
    {
        return SceneManager.GetActiveScene().name == "StartScene";
    }

    void Build()
    {
        EnsureTexture();

        if (canvas == null)
        {
            Transform existing = transform.Find(CanvasName);
            canvas = existing != null ? existing.GetComponent<Canvas>() : null;
        }

        if (canvas == null)
        {
            GameObject canvasGo = new GameObject(CanvasName);
            canvasGo.transform.SetParent(transform, false);
            canvasGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }
        canvas.sortingOrder = sortingOrder;

        if (image == null)
        {
            Transform existingImg = canvas.transform.Find(ImageName);
            image = existingImg != null ? existingImg.GetComponent<RawImage>() : null;
        }

        if (image == null)
        {
            GameObject imgGo = new GameObject(ImageName);
            imgGo.transform.SetParent(canvas.transform, false);
            imgGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            image = imgGo.AddComponent<RawImage>();
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        image.texture = texture;
    }

    void Apply()
    {
        if (image == null)
            return;

        // 색(갈색 그라데이션)은 텍스처에 구워져 있고, 여기선 전체 강도만 곱한다.
        float a = debugBoost ? debugAlpha : alpha;
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));

        if (canvas != null)
            canvas.sortingOrder = sortingOrder;
    }

    void EnsureTexture()
    {
        int key = new Vector4(innerRadius, outerRadius, rimStart, 0f).GetHashCode()
                  ^ edgeColor.GetHashCode() * 7
                  ^ rimColor.GetHashCode() * 13;
        if (texture != null && key == builtKey)
            return;
        builtKey = key;

        const int size = 256;
        if (texture == null)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VignetteOverlayTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
        }

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float lo = Mathf.Min(innerRadius, outerRadius - 0.02f);
        const float rimEnd = 1.45f; // 코너 최대 거리(√2)까지
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x) / center.x;
                float dy = Mathf.Abs(y - center.y) / center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float shape = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo, outerRadius, dist));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rimStart, rimEnd, dist));

                Color rgb = Color.Lerp(edgeColor, rimColor, rim);
                float a = Mathf.Clamp01(Mathf.Max(shape, rim)); // 완전 끝부분은 rim이 알파를 최대로 끌어올림
                texture.SetPixel(x, y, new Color(rgb.r, rgb.g, rgb.b, a));
            }
        }
        texture.Apply();
    }
}
