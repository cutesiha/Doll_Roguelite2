using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerBodyPartVisuals : MonoBehaviour
{
    [SerializeField] bool useLayeredBodySprites = false;
    [SerializeField] Sprite bodySprite;
    [SerializeField] Sprite headSprite;
    [SerializeField] Sprite eyeLeftSprite;
    [SerializeField] Sprite eyeRightSprite;
    [SerializeField] Sprite armLeftSprite;
    [SerializeField] Sprite armRightSprite;
    [SerializeField] Sprite legLeftSprite;
    [SerializeField] Sprite legRightSprite;

    SpriteRenderer baseRenderer;
    SpriteRenderer headRenderer;
    SpriteRenderer eyeLeftRenderer;
    SpriteRenderer eyeRightRenderer;
    SpriteRenderer armLeftRenderer;
    SpriteRenderer armRightRenderer;
    SpriteRenderer legLeftRenderer;
    SpriteRenderer legRightRenderer;

    void Awake()
    {
        baseRenderer = GetComponent<SpriteRenderer>();
        LoadSpritesIfMissing();
        BuildRenderers();
        Apply();
    }

    void OnEnable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Apply;
    }

    void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Apply;
    }

    void LateUpdate()
    {
        Apply();
    }

    void BuildRenderers()
    {
        if (!useLayeredBodySprites || baseRenderer == null)
            return;

        headRenderer = EnsureRenderer("PlayerPart_Head", headSprite, 1);
        legLeftRenderer = EnsureRenderer("PlayerPart_LegLeft", legLeftSprite, 2);
        legRightRenderer = EnsureRenderer("PlayerPart_LegRight", legRightSprite, 3);
        armLeftRenderer = EnsureRenderer("PlayerPart_ArmLeft", armLeftSprite, 4);
        armRightRenderer = EnsureRenderer("PlayerPart_ArmRight", armRightSprite, 5);
        eyeLeftRenderer = EnsureRenderer("PlayerPart_EyeLeft", eyeLeftSprite, 6);
        eyeRightRenderer = EnsureRenderer("PlayerPart_EyeRight", eyeRightSprite, 7);
    }

    SpriteRenderer EnsureRenderer(string objectName, Sprite sprite, int orderOffset)
    {
        Transform existing = transform.Find(objectName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.sortingLayerID = baseRenderer.sortingLayerID;
        renderer.sortingOrder = baseRenderer.sortingOrder + orderOffset;
        renderer.color = Color.white;
        return renderer;
    }

    void Apply()
    {
        if (!useLayeredBodySprites || baseRenderer == null)
            return;

        LoadSpritesIfMissing();

        ApplyPart(headRenderer, headSprite, true);
        ApplyPart(eyeLeftRenderer, eyeLeftSprite, BodyConditionUtility.HasPart(BodySlot.EyeLeft));
        ApplyPart(eyeRightRenderer, eyeRightSprite, BodyConditionUtility.HasPart(BodySlot.EyeRight));
        ApplyPart(armLeftRenderer, armLeftSprite, BodyConditionUtility.HasPart(BodySlot.ArmLeft));
        ApplyPart(armRightRenderer, armRightSprite, BodyConditionUtility.HasPart(BodySlot.ArmRight));
        ApplyPart(legLeftRenderer, legLeftSprite, BodyConditionUtility.HasPart(BodySlot.LegLeft));
        ApplyPart(legRightRenderer, legRightSprite, BodyConditionUtility.HasPart(BodySlot.LegRight));
    }

    void ApplyPart(SpriteRenderer renderer, Sprite sprite, bool visible)
    {
        if (renderer == null)
            return;

        renderer.sprite = sprite;
        renderer.enabled = visible && sprite != null;
        renderer.sortingLayerID = baseRenderer.sortingLayerID;
    }

    void LoadSpritesIfMissing()
    {
        if (bodySprite == null) bodySprite = LoadSprite("body");
        if (headSprite == null) headSprite = LoadSprite("head");
        if (eyeLeftSprite == null) eyeLeftSprite = LoadSprite("eye_left");
        if (eyeRightSprite == null) eyeRightSprite = LoadSprite("eye_right");
        if (armLeftSprite == null) armLeftSprite = LoadSprite("arm_left");
        if (armRightSprite == null) armRightSprite = LoadSprite("arm_right");
        if (legLeftSprite == null) legLeftSprite = LoadSprite("leg_left");
        if (legRightSprite == null) legRightSprite = LoadSprite("leg_right");
    }

    Sprite LoadSprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/Player/" + spriteName);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>("Sprites/Player/" + spriteName);
        if (sprites.Length > 0)
            return sprites[0];

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Sprites/Player/" + spriteName + ".png");
        for (int i = 0; i < assets.Length; i++)
            if (assets[i] is Sprite editorSprite)
                return editorSprite;

        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/Player/" + spriteName + ".png");
#else
        return null;
#endif
    }
}
