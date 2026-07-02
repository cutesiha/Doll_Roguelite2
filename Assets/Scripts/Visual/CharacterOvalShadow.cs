using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class CharacterOvalShadow : MonoBehaviour
{
    const string ShadowName = "Character Direction Shadow";
    const string LegacyShadowName = "Oval Shadow";

    [SerializeField] Vector2 footAnchorOffset = new Vector2(0f, 0f);
    [SerializeField] Vector2 shadowScale = new Vector2(1.08f, 0.42f);
    [SerializeField] float rotationZ = -28f;
    [SerializeField] Color shadowColor = new Color(0.03f, 0.022f, 0.018f, 0.3f);
    [SerializeField] int sortingOrderOffset = -1;
    [SerializeField] bool forceShadow;

    SpriteRenderer ownerRenderer;
    SpriteRenderer shadowRenderer;
    PlayerController playerController;

    void Awake()
    {
    }

    void OnEnable()
    {
        RequestConfigure();
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif
        Configure();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RequestConfigure();
    }
#endif

    void RequestConfigure()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= DelayedConfigure;
            EditorApplication.delayCall += DelayedConfigure;
            return;
        }
#endif

        // In play mode this can be called while Unity is still inside another
        // component's Awake/OnEnable. Runtime configuration is handled in LateUpdate
        // so child renderers are not created during that restricted window.
    }

#if UNITY_EDITOR
    void DelayedConfigure()
    {
        if (this != null)
            Configure();
    }
#endif

    void Configure()
    {
        ownerRenderer = GetComponent<SpriteRenderer>();
        playerController = GetComponent<PlayerController>();

        if (!IsShadowTarget())
        {
            HideShadow();
            return;
        }

        // forceShadow is used for static props whose shadow is hand-authored in the
        // editor. Once such a shadow exists, leave its transform/look exactly as the
        // user adjusted it instead of regenerating it from the serialized defaults.
        bool authored = forceShadow && transform.Find(ShadowName) != null;

        shadowRenderer = EnsureShadowRenderer();
        if (shadowRenderer == null)
            return;

        if (authored)
            return;

        Sprite sourceSprite = playerController != null ? playerController.GetShadowSourceSprite() : ownerRenderer.sprite;
        shadowRenderer.sprite = sourceSprite;
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingLayerID = ownerRenderer != null ? ownerRenderer.sortingLayerID : 0;
        shadowRenderer.sortingOrder = ownerRenderer != null ? ownerRenderer.sortingOrder + sortingOrderOffset : sortingOrderOffset;
        shadowRenderer.sharedMaterial = ownerRenderer != null ? ownerRenderer.sharedMaterial : null;
        shadowRenderer.enabled = sourceSprite != null;

        Transform shadowTransform = shadowRenderer.transform;
        shadowTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        shadowTransform.localScale = new Vector3(Mathf.Max(0.01f, shadowScale.x), Mathf.Max(0.01f, shadowScale.y), 1f);
        shadowTransform.localPosition = CalculateFootLockedShadowPosition(sourceSprite, shadowTransform.localRotation);
    }

    public void SetForceShadow(bool force) { forceShadow = force; }

    // 그림자 기울기(Z 회전) 설정. 보스별로 각도를 맞출 때 사용(매 LateUpdate의 Configure가 이 값을 반영).
    public void SetShadowRotation(float z) { rotationZ = z; }

    bool IsShadowTarget()
    {
        return ownerRenderer != null && (CompareTag("Player") || GetComponent<EnemyBase>() != null || forceShadow);
    }

    SpriteRenderer EnsureShadowRenderer()
    {
        Transform legacy = transform.Find(LegacyShadowName);
        if (legacy != null)
            legacy.gameObject.SetActive(false);

        Transform previousPlayerShadow = transform.Find("Player Direction Shadow");
        if (previousPlayerShadow != null && previousPlayerShadow.name != ShadowName)
            previousPlayerShadow.gameObject.SetActive(false);

        Transform existing = transform.Find(ShadowName);
        if (existing == null)
        {
            GameObject shadowObject = new GameObject(ShadowName);
            shadowObject.transform.SetParent(transform, false);
            existing = shadowObject.transform;
        }

        existing.gameObject.SetActive(true);

        SpriteRenderer renderer = existing.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = existing.gameObject.AddComponent<SpriteRenderer>();

        return renderer;
    }

    Vector3 CalculateFootLockedShadowPosition(Sprite sourceSprite, Quaternion shadowRotation)
    {
        Bounds sourceBounds = sourceSprite != null ? sourceSprite.bounds : ownerRenderer.localBounds;
        Vector2 footAnchor = new Vector2(sourceBounds.center.x, sourceBounds.min.y) + footAnchorOffset;
        Vector2 shadowFootPoint = new Vector2(sourceBounds.center.x, sourceBounds.min.y);
        Vector2 scaledShadowFootPoint = new Vector2(shadowFootPoint.x * shadowScale.x, shadowFootPoint.y * shadowScale.y);
        Vector2 rotatedShadowFootPoint = shadowRotation * scaledShadowFootPoint;
        Vector2 position = footAnchor - rotatedShadowFootPoint;
        position.y += Mathf.Max(0f, sourceBounds.min.y - MinTransformedShadowY(sourceBounds, position, shadowRotation));

        return new Vector3(position.x, position.y, 0.02f);
    }

    float MinTransformedShadowY(Bounds sourceBounds, Vector2 position, Quaternion shadowRotation)
    {
        Vector2[] corners =
        {
            new Vector2(sourceBounds.min.x, sourceBounds.min.y),
            new Vector2(sourceBounds.min.x, sourceBounds.max.y),
            new Vector2(sourceBounds.max.x, sourceBounds.min.y),
            new Vector2(sourceBounds.max.x, sourceBounds.max.y)
        };

        float minY = float.MaxValue;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 scaled = new Vector2(corners[i].x * shadowScale.x, corners[i].y * shadowScale.y);
            Vector2 transformed = position + (Vector2)(shadowRotation * scaled);
            minY = Mathf.Min(minY, transformed.y);
        }

        return minY;
    }

    void HideShadow()
    {
        HideChild(ShadowName);
        HideChild(LegacyShadowName);

        if (shadowRenderer != null)
            shadowRenderer.enabled = false;
    }

    void HideChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null)
            child.gameObject.SetActive(false);
    }
}
