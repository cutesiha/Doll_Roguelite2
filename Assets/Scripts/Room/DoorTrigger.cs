using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] string roomSceneName = "RoomScene";
    [SerializeField] string bossSceneName = "BossScene";
    [SerializeField] Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] Color openColor = new Color(0.85f, 0.62f, 0.25f, 1f);
    [SerializeField] Color blockedColor = new Color(0.65f, 0.15f, 0.15f, 1f);

    MapNode targetNode;
    bool isOpen;
    bool playerNearby;
    Renderer cachedRenderer;
    TMPro.TextMeshPro promptLabel;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        EnsurePrompt();
        ApplyVisual();
    }

    public void Configure(MapNode node, bool open)
    {
        targetNode = node;
        isOpen = open;
        gameObject.SetActive(open && node != null);
        ApplyVisual();
        UpdatePrompt(false);
    }

    void Update()
    {
        if (!playerNearby || !isOpen || targetNode == null) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (!CanPass(targetNode, BodyManager.Instance?.State))
        {
            StartCoroutine(ShowBlockedPrompt());
            return;
        }

        if (!MapRunState.BeginRoom(targetNode))
        {
            StartCoroutine(ShowBlockedPrompt());
            return;
        }

        SceneManager.LoadScene(SceneNameFor(targetNode));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = true;
        UpdatePrompt(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = false;
        UpdatePrompt(false);
    }

    void ApplyVisual()
    {
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null) return;

        var material = cachedRenderer.material;
        if (material == null) return;

        Color color = isOpen && targetNode != null ? openColor : lockedColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else material.color = color;
    }

    void EnsurePrompt()
    {
        if (promptLabel != null) return;

        var go = new GameObject("DoorPrompt");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.8f, -0.1f);

        promptLabel = go.AddComponent<TMPro.TextMeshPro>();
        promptLabel.alignment = TMPro.TextAlignmentOptions.Center;
        promptLabel.fontSize = 2.2f;
        promptLabel.color = Color.white;
        promptLabel.sortingOrder = 20;
        promptLabel.text = "";
        promptLabel.gameObject.SetActive(false);
    }

    void UpdatePrompt(bool show)
    {
        EnsurePrompt();
        if (promptLabel == null) return;

        promptLabel.text = targetNode != null
            ? $"E: {DoorLabel(targetNode)}"
            : "잠김";
        promptLabel.gameObject.SetActive(show && isOpen && targetNode != null);
    }

    System.Collections.IEnumerator ShowBlockedPrompt()
    {
        EnsurePrompt();
        if (promptLabel == null) yield break;

        var oldColor = promptLabel.color;
        promptLabel.text = "조건이 맞지 않음";
        promptLabel.color = blockedColor;
        promptLabel.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.7f);
        promptLabel.color = oldColor;
        UpdatePrompt(playerNearby);
    }

    static bool CanPass(MapNode node, BodyState state)
    {
        if (node.roomType != RoomType.ConditionCombat) return true;
        if (state == null) return true;

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
            default: return roomSceneName;
        }
    }

    static string DoorLabel(MapNode node)
    {
        switch (node.roomType)
        {
            case RoomType.Boss: return "보스 방";
            case RoomType.Supply: return "보급 방";
            case RoomType.Event: return "이벤트 방";
            case RoomType.ConditionCombat: return "조건 전투";
            default: return "전투 방";
        }
    }
}
