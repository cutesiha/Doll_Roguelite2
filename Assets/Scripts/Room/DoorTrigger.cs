using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField] string supplySceneName = "PresentScene";
    [SerializeField] string eventSceneName = "EventScene";
    [SerializeField] Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color openColor = new Color(0.85f, 0.62f, 0.25f, 1f);
    [SerializeField] Color blockedColor = new Color(0.65f, 0.15f, 0.15f, 1f);
    [SerializeField] Color enterPromptColor = new Color(0.46f, 1f, 0.58f, 1f);
    [SerializeField] Color blockedPromptColor = new Color(1f, 0.32f, 0.28f, 1f);

    MapNode targetNode;
    bool isOpen;
    bool playerNearby;
    Renderer cachedRenderer;
    TMPro.TextMeshPro promptLabel;
    TMPro.TextMeshPro doorTitleLabel;
    Transform promptTransform;
    Transform doorTitleTransform;

    static Canvas screenPromptCanvas;
    static TMPro.TextMeshProUGUI screenPromptLabel;
    static DoorTrigger screenPromptOwner;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        ApplyVisual();
    }

    public void Configure(MapNode node, bool open)
    {
        ResetLabelReferences();
        targetNode = node;
        isOpen = open;
        gameObject.SetActive(open && node != null);
        ApplyVisual();
        UpdateDoorTitle();
        HideScreenPrompt(this);
    }

    void ResetLabelReferences()
    {
        promptLabel = null;
        doorTitleLabel = null;
        promptTransform = null;
        doorTitleTransform = null;
    }

    void Update()
    {
        if (!playerNearby || !isOpen || targetNode == null) return;

        var kb = Keyboard.current;
        bool canPass = CanPass(targetNode, BodyManager.Instance?.State);
        ShowScreenPrompt(this, canPass);

        if (kb == null || (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame))
            return;

        if (!canPass)
        {
            ShowScreenPrompt(this, false);
            return;
        }

        if (!MapRunState.BeginRoom(targetNode))
        {
            ShowScreenPrompt(this, false);
            return;
        }

        SceneManager.LoadScene(SceneNameFor(targetNode));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = true;
        if (isOpen && targetNode != null)
            ShowScreenPrompt(this, CanPass(targetNode, BodyManager.Instance?.State));
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = false;
        HideScreenPrompt(this);
    }

    void OnDestroy()
    {
        if (promptTransform != null)
            Destroy(promptTransform.gameObject);

        if (doorTitleTransform != null)
            Destroy(doorTitleTransform.gameObject);

        HideScreenPrompt(this);
    }

    void ApplyVisual()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null) return;

        var material = Application.isPlaying ? cachedRenderer.material : cachedRenderer.sharedMaterial;
        if (material == null) return;

        Color color = isOpen && targetNode != null ? openColor : lockedColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else material.color = color;
    }

    void EnsurePrompt()
    {
        if (promptLabel != null) return;

        var go = new GameObject("DoorPrompt");
        promptTransform = go.transform;
        PositionLabel(promptTransform, 0.8f);

        promptLabel = go.AddComponent<TMPro.TextMeshPro>();
        promptLabel.alignment = TMPro.TextAlignmentOptions.Center;
        promptLabel.fontSize = 2.2f;
        promptLabel.font = UIThinDungFont.Get();
        promptLabel.color = Color.white;
        promptLabel.sortingOrder = 20;
        promptLabel.text = "";
        promptLabel.gameObject.SetActive(false);
    }

    void EnsureDoorTitle()
    {
        if (doorTitleLabel != null) return;

        var go = new GameObject("DoorTitle");
        doorTitleTransform = go.transform;
        PositionLabel(doorTitleTransform, 1.55f);

        doorTitleLabel = go.AddComponent<TMPro.TextMeshPro>();
        doorTitleLabel.alignment = TMPro.TextAlignmentOptions.Center;
        doorTitleLabel.fontSize = 4.8f;
        doorTitleLabel.font = UIThinDungFont.Get();
        doorTitleLabel.fontStyle = TMPro.FontStyles.Bold;
        doorTitleLabel.color = Color.white;
        doorTitleLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        doorTitleLabel.sortingOrder = 35;
        doorTitleLabel.text = "";
        doorTitleLabel.gameObject.SetActive(false);
    }

    void PositionLabel(Transform labelTransform, float yOffset)
    {
        if (labelTransform == null) return;

        labelTransform.SetParent(null, true);
        labelTransform.position = transform.position + new Vector3(0f, yOffset, -0.1f);
        labelTransform.rotation = Quaternion.identity;
        labelTransform.localScale = Vector3.one;
    }

    void UpdateDoorTitle()
    {
        EnsureDoorTitle();
        if (doorTitleLabel == null) return;

        PositionLabel(doorTitleTransform, 1.55f);
        doorTitleLabel.text = targetNode != null ? DoorTitle(targetNode) : "";
        doorTitleLabel.gameObject.SetActive(isOpen && targetNode != null);
    }

    void UpdatePrompt(bool show)
    {
        EnsurePrompt();
        if (promptLabel == null) return;

        PositionLabel(promptTransform, 0.8f);
        promptLabel.text = targetNode != null
            ? PromptLabel(targetNode)
            : "\uC7A0\uAE40";
        promptLabel.gameObject.SetActive(show && isOpen && targetNode != null);
    }

    static void EnsureScreenPrompt()
    {
        if (screenPromptLabel != null) return;

        var canvasGO = new GameObject("DoorScreenPromptCanvas");
        if (Application.isPlaying)
            DontDestroyOnLoad(canvasGO);

        screenPromptCanvas = canvasGO.AddComponent<Canvas>();
        screenPromptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenPromptCanvas.overrideSorting = true;
        screenPromptCanvas.sortingOrder = 900;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var labelGO = new GameObject("DoorScreenPrompt");
        labelGO.transform.SetParent(canvasGO.transform, false);

        var rect = labelGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -156f);
        rect.sizeDelta = new Vector2(980f, 96f);

        screenPromptLabel = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        screenPromptLabel.alignment = TMPro.TextAlignmentOptions.Center;
        screenPromptLabel.font = UIThinDungFont.Get();
        screenPromptLabel.fontSize = 58f;
        screenPromptLabel.fontStyle = TMPro.FontStyles.Bold;
        screenPromptLabel.raycastTarget = false;
        screenPromptLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        screenPromptLabel.gameObject.SetActive(false);
    }

    static void ShowScreenPrompt(DoorTrigger owner, bool canEnter)
    {
        if (owner == null) return;

        EnsureScreenPrompt();
        screenPromptOwner = owner;
        screenPromptLabel.text = canEnter
            ? "[Enter\uB97C \uB20C\uB7EC \uC785\uC7A5\uD558\uC138\uC694]"
            : "\uC870\uAC74\uC5D0 \uBD80\uD569\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4";
        screenPromptLabel.color = canEnter ? owner.enterPromptColor : owner.blockedPromptColor;
        screenPromptLabel.gameObject.SetActive(true);
    }

    static void HideScreenPrompt(DoorTrigger owner)
    {
        if (screenPromptOwner != owner)
            return;

        screenPromptOwner = null;
        if (screenPromptLabel != null)
            screenPromptLabel.gameObject.SetActive(false);
    }

    System.Collections.IEnumerator ShowBlockedPrompt()
    {
        EnsurePrompt();
        if (promptLabel == null) yield break;

        var oldColor = promptLabel.color;
        promptLabel.text = "\uC870\uAC74\uC774 \uB9DE\uC9C0 \uC54A\uC74C";
        promptLabel.color = blockedColor;
        promptLabel.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.7f);
        promptLabel.color = oldColor;
        UpdatePrompt(playerNearby);
    }

    static bool CanPass(MapNode node, BodyState state)
    {
        if (node.roomType != RoomType.ConditionCombat) return true;
        if (state == null) state = new BodyState();

        switch (node.conditionType)
        {
            case NodeConditionType.NoLeftArm: return !state.armLeft;
            case NodeConditionType.NoRightEye: return !state.eyeRight;
            case NodeConditionType.NoLeftLeg: return !state.legLeft;
            case NodeConditionType.NoRightLeg: return !state.legRight;
            default: return true;
        }
    }

    string SceneNameFor(MapNode node)
    {
        switch (node.roomType)
        {
            case RoomType.Boss: return bossSceneName;
            case RoomType.Supply: return supplySceneName;
            case RoomType.Event: return eventSceneName;
            default: return roomSceneName;
        }
    }

    static string DoorLabel(MapNode node)
    {
        switch (node.roomType)
        {
            case RoomType.Boss: return "\uBCF4\uC2A4 \uBC29";
            case RoomType.Supply: return "\uBCF4\uAE09 \uBC29";
            case RoomType.Event: return "\uC774\uBCA4\uD2B8 \uBC29";
            case RoomType.ConditionCombat: return "\uC870\uAC74 \uC804\uD22C";
            default: return "\uC804\uD22C \uBC29";
        }
    }

    static string DoorTitle(MapNode node)
    {
        if (node.roomType == RoomType.ConditionCombat)
            return ConditionLabel(node.conditionType);

        return DoorLabel(node);
    }

    static string PromptLabel(MapNode node)
    {
        switch (node.roomType)
        {
            case RoomType.Boss: return "E: \uBCF4\uC2A4 \uBC29";
            case RoomType.Supply: return "E: \uBCF4\uAE09 \uBC29";
            case RoomType.Event: return "E: \uC774\uBCA4\uD2B8 \uBC29";
            case RoomType.ConditionCombat: return "E: \uC870\uAC74 \uC804\uD22C";
            default: return "E: \uC804\uD22C \uBC29";
        }
    }

    static string ConditionLabel(NodeConditionType condition)
    {
        switch (condition)
        {
            case NodeConditionType.NoLeftArm: return "\uC67C\uD314 \uC5C6\uC74C";
            case NodeConditionType.NoRightEye: return "\uC624\uB978\uB208 \uC5C6\uC74C";
            case NodeConditionType.NoLeftLeg: return "\uC67C\uB2E4\uB9AC \uC5C6\uC74C";
            case NodeConditionType.NoRightLeg: return "\uC624\uB978\uB2E4\uB9AC \uC5C6\uC74C";
            default: return "\uC870\uAC74 \uC804\uD22C";
        }
    }
}
