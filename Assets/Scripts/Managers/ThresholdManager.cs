using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ThresholdManager : MonoBehaviour
{
    [Header("Vision Overlay")]
    // 왼쪽 눈이 없을 때 / 오른쪽 눈이 없을 때 화면 전체에 띄울 이미지 (1920x1080, 화면에 맞춤)
    [SerializeField] Sprite leftEyeMissingImage;
    [SerializeField] Sprite rightEyeMissingImage;

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
        EnsureSprites();
        if (overlayCanvas == null)
            BuildOverlay();
        else
            ApplyOverlaySprites();
    }

    void Update()
    {
        if (bodyManager == null)
            bodyManager = BodyManager.Instance;

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

        // 스프라이트가 없으면(미할당 씬) 흰 화면이 뜨지 않도록 표시하지 않는다.
        leftOverlay.gameObject.SetActive((showLeft || showBoth) && leftOverlay.sprite != null);
        rightOverlay.gameObject.SetActive((showRight || showBoth) && rightOverlay.sprite != null);
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

        // 화면 전체를 덮는 두 오버레이 (왼쪽 눈 없음 / 오른쪽 눈 없음 이미지)
        leftOverlay  = CreateOverlayImage("LeftEyeMissingOverlay", leftEyeMissingImage);
        rightOverlay = CreateOverlayImage("RightEyeMissingOverlay", rightEyeMissingImage);

        leftOverlay.gameObject.SetActive(false);
        rightOverlay.gameObject.SetActive(false);
    }

    Image CreateOverlayImage(string name, Sprite sprite)
    {
        var imageGO = new GameObject(name);
        imageGO.hideFlags = HideFlags.HideAndDontSave;
        imageGO.transform.SetParent(overlayCanvas.transform, false);

        var image = imageGO.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;            // 검정 채움 제거 — 이미지 그대로 표시
        image.preserveAspect = false;         // 1920x1080 이미지를 화면에 맞춤
        image.raycastTarget = false;

        var rt = image.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;          // 화면 전체
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return image;
    }

    void ApplyOverlaySprites()
    {
        if (leftOverlay != null)
        {
            leftOverlay.sprite = leftEyeMissingImage;
            leftOverlay.color = Color.white;
            leftOverlay.preserveAspect = false;
            StretchFull(leftOverlay.rectTransform);
        }
        if (rightOverlay != null)
        {
            rightOverlay.sprite = rightEyeMissingImage;
            rightOverlay.color = Color.white;
            rightOverlay.preserveAspect = false;
            StretchFull(rightOverlay.rectTransform);
        }
    }

    static void StretchFull(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void EnsureSprites()
    {
        if (leftEyeMissingImage == null)
            leftEyeMissingImage = LoadInterfaceSprite("lefteye_no");
        if (rightEyeMissingImage == null)
            rightEyeMissingImage = LoadInterfaceSprite("righteye_no");
    }

    static Sprite LoadInterfaceSprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/interface/" + spriteName);
        if (sprite != null)
            return sprite;
#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/interface/" + spriteName + ".png");
        if (sprite != null)
            return sprite;
#endif
        return null;
    }
}
